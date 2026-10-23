"""Local stand-in for loedata's /dev/ folder

    devtest.py serve                   log in as tester / testpass, player / testpass has no access
    devtest.py publish-fake
    devtest.py publish <build-folder>
    devtest.py run dev|live

Everything goes under .run/
"""
import argparse
import json
import os
import random
import secrets
import shutil
import subprocess
import sys
from datetime import datetime, timezone
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RUN = os.path.join(REPO, ".run")
SERVED = os.path.join(RUN, "dev-server")
LAUNCHER_PROJECT = os.path.join(REPO, "LoE-Launcher", "LoE-Launcher.csproj")
DEFAULT_PATCH_GENERATOR = os.path.join(os.path.dirname(REPO), "LoETools", "PatchGenerator", "PatchGenerator",
                                       "PatchGenerator.csproj")
PLATFORM_KEYS = ["Win64", "Win32", "Linux", "Mac_Intel", "Mac_Arm"]


# Stand-in forum accounts and access levels, dev builds need 20 (team members)
ACCOUNTS = {"tester": ("testpass", 20), "player": ("testpass", 1)}


def base_url(port):
    return f"http://localhost:{port}/dev/"


def login_url(port):
    return f"http://localhost:{port}/dev-auth/login.php"


def serve(args):
    os.makedirs(SERVED, exist_ok=True)
    tokens = {}

    class Handler(SimpleHTTPRequestHandler):
        def __init__(self, *a, **kw):
            super().__init__(*a, directory=SERVED, **kw)

        def log_message(self, fmt, *a):
            pass

        def log(self, status, size=0):
            auth = "token" if self.headers.get("Authorization") else "no token"
            rng = self.headers.get("Range", "")
            print(f"{status} {auth:8} {rng:18} {self.path} {size}B", flush=True)

        def reply_json(self, status, body):
            data = json.dumps(body).encode()
            self.send_response(status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def do_POST(self):
            if self.path != "/dev-auth/login.php":
                self.send_error(404)
                return
            form = parse_qs(self.rfile.read(int(self.headers.get("Content-Length", 0))).decode())
            user = form.get("username", [""])[0].strip().lower()
            password = form.get("password", [""])[0]
            account = ACCOUNTS.get(user)
            if not account or account[0] != password:
                print(f"login {user!r}: wrong login", flush=True)
                self.reply_json(401, {"error": "wrong_login"})
            elif account[1] < 20:
                print(f"login {user!r}: no access", flush=True)
                self.reply_json(403, {"error": "no_access"})
            else:
                token = secrets.token_hex(32)
                tokens[token] = user
                print(f"login {user!r}: ok", flush=True)
                self.reply_json(200, {"token": token, "expires": "2099-01-01T00:00:00Z"})

        def do_GET(self):
            if not self.path.startswith("/dev/"):
                self.log(404)
                self.send_error(404)
                return
            auth = self.headers.get("Authorization", "")
            if not auth.startswith("Bearer ") or auth[7:] not in tokens:
                self.log(401)
                self.send_response(401)
                self.send_header("Content-Length", "0")
                self.end_headers()
                return

            path = self.translate_path(self.path[len("/dev"):])
            if not os.path.isfile(path):
                self.log(404)
                self.send_error(404)
                return

            size = os.path.getsize(path)
            rng = self.headers.get("Range")
            with open(path, "rb") as f:
                if rng and rng.startswith("bytes="):
                    start_s, end_s = rng[6:].split("-")
                    start = int(start_s)
                    end = min(int(end_s) if end_s else size - 1, size - 1)
                    f.seek(start)
                    body = f.read(end - start + 1)
                    self.send_response(206)
                    self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
                else:
                    body = f.read()
                    self.send_response(200)
                self.send_header("Content-Length", str(len(body)))
                self.send_header("Accept-Ranges", "bytes")
                self.end_headers()
                self.wfile.write(body)
            self.log(206 if rng else 200, len(body))

    print(f"Serving {SERVED} at {base_url(args.port)}, log in as tester / testpass", flush=True)
    ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()


def patch_generator_command(target, url):
    generator = os.environ.get("LOE_PATCH_GENERATOR", DEFAULT_PATCH_GENERATOR)
    if not os.path.exists(generator):
        sys.exit(f"PatchGenerator not found at {generator}. Set LOE_PATCH_GENERATOR to its .exe or .csproj.")
    if generator.endswith(".csproj"):
        return ["dotnet", "run", "--project", generator, "--", target, "-u", url]
    return [generator, target, "-u", url]


def publish_folder(source, build_id, port, platforms, branch, commit):
    os.makedirs(SERVED, exist_ok=True)
    target = os.path.join(SERVED, build_id)
    if os.path.exists(target):
        shutil.rmtree(target)
    # PatchGenerator rewrites the folder in place
    shutil.copytree(source, target)

    url = base_url(port) + build_id + "/"
    subprocess.run(patch_generator_command(target, url), check=True)

    manifest = {key: build_id for key in platforms}
    manifest["Build"] = {
        "Id": build_id,
        "Branch": branch,
        "Commit": commit,
        "Time": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    # Written last so a half-patched build is never current
    with open(os.path.join(SERVED, "versions-dev.json"), "w") as f:
        json.dump(manifest, f, indent=2)
    print(f"Published {build_id} for {', '.join(platforms)} at {url}")


def next_build_id():
    existing = [int(n.split("-")[1]) for n in os.listdir(SERVED)
                if n.startswith("build-") and n.split("-")[1].isdigit()] if os.path.isdir(SERVED) else []
    return f"build-{max(existing, default=0) + 1}"


def publish_fake(args):
    build_id = next_build_id()
    source = os.path.join(RUN, "fake-build")
    if not os.path.isdir(source):
        os.makedirs(os.path.join(source, "data"))
        with open(os.path.join(source, "loe.exe"), "w") as f:
            f.write("fake game executable\n")
        with open(os.path.join(source, "data", "big.bin"), "wb") as f:
            f.write(random.Random(1).randbytes(300_000))
    with open(os.path.join(source, "data", "config.txt"), "w") as f:
        f.write(f"build={build_id}\n")
    publish_folder(source, build_id, args.port, PLATFORM_KEYS, "local", "fake")


def publish(args):
    build_id = args.build_id or next_build_id()
    publish_folder(os.path.abspath(args.folder), build_id, args.port, args.platforms, args.branch, args.commit)


def run(args):
    folder = os.path.join(RUN, f"launcher-{args.channel}")
    os.makedirs(folder, exist_ok=True)
    settings_path = os.path.join(folder, "settings.json")
    settings = {}
    if os.path.exists(settings_path):
        with open(settings_path) as f:
            settings = json.load(f)
    if args.channel == "dev":
        settings.update({
            "DevStream": base_url(args.port) + "versions-dev.json",
            "DevZsyncLocation": base_url(args.port) + "{version}/",
            "DevLoginUrl": login_url(args.port),
        })
    else:
        settings["Channel"] = "Live"
    with open(settings_path, "w") as f:
        json.dump(settings, f, indent=2)

    sys.exit(subprocess.run(["dotnet", "run", "--project", LAUNCHER_PROJECT], cwd=folder).returncode)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--port", type=int, default=8765)
    sub = parser.add_subparsers(dest="command", required=True)

    p = sub.add_parser("serve")
    p.set_defaults(func=serve)

    p = sub.add_parser("publish-fake")
    p.set_defaults(func=publish_fake)

    p = sub.add_parser("publish")
    p.add_argument("folder")
    p.add_argument("--build-id")
    p.add_argument("--platforms", nargs="+", default=["Win64"], choices=PLATFORM_KEYS)
    p.add_argument("--branch", default="local")
    p.add_argument("--commit", default="")
    p.set_defaults(func=publish)

    p = sub.add_parser("run")
    p.add_argument("channel", choices=["dev", "live"])
    p.set_defaults(func=run)

    args = parser.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
