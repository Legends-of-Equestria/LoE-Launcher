using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoE_Launcher.Core
{
    partial class Downloader
    {
        public class RefreshProgress : ProgressData
        {
            public RefreshProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    if (Model._data?.ToProcess.Count != 0)
                    {
                        return "Files to Update: {0}".Format(Model._data?.ToProcess.Count);
                    }
                    return "Ready to Launch!";
                }
                else
                {
                    return "Preparing...";
                }
            }
        }
        public class PreparingProgress : ProgressData
        {
            public PreparingProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    return "Preparing Install...";
                }
                else
                {
                    if (Marquee)
                        return "Preparing Install...";
                    return "Preparing Install ({0}/{1})...".Format(Current, Max);
                }
            }
        }
        public class InstallingProgress : ProgressData
        {
            public InstallingProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    return "Installing...";
                }
                else
                {
                    if (Marquee)
                        return "Installing...";
                    return "Installing ({0}/{1})...".Format(Current, Max);
                }
            }
        }

        public class RepairingProgress : ProgressData
        {
            public RepairingProgress(Downloader model) : base(model)
            { }

            protected override string GetText()
            {
                if (IsFinished)
                    return "Deleting...";
                else
                {
                    if (Marquee)
                        return "Deleting...";
                    else
                        return $"Deleting ({Current}/{Max})";
                }
            }
        }

        public class UnzipProgress : ProgressData
        {
            public UnzipProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    return "Extracting...";
                }
                else
                {
                    return "Extracting...";
                }
            }
        }
        public class CleanupProgress : ProgressData
        {
            public CleanupProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                if (IsFinished)
                {
                    return "Cleaning up...";
                }
                else
                {
                    return "Cleaning up...";
                }
            }
        }
        public class UpToDateProgress : ProgressData
        {
            public UpToDateProgress(Downloader model) : base(model)
            {
            }

            protected override string GetText()
            {
                return "Up to date";
            }
        }

        public class ErrorProgress : ProgressData
        {
            readonly string _message;

            public ErrorProgress(string message, Downloader model) : base(model)
            {
                _message = message;
            }

            protected override string GetText()
            {
                return _message;
            }
        }
    }
}
