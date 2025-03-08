using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LoE_Launcher.Core.Utils;

namespace LoE_Launcher.Core;

partial class Downloader
{
    public class RefreshProgress(Downloader model) : ProgressData(model)
    {

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
                return "Verifying Files...";
            }
        }
    }
    public class PreparingProgress(Downloader model) : ProgressData(model)
    {

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
    public class InstallingProgress(Downloader model) : ProgressData(model)
    {
        public string FlavorText { get; set; }

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

                if (!string.IsNullOrWhiteSpace(FlavorText))
                    return $"Installing ({Current}/{Max})... {FlavorText}";

                return $"Installing ({Current}/{Max})...";
            }
        }
    }

    public class RepairingProgress(Downloader model) : ProgressData(model)
    {

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

    public class UnzipProgress(Downloader model) : ProgressData(model)
    {

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
    public class CleanupProgress(Downloader model) : ProgressData(model)
    {

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
    public class UpToDateProgress(Downloader model) : ProgressData(model)
    {

        protected override string GetText()
        {
            return "Up to date";
        }
    }

    public class ErrorProgress(string message, Downloader model) : ProgressData(model)
    {

        protected override string GetText()
        {
            return message;
        }
    }
}