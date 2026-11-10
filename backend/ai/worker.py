"""Private JSON-lines worker. Uses the supplied ONNX weights; never downloads/trains a model.
stdout is protocol only. Input paths originate from the API's private storage, not clients.
Risk decisions are made in the shared .NET RiskDecisionEngine for both LIVE and UPLOAD.
"""
import os
os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")
os.environ.setdefault("YOLO_AUTOINSTALL", "false")

import ast
import base64
import contextlib
import json
import sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".deps"))
from pathlib import Path
from types import SimpleNamespace

import cv2
import numpy as np
import onnxruntime as ort
from rendering import draw
from ultralytics.engine.results import Boxes
from ultralytics.trackers.byte_tracker import BYTETracker


class CameraTracker(BYTETracker):
    @staticmethod
    def reset_id():
        # Ultralytics normally resets a process-global counter in each constructor.
        # With interleaved cameras that can reuse IDs still active in another tracker.
        # Keep IDs monotonic for the lifetime of this single-threaded worker instead.
        pass


class Detector:
    def __init__(self, model_path):
        path = Path(model_path)
        if not path.is_file() or path.suffix.lower() != ".onnx":
            raise ValueError("A local ONNX model is required")
        opts = ort.SessionOptions()
        # Backend sets the CPU fallback thread budget; LIVE is configurable. GPU is
        # preferred where available, and upload still runs in a separate process.
        threads = max(1, int(os.environ.get("OMP_NUM_THREADS", "1")))
        opts.intra_op_num_threads = threads
        opts.inter_op_num_threads = 1
        # Ưu tiên GPU (DirectML) nếu build onnxruntime-directml được cài; không thì tự chạy CPU.
        # DmlExecutionProvider (onnxruntime-directml), không phải "DirectMLExecutionProvider".
        preferred = os.environ.get("SAFETY_ORT_PROVIDER", "DmlExecutionProvider,CPUExecutionProvider").split(",")
        available = set(ort.get_available_providers())
        providers = [p for p in preferred if p in available] or ["CPUExecutionProvider"]
        if "DmlExecutionProvider" in providers:
            opts.enable_mem_pattern = False
            opts.execution_mode = ort.ExecutionMode.ORT_SEQUENTIAL
        self.session = ort.InferenceSession(str(path), sess_options=opts, providers=providers)
        metadata = self.session.get_modelmeta().custom_metadata_map
        self.names = {int(k): str(v).lower() for k, v in ast.literal_eval(metadata["names"]).items()}
        if set(self.names.values()) != {"normal", "drowning"}:
            raise ValueError("Model classes must be normal and drowning")
        shape = self.session.get_inputs()[0].shape
        if len(shape) != 4 or shape[0] != 1 or shape[1] != 3 or not isinstance(shape[2], int) or shape[2] != shape[3]:
            raise ValueError("Only static batch-1 square YOLO ONNX inputs are supported")
        self.size = shape[2]
        self.trackers = {}

    def infer(self, image, key):
        h, w = image.shape[:2]
        scale = min(self.size / w, self.size / h)
        nw, nh = round(w * scale), round(h * scale)
        left, top = (self.size - nw) // 2, (self.size - nh) // 2
        padded = np.full((self.size, self.size, 3), 114, dtype=np.uint8)
        padded[top:top + nh, left:left + nw] = cv2.resize(image, (nw, nh))
        tensor = padded[:, :, ::-1].transpose(2, 0, 1).copy()[None].astype(np.float32) / 255.0
        output = self.session.run(None, {self.session.get_inputs()[0].name: tensor})[0]
        if output.ndim != 3 or output.shape[1] != 4 + len(self.names):
            raise ValueError("Unsupported YOLO output shape")
        rows = output[0].T
        classes = rows[:, 4:].argmax(1)
        scores = rows[np.arange(len(rows)), classes + 4]
        mask = scores >= 0.10  # ByteTrack needs low-confidence detections too.
        rows, classes, scores = rows[mask], classes[mask], scores[mask]
        boxes = rows[:, :4].copy()
        boxes[:, 0] = (boxes[:, 0] - boxes[:, 2] / 2 - left) / scale
        boxes[:, 1] = (boxes[:, 1] - boxes[:, 3] / 2 - top) / scale
        boxes[:, 2:] /= scale
        indices = cv2.dnn.NMSBoxes(boxes.tolist(), scores.tolist(), 0.10, 0.45, top_k=300)
        indices = np.asarray(indices, dtype=int).flatten()
        boxes, scores, classes = boxes[indices], scores[indices], classes[indices]
        boxes[:, 2:] += boxes[:, :2]
        boxes[:, [0, 2]] = boxes[:, [0, 2]].clip(0, w)
        boxes[:, [1, 3]] = boxes[:, [1, 3]].clip(0, h)
        data = np.column_stack((boxes, scores, classes)).astype(np.float32)
        if key not in self.trackers:
            if len(self.trackers) >= 32:
                self.trackers.pop(next(iter(self.trackers)))
            self.trackers[key] = CameraTracker(SimpleNamespace(track_high_thresh=0.25, track_low_thresh=0.10,
                new_track_thresh=0.25, track_buffer=30, match_thresh=0.8, fuse_score=True))
        tracks = self.trackers[key].update(Boxes(data, (h, w)).numpy(), image)
        return [{"trackId": str(int(t[4])), "class": self.names[int(t[6])], "confidence": float(t[5]),
                 "box": [float(np.clip(t[0] / w, 0, 1)), float(np.clip(t[1] / h, 0, 1)),
                         float(np.clip(t[2] / w, 0, 1)), float(np.clip(t[3] / h, 0, 1))]} for t in tracks]


def main():
    # Respect the process-specific thread budget for preprocessing as well as ORT.
    cv2.setNumThreads(max(1, int(os.environ.get("OMP_NUM_THREADS", "1"))))
    detector = Detector(sys.argv[1])
    font = sys.argv[2] if len(sys.argv) > 2 else None
    image = None
    capture = writer = None
    frame_index = 0
    fps = total = 0
    print(json.dumps({"ready": True, "classes": detector.names}), flush=True)
    for line in sys.stdin:
        request = json.loads(line)
        op = request["op"]
        if op == "infer":
            image = cv2.imdecode(np.frombuffer(base64.b64decode(request["jpeg"]), np.uint8), cv2.IMREAD_COLOR)
            if image is None:
                raise ValueError("Invalid JPEG")
            result = {"detections": detector.infer(image, request["key"])}
        elif op == "open":
            # Phase 1 (analyze): output = null → chỉ mở capture, KHÔNG mở writer.
            # Bỏ encode/paint ở phase này để AI chạy nhanh hơn real-time (lead với video).
            capture = cv2.VideoCapture(request["input"])
            if not capture.isOpened():
                raise ValueError("Video decoder unavailable")
            fps = float(request["fps"])
            # Container duration * FPS can overstate frames (e.g. MP4 edit lists).
            # The backend has already decoded/verified the timeline with ffprobe.
            verified_frames = request.get("frames")
            total = int(verified_frames if verified_frames is not None else capture.get(cv2.CAP_PROP_FRAME_COUNT))
            frame_index = 0
            width, height = int(capture.get(cv2.CAP_PROP_FRAME_WIDTH)), int(capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
            output = request.get("output")
            if output:
                writer = cv2.VideoWriter(output, cv2.VideoWriter_fourcc(*"mp4v"), fps, (width, height))
                if not writer.isOpened():
                    raise ValueError("Video encoder unavailable")
            else:
                writer = None
            result = {"opened": True}
        elif op == "next":
            ok, image = capture.read()
            if not ok:
                if frame_index != total:
                    raise ValueError("Video decoding ended before the final frame")
                if writer:
                    writer.release()
                capture.release()
                result = {"done": True}
            else:
                result = {"timestamp": frame_index / fps, "progress": frame_index / max(total, 1),
                          "detections": detector.infer(image, "upload")}
                frame_index += 1
        elif op == "reopen_render":
            # Phase 2 (render offline): mở lại capture + writer để vẽ khung từ decisions đã
            # lưu (backend gửi qua "paint"). Chạy sau khi phân tích xong — không ảnh hưởng realtime.
            if writer:
                writer.release()
            capture = cv2.VideoCapture(request["input"])
            if not capture.isOpened():
                raise ValueError("Video decoder unavailable")
            fps = float(request["fps"])
            verified_frames = request.get("frames")
            total = int(verified_frames if verified_frames is not None else capture.get(cv2.CAP_PROP_FRAME_COUNT))
            frame_index = 0
            width, height = int(capture.get(cv2.CAP_PROP_FRAME_WIDTH)), int(capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
            writer = cv2.VideoWriter(request["output"], cv2.VideoWriter_fourcc(*"mp4v"), fps, (width, height))
            if not writer.isOpened():
                raise ValueError("Video encoder unavailable")
            result = {"opened": True}
        elif op == "capture":
            # Frame gốc chưa vẽ khung, dùng làm ảnh nền cho snapshot của cảnh báo.
            if image is None:
                raise ValueError("No frame captured yet")
            ok, encoded = cv2.imencode(".jpg", image, [cv2.IMWRITE_JPEG_QUALITY, 90])
            if not ok:
                raise ValueError("Snapshot encoding failed")
            result = {"jpeg": base64.b64encode(encoded).decode("ascii")}
        elif op == "step":
            # Phase 2 (render): chỉ đọc frame kế tiếp, KHÔNG infer (decisions đã có sẵn
            # từ phase 1 do backend gửi theo "paint").
            ok, image = capture.read()
            if not ok:
                if frame_index != total:
                    raise ValueError("Video decoding ended before the final frame")
                if writer:
                    writer.release()
                capture.release()
                result = {"done": True}
            else:
                result = {"timestamp": frame_index / fps, "progress": frame_index / max(total, 1)}
                frame_index += 1
        elif op in ("render", "paint"):
            # Chỉ vẽ khi có cảnh báo (WARNING/CRITICAL) trong khung; khung thường thì ghi thẳng
            # frame gốc — tránh tốn PIL/cvtColor mỗi frame (pipeline realtime).
            annotated = draw(image, request["decisions"], font) if any(
                d.get("severity") in ("WARNING", "CRITICAL") for d in request["decisions"]) else image
            if op == "paint":
                writer.write(annotated)
                result = {"painted": True}
            else:
                ok, encoded = cv2.imencode(".jpg", annotated, [cv2.IMWRITE_JPEG_QUALITY, 95])
                if not ok:
                    raise ValueError("Snapshot encoding failed")
                result = {"jpeg": base64.b64encode(encoded).decode("ascii")}
        else:
            raise ValueError("Unknown operation")
        print(json.dumps(result, allow_nan=False), flush=True)


if __name__ == "__main__":
    # Third-party inference libraries must not corrupt the protocol or leak file paths in API logs.
    protocol = sys.stdout
    try:
        with contextlib.redirect_stdout(sys.stderr):
            # main uses protocol stdout explicitly via a small print wrapper below.
            import builtins
            _print = builtins.print
            def print(*args, **kwargs):
                _print(*args, file=protocol, **kwargs)
            main()
    except Exception:
        protocol.write(json.dumps({"error": "AI_WORKER_FAILED"}) + "\n")
        protocol.flush()
        sys.exit(1)
