
# Intel Drone Vision Service

Computer vision service for autonomous drone swarm
simulation using YOLO and Intel OpenVINO.

## Technologies

- Python 3.11
- YOLO11n
- Intel OpenVINO
- OpenCV
- FastAPI
- NumPy

## Installation

Create a virtual environment:

```bash
python -m venv .venv
```

Activate on Windows:

```powershell
.\.venv\Scripts\Activate.ps1
```

Install dependencies:

```bash
pip install -r requirements.txt
```

## Export model

```bash
python export_model.py
```

## Run service

```bash
uvicorn app.main:app --reload
```

## API documentation

http://localhost:8000/docs

## Endpoints

GET /health

GET /v1/detections/health

POST /v1/detections

## Responsibilities

- Receive images from Unity.
- Detect people using YOLO.
- Execute inference using OpenVINO.
- Analyze basic visual attributes.
- Return structured JSON detections.
- Provide detection results to AgentPy.
