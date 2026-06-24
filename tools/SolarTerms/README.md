# Solar Terms Data Generation

`src/QingLi.Core/Calendars/SolarTermService.cs` 中的 `DayTable` 是离线生成并固化的 1901–2100 节气日期表。运行时只读内嵌表，不做联网或天文查询。

生成思路：

- 以二十四节气的现代定义为准：太阳视黄经每增加 15° 命中一个节气。
- 用近似太阳黄经公式求某时刻的太阳视黄经。
- 以每个节气的常见公历落点为初值，在 `±2` 天窗口内二分搜索命中时刻。
- 把 UTC 命中时刻换算成中国时区（UTC+08:00）后，取本地日期写入表。

生成时使用的参考思路：

- NOAA Solar Calculator 常用太阳位置近似公式（用于太阳平黄经、平近点角、黄经章动修正）
- 二十四节气按太阳视黄经定义的通行天文学口径

本次生成结果中，`2026-06-21` 命中 `夏至`，因此没有对 2026 年夏至做人为覆盖。

下面是用于生成 `DayTable` 的脚本摘录：

```python
import math
from datetime import datetime, timedelta, timezone

TZ8 = timezone(timedelta(hours=8))
TERMS = [
    ("小寒", 285, 1, 5), ("大寒", 300, 1, 20), ("立春", 315, 2, 4), ("雨水", 330, 2, 19),
    ("惊蛰", 345, 3, 6), ("春分",   0, 3, 20), ("清明",  15, 4, 5), ("谷雨",  30, 4, 20),
    ("立夏",  45, 5, 5), ("小满",  60, 5, 21), ("芒种",  75, 6, 6), ("夏至",  90, 6, 21),
    ("小暑", 105, 7, 7), ("大暑", 120, 7, 23), ("立秋", 135, 8, 7), ("处暑", 150, 8, 23),
    ("白露", 165, 9, 7), ("秋分", 180, 9, 23), ("寒露", 195,10, 8), ("霜降", 210,10, 23),
    ("立冬", 225,11, 7), ("小雪", 240,11, 22), ("大雪", 255,12, 7), ("冬至", 270,12, 21),
]

def julian_day(dt):
    y, m = dt.year, dt.month
    d = dt.day + (dt.hour + (dt.minute + dt.second / 60) / 60) / 24
    if m <= 2:
        y -= 1
        m += 12
    a = y // 100
    b = 2 - a + a // 4
    return int(365.25 * (y + 4716)) + int(30.6001 * (m + 1)) + d + b - 1524.5

def solar_longitude(dt):
    t = (julian_day(dt) - 2451545.0) / 36525
    l0 = (280.46646 + t * (36000.76983 + t * 0.0003032)) % 360
    m = math.radians((357.52911 + t * (35999.05029 - 0.0001537 * t)) % 360)
    c = (
        math.sin(m) * (1.914602 - t * (0.004817 + 0.000014 * t))
        + math.sin(2 * m) * (0.019993 - 0.000101 * t)
        + math.sin(3 * m) * 0.000289
    )
    true_longitude = l0 + c
    omega = math.radians(125.04 - 1934.136 * t)
    return (true_longitude - 0.00569 - 0.00478 * math.sin(omega)) % 360

def signed_diff(angle, target):
    return (angle - target + 540) % 360 - 180

def find_term(year, target, month, day):
    lo = datetime(year, month, day, tzinfo=timezone.utc) - timedelta(days=2)
    hi = datetime(year, month, day, tzinfo=timezone.utc) + timedelta(days=2)
    dlo = signed_diff(solar_longitude(lo), target)
    dhi = signed_diff(solar_longitude(hi), target)
    while dhi < dlo:
        dhi += 360

    for _ in range(80):
        mid = lo + (hi - lo) / 2
        dmid = signed_diff(solar_longitude(mid), target)
        while dmid < dlo:
            dmid += 360
        if dmid <= 0:
            lo, dlo = mid, dmid
        else:
            hi, dhi = mid, dmid

    return hi.astimezone(TZ8).date()
```
