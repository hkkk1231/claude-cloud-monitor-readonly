"""Read-only Linux performance snapshot; no process, command or session output."""
import datetime
import json
import os
import shutil
import time
from pathlib import Path


def cpu_counters():
    # guest counters are already included in user/nice.
    values = [int(x) for x in Path('/proc/stat').read_text().splitlines()[0].split()[1:9]]
    return sum(values), values[3] + values[4]


def cpu_percent(before, after):
    total = after[0] - before[0]
    return None if total <= 0 else round(max(0, min(100, 100 * (1 - (after[1] - before[1]) / total))), 1)


def network_counters():
    rx = tx = 0
    for line in Path('/proc/net/dev').read_text().splitlines()[2:]:
        name, values = line.split(':', 1)
        if name.strip() == 'lo':
            continue
        fields = values.split()
        rx += int(fields[0])
        tx += int(fields[8])
    return rx, tx


def collect():
    before = cpu_counters()
    net_before = network_counters()
    start = time.monotonic()
    time.sleep(0.35)
    after = cpu_counters()
    net_after = network_counters()
    elapsed = time.monotonic() - start
    memory = {}
    for line in Path('/proc/meminfo').read_text().splitlines():
        key, value = line.split(':', 1)
        memory[key] = int(value.split()[0]) * 1024
    disk = shutil.disk_usage('/')
    total = memory['MemTotal']
    available = memory.get('MemAvailable')
    used = total - available if available is not None else None
    return {
        'ok': True, 'checkedAt': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'cpuPercent': cpu_percent(before, after), 'cpuCount': os.cpu_count(),
        'load': list(os.getloadavg()), 'memoryTotal': total, 'memoryUsed': used,
        'memoryPercent': round(used / total * 100, 1) if used is not None and total else None,
        'swapTotal': memory.get('SwapTotal'),
        'swapUsed': memory.get('SwapTotal', 0) - memory.get('SwapFree', 0),
        'diskTotal': disk.total, 'diskUsed': disk.used, 'diskFree': disk.free,
        'diskPercent': round(disk.used / disk.total * 100, 1),
        'networkRxPerSecond': round(max(0, net_after[0] - net_before[0]) / elapsed),
        'networkTxPerSecond': round(max(0, net_after[1] - net_before[1]) / elapsed),
        'uptimeSeconds': float(Path('/proc/uptime').read_text().split()[0]),
    }


if __name__ == '__main__':
    try:
        result = collect()
    except Exception:
        result = {'ok': False, 'error': 'performance_unavailable'}
    print(json.dumps(result))
