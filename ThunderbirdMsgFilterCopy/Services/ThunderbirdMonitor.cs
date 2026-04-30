using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ThunderbirdMsgFilterCopy.Services;

public sealed class ThunderbirdMonitor : IThunderbirdMonitor
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2); // SPEC §3.4.2
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private bool _isRunning;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (_isRunning == value) return;
            _isRunning = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Start()
    {
        IsRunning = ProbeProcess();
        _loop ??= Task.Run(LoopAsync);
    }

    public void Stop() => _cts.Cancel();

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task LoopAsync()
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                IsRunning = ProbeProcess();
            }
        }
        catch (OperationCanceledException) { }
    }

    private static bool ProbeProcess()
    {
        // SPEC §3.4.1: Windows/macOS both use "thunderbird" as the process name.
        try
        {
            return Process.GetProcessesByName("thunderbird").Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
