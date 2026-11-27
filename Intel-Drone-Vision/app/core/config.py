
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[2]

MODEL_PATH = (
    PROJECT_ROOT /
    "models" /
    "yolo11n_openvino_model"
)

CONFIDENCE_THRESHOLD = 0.35

IMAGE_SIZE = 640

MAX_IMAGE_SIZE = 10 * 1024 * 1024
