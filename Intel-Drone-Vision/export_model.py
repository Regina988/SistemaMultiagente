
from ultralytics import YOLO


def main():
    print("Loading YOLO11n...")

    model = YOLO("yolo11n.pt")

    print("Exporting model to OpenVINO...")

    model.export(
        format="openvino",
        imgsz=640,
        half=False,
        project="models",
        name="yolo11n_openvino_model"
    )

    print("Export completed!")


if __name__ == "__main__":
    main()
