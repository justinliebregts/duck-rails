import csv, json, statistics, math
from pathlib import Path

root = Path('X:/Work/duck-rails')
base = root / 'output/quest-performance'
out = root / 'docs/validation/quest-crew-load-2026-11-14'
out.mkdir(parents=True, exist_ok=True)
runs = json.loads((base / 'crew-comparison-20261114-v2/runs.json').read_text(encoding='utf-8-sig'))
summaries = []
poses = []
def pose_median(rows, axis):
    values = [float(r[axis]) for r in rows]
    if axis in ('head_yaw', 'head_pitch'):
        reference = values[0]
        return (reference + statistics.median((v - reference + 180) % 360 - 180 for v in values)) % 360
    return statistics.median(values)

for run in runs:
    folder = base / run['id']
    summary = json.loads((folder / 'summary.json').read_text())
    meta = summary['metadata']
    assert 'Local visual hands use fixed head-relative animated targets' in meta['scope'], 'Old fixture build'
    assert summary['status']['state'] == 'complete', run
    assert meta['syntheticRigs'] == run['crew'] - 1 and meta['syntheticLights'] == run['crew'], run
    assert meta['crewPoseApplications'] > 10000 and meta['crewLipApplications'] == meta['crewPoseApplications'], run
    views = [s for s in summary['segments'] if '/yaw-' in s['segment']]
    assert len(views) == 24 and all(s['focus_tracking_pct'] >= 95 and s['native_samples'] >= 5 for s in views), run
    summary['run'] = run
    device_rows = []
    for csv_path in (folder / 'ovr-metrics').glob('*.csv'):
        with csv_path.open(newline='', encoding='utf-8-sig') as source:
            device_rows.extend(r for r in csv.DictReader(source) if float(r['Time Stamp']) > 60000)
    context = {}
    for field in ('battery_temperature_celcius', 'cpu_frequency_MHz', 'gpu_frequency_MHz', 'app_pss_MB', 'eye_buffer_width', 'eye_buffer_height', 'display_refresh_rate', 'render_scale'):
        values = [float(r[field]) for r in device_rows if r.get(field) and float(r[field]) > 0]
        if values: context[field] = {'min': min(values), 'max': max(values), 'median': statistics.median(values)}
    summary['device_context_after_first_minute'] = context
    summaries.append(summary)
    by_segment = {}
    with (folder / 'frames.csv').open(newline='') as f:
        for row in csv.DictReader(f):
            if '/yaw-' in row['segment'] and row['valid'] == '1':
                by_segment.setdefault(row['segment'], []).append(row)
    poses.append({k: {axis: pose_median(rows, axis) for axis in ('head_x', 'head_y', 'head_z', 'head_yaw', 'head_pitch')} for k, rows in by_segment.items()})
    (out / f"{run['label']}-metadata.json").write_text(json.dumps(meta, indent=2))
    (out / f"{run['label']}-summary.json").write_text(json.dumps(summary, indent=2))

keys = ['buildGuid', 'unity', 'graphicsApi', 'quality', 'eyeWidth', 'eyeHeight', 'msaa', 'renderScale']
for key in keys:
    assert len({str(s['metadata'][key]) for s in summaries}) == 1, key
for key in ('seed', 'blocks', 'viewSeconds', 'farClipMeters'):
    assert len({str(s['metadata']['request'][key]) for s in summaries}) == 1, key

metrics = ('native_app_fps_p50', 'app_cpu_p50_ms', 'app_gpu_p50_ms', 'app_cpu_p95_ms', 'app_gpu_p95_ms', 'draw_calls_p50', 'triangles_p50', 'long_update_intervals_pct', 'unity_memory_max_mb', 'native_stale_frames')
rows = []
names = [s['segment'] for s in summaries[0]['segments'] if '/yaw-' in s['segment']]
for name in names:
    row = {'segment': name}
    for crew in (4, 8):
        views = [next(v for v in s['segments'] if v['segment'] == name) for s in summaries if s['run']['crew'] == crew]
        assert views, crew
        for metric in metrics:
            vals = [v[metric] for v in views]
            assert all(v is not None for v in vals), (name, metric)
            row[f'{crew}_{metric}'] = statistics.mean(vals)
            row[f'{crew}_{metric}_range'] = [min(vals), max(vals)]
        row[f'{crew}_worst_p95_ms'] = max(max(v['app_cpu_p95_ms'], v['app_gpu_p95_ms']) for v in views)
        row[f'{crew}_native_samples'] = sum(v['native_samples'] for v in views)
    row['fps_delta_8_minus_4'] = row['8_native_app_fps_p50'] - row['4_native_app_fps_p50']
    row['cpu_delta_ms'] = row['8_app_cpu_p50_ms'] - row['4_app_cpu_p50_ms']
    row['gpu_delta_ms'] = row['8_app_gpu_p50_ms'] - row['4_app_gpu_p50_ms']
    pairs = [(a[name], b[name]) for a in poses for b in poses]
    row['max_head_position_difference_m'] = max(math.sqrt(sum((a[k] - b[k]) ** 2 for k in ('head_x', 'head_y', 'head_z'))) for a, b in pairs)
    row['max_pitch_difference_deg'] = max(abs((a['head_pitch'] - b['head_pitch'] + 180) % 360 - 180) for a, b in pairs)
    row['max_yaw_difference_deg'] = max(abs((a['head_yaw'] - b['head_yaw'] + 180) % 360 - 180) for a, b in pairs)
    rows.append(row)

result = {'runs': runs, 'settings': {k: summaries[0]['metadata'][k] for k in keys}, 'views': rows,
          'aggregation': 'For each view, arithmetic mean of per-run medians/p95 values; ranges retain repeat variation. Worst p95 is max across CPU/GPU and repeats. This is not a pooled percentile.',
          'head_pose_max_difference_m': max(r['max_head_position_difference_m'] for r in rows),
          'head_pitch_max_difference_deg': max(r['max_pitch_difference_deg'] for r in rows),
          'head_yaw_max_difference_deg': max(r['max_yaw_difference_deg'] for r in rows)}
(out / 'comparison.json').write_text(json.dumps(result, indent=2))
with (out / 'comparison.csv').open('w', newline='') as f:
    writer = csv.DictWriter(f, fieldnames=list(rows[0])); writer.writeheader(); writer.writerows(rows)
print(json.dumps({k:v for k,v in result.items() if k != 'views'}, indent=2))
for row in rows:
    print(row['segment'], 'fps4/8', round(row['4_native_app_fps_p50'],1), round(row['8_native_app_fps_p50'],1),
          'cpu4/8', round(row['4_app_cpu_p50_ms'],2), round(row['8_app_cpu_p50_ms'],2),
          'gpu4/8', round(row['4_app_gpu_p50_ms'],2), round(row['8_app_gpu_p50_ms'],2),
          'worstP95-4/8', round(row['4_worst_p95_ms'],2), round(row['8_worst_p95_ms'],2))
