
from pydantic import BaseModel
from typing import Optional


class DronePosition(BaseModel):
    x: float
    y: float
    z: float


class BoundingBox(BaseModel):
    x1: float
    y1: float
    x2: float
    y2: float


class DetectionAttributes(BaseModel):
    detected_color: str
    color_score: float


class TargetMatch(BaseModel):
    requested_color: Optional[str] = None
    color_match: bool


class Detection(BaseModel):
    class_name: str
    class_id: int
    confidence: float
    bounding_box: BoundingBox
    attributes: Optional[DetectionAttributes] = None
    target_match: Optional[TargetMatch] = None


class DetectionResponse(BaseModel):
    drone_id: str
    timestamp: str
    drone_position: DronePosition
    image_dimensions: dict
    detection_count: int
    detections: list[dict]
