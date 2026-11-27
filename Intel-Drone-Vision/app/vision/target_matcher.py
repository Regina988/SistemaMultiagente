
import cv2
import numpy as np


class TargetMatcher:

    def __init__(self):
        # Supported attributes in this first prototype.
        self.supported_colors = {
            "orange",
            "red",
            "blue",
            "green",
            "yellow",
            "black",
            "white"
        }

    def detect_color(self, crop: np.ndarray):

        if crop.size == 0:
            return "unknown", 0.0

        height, width = crop.shape[:2]

        # Approximate the clothing region:
        # ignore the head and focus on the torso.
        y1 = int(height * 0.20)
        y2 = int(height * 0.75)

        x1 = int(width * 0.15)
        x2 = int(width * 0.85)

        torso = crop[y1:y2, x1:x2]

        if torso.size == 0:
            return "unknown", 0.0

        hsv = cv2.cvtColor(
            torso,
            cv2.COLOR_BGR2HSV
        )

        color_ranges = {
            "orange": ((5, 70, 50), (25, 255, 255)),
            "red": ((0, 70, 50), (10, 255, 255)),
            "blue": ((90, 70, 50), (130, 255, 255)),
            "green": ((35, 70, 50), (85, 255, 255)),
            "yellow": ((20, 70, 50), (35, 255, 255)),
            "white": ((0, 0, 190), (180, 60, 255)),
            "black": ((0, 0, 0), (180, 255, 50))
        }

        scores = {}

        for color, (lower, upper) in color_ranges.items():

            mask = cv2.inRange(
                hsv,
                np.array(lower),
                np.array(upper)
            )

            scores[color] = (
                np.count_nonzero(mask) / mask.size
            )

        detected_color = max(
            scores,
            key=scores.get
        )

        return detected_color, round(
            float(scores[detected_color]),
            4
        )

    def match(self, image, detections, target_description):

        description = target_description.lower()

        requested_color = next(
            (
                color
                for color in self.supported_colors
                if color in description
            ),
            None
        )

        results = []

        for detection in detections:

            box = detection["bounding_box"]

            x1 = max(0, int(box["x1"]))
            y1 = max(0, int(box["y1"]))
            x2 = min(image.shape[1], int(box["x2"]))
            y2 = min(image.shape[0], int(box["y2"]))

            crop = image[y1:y2, x1:x2]

            detected_color, color_score = (
                self.detect_color(crop)
            )

            color_match = (
                requested_color is not None
                and detected_color == requested_color
            )

            results.append({
                **detection,
                "attributes": {
                    "detected_color": detected_color,
                    "color_score": color_score
                },
                "target_match": {
                    "requested_color": requested_color,
                    "color_match": color_match
                }
            })

        return results
