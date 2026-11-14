"""Summarize an explicit QuestPerformance capture; never reads diagnostic reports."""
import argparse
import csv
import json
import math
import re
from datetime import datetime
from collections import defaultdict
from pathlib import Path


def percentile(values, quantile):
    if not values:
        return None
    values = sorted(values)
    index = (len(values) - 1) * quantile
    lower = int(index)
    return values[lower] + (values[math.ceil(index)] - values[lower]) * (index - lower)


def numbers(rows, key, minimum=0):
    result = []
    for row in rows:
        value = row.get(key, '')
        if value:
            number = float(value)
            if math.isfinite(number) and number >= minimum:
                result.append(number)
    return result


def summarize(folder):
    status = json.loads((folder / 'status.json').read_text(encoding='utf-8-sig'))
    metadata = json.loads((folder / 'metadata.json').read_text(encoding='utf-8-sig'))
    stop_path = folder / 'capture-stop.json'
    stop = json.loads(stop_path.read_text(encoding='utf-8-sig')) if stop_path.exists() else None
    native = []
    seen = set()
    native_path = folder / 'vrapi-metrics.txt'
    origin = datetime.fromisoformat(metadata['utc'].replace('Z', '+00:00')).timestamp()
    if native_path.exists():
        for line in native_path.read_text(encoding='utf-8-sig', errors='replace').splitlines():
            stamp = re.match(r'\s*(\d+\.\d+)', line)
            fps = re.search(r'FPS=(\d+)/(\d+)', line)
            gpu = re.search(r'(?:^|,)App=([\d.]+)ms', line)
            stale = re.search(r'(?:^|,)Stale=(\d+)', line)
            if not (stamp and fps and gpu) or stamp[1] in seen:
                continue
            seen.add(stamp[1])
            native.append({'elapsed': float(stamp[1]) - origin, 'fps': float(fps[1]),
                           'hz': float(fps[2]), 'gpu_ms': float(gpu[1]),
                           'stale': int(stale[1]) if stale else None})
    groups = defaultdict(list)
    with (folder / 'frames.csv').open(encoding='utf-8-sig', newline='') as source:
        for row in csv.DictReader(source):
            groups[row['segment']].append(row)
    segments = []
    for name, all_rows in groups.items():
        rows = [r for r in all_rows if r['valid'] == '1']
        focused = [r for r in all_rows if r['valid'] != '0']
        dt = numbers(rows, 'interval_ms')
        hz = numbers(rows, 'refresh_hz', 1)
        cpu = numbers(rows, 'app_cpu_ms', 0.00001)
        gpu = numbers(rows, 'app_gpu_ms', 0.00001)
        cpu_source = 'OpenXR provider milliseconds'
        gpu_source = 'OpenXR provider milliseconds'
        if not cpu:
            cpu = [v * 1000 for v in numbers(rows, 'ovr_cpu_raw', .00000001)]
            cpu_source = 'OVRPlugin App_CpuTime seconds converted to milliseconds' if cpu else 'unavailable'
        if not gpu:
            gpu = [v * 1000 for v in numbers(rows, 'ovr_gpu_raw', .00000001)]
            gpu_source = 'OVRPlugin App_GpuTime seconds converted to milliseconds; checked against native/OVR CSV' if gpu else 'unavailable'
        budget = 1000 / percentile(hz, .5) if hz else None
        dropped = numbers(focused, 'dropped_total')
        missed = sum(float(r['interval_ms']) > 1.5 * 1000 / float(r['refresh_hz'])
                     for r in rows if r['refresh_hz'] and float(r['refresh_hz']) > 0)
        eligible = sum(bool(r['refresh_hz']) and float(r['refresh_hz']) > 0 for r in rows)
        # Native reports describe the previous second. Exclude reports that cross
        # segment/settling boundaries or segments with more than 5% invalid frames.
        device = [s for s in native if rows and len(focused) / len(all_rows) >= .95 and
                  float(rows[0]['elapsed_s']) + 1 <= s['elapsed'] <= float(rows[-1]['elapsed_s'])]
        memory = numbers(rows, 'unity_memory_bytes')
        main = percentile(numbers(rows, 'main_thread_ns'), .95)
        segments.append({
            'segment': name, 'frames': len(all_rows), 'analyzed_frames': len(rows),
            'focus_tracking_pct': 100 * len(focused) / len(all_rows),
            'refresh_hz': percentile(hz, .5), 'budget_ms': budget,
            'update_interval_p50_ms': percentile(dt, .5), 'update_interval_p95_ms': percentile(dt, .95),
            'update_interval_p99_ms': percentile(dt, .99), 'update_interval_max_ms': max(dt, default=None),
            'long_update_intervals_pct': 100 * missed / eligible if eligible else None,
            'app_cpu_p50_ms': percentile(cpu, .5), 'app_cpu_p95_ms': percentile(cpu, .95),
            'app_gpu_p50_ms': percentile(gpu, .5), 'app_gpu_p95_ms': percentile(gpu, .95),
            'cpu_samples': len(cpu), 'gpu_samples': len(gpu),
            'cpu_source': cpu_source, 'gpu_source': gpu_source,
            'compositor_dropped_delta': max(dropped) - min(dropped) if dropped and dropped == sorted(dropped) else None,
            'draw_calls_p50': percentile(numbers(rows, 'draw_calls'), .5),
            'triangles_p50': percentile(numbers(rows, 'triangles'), .5),
            'gc_bytes_p95': percentile(numbers(rows, 'gc_bytes'), .95),
            'unity_memory_max_mb': max(memory) / 1048576 if memory else None,
            'main_thread_p95_ms_including_waits': main / 1000000 if main is not None else None,
            'head_pitch_p50_degrees': percentile(numbers(rows, 'head_pitch'), .5),
            'native_samples': len(device),
            'native_app_gpu_p50_ms': percentile([s['gpu_ms'] for s in device], .5),
            'native_app_gpu_p95_ms': percentile([s['gpu_ms'] for s in device], .95),
            'native_app_fps_p50': percentile([s['fps'] for s in device], .5),
            'native_stale_frames': sum(s['stale'] for s in device if s['stale'] is not None) if device else None,
        })
    # A successful state alone is insufficient: a briefly untracked viewpoint can
    # finish before the runner's sustained-focus-loss timeout.
    views = [s for s in segments if '/yaw-' in s['segment']]
    rejected_views = [s['segment'] for s in views if s['focus_tracking_pct'] < 95 or
                      s['analyzed_frames'] == 0]
    coverage_valid = status['state'] == 'complete' and len(views) == 24 and not rejected_views
    result = {
        'status': status, 'metadata': metadata,
        'capture_stop': stop,
        'coverage': {'valid_solo_survey': coverage_valid, 'expected_views': 24,
                     'observed_views': len(views), 'rejected_views': rejected_views},
        'notes': [
            'Update intervals are measured between Unity Updates; they are not compositor presentation FPS.',
            'Only valid=1 samples analyzed; valid=0 lacks focus/tracking, valid=2 is viewpoint settling.',
            'CPU/GPU fields are OpenXR META milliseconds. Missing counters are unavailable, not zero cost.',
            'When provider counters are absent, use OVRPlugin GetPerfMetricsFloat seconds ×1000. Raw OVR values are retained in frames.csv; this build rounds them to 0.0001 seconds (0.1 ms).',
            'native_* uses this game PID\'s VrApi one-second performance reports, aligned by device UTC with runner metadata. Boundary-spanning reports are excluded. App is GPU milliseconds, cross-checked against OVR Metrics app_gpu_time_microseconds.',
            'Main/render thread profiler durations may include waits; do not equate them with active CPU cost.',
            'A completed solo rail survey does not establish interior, combat, multiplayer or release-build acceptance.',
            'Check head pose and visual framing before comparing runs. Thermal/charging context is in the adjacent preflight files.',
        ],
        'segments': segments,
    }
    (folder / 'summary.json').write_text(json.dumps(result, indent=2, allow_nan=False), encoding='utf-8')
    if segments:
        with (folder / 'summary.csv').open('w', newline='', encoding='utf-8') as output:
            writer = csv.DictWriter(output, fieldnames=list(segments[0]))
            writer.writeheader()
            writer.writerows(segments)
    print(f"{status['state']}: {len(segments)} segments; summary written to {folder}")
    if status['state'] != 'complete':
        print('PARTIAL ROUTE: use valid measured segments for tuning; this is not full-route acceptance.' if stop and status.get('message') == 'Cancelled by PC' else
              'INCOMPLETE RUN: inspect the failure before using measured segments.')
    elif not coverage_valid:
        print('INVALID VIEW COVERAGE: inspect missing/untracked viewpoints before using this survey.')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', type=Path)
    summarize(parser.parse_args().folder)
