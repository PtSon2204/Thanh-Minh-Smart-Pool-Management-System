"""Measure the real Python binary protocol, inference and render separately.

The boxes are render-only fixtures; this script never calls the API or writes
alerts/incidents. Results measure processing cost, not model accuracy or FPS.
"""
import argparse
import json
import os
from pathlib import Path
import statistics
import subprocess
import sys
import time

import cv2


def main():
    root = Path(__file__).resolve().parents[2]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--font", default="")
    parser.add_argument("--iterations", type=int, default=20)
    parser.add_argument("--output", type=Path, default=root / "backend/.test-runtime/safety-audit/render-timing.json")
    args = parser.parse_args()
    if not 3 <= args.iterations <= 100:
        parser.error("iterations must be 3..100")
    image = cv2.imread(str(root / "frontend/thanh-minh-smart-pool/src/assets/anh-be-boi.jpg"))
    if image is None:
        raise ValueError("Missing local benchmark image")
    ratio = min(1, 1280 / image.shape[1], 720 / image.shape[0])
    image = cv2.resize(image, (round(image.shape[1] * ratio), round(image.shape[0] * ratio)))
    ok, encoded = cv2.imencode(".jpg", image, [cv2.IMWRITE_JPEG_QUALITY, 90])
    if not ok:
        raise ValueError("Cannot encode local benchmark image")
    jpeg = encoded.tobytes()
    env = dict(os.environ, OMP_NUM_THREADS="1", MKL_NUM_THREADS="1", SAFETY_ORT_PROVIDER="CPUExecutionProvider")
    worker = subprocess.Popen([sys.executable, "-u", str(root / "backend/ai/worker.py"), str(root / "model/best.onnx"), args.font],
                              stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, env=env)
    def send(header, body=None):
        worker.stdin.write(json.dumps(header).encode() + b"\n")
        if body is not None:
            worker.stdin.write(body)
        worker.stdin.flush()
        line = worker.stdout.readline()
        if not line:
            raise RuntimeError("Worker stopped")
        response = json.loads(line)
        if "error" in response:
            raise RuntimeError("Worker operation failed")
        return response
    try:
        if not json.loads(worker.stdout.readline()).get("ready"):
            raise RuntimeError("Worker not ready")
        results = []
        for count in (1, 5):
            boxes = [{"trackId": str(i), "episode": "render-only", "severity": "CRITICAL", "ratio": .9, "timestamp": 0,
                      "box": [.05 + i * .15, .2, .15 + i * .15, .7]} for i in range(count)]
            inference, render = [], []
            for index in range(args.iterations + 3):
                started = time.perf_counter()
                send({"op": "infer", "key": "benchmark", "resetTracker": index == 0, "jpegLength": len(jpeg)}, jpeg)
                inferred = time.perf_counter()
                response = send({"op": "render", "decisions": boxes, "binary": True})
                length = response.get("jpegLength", 0)
                if not 4 <= length <= 4 * 1024 * 1024 or len(worker.stdout.read(length)) != length:
                    raise RuntimeError("Truncated rendered JPEG")
                finished = time.perf_counter()
                if index >= 3:
                    inference.append((inferred - started) * 1000)
                    render.append((finished - inferred) * 1000)
            results.append({"renderOnlyBoxes": count, "inferenceMedianMs": round(statistics.median(inference), 2),
                            "renderMedianMs": round(statistics.median(render), 2),
                            "inferPlusRenderMedianMs": round(statistics.median([a + b for a, b in zip(inference, render)]), 2)})
        report = {"fixtureOnly": True, "databaseWrites": False, "provider": "CPUExecutionProvider", "threads": 1,
                  "width": image.shape[1], "height": image.shape[0], "iterations": args.iterations, "results": results}
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report, indent=2))
    finally:
        worker.kill()
        worker.wait(timeout=5)


if __name__ == "__main__":
    main()
