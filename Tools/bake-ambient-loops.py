"""Bake circular tail/head crossfades from the approved, original Artlist WAVs.

Run from the repository root with --source-dir pointing to the original downloads.
Re-running always reads originals, so overlaps never accumulate. Existing Unity
asset paths and GUIDs are retained. Requires only Python's standard library.
"""

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import wave


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def sample(data, offset):
    return int.from_bytes(data[offset:offset + 3], "little", signed=True)


def bake(source, destination, seconds):
    if source.resolve() == destination.resolve():
        raise ValueError("The input must be an original download outside the output path")
    temporary = destination.with_suffix(".wav.baking")
    with wave.open(str(source), "rb") as original:
        channels, width, rate, frames, compression, _ = original.getparams()
        if width != 3 or compression != "NONE":
            raise ValueError(f"Expected approved 24-bit PCM master: {source}")
        overlap = min(round(seconds * rate), frames // 3)
        if overlap < 2:
            raise ValueError("Insufficient overlap samples")
        stride = channels * width
        head = original.readframes(overlap)
        original.setpos(frames - overlap)
        tail = original.readframes(overlap)
        blend = bytearray(len(head))
        for frame in range(overlap):
            # Complementary cosine ramps keep gain bounded and have zero endpoint slope.
            incoming = 0.5 - 0.5 * math.cos(math.pi * frame / (overlap - 1))
            outgoing = 1.0 - incoming
            for channel in range(channels):
                offset = frame * stride + channel * width
                value = round(sample(tail, offset) * outgoing + sample(head, offset) * incoming)
                blend[offset:offset + width] = value.to_bytes(width, "little", signed=True)
        assert blend[:stride] == tail[:stride]
        assert blend[-stride:] == head[-stride:]
        # Start after the source head. The loop boundary becomes the original
        # adjacent frames head[-1] -> source[overlap], with no artificial jump.
        original.setpos(overlap)
        try:
            with wave.open(str(temporary), "wb") as output:
                output.setparams((channels, width, rate, frames - overlap, "NONE", "not compressed"))
                remaining = frames - 2 * overlap
                while remaining:
                    count = min(remaining, 65536)
                    output.writeframesraw(original.readframes(count))
                    remaining -= count
                output.writeframesraw(blend)
            with wave.open(str(temporary), "rb") as check:
                assert check.getnframes() == frames - overlap
                first = check.readframes(1)
                check.setpos(check.getnframes() - 1)
                last = check.readframes(1)
                original.setpos(overlap - 1)
                assert last + first == original.readframes(2)
            os.replace(temporary, destination)
        finally:
            if temporary.exists():
                temporary.unlink()
    return {
        "source_sha256": sha256(source),
        "output_sha256": sha256(destination),
        "overlap_seconds": overlap / rate,
        "source_seconds": frames / rate,
        "loop_seconds": (frames - overlap) / rate,
        "checks": "24-bit PCM; overlap endpoints exact; frame count exact; wrap equals adjacent original samples",
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    folder = root / "Assets/Audio/Ambience"
    manifest = json.loads((folder / "ArtlistSources.json").read_text())
    tracks = [track for track in manifest["tracks"] if track["bed"]]
    # Check all originals before replacing any outputs.
    for track in tracks:
        if not (args.source_dir / track["download"]).is_file():
            raise FileNotFoundError(track["download"])
    results = {}
    for track in tracks:
        results[track["id"]] = bake(args.source_dir / track["download"], folder / (track["id"] + ".wav"), 3.0)
        print(f"{track['id']}: three-second overlap baked and wrap verified", flush=True)
    evidence = root / "docs/validation/ambient-director/loop-bakes.json"
    evidence.write_text(json.dumps(results, indent=2) + "\n")


if __name__ == "__main__":
    main()
