using System;
using Pantheon.Networking.Transport;

namespace Pantheon.Game.Multiplayer;

/// <summary>Bytes per second sent and received, smoothed over a few seconds, for the debug panel (spec §106).</summary>
public sealed class TrafficMeter
{
    // Seconds over which a change in traffic shows fully.
    private const double Smoothing = 3.0;

    private TransportStats _last;
    private bool _started;

    /// <summary>Bytes sent per second, smoothed.</summary>
    public double SentPerSecond { get; private set; }

    /// <summary>Bytes received per second, smoothed.</summary>
    public double ReceivedPerSecond { get; private set; }

    /// <summary>Takes the traffic counted so far after some real time.</summary>
    /// <param name="stats">The transport's totals.</param>
    /// <param name="seconds">Real seconds since the last call.</param>
    public void Update(TransportStats stats, double seconds)
    {
        if (!_started)
        {
            (_last, _started) = (stats, true);
            return;
        }

        if (seconds <= 0.0)
        {
            return;
        }

        var weight = 1.0 - Math.Exp(-seconds / Smoothing);
        SentPerSecond += ((stats.BytesSent - _last.BytesSent) / seconds - SentPerSecond) * weight;
        ReceivedPerSecond += ((stats.BytesReceived - _last.BytesReceived) / seconds - ReceivedPerSecond) * weight;
        _last = stats;
    }
}
