"""Rendering shared by live snapshots, paired AI preview and uploaded video."""
from pathlib import Path
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont


def draw(image, decisions, font_path=None):
    pil = Image.fromarray(cv2.cvtColor(image, cv2.COLOR_BGR2RGB))
    canvas = ImageDraw.Draw(pil)
    font = ImageFont.truetype(font_path, 20) if font_path and Path(font_path).is_file() else ImageFont.load_default(size=18)
    for d in decisions:
        if d["severity"] not in ("WARNING", "CRITICAL"):
            continue
        color = "#EF4444" if d["severity"] == "CRITICAL" else "#F97316"
        x1, y1, x2, y2 = [d["box"][i] * (pil.width if i % 2 == 0 else pil.height) for i in range(4)]
        canvas.rectangle((x1, y1, x2, y2), outline=color, width=3)
        label = ("Khẩn cấp" if d["severity"] == "CRITICAL" else "Chú ý") + " · " + d["trackId"]
        canvas.text((x1, max(0, y1 - 24)), label, fill=color, font=font, stroke_width=1)
    return cv2.cvtColor(np.asarray(pil), cv2.COLOR_RGB2BGR)
