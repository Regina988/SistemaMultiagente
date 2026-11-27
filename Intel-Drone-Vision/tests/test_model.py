
from ultralytics import YOLO
from pathlib import Path

def main():
    model_path = Path("models/yolo11n_openvino_model")

    print("Loading OpenVINO model...")
    model = YOLO(str(model_path), task="detect")

    print("Running inference...")
    results = model.predict(
        source="https://ultralytics.com/images/bus.jpg",
        imgsz=640,
        conf=0.35,
        verbose=True
    )

    for result in results:
        print("\nDetected objects:")

        for box in result.boxes:
            class_id = int(box.cls[0])
            confidence = float(box.conf[0])
            class_name = result.names[class_id]

            print(
                f"Object: {class_name} | "
                f"Confidence: {confidence:.2f}"
            )

    print("\nInference completed!")

if __name__ == "__main__":
    main()
