using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using LoE_Launcher.Core;
using LoE_Launcher.Properties;
using Nito.AsyncEx;

namespace LoE_Launcher
{
    public partial class Form1 : Form
    {
        Downloader _downloader = new Downloader();

        public Form1()
        {
            InitializeComponent();
            
            MakeTransparent(pictureBox2, pictureBox1);
            MakeTransparent(label2, pictureBox2);
            MakeTransparent(lblDownloadedAmount, pictureBox2);
            //pictureBox1.Controls.Add(pictureBox2);
            //pictureBox2.Location = new Point(0, 0);
            //pictureBox2.BackColor = Color.Transparent;

            label2.Text = label2.Text + " Platform: ";
            switch (Downloader.OperatingSystem)
            {
                case OS.WindowsX86:
                    label2.Text = label2.Text + "Windows x86";
                    break;
                case OS.WindowsX64:
                    label2.Text = label2.Text + "Windows x64";
                    break;
                case OS.Mac:
                    label2.Text = label2.Text + "Mac";
                    break;
                case OS.X11:
                    label2.Text = label2.Text + "Linux";
                    break;
                case OS.Other:
                    label2.Text = label2.Text + "Unknown";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            timer1.Start();
            btnAction.Text = "Unknown";
            btnAction.Enabled = false;
            //AsyncContext.Run(() => _downloader.Cleanup());
            Task.Run(async () =>
            {
                //btnAction.Enabled = false;
                //await _downloader.ExtractContent();
                await _downloader.Cleanup();
                await _downloader.RefreshState();
                //btnAction.Enabled = true;
            });
        }

        private void MakeTransparent(Control label, Control pictureBox)
        {
            var pos = this.PointToScreen(label.Location);
            pos = pictureBox1.PointToClient(pos);
            label.Parent = pictureBox1;
            label.Location = pos;
            label.BackColor = Color.Transparent;
        }

        private static PrivateFontCollection LoadPrivateFont(byte[] celestiaMediumReduxName)
        {
//Create your private font collection object.
            PrivateFontCollection pfc = new PrivateFontCollection();
            //Select your font from the resources.
            //My font here is "Digireu.ttf"
            int fontLength = celestiaMediumReduxName.Length;

            // create a buffer to read in to
            byte[] fontdata = celestiaMediumReduxName;

            // create an unsafe memory block for the font data
            System.IntPtr data = Marshal.AllocCoTaskMem(fontLength);

            // copy the bytes to the unsafe memory block
            Marshal.Copy(fontdata, 0, data, fontLength);

            // pass the font to the font collection
            pfc.AddMemoryFont(data, fontLength);

            // free up the unsafe memory
            Marshal.FreeCoTaskMem(data);
            return pfc;
        }

        private async void btnAction_Click(object sender, EventArgs e)
        {
            btnAction.Enabled = false;
            switch (_downloader.State)
            {
                case GameState.Unknown:
                    break;
                case GameState.NotFound:
                case GameState.UpdateAvailable:
                    var task1 = Task.Run(() => _downloader.DoInstallation());
                    try{
                        await task1;
                    }catch(Exception ex){
                        MessageBox.Show(ex.ToString());
                    }
                    break;
                case GameState.UpToDate:
                    if(Downloader.OperatingSystem == OS.WindowsX64 || Downloader.OperatingSystem == OS.WindowsX86)
                        Process.Start("game\\loe.exe");
                    else{
						if (Downloader.OperatingSystem == OS.X11 || Downloader.OperatingSystem == OS.Mac) {
							var loe = "LoE.app";

							new Process().RunInlineAndWait(new ProcessStartInfo("chmod", "-R 777 " + _downloader.GameInstallFolder.GetChildFileWithName(loe))
							{
								UseShellExecute = false,
								WindowStyle = ProcessWindowStyle.Minimized
							});
							Process.Start("./game/" + loe);
						}
						else {
							var loe = "LoE.x86" + (Environment.Is64BitProcess ? "_64" : "");
							Process.Start("./game/" + loe);
						}
                    }
                    break;
                case GameState.Offline:
                    break;
                case GameState.LauncherOutOfDate:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            btnAction.Enabled = true;
            //AsyncContext.Run(() => _downloader.PrepareUpdate());
            //AsyncContext.Run(() => _downloader.InstallUpdate());

            //MessageBox.Show("To Download: " + _downloader._data.ToProcess.Count);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            pbState.Maximum = _downloader.Progress.Max;
            pbState.Value = _downloader.Progress.Current;
            lblDownloadedAmount.Text = $"{_downloader.Progress.Text}\n{BytesToString(_downloader.BytesDownloaded)} downloaded";
            if (_downloader.Progress.Marquee)
            {
                pbState.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                pbState.Style = ProgressBarStyle.Continuous;
            }
            var enabledState = btnAction.Enabled;
            switch (_downloader.State)
            {
                case GameState.Unknown:
                    btnAction.Text = "Unknown";
                    enabledState = false;
                    break;
                case GameState.NotFound:
                    btnAction.Text = "Install";
                    enabledState = true;
                    break;
                case GameState.UpdateAvailable:
                    btnAction.Text = "Update";
                    enabledState = true;
                    break;
                case GameState.UpToDate:
                    btnAction.Text = "Launch";
                    enabledState = true;
                    break;
                case GameState.Offline:
                    btnAction.Text = "Offline";
                    enabledState = false;
                    break;
                case GameState.LauncherOutOfDate:
                    btnAction.Text = "Error";
                    enabledState = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            if (_downloader.Progress.Processing)
            {
                enabledState = false;
            }

            btnAction.Enabled = enabledState;
        }

        static String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
    }
}
