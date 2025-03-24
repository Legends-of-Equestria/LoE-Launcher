using System;
using System.Collections.Generic;

namespace LoE_Launcher.Core;

public class DownloadStats
{
    private readonly List<(DateTime Time, long Bytes, double Progress)> _samples = new List<(DateTime, long, double)>();
    private readonly int _maxSamples;
    private readonly TimeSpan _sampleInterval;
    
    private long _lastBytesDownloaded;
    private double _lastProgress;
    private DateTime _lastSampleTime;
    private double _currentSpeedBps;
    private TimeSpan _timeRemaining = TimeSpan.MaxValue;
    private bool _hasValidTimeEstimate;

    public string CurrentSpeed => GetFormattedSpeed();
    public string TimeRemaining => GetFormattedTimeRemaining();
    public bool HasValidSpeed => _currentSpeedBps > 0;
    public bool HasValidTimeEstimate => _hasValidTimeEstimate;

    public DownloadStats(int maxSamples = 5, TimeSpan? sampleInterval = null)
    {
        _maxSamples = maxSamples;
        _sampleInterval = sampleInterval ?? TimeSpan.FromSeconds(1);
        _lastSampleTime = DateTime.UtcNow;
    }

    public void Reset()
    {
        _samples.Clear();
        _lastBytesDownloaded = 0;
        _lastProgress = 0;
        _lastSampleTime = DateTime.UtcNow;
        _currentSpeedBps = 0;
        _timeRemaining = TimeSpan.MaxValue;
        _hasValidTimeEstimate = false;
    }

    public void Update(long bytesDownloaded, double progressPercentage)
    {
        try
        {
            var now = DateTime.UtcNow;
            if ((now - _lastSampleTime) < _sampleInterval)
            {
                return;
            }

            var bytesDelta = bytesDownloaded - _lastBytesDownloaded;
            var progressDelta = progressPercentage - _lastProgress;
            
            if (bytesDelta <= 0 || progressDelta <= 0)
            {
                _lastBytesDownloaded = bytesDownloaded;
                _lastProgress = progressPercentage;
                _lastSampleTime = now;
                return;
            }

            _samples.Add((now, bytesDownloaded, progressPercentage));
            
            while (_samples.Count > _maxSamples)
            {
                _samples.RemoveAt(0);
            }
            
            if (_samples.Count < 2)
            {
                _lastBytesDownloaded = bytesDownloaded;
                _lastProgress = progressPercentage;
                _lastSampleTime = now;
                return;
            }
            
            var firstSample = _samples[0];
            var windowTimespan = (now - firstSample.Time).TotalSeconds;
            var windowBytesDelta = bytesDownloaded - firstSample.Bytes;
            
            if (windowTimespan > 0 && windowBytesDelta > 0)
            {
                var newSpeed = windowBytesDelta / windowTimespan;
                
                if (_currentSpeedBps == 0)
                {
                    _currentSpeedBps = newSpeed;
                }
                else
                {
                    _currentSpeedBps = (0.3 * newSpeed) + (0.7 * _currentSpeedBps);
                }
                
                var progressWindowDelta = progressPercentage - firstSample.Progress;
                if (progressWindowDelta > 0)
                {
                    var secondsPerPercentage = windowTimespan / progressWindowDelta;
                    var percentageRemaining = 100 - progressPercentage;
                    var estimatedSecondsRemaining = secondsPerPercentage * percentageRemaining;
                    
                    if (estimatedSecondsRemaining > 0 && estimatedSecondsRemaining < 86400)
                    {
                        var newTimeRemaining = TimeSpan.FromSeconds(estimatedSecondsRemaining);
                        
                        if (_hasValidTimeEstimate)
                        {
                            _timeRemaining = TimeSpan.FromSeconds(
                                (0.3 * newTimeRemaining.TotalSeconds) + 
                                (0.7 * _timeRemaining.TotalSeconds));
                        }
                        else
                        {
                            _timeRemaining = newTimeRemaining;
                            _hasValidTimeEstimate = true;
                        }
                    }
                }
            }
            
            _lastBytesDownloaded = bytesDownloaded;
            _lastProgress = progressPercentage;
            _lastSampleTime = now;
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public string GetFormattedTimeRemaining()
    {
        if (!_hasValidTimeEstimate || _timeRemaining >= TimeSpan.FromHours(24))
        {
            return "Calculating...";
        }
        
        if (_timeRemaining.TotalHours >= 1)
        {
            var hours = (int)_timeRemaining.TotalHours;
            var minutes = _timeRemaining.Minutes;

            if (minutes > 0)
            {
                return $"{hours}h {minutes}m";
            }
            else
            {
                return $"{hours}h";
            }
        }
        else if (_timeRemaining.TotalMinutes >= 1)
        {
            var minutes = (int)_timeRemaining.TotalMinutes;
            var seconds = _timeRemaining.Seconds;

            if (seconds > 0 && minutes < 10)
            {
                return $"{minutes}m {seconds}s";
            }
            else
            {
                return $"{minutes}m";
            }
        }
        else
        {
            return $"{_timeRemaining.Seconds}s";
        }
    }
    
    public string GetFormattedSpeed()
    {
        if (_currentSpeedBps <= 0)
        {
            return "";
        }
            
        string[] units = ["B/s", "KB/s", "MB/s", "GB/s"];
        var unitIndex = 0;
        var speed = _currentSpeedBps;

        while (speed >= 1024 && unitIndex < units.Length - 1)
        {
            speed /= 1024;
            unitIndex++;
        }

        return $"{speed:F1} {units[unitIndex]}";
    }
}