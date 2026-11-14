"""Compare explicitly armed, same-build Motel wing or full-map performance captures."""
import argparse
import csv
import json
import statistics
from pathlib import Path


def read(folder):
    summary = json.loads((folder / 'summary.json').read_text(encoding='utf-8-sig'))
    with (folder / 'frames.csv').open(encoding='utf-8-sig', newline='') as f:
        frames = list(csv.DictReader(f))
    return summary, frames


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('control', type=Path)
    parser.add_argument('atlas', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--scope', choices=['wing', 'full-map'], default='wing')
    args = parser.parse_args()
    a, af = read(args.control)
    b, bf = read(args.atlas)
    for summary, expected in [(a, 'control'), (b, 'atlas')]:
        meta = summary['metadata']
        request_key, applied_key = ('motelFullMapVariant', 'appliedMotelFullMapVariant') if args.scope == 'full-map' else ('motelWingVariant', 'appliedMotelWingVariant')
        if meta['request'].get(request_key) != expected or meta.get(applied_key) != expected:
            raise ValueError(f'{expected}: requested/applied variant mismatch')
    for key in ['buildGuid', 'quality', 'graphicsApi', 'eyeWidth', 'eyeHeight', 'msaa', 'renderScale']:
        if a['metadata'][key] != b['metadata'][key]:
            raise ValueError(f'Mismatched build/settings: {key}')
    for key in ['seed', 'warmupSeconds', 'compoundSeconds', 'viewSeconds', 'farClipMeters', 'blocks']:
        if a['metadata']['request'][key] != b['metadata']['request'][key]:
            raise ValueError(f'Mismatched request: {key}')
    def pose(frames, segment):
        rows = [r for r in frames if r['segment'] == segment and r['valid'] == '1']
        return {k: statistics.median(float(r[k]) for r in rows) for k in ['head_pitch', 'head_yaw', 'head_x', 'head_y', 'head_z']}
    left = {s['segment']: s for s in a['segments'] if 'roadside-motel-v1/yaw-' in s['segment']}
    right = {s['segment']: s for s in b['segments'] if 'roadside-motel-v1/yaw-' in s['segment']}
    if len(left) != 4 or left.keys() != right.keys():
        raise ValueError('Both captures must contain the same four Motel views')
    rows = []
    for name, x in left.items():
        y = right[name]
        if min(x['focus_tracking_pct'], y['focus_tracking_pct']) < 95 or min(x['analyzed_frames'], y['analyzed_frames']) == 0:
            raise ValueError('Tracking coverage rejected: ' + name)
        pa, pb = pose(af, name), pose(bf, name)
        row = {'segment': name, 'control_run': args.control.name, 'atlas_run': args.atlas.name}
        for key in ['app_cpu_p50_ms', 'app_cpu_p95_ms', 'app_gpu_p50_ms', 'app_gpu_p95_ms', 'draw_calls_p50', 'triangles_p50', 'native_app_fps_p50', 'native_stale_frames', 'unity_memory_max_mb', 'long_update_intervals_pct']:
            row['control_' + key] = x[key]
            row['atlas_' + key] = y[key]
            row['delta_' + key] = y[key] - x[key] if x[key] is not None and y[key] is not None else None
        row['control_pitch_deg'] = (pa['head_pitch'] + 180) % 360 - 180
        row['atlas_pitch_deg'] = (pb['head_pitch'] + 180) % 360 - 180
        row['pitch_delta_deg'] = (pb['head_pitch'] - pa['head_pitch'] + 180) % 360 - 180
        row['yaw_delta_deg'] = (pb['head_yaw'] - pa['head_yaw'] + 180) % 360 - 180
        row['head_position_delta_m'] = sum((pb[k]-pa[k])**2 for k in ['head_x','head_y','head_z'])**.5
        row['pose_within_1deg_5cm'] = abs(row['pitch_delta_deg']) <= 1 and abs(row['yaw_delta_deg']) <= 1 and row['head_position_delta_m'] <= .05
        rows.append(row)
    args.output.mkdir(parents=True, exist_ok=True)
    with (args.output / 'comparison.csv').open('w', newline='') as f:
        writer = csv.DictWriter(f, fieldnames=rows[0].keys()); writer.writeheader(); writer.writerows(rows)
    baseline = 'Control uses the exact production consolidation meshes/materials and retained originals.' if args.scope == 'full-map' else 'Control wing uses original materials with boundary-clipped spatial chunks, not an exact production-prefab baseline.'
    result = {'scope': args.scope, 'views': rows, 'limits': ['Both variants resident: memory/load differences do not measure shipping asset savings.', baseline, 'Short solo rail views; no interior/combat, sustained thermal or multiplayer acceptance.']}
    (args.output / 'comparison.json').write_text(json.dumps(result, indent=2))
    for row in rows:
        print(row['segment'], 'CPU', row['control_app_cpu_p50_ms'], '->', row['atlas_app_cpu_p50_ms'], 'GPU', row['control_app_gpu_p50_ms'], '->', row['atlas_app_gpu_p50_ms'], 'draws', row['control_draw_calls_p50'], '->', row['atlas_draw_calls_p50'], 'pose matched', row['pose_within_1deg_5cm'])


if __name__ == '__main__':
    main()
