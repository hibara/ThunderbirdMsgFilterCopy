using System;
using System.ComponentModel;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// SPEC §3.4: polls Process.GetProcessesByName("thunderbird") every 2 seconds and
/// exposes <see cref="IsRunning"/> via INotifyPropertyChanged.
/// </summary>
public interface IThunderbirdMonitor : INotifyPropertyChanged, IDisposable
{
    bool IsRunning { get; }
    void Start();
    void Stop();
}
