
from pathlib import Path

import numpy as np
from ultralytics import YOLO


class PersonDetector:

    def __init__(self, model_path: str):

        path = Path(model_path)

        if not path.exists():
            raise FileNotFoundError(
                f"OpenVINO model not found: {path}. "
                "Run export_model.py first."
            )

        print(f"Loading OpenVINO model: {path}")

        self.model = YOLO(str(path), task="detect")

        print("OpenVINO detector ready!")

    def detect(self, image: np.ndarray):

        results = self.model.predict(
            source=image,
            conf=0.35,
            imgsz=640,
            verbose=False
        )

        detections = []

        for result in results:

            if result.boxes is None:
                continue

            for box in result.boxes:

                class_id = int(box.cls[0])

                # COCO class 0 represents a person.
                if class_id != 0:
                    continue

                confidence = float(box.conf[0])

                x1, y1, x2, y2 = (
                    box.xyxy[0].tolist()
                )

                detections.append({
                    "class": "person",
                    "class_id": class_id,
                    "confidence": round(confidence, 4),
                    "bounding_box": {
                        "x1": round(x1, 2),
                        "y1": round(y1, 2),
                        "x2": round(x2, 2),
                        "y2": round(y2, 2)
                    }
                })

        return detections
