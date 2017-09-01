using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoE_Launcher.Core
{
    public class ProgressData
    {
        protected Downloader Model { get; private set; }
        public int Max { get; set; } = 100;
        public int Current { get; set; } = 0;
        public bool Marquee { get; set; } = false;
        public bool Processing { get; set; } = false;
        public bool IsFinished { get; set; } = false;
        public string Text => GetText();

        public ProgressData(Downloader model)
        {
            Model = model;
        }

        public void ResetCounter(int count, bool changeFromMarquee = false)
        {
            Current = 0;
            Max = count;
            if (changeFromMarquee)
                Marquee = false;
        }

        public void Count(int count = 1)
        {
            if (Current + count > Max)
            {
                throw new ArithmeticException("Current can not be higher than Maximum");
                //return;
            }
            Current += count;
        }

        protected virtual string GetText()
        {
            return "Processing....";
        }
    }
}
