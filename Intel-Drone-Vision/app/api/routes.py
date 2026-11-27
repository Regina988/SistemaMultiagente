
from datetime import datetime, timezone

import cv2
import numpy as np

from fastapi import (
    APIRouter,
    UploadFile,
    File,
    Form,
    HTTPException,
    Request
)

from app.vision.target_matcher import TargetMatcher
from app.core.config import MAX_IMAGE_SIZE


router = APIRouter()

target_matcher = TargetMatcher()


@router.post("/v1/detections")
async def process_detection(
    request: Request,
    drone_id: str = Form(...),
    drone_x: float = Form(...),
    drone_y: float = Form(...),
    drone_z: float = Form(...),
    target_description: str = Form(
        default=""
    ),
    image: UploadFile = File(...)
):

    # Validate image type.
    if image.content_type not in [
        "image/jpeg",
        "image/png"
    ]:
        raise HTTPException(
            status_code=415,
            detail="Only JPEG and PNG images are supported"
        )

    # Read uploaded image.
    image_bytes = await image.read(
        MAX_IMAGE_SIZE + 1
    )

    if not image_bytes:
        raise HTTPException(
            status_code=400,
            detail="Empty image"
        )

    if len(image_bytes) > MAX_IMAGE_SIZE:
        raise HTTPException(
            status_code=413,
            detail="Image exceeds 10 MB limit"
        )

    # Decode image.
    image_array = np.frombuffer(
        image_bytes,
        dtype=np.uint8
    )

    frame = cv2.imdecode(
        image_array,
        cv2.IMREAD_COLOR
    )

    if frame is None:
        raise HTTPException(
            status_code=400,
            detail="Invalid image"
        )

    height, width = frame.shape[:2]

    # Retrieve the detector initialized at startup.
    detector = request.app.state.detector

    # Run YOLO + OpenVINO.
    detections = detector.detect(frame)

    # Compare detections with target description.
    if target_description.strip():

        detections = target_matcher.match(
            frame,
            detections,
            target_description
        )

    return {
        "drone_id": drone_id,

        "timestamp": datetime.now(
            timezone.utc
        ).isoformat(),

        "drone_position": {
            "x": drone_x,
            "y": drone_y,
            "z": drone_z
        },

        "image_dimensions": {
            "width": width,
            "height": height
        },

        "detection_count": len(detections),

        "detections": detections
    }


@router.get("/v1/detections/health")
def detection_health():

    return {
        "status": "healthy",
        "module": "vision"
    }
