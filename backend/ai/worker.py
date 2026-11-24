"""Private worker: JSON-line headers with optional length-delimited JPEG bodies for LIVE.
Uses the supplied ONNX weights; never downloads/trains a model.
stdout is protocol only. Input paths originate from the API's private storage, not clients.
Risk decisions are made in the shared .NET RiskDecisionEngine for both LIVE and UPLOAD.
"""
from ultralytics.engine.results import Boxes  # đóng gói các kết quả phát hiện đối tượng từ mô hình YOLO, bao gồm các hộp giới hạn (bounding boxes), điểm số (scores) và nhãn lớp (class labels).
import cv2  # decode jpeg, resize, nms
# theo dõi đối tượng bằng cách sử dụng thuật toán ByteTrack, một phương pháp theo dõi đối tượng dựa trên khung hình và các đặc trưng của đối tượng.
from ultralytics.trackers.byte_tracker import BYTETracker
from rendering import draw
import onnxruntime as ort
import numpy as np  # xử lý tensor, tính toán box, v.v.
from types import SimpleNamespace
from pathlib import Path
import sys
import json
import contextlib
import base64
import ast
import os
os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")
os.environ.setdefault("YOLO_AUTOINSTALL", "false")

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".deps"))


# theo dõi các đối tượng trong video từ camera, sử dụng thuật toán ByteTrack để duy trì danh tính của các đối tượng qua các khung hình liên tiếp.
class CameraTracker(BYTETracker):
    def _apply_match(self, track, detection, activated, refind):
        # của frame hiện tại, không phải vị trí lọc còn trễ phía sau người đang bơi.
        observation = detection.xyxy.copy()
        # liên kết detection với track hiện tại, cập nhật trạng thái của track và thêm track vào danh sách activated hoặc refind.
        super()._apply_match(track, detection, activated, refind)
        track.observation_box = observation

    # chọn bouding box để trả về cho backend, ưu tiên box quan sát thực của frame hiện tại (nếu có) thay vì box dự đoán từ bộ lọc Kalman.
    def _format_output(self):
        rows = []
        # danh sách các track đang được theo dõi, mỗi track đại diện cho một đối tượng đã được phát hiện và theo dõi qua các khung hình.
        for track in self.tracked_stracks:
            if not track.is_activated:
                continue
            row = track.result
            if track.frame_id == self.frame_id and hasattr(track, 'observation_box'):
                # tọa độ của bounding box quan sát thực tế trong frame hiện tại, được lưu trữ trong thuộc tính observation_box của track.
                row[:4] = track.observation_box.tolist()
            rows.append(row)
        return np.asarray(rows, dtype=np.float32)

    @staticmethod
    # kiểm tra 2 bounding box có gần như lồng nhau hay không, dựa trên tỷ lệ diện tích, diện tích giao nhau và khoảng cách giữa tâm của hai box.
    def _nested_observation(first, second):
        """Hai box gần đồng tâm, một box hầu như nằm trong box kia.

        Chỉ dùng để cứu ghép/bỏ birth trùng; không gộp hai ID đã được xác nhận.
        """
        first, second = np.asarray(first), np.asarray(second)
        # kiểm tra xem tất cả các giá trị trong hai bounding box có phải là số hữu hạn hay không, để đảm bảo rằng các phép tính tiếp theo không gặp lỗi do giá trị vô hạn hoặc NaN.
        if not np.isfinite(first).all() or not np.isfinite(second).all():
            return False
        # [x2 - x1, y2 - y1]
        sizes = np.array([first[2:] - first[:2], second[2:] - second[:2]])
        if (sizes <= 0).any():
            return False
        # tính diện tích của hai bounding box bằng cách nhân chiều rộng và chiều cao của chúng.
        areas = sizes.prod(axis=1)
        ratio = areas.min() / areas.max()
        intersection = np.maximum(0, np.minimum(
            first[2:], second[2:]) - np.maximum(first[:2], second[:2])).prod()
        centers = np.array([(first[:2] + first[2:]) / 2,
                           (second[:2] + second[2:]) / 2])
        center_distance = np.linalg.norm(
            centers[0] - centers[1]) / np.linalg.norm(sizes[areas.argmax()])
        return 0.20 <= ratio <= 0.70 and intersection / areas.min() >= 0.95 and center_distance <= 0.20

    def _post_first_association(self, pool, detections, u_track, u_detection, activated, refind):
        # xử lý các track và detection chưa được ghép sau bước ghép đầu tiên, nhằm tìm kiếm các cặp track-detection có khả năng lồng nhau và chỉ cứu những cặp duy nhất thỏa mãn điều kiện về số lần update gần đây.
        # Score fusion có thể bỏ ghép khi người được detect từ cả thân -> phần thân nhỏ.
        # Chỉ cứu cặp duy nhất theo cả hai chiều, vừa thấy <= 3 lần update trước.
        if len(u_track) == 0 or len(u_detection) == 0:
            return u_track, u_detection
        candidates = [(ti, di) for ti, track in enumerate(pool) for di, detection in enumerate(detections)
                      if track.is_activated and self.frame_id - track.frame_id <= 3
                      and self._nested_observation(track.xyxy, detection.xyxy)]
        track_counts, detection_counts = {}, {}
        for ti, di in candidates:
            track_counts[ti] = track_counts.get(ti, 0) + 1
            detection_counts[di] = detection_counts.get(di, 0) + 1
        matched_tracks, matched_detections = set(), set()
        for ti, di in candidates:
            if ti not in u_track or di not in u_detection:
                continue
            if track_counts[ti] != 1 or detection_counts[di] != 1:
                continue
            self._apply_match(pool[ti], detections[di], activated, refind)
            matched_tracks.add(ti)
            matched_detections.add(di)
        return ([ti for ti in u_track if ti not in matched_tracks],
                [di for di in u_detection if di not in matched_detections])

    def _init_new_tracks(self, u_detection, detections, activated, refind=None):
        # tránh tạo track ID mới từ các detection nhỏ, có confidence thấp hơn, nằm trong một track đã được ghép bằng quan sát thực của frame hiện tại.
        remaining = []
        for di in u_detection:
            detection = detections[di]
            # Không sinh ID thứ hai từ box nhỏ, confidence thấp hơn, nằm trong
            # một track đã được ghép bằng quan sát thực của CHÍNH frame hiện tại.
            owners = [track for track in activated + (refind or [])
                      if track.is_activated and track.frame_id == self.frame_id
                      and track.cls == detection.cls and track.score >= detection.score
                      and np.prod(track.xyxy[2:] - track.xyxy[:2]) > np.prod(detection.xyxy[2:] - detection.xyxy[:2])
                      and self._nested_observation(track.xyxy, detection.xyxy)]
            if len(owners) != 1:
                remaining.append(di)
        for di in remaining:
            detections[di].observation_box = detections[di].xyxy.copy()
        super()._init_new_tracks(remaining, detections, activated, refind)

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
        # số luồng tối đa mà ONNX Runtime sẽ sử dụng để thực hiện các phép toán trong một graph. Việc giới hạn số luồng này giúp kiểm soát tài nguyên CPU và tránh tình trạng quá tải khi chạy nhiều inference song song.
        opts.intra_op_num_threads = threads
        # số luồng tối đa mà ONNX Runtime sẽ sử dụng để thực hiện các phép toán giữa các graph. Việc giới hạn số luồng này giúp kiểm soát tài nguyên CPU và tránh tình trạng quá tải khi chạy nhiều inference song song.
        opts.inter_op_num_threads = 1
        # Ưu tiên GPU (DirectML) nếu build onnxruntime-directml được cài; không thì tự chạy CPU.
        # DmlExecutionProvider (onnxruntime-directml), không phải "DirectMLExecutionProvider".
        # dahh sách các nhà cung cấp thực thi (execution providers) mà ONNX Runtime sẽ ưu tiên sử dụng để chạy mô hình. Nếu không có nhà cung cấp nào được chỉ định, mặc định sẽ ưu tiên DirectML (DmlExecutionProvider) và sau đó là CPU (CPUExecutionProvider).
        preferred = os.environ.get(
            "SAFETY_ORT_PROVIDER", "DmlExecutionProvider,CPUExecutionProvider").split(",")
        available = set(ort.get_available_providers())
        # có trong cả 2 danh sách preferred và danh sách các nhà cung cấp có sẵn. Nếu không có nhà cung cấp nào trong danh sách preferred có sẵn, mặc định sẽ sử dụng CPUExecutionProvider.
        providers = [p for p in preferred if p in available] or [
            "CPUExecutionProvider"]
        # cấu hình các tùy chọn cụ thể cho nhà cung cấp DirectML (DmlExecutionProvider) nếu nó được sử dụng. Cụ thể, nó tắt pattern memory (enable_mem_pattern = False) và đặt chế độ thực thi (execution_mode) thành ORT_SEQUENTIAL, có nghĩa là các phép toán sẽ được thực hiện tuần tự thay vì song song.
        if "DmlExecutionProvider" in providers:
            opts.enable_mem_pattern = False
            opts.execution_mode = ort.ExecutionMode.ORT_SEQUENTIAL
        # khởi tạo một phiên làm việc (InferenceSession) của ONNX Runtime với mô hình được chỉ định bởi model_path, sử dụng các tùy chọn đã cấu hình và danh sách các nhà cung cấp thực thi (execution providers) đã xác định.
        self.session = ort.InferenceSession(
            str(path), sess_options=opts, providers=providers)
        metadata = self.session.get_modelmeta().custom_metadata_map
        # lấy thông tin về các lớp (classes) mà mô hình có thể phát hiện từ metadata của mô hình ONNX. Nó tạo ra một từ điển (dictionary) ánh xạ từ chỉ số lớp (class index) sang tên lớp (class name) dưới dạng chữ thường.
        self.names = {int(k): str(v).lower()
                      for k, v in ast.literal_eval(metadata["names"]).items()}
        if set(self.names.values()) != {"normal", "drowning"}:
            raise ValueError("Model classes must be normal and drowning")
        shape = self.session.get_inputs()[0].shape
        # kiểm tra hình dạng (shape) của đầu vào (input) mà mô hình ONNX yêu cầu. Nó đảm bảo rằng đầu vào là một batch duy nhất (batch size = 1), có 3 kênh màu (RGB), và kích thước chiều cao và chiều rộng là bằng nhau (square). Nếu không thỏa mãn các điều kiện này, nó sẽ ném ra một ngoại lệ ValueError.
        if len(shape) != 4 or shape[0] != 1 or shape[1] != 3 or not isinstance(shape[2], int) or shape[2] != shape[3]:
            raise ValueError(
                "Only static batch-1 square YOLO ONNX inputs are supported")
        self.size = shape[2]
        # tạo một từ điển (dictionary) trống để lưu trữ các đối tượng CameraTracker cho từng camera hoặc nguồn video. Mỗi khóa trong từ điển là một chuỗi đại diện cho một camera hoặc nguồn video, và giá trị tương ứng là một đối tượng CameraTracker để theo dõi các đối tượng trong video từ camera đó.
        self.trackers = {}

    def infer(self, image, key):
        # [height, width] của khung hình đầu vào (image) được cung cấp. Nó lấy kích thước của khung hình để chuẩn bị cho các bước xử lý tiếp theo, chẳng hạn như thay đổi kích thước (resize) và chuẩn hóa (normalization) trước khi đưa vào mô hình YOLO để phát hiện đối tượng.
        h, w = image.shape[:2]
        scale = min(self.size / w, self.size / h)  # giữ tỉ lệ ảnh
        nw, nh = round(w * scale), round(h * scale)
        # tính padding để căn giữa ảnh gốc trong khung vuông có kích thước self.size x self.size. left là khoảng cách từ cạnh trái của khung vuông đến cạnh trái của ảnh gốc sau khi resize, và top là khoảng cách từ cạnh trên của khung vuông đến cạnh trên của ảnh gốc sau khi resize.
        left, top = (self.size - nw) // 2, (self.size - nh) // 2
        # tạo một mảng numpy (numpy array) có kích thước (self.size, self.size, 3) với tất cả các giá trị được điền bằng 114 (màu xám nhạt). Mảng này sẽ được sử dụng làm khung nền (background) để chèn ảnh gốc đã được resize vào giữa, đảm bảo rằng ảnh đầu vào cho mô hình YOLO có kích thước cố định và giữ được tỷ lệ gốc của ảnh.
        padded = np.full((self.size, self.size, 3), 114, dtype=np.uint8)
        # chèn ảnh đã resize vào giữa khung vuông, với các vùng còn lại được điền bằng màu xám (giá trị 114). Điều này đảm bảo rằng ảnh đầu vào cho mô hình YOLO có kích thước cố định và giữ được tỷ lệ gốc của ảnh.
        padded[top:top + nh, left:left + nw] = cv2.resize(image, (nw, nh))
        # chuẩn hóa ảnh đầu vào cho mô hình YOLO. Cụ thể, nó chuyển đổi ảnh từ định dạng BGR (OpenCV) sang RGB, thay đổi thứ tự các chiều để phù hợp với định dạng đầu vào của mô hình (channels-first), thêm một chiều batch (batch size = 1), và chuẩn hóa giá trị pixel từ [0, 255] sang [0, 1] bằng cách chia cho 255.0.
        tensor = padded[:, :, ::-
                        1].transpose(2, 0, 1).copy()[None].astype(np.float32) / 255.0
        # opencv: BGR → RGB, channels-last → channels-first, batch-1, float32, [0, 1]
        output = self.session.run(
            None, {self.session.get_inputs()[0].name: tensor})[0]
        # output(batch, num_detections, 4 + num_classes) có 3 chiều, trong đó chiều thứ hai (num_detections) chứa các bounding box và điểm số dự đoán cho mỗi detection. Nếu output không có 3 chiều hoặc số lượng điểm số dự đoán không khớp với số lượng lớp (classes) mà mô hình được huấn luyện để phát hiện, nó sẽ ném ra một ngoại lệ ValueError.
        if output.ndim != 3 or output.shape[1] != 4 + len(self.names):
            raise ValueError("Unsupported YOLO output shape")
        rows = output[0].T
        # lấy class có confidence cao nhất cho mỗi detection bằng cách tìm chỉ số của giá trị lớn nhất trong các điểm số dự đoán (scores) cho từng detection. Chỉ số này tương ứng với lớp (class) mà mô hình dự đoán là phù hợp nhất cho detection đó.
        classes = rows[:, 4:].argmax(1)
        scores = rows[np.arange(len(rows)), classes + 4]
        mask = scores >= 0.10  # ByteTrack needs low-confidence detections too.
        rows, classes, scores = rows[mask], classes[mask], scores[mask]
        boxes = rows[:, :4].copy()
        boxes[:, 0] = (boxes[:, 0] - boxes[:, 2] / 2 - left) / scale
        boxes[:, 1] = (boxes[:, 1] - boxes[:, 3] / 2 - top) / scale
        boxes[:, 2:] /= scale
        # loại bỏ những box trùng lặp, 0.45 là ngưỡng IOU (Intersection over Union) để xác định xem hai box có trùng lặp hay không. Nếu IOU của hai box lớn hơn 0.45, box có confidence thấp hơn sẽ bị loại bỏ. top_k=300 giới hạn số lượng box được giữ lại sau khi áp dụng NMS, chỉ giữ lại tối đa 300 box có confidence cao nhất.
        indices = cv2.dnn.NMSBoxes(
            boxes.tolist(), scores.tolist(), 0.10, 0.45, top_k=300)
        # giữ detection đc NMS chọn
        indices = np.asarray(indices, dtype=int).flatten()
        boxes, scores, classes = boxes[indices], scores[indices], classes[indices]
        boxes[:, 2:] += boxes[:, :2]
        boxes[:, [0, 2]] = boxes[:, [0, 2]].clip(0, w)
        boxes[:, [1, 3]] = boxes[:, [1, 3]].clip(0, h)
        data = np.column_stack((boxes, scores, classes)).astype(np.float32)
        if key not in self.trackers:  # key: là một chuỗi đại diện cho một camera hoặc nguồn video cụ thể. Nếu key chưa tồn tại trong từ điển self.trackers, nó sẽ tạo một đối tượng CameraTracker mới và thêm vào từ điển với key tương ứng. Nếu số lượng trackers đã đạt đến giới hạn 32, nó sẽ loại bỏ tracker cũ nhất trước khi thêm tracker mới.
            if len(self.trackers) >= 32:
                self.trackers.pop(next(iter(self.trackers)))
            self.trackers[key] = CameraTracker(SimpleNamespace(track_high_thresh=0.25, track_low_thresh=0.10,
                                                               new_track_thresh=0.25, track_buffer=30, match_thresh=0.8, fuse_score=True))
        tracks = self.trackers[key].update(Boxes(data, (h, w)).numpy(), image)
        return [{"trackId": str(int(t[4])), "class": self.names[int(t[6])], "confidence": float(t[5]),
                 "box": [float(np.clip(t[0] / w, 0, 1)), float(np.clip(t[1] / h, 0, 1)),
                         # chuẩn hóa tọa độ bounding box của các đối tượng được theo dõi (tracks) sang tỷ lệ phần trăm so với kích thước khung hình gốc. Các giá trị được cắt (clip) để đảm bảo rằng chúng nằm trong khoảng từ 0 đến 1, đại diện cho vị trí và kích thước của bounding box dưới dạng tỷ lệ phần trăm của chiều rộng và chiều cao khung hình.
                         float(np.clip(t[2] / w, 0, 1)), float(np.clip(t[3] / h, 0, 1))]} for t in tracks]


# đọc đúng số byte được chỉ định từ một luồng (stream) và trả về dữ liệu dưới dạng bytearray. Nó đảm bảo rằng dữ liệu được đọc đầy đủ và không bị cắt ngắn, đồng thời kiểm tra tính hợp lệ của độ dài JPEG.
def read_exact(stream, length):
    if type(length) is not int or not 4 <= length <= 4 * 1024 * 1024:
        raise ValueError("Invalid JPEG length")
    data = bytearray()  # tạo vùng nhớ tạm thời để lưu trữ dữ liệu JPEG được đọc từ luồng (stream). bytearray() là một kiểu dữ liệu trong Python cho phép tạo một mảng các byte có thể thay đổi kích thước, giúp lưu trữ dữ liệu nhị phân như hình ảnh JPEG.
    while len(data) < length:
        chunk = stream.read(length - len(data))
        if not chunk:
            raise ValueError("Truncated JPEG")
        data.extend(chunk)
    return data


def main(protocol):
    # Respect the process-specific thread budget for preprocessing as well as ORT.
    cv2.setNumThreads(max(1, int(os.environ.get("OMP_NUM_THREADS", "1"))))
    # khởi tạo detector với mô hình ONNX được cung cấp từ đường dẫn trong sys.argv[1]. Detector sẽ sử dụng mô hình này để thực hiện việc phát hiện đối tượng và theo dõi các đối tượng trong video hoặc hình ảnh đầu vào.
    detector = Detector(sys.argv[1])
    # Pay graph/provider initialization once before advertising readiness.
    detector.session.run(None, {detector.session.get_inputs()[0].name:
                                np.zeros((1, 3, detector.size, detector.size), dtype=np.float32)})
    font = sys.argv[2] if len(sys.argv) > 2 else None
    image = None
    capture = writer = None
    frame_index = 0
    fps = total = 0
    # số lượng khung hình được xác minh từ metadata của video, nếu có. Nếu không có thông tin này, expected_frames sẽ là None và số lượng khung hình sẽ được xác định từ capture.get(cv2.CAP_PROP_FRAME_COUNT).
    expected_frames = None
    print(json.dumps({"ready": True, "classes": detector.names}), flush=True)
    for line in sys.stdin.buffer:
        request = json.loads(line)
        op = request["op"]
        binary_image = None
        if op == "infer":  # Chạy AI trên JPEG được gửi
            # Backend chỉ yêu cầu reset cho session RTSP simulator mới; UPLOAD giữ nguyên.
            if request.get("resetTracker", False):
                detector.trackers.pop(request["key"], None)
            encoded = read_exact(sys.stdin.buffer, request["jpegLength"]) if "jpegLength" in request else base64.b64decode(
                request["jpeg"])  # đọc JPEG bytes
            image = cv2.imdecode(np.frombuffer(
                encoded, np.uint8), cv2.IMREAD_COLOR)  # decode JPEG → BGR
            if image is None:
                raise ValueError("Invalid JPEG")
            result = {"detections": detector.infer(image, request["key"])}
        elif op == "open":
            detector.trackers.pop("upload", None)
            # Phase 1 (analyze): output = null → chỉ mở capture, KHÔNG mở writer.
            # Bỏ encode/paint ở phase này để AI chạy nhanh hơn real-time (lead với video).
            capture = cv2.VideoCapture(request["input"])
            if not capture.isOpened():
                raise ValueError("Video decoder unavailable")
            fps = float(request["fps"])
            # Container duration * FPS can overstate frames (e.g. MP4 edit lists).
            # The backend checks each decoded timestamp before publishing its result.
            verified_frames = request.get("frames")
            expected_frames = verified_frames
            total = int(verified_frames if verified_frames is not None else capture.get(
                cv2.CAP_PROP_FRAME_COUNT))
            frame_index = 0
            width, height = int(capture.get(cv2.CAP_PROP_FRAME_WIDTH)), int(
                capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
            output = request.get("output")
            if output:
                writer = cv2.VideoWriter(
                    output, cv2.VideoWriter_fourcc(*"mp4v"), fps, (width, height))
                if not writer.isOpened():
                    raise ValueError("Video encoder unavailable")
            else:
                writer = None
            result = {"opened": True}
        elif op == "close":
            if writer:
                writer.release()
            if capture:
                capture.release()
            writer = capture = image = None
            detector.trackers.pop("upload", None)
            result = {"closed": True}
        elif op == "next":
            ok, image = capture.read()
            if not ok:
                if expected_frames is not None and frame_index != expected_frames:
                    raise ValueError(
                        "Video decoding ended before the final frame")
                if writer:
                    writer.release()
                capture.release()
                result = {"done": True}
            else:
                result = {"timestamp": frame_index / fps, "progress": frame_index / max(total, 1),
                          "detections": detector.infer(image, "upload")}
                frame_index += 1
        elif op == "reopen_render":  # Mở lại video để render offline
            # Phase 2 (render offline): mở lại capture + writer để vẽ khung từ decisions đã
            # lưu (backend gửi qua "paint"). Chạy sau khi phân tích xong — không ảnh hưởng realtime.
            if writer:
                writer.release()
            capture = cv2.VideoCapture(request["input"])
            if not capture.isOpened():
                raise ValueError("Video decoder unavailable")
            fps = float(request["fps"])
            verified_frames = request.get("frames")
            total = int(verified_frames if verified_frames is not None else capture.get(
                cv2.CAP_PROP_FRAME_COUNT))
            frame_index = 0
            width, height = int(capture.get(cv2.CAP_PROP_FRAME_WIDTH)), int(
                capture.get(cv2.CAP_PROP_FRAME_HEIGHT))
            writer = cv2.VideoWriter(
                request["output"], cv2.VideoWriter_fourcc(*"mp4v"), fps, (width, height))
            if not writer.isOpened():
                raise ValueError("Video encoder unavailable")
            result = {"opened": True}
        elif op == "capture":  # Mã hóa frame hiện tại thành JPEG base64
            # Frame gốc chưa vẽ khung, dùng làm ảnh nền cho snapshot của cảnh báo.
            if image is None:
                raise ValueError("No frame captured yet")
            ok, encoded = cv2.imencode(
                ".jpg", image, [cv2.IMWRITE_JPEG_QUALITY, 90])
            if not ok:
                raise ValueError("Snapshot encoding failed")
            result = {"jpeg": base64.b64encode(encoded).decode("ascii")}
        elif op == "step":  # Đọc frame tiếp theo để render, không inference
            # Phase 2 (render): chỉ đọc frame kế tiếp, KHÔNG infer (decisions đã có sẵn
            # từ phase 1 do backend gửi theo "paint").
            ok, image = capture.read()
            if not ok:
                if frame_index != total:
                    raise ValueError(
                        "Video decoding ended before the final frame")
                if writer:
                    writer.release()
                capture.release()
                result = {"done": True}
            else:
                result = {"timestamp": frame_index / fps,
                          "progress": frame_index / max(total, 1)}
                frame_index += 1
        elif op in ("render", "paint"):  # Vẽ ảnh và trả JPEG
            # Chỉ vẽ khi có cảnh báo (WARNING/CRITICAL) trong khung; khung thường thì ghi thẳng
            # frame gốc — tránh tốn PIL/cvtColor mỗi frame (pipeline realtime).
            annotated = draw(image, request["decisions"], font) if any(
                d.get("severity") in ("WARNING", "CRITICAL") for d in request["decisions"]) else image
            if op == "paint":
                writer.write(annotated)
                result = {"painted": True}
            else:
                # mã hóa ảnh đã vẽ thành JPEG với chất lượng 95. Nếu việc mã hóa thất bại, nó sẽ ném ra một ngoại lệ ValueError. Nếu request yêu cầu trả về dữ liệu nhị phân (binary), nó sẽ chuyển đổi ảnh đã mã hóa thành bytes và trả về độ dài của JPEG. Nếu không, nó sẽ trả về ảnh đã mã hóa dưới dạng base64.
                ok, encoded = cv2.imencode(
                    ".jpg", annotated, [cv2.IMWRITE_JPEG_QUALITY, 95])
                if not ok:
                    raise ValueError("Snapshot encoding failed")
                if request.get("binary", False):
                    binary_image = encoded.tobytes()
                    result = {"jpegLength": len(binary_image)}
                else:
                    result = {"jpeg": base64.b64encode(
                        encoded).decode("ascii")}
        else:
            raise ValueError("Unknown operation")
        print(json.dumps(result, allow_nan=False), flush=True)
        if binary_image is not None:
            protocol.buffer.write(binary_image)
            protocol.buffer.flush()


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
            main(protocol)
    except Exception:
        protocol.write(json.dumps({"error": "AI_WORKER_FAILED"}) + "\n")
        protocol.flush()
        sys.exit(1)
