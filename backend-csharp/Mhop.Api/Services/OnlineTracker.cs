using System.Collections.Concurrent;

namespace Mhop.Services;

/// <summary>在线人数：心跳 + 90 秒滑动窗口（对应 Python online.py 的单机内存实现）。</summary>
public sealed class OnlineTracker
{
    private const double WindowSeconds = 90;
    private readonly ConcurrentDictionary<string, double> _lastSeen = new();

    public int Heartbeat(string key)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _lastSeen[key] = now;
        Evict(now);
        return _lastSeen.Count;
    }

    public int Count()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Evict(now);
        return _lastSeen.Count;
    }

    private void Evict(double now)
    {
        foreach (var (k, ts) in _lastSeen)
            if (now - ts > WindowSeconds)
                _lastSeen.TryRemove(k, out _);
    }
}
