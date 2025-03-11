using System;
namespace LoE_Launcher.Core;

public class ProgressData(Downloader model)
{
    private readonly object countLock = new object();

    protected Downloader Model { get; private set; } = model;
    public int Max { get; set; } = 100;
    public int Current { get; set; } = 0;
    public bool Marquee { get; set; } = false;
    public bool Processing { get; set; } = false;
    public bool IsFinished { get; set; } = false;
    public string Text => GetText();

    public void ResetCounter(int count, bool changeFromMarquee = false)
    {
        Current = 0;
        Max = count;
        if (changeFromMarquee)
        {
            Marquee = false;
        }
    }

    public void Count(int count = 1)
    {
        lock (countLock)
        {
            if (Current + count > Max)
            {
                throw new ArithmeticException("Current can not be higher than Maximum");
                //return;
            }
            Current += count;
        }
    }
    
    public void SetCount(int value)
    {
        Current = value;
    }

    public void Complete()
    {
        Processing = false;
        IsFinished = true;
    }
    
    protected virtual string GetText()
    {
        return "Processing....";
    }
}