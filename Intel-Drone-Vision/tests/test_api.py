
import numpy as np

from app.vision.target_matcher import TargetMatcher


def test_empty_crop():

    matcher = TargetMatcher()

    empty_image = np.zeros(
        (0, 0, 3),
        dtype=np.uint8
    )

    color, score = matcher.detect_color(
        empty_image
    )

    assert color == "unknown"
    assert score == 0.0


def test_color_detection():

    matcher = TargetMatcher()

    # Create an artificial orange image.
    image = np.zeros(
        (100, 100, 3),
        dtype=np.uint8
    )

    image[:] = (0, 140, 255)

    color, score = matcher.detect_color(
        image
    )

    assert color == "orange"
    assert score > 0


def test_target_matching():

    matcher = TargetMatcher()

    image = np.zeros(
        (200, 200, 3),
        dtype=np.uint8
    )

    image[:] = (0, 140, 255)

    detections = [
        {
            "class": "person",
            "class_id": 0,
            "confidence": 0.95,
            "bounding_box": {
                "x1": 0,
                "y1": 0,
                "x2": 200,
                "y2": 200
            }
        }
    ]

    results = matcher.match(
        image,
        detections,
        "Person wearing an orange jacket"
    )

    assert len(results) == 1
    assert results[0]["target_match"]["color_match"]
