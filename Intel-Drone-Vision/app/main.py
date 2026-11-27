
from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.api.routes import router
from app.core.config import MODEL_PATH
from app.vision.detector import PersonDetector


@asynccontextmanager
async def lifespan(app: FastAPI):

    print("Starting Intel Drone Vision Service...")

    if not MODEL_PATH.exists():
        raise RuntimeError(
            f"Model not found: {MODEL_PATH}. "
            "Run python export_model.py first."
        )

    app.state.detector = PersonDetector(
        str(MODEL_PATH)
    )

    print("Vision service initialized!")

    yield

    print("Shutting down Vision Service...")


app = FastAPI(
    title="Intel Drone Vision Service",
    description=(
        "YOLO and OpenVINO vision service "
        "for autonomous drone swarms."
    ),
    version="1.0.0",
    lifespan=lifespan
)

app.include_router(router)


@app.get("/health")
def health_check():

    return {
        "status": "healthy",
        "service": "intel-drone-vision"
    }
