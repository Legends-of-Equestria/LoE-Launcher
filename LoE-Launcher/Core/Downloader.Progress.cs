using Models.Utils;

namespace LoE_Launcher.Core;

partial class Downloader
{
    public class RefreshProgress(Downloader model) : ProgressData(model)
    {
        protected override string GetText()
        {
            if (IsFinished)
            {
                if (Model._data?.ToProcess?.Count > 0)
                {
                    return "Files to update: {0}".Format(Model._data.ToProcess.Count);
                }

                return "Ready to launch!";
            }
            else
            {
                return "Verifying files...";
            }
        }
    }

    public class PreparingProgress(Downloader model) : ProgressData(model)
    {
        protected override string GetText()
        {
            if (IsFinished)
            {
                return "Preparing install...";
            }
            else
            {
                if (Marquee)
                {
                    return "Preparing install...";
                }

                return "Preparing install ({0}/{1})...".Format(Current, Max);
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
                {
                    
                }

                // if (!string.IsNullOrWhiteSpace(FlavorText))
                // {
                //     return $"Installing ({Current}/{Max})... {FlavorText}";
                // }

                return $"Installing ({Current}/{Max})...";
            }
        }
    }

    /// <summary>
    /// Combined progress for the continuous installation process
    /// </summary>
    public class CombinedProgress(Downloader model) : InstallingProgress(model)
    {
        public string CurrentOperation { get; set; } = "Preparing";

        protected override string GetText()
        {
            if (IsFinished)
            {
                return "Update complete";
            }
            else
            {
                if (Marquee)
                {
                    return $"{CurrentOperation}...";
                }

                // if (!string.IsNullOrWhiteSpace(FlavorText))
                // {
                //     return $"{CurrentOperation} ({Current}/{Max})... {FlavorText}";
                // }
                
                return $"{CurrentOperation} ({Current}/{Max})...";
            }
        }
    }

    public class RepairingProgress(Downloader model) : ProgressData(model)
    {
        protected override string GetText()
        {
            if (IsFinished)
            {
                return "Repairing...";
            }
            else
            {
                if (Marquee)
                {
                    return "Repairing...";
                }

                return "Repairing ({0}/{1})...".Format(Current, Max);
            }
        }
    }

    public class LaunchingProgress(Downloader model) : ProgressData(model)
    {
        protected override string GetText()
        {
            if (IsFinished)
            {
                 return "Launching...";
            }
            else
            {
                return "Launching...";
            }
        }
    }
    
    public class ErrorProgress(string errorMessage, Downloader model) : ProgressData(model)
    {
        protected override string GetText()
        {
            return errorMessage;
        }
    }
    
    public class CleanupProgress(Downloader model) : ProgressData(model)
    {
        protected override string GetText()
        {
            if (IsFinished)
            {
                return "Cleanup complete";
            }
            else
            {
                return "Cleaning up...";
            }
        }
    }
}