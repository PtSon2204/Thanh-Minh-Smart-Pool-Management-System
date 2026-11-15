"""Integration smoke test with repository ONNX weights and a real pool photograph.
No assertion about drowning sensitivity/specificity: that requires labelled validation videos.
"""
import base64
import json
import os
import subprocess
import sys
import tempfile
import unittest
from types import SimpleNamespace
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / ".deps"))
import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[2]


class WorkerSmokeTests(unittest.TestCase):
    def test_interleaved_cameras_keep_distinct_and_stable_track_ids(self):
        from worker import CameraTracker
        from ultralytics.engine.results import Boxes
        args = SimpleNamespace(track_high_thresh=.25, track_low_thresh=.1, new_track_thresh=.25,
                               track_buffer=30, match_thresh=.8, fuse_score=True)
        frame = np.zeros((100, 100, 3), dtype=np.uint8)
        boxes = np.array([[1, 1, 15, 15, .99, 0], [30, 30, 45, 45, .99, 0]], dtype=np.float32)
        first_camera = CameraTracker(args)
        initial = first_camera.update(Boxes(boxes, (100, 100)).numpy(), frame)
        initial_ids = set(initial[:, 4])
        self.assertEqual(2, len(initial_ids))
        other_camera = CameraTracker(args)
        other_camera.update(Boxes(boxes[:1], (100, 100)).numpy(), frame)
        expanded = np.vstack((boxes, np.array([[70, 70, 90, 90, .99, 0]], dtype=np.float32)))
        first_camera.update(Boxes(expanded, (100, 100)).numpy(), frame)
        tracked = first_camera.update(Boxes(expanded, (100, 100)).numpy(), frame)
        self.assertEqual(3, len(set(tracked[:, 4])))
        self.assertTrue(initial_ids.issubset(set(tracked[:, 4])))

    def test_real_model_protocol_snapshot_and_video(self):
        runtime = ROOT / "backend" / ".test-runtime"
        runtime.mkdir(exist_ok=True)
        image = cv2.imread(str(ROOT / "frontend/thanh-minh-smart-pool/src/assets/anh-be-boi.jpg"))
        self.assertIsNotNone(image)
        image = cv2.resize(image, (640, 360))
        process = subprocess.Popen([sys.executable, "-u", str(ROOT / "backend/ai/worker.py"), str(ROOT / "model/best.onnx"),
                                   "C:/Windows/Fonts/arial.ttf"], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True)
        def receive():
            result = json.loads(process.stdout.readline())
            self.assertNotIn("error", result)
            return result
        def send(payload):
            process.stdin.write(json.dumps(payload) + "\n")
            process.stdin.flush()
            return receive()
        try:
            self.assertTrue(receive()["ready"])
            ok, jpeg = cv2.imencode(".jpg", image)
            self.assertTrue(ok)
            for _ in range(5):
                result = send({"op": "infer", "key": "real-pool", "jpeg": base64.b64encode(jpeg).decode()})
                for detection in result["detections"]:
                    self.assertIn(detection["class"], ("normal", "drowning"))
                    self.assertTrue(all(0 <= x <= 1 for x in detection["box"]))
            # Renderer colors are tested independently from model accuracy.
            rendered = send({"op": "render", "decisions": [{"trackId": "test-render", "severity": "CRITICAL", "box": [.1, .1, .5, .8]}]})
            annotated = cv2.imdecode(np.frombuffer(base64.b64decode(rendered["jpeg"]), np.uint8), cv2.IMREAD_COLOR)
            self.assertEqual(image.shape, annotated.shape)
            self.assertGreater(int(annotated[36, 64, 2]), int(annotated[36, 64, 0]))
            # Model preprocessing may resize its tensor, but must retain the Full HD display image.
            full_hd = cv2.resize(image, (1920, 1080))
            ok, full_hd_jpeg = cv2.imencode(".jpg", full_hd)
            self.assertTrue(ok)
            send({"op": "infer", "key": "full-hd", "jpeg": base64.b64encode(full_hd_jpeg).decode()})
            rendered_hd = send({"op": "render", "decisions": []})
            decoded_hd = cv2.imdecode(np.frombuffer(base64.b64decode(rendered_hd["jpeg"]), np.uint8), cv2.IMREAD_COLOR)
            self.assertEqual((1080, 1920, 3), decoded_hd.shape)
            with tempfile.TemporaryDirectory(dir=runtime) as directory:
                source = str(Path(directory) / "input.avi")
                output = str(Path(directory) / "render.avi")
                writer = cv2.VideoWriter(source, cv2.VideoWriter_fourcc(*"mp4v"), 2, (640, 360))
                self.assertTrue(writer.isOpened())
                for _ in range(4):
                    writer.write(image)
                writer.release()
                self.assertTrue(send({"op": "open", "input": source, "output": output, "fps": 2})["opened"])
                for index in range(4):
                    next_frame = send({"op": "next"})
                    self.assertAlmostEqual(index / 2, next_frame["timestamp"])
                    # Capture exposes the raw (unannotated) frame as a decodable JPEG.
                    snap = send({"op": "capture"})
                    self.assertIsNotNone(cv2.imdecode(np.frombuffer(base64.b64decode(snap["jpeg"]), np.uint8), cv2.IMREAD_COLOR))
                    self.assertTrue(send({"op": "paint", "decisions": []})["painted"])
                self.assertTrue(send({"op": "next"})["done"])
                check = cv2.VideoCapture(output)
                self.assertEqual(4, int(check.get(cv2.CAP_PROP_FRAME_COUNT)))
                self.assertEqual(2, check.get(cv2.CAP_PROP_FPS))
                check.release()
        finally:
            process.kill()
            process.wait(timeout=10)
            process.stdin.close()
            process.stdout.close()


if __name__ == "__main__":
    unittest.main()
