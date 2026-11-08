import json
import os
os.environ.setdefault("OMP_NUM_THREADS", "1")
os.environ.setdefault("MKL_NUM_THREADS", "1")
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent / ".deps"))
import cv2
from rendering import draw

if __name__ == "__main__":
    try:
        cv2.setNumThreads(1)
        image = cv2.imread(sys.argv[1])
        if image is None:
            raise ValueError("Invalid snapshot")
        with open(sys.argv[2], encoding="utf-8") as stream:
            decisions = json.load(stream)
        annotated = draw(image, decisions, sys.argv[4])
        if not cv2.imwrite(sys.argv[3], annotated, [cv2.IMWRITE_JPEG_QUALITY, 90]):
            raise ValueError("Snapshot write failed")
    except Exception:
        sys.exit(1)
