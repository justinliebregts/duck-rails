import json, statistics, shutil
from pathlib import Path

root = Path('X:/Work/duck-rails')
out = root / 'docs/validation/quest-crew-load-2026-11-14'
data = json.loads((out / 'comparison.json').read_text())
assert [r['crew'] for r in data['runs']] == [8, 4, 4, 8], 'Wait for all four completed captures'
views = data['views']
summaries = [json.loads((out / (r['label'] + '-summary.json')).read_text()) for r in data['runs']]
labels = {'container-yard-v1':'Container Yard', 'convenience-store-v1':'Convenience Store', 'midpoint-helipad-v1':'Helipad', 'farm-silos-v1':'Farm Silos', 'skyscraper-works-v1':'Skyscraper Works', 'finale-approach':'Finale approach'}
blocks = list(labels)
stats = {
    'mean_cpu_delta_ms': statistics.mean(v['cpu_delta_ms'] for v in views),
    'mean_gpu_delta_ms': statistics.mean(v['gpu_delta_ms'] for v in views),
    'median_cpu_delta_ms': statistics.median(v['cpu_delta_ms'] for v in views),
    'median_gpu_delta_ms': statistics.median(v['gpu_delta_ms'] for v in views),
    'measured_frames': sum(v['analyzed_frames'] for s in summaries for v in s['segments'] if '/yaw-' in v['segment']),
    'elapsed_minutes': sum(s['status']['elapsed'] for s in summaries) / 60,
    'min_focus_pct': min(v['focus_tracking_pct'] for s in summaries for v in s['segments'] if '/yaw-' in v['segment']),
    'run_stats': []
}
for s in summaries:
    vv = [v for v in s['segments'] if '/yaw-' in v['segment']]
    stats['run_stats'].append({'run':s['run'], 'min_view_fps_median':min(v['native_app_fps_p50'] for v in vv), 'stale_frames':sum(v['native_stale_frames'] for v in vv), 'native_seconds':sum(v['native_samples'] for v in vv), 'pose_applications':s['metadata']['crewPoseApplications'], 'temperature':s['device_context_after_first_minute']['battery_temperature_celcius'], 'cpu_clock':s['device_context_after_first_minute']['cpu_frequency_MHz'], 'gpu_clock':s['device_context_after_first_minute']['gpu_frequency_MHz']})
for crew in (4,8):
    stats[str(crew)] = {metric: statistics.mean(v[f'{crew}_{metric}'] for v in views) for metric in ('app_cpu_p50_ms','app_gpu_p50_ms','draw_calls_p50','triangles_p50')}
    stats[str(crew)]['fps_range'] = [min(v[f'{crew}_native_app_fps_p50_range'][0] for v in views),max(v[f'{crew}_native_app_fps_p50_range'][1] for v in views)]
    stats[str(crew)]['worst_p95_ms'] = max(v[f'{crew}_worst_p95_ms'] for v in views)
    stats[str(crew)]['max_repeat_cpu_median_difference_ms'] = max(v[f'{crew}_app_cpu_p50_ms_range'][1]-v[f'{crew}_app_cpu_p50_ms_range'][0] for v in views)
    stats[str(crew)]['max_repeat_gpu_median_difference_ms'] = max(v[f'{crew}_app_gpu_p50_ms_range'][1]-v[f'{crew}_app_gpu_p50_ms_range'][0] for v in views)
stats['blocks'] = []
for block in blocks:
    vv = [v for v in views if v['segment'].split('/')[0] == block]
    stats['blocks'].append({'id':block,'label':labels[block], **{str(c): {'worst_p95_ms':max(v[f'{c}_worst_p95_ms'] for v in vv), 'min_view_fps_median':min(v[f'{c}_native_app_fps_p50_range'][0] for v in vv)} for c in (4,8)}})
(out / 'overview.json').write_text(json.dumps(stats,indent=2))
shutil.copy(root/'output/quest-performance/analyze-crew.py', out/'analyze.py')
shutil.copy(root/'output/quest-performance/build-status.txt', out/'build-status.txt')
block_lines = []
for b in stats['blocks']:
    block_lines.append(f"- **{b['label']}**: worst CPU/GPU p95 {b['4']['worst_p95_ms']:.1f} / {b['8']['worst_p95_ms']:.1f} ms; lowest individual-view median FPS {b['4']['min_view_fps_median']:.1f} / {b['8']['min_view_fps_median']:.1f} (four / eight).")
run_lines = []
for s in stats['run_stats']:
    r = s['run']
    run_lines.append(f"- {r['label']}: `{r['id']}`; {s['pose_applications']:,} remote pose/lip applications; {s['stale_frames']} native stale frames across {s['native_seconds']} sampled seconds. [Summary]({r['label']}-summary.json).")
result_text = f"""## Results

**Both workloads generally held the 72 Hz target. The forward Skyscraper view
has little GPU headroom even with four ducks.** View medians ranged
{stats['4']['fps_range'][0]:.0f}–{stats['4']['fps_range'][1]:.0f} FPS with four and
{stats['8']['fps_range'][0]:.0f}–{stats['8']['fps_range'][1]:.0f} FPS with eight.
Native one-second counters sometimes report 73 at a 72 Hz display; that is counter
quantization, not a higher refresh rate. These are view medians, not minimum
instantaneous frame rates.

Across the 24 matched views, the equal-weight mean of per-view median CPU time
was **{stats['4']['app_cpu_p50_ms']:.2f} → {stats['8']['app_cpu_p50_ms']:.2f} ms**
and GPU time **{stats['4']['app_gpu_p50_ms']:.2f} → {stats['8']['app_gpu_p50_ms']:.2f} ms**
when moving from four rigs/four lights to eight/eight. The observed differences
were **{stats['mean_cpu_delta_ms']:+.2f} ms CPU** and **{stats['mean_gpu_delta_ms']:+.2f} ms GPU**.
These are small measured differences under the tested conditions, not isolated
per-avatar costs or a guarantee of spare capacity during real multiplayer play.

{chr(10).join(block_lines)}

All **96 measured views** passed, with **{stats['min_focus_pct']:.0f}% focus/tracking**
and {stats['measured_frames']:,} analyzed frame samples. Four surveys took
{stats['elapsed_minutes']:.1f} minutes of app capture; measured stationary views
account for 19.2 minutes. The same eye resolution, render scale and 72 Hz refresh
were retained. Battery temperature after the first minute ranged
{min(s['temperature']['min'] for s in stats['run_stats']):.0f}–{max(s['temperature']['max'] for s in stats['run_stats']):.0f}°C.

Largest paired camera position difference was **{data['head_pose_max_difference_m']:.2f} m**;
pitch/yaw differences were {data['head_pitch_max_difference_deg']:.3f}° /
{data['head_yaw_max_difference_deg']:.3f}°. The largest same-workload repeat
difference in a view's median GPU time was
{max(stats[str(c)]['max_repeat_gpu_median_difference_ms'] for c in (4,8)):.2f} ms,
so normal scene/pose/governor variation is material relative to the aggregate
workload delta. CPU and GPU times are parallel pipeline measurements and must
not be added together.

Native stale frames were nonzero; stable medians do not mean every frame was
on time. These counts cover only native sampling windows within measured views,
not the whole session or a compositor dropped-frame rate:

{chr(10).join(run_lines)}

Accepted capture window: {data['runs'][0]['startedUtc']} through
{data['runs'][-1]['finishedUtc']} (UTC). The practical finding is that this
synthetic eight-duck workload is feasible in the surveyed views, while the
Skyscraper forward view deserves further GPU headroom work before treating
72 FPS as robust across real gameplay. No performance optimization was applied
as part of this measurement task.

"""
report_path = out/'README.md'
report = report_path.read_text(encoding='utf-8')
start = report.find('## Results\n')
end = report.index('## Test conditions\n')
if start >= 0: report = report[:start] + report[end:]
anchor = report.index('## Test conditions\n')
report_path.write_text(report[:anchor] + result_text + report[anchor:], encoding='utf-8')
print(json.dumps(stats,indent=2))
