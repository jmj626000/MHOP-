"""在线人数：心跳 + 滑动窗口计数。

单机内存实现，接口语义与 Redis 一致；上生产替换为 Redis ZSET 即可：
  ZADD online <ts> <key> / ZREMRANGEBYSCORE online 0 <ts-90> / ZCARD online
"""
import time

_last_seen: dict[str, float] = {}

WINDOW_SECONDS = 90


def heartbeat(key: str) -> int:
    now = time.time()
    _last_seen[key] = now
    expired = [k for k, ts in _last_seen.items() if now - ts > WINDOW_SECONDS]
    for k in expired:
        _last_seen.pop(k, None)
    return len(_last_seen)


def online_count() -> int:
    now = time.time()
    return sum(1 for ts in _last_seen.values() if now - ts <= WINDOW_SECONDS)
