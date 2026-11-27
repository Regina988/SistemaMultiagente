"""Servidor de PRUEBA del nodo de visión (sustituye a YOLO/OpenVINO mientras se integra).

Recibe las imágenes de las cámaras de los drones, las muestra en una ventana por dron
y responde a Unity con un resultado de detección vacío.

Uso:
    python servidor_vision.py
Luego dar Play en Unity (escena Bosque). Protocolo: docs/COMUNICACION.md
"""

import asyncio
import json
import time

import cv2
import numpy as np
import websockets

PUERTO = 8766

# Las ventanas de OpenCV deben actualizarse desde un solo lugar; guardamos el último
# frame de cada dron y un bucle aparte los muestra.
ultimos_frames: dict[int, np.ndarray] = {}


async def recibir_imagenes(websocket):
    print("Cámara de Unity conectada al nodo de visión.")
    try:
        async for mensaje in websocket:
            if not isinstance(mensaje, bytes) or len(mensaje) < 2:
                continue

            t_inicio = time.perf_counter()

            # [byte 0 = id del dron][resto = JPG]
            id_dron = mensaje[0]
            frame = cv2.imdecode(np.frombuffer(mensaje[1:], dtype=np.uint8), cv2.IMREAD_COLOR)
            if frame is None:
                continue

            alto, ancho, _ = frame.shape
            cv2.putText(frame, f"Dron {id_dron} | {ancho}x{alto}", (15, 35),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.9, (0, 255, 0), 2)
            ultimos_frames[id_dron] = frame

            # Aquí iría la inferencia de YOLO/OpenVINO.
            respuesta = {
                "id_dron": id_dron,
                "detectado": False,
                "clase": "ninguna",
                "confianza": 0.0,
                "latencia_ms": round((time.perf_counter() - t_inicio) * 1000, 2),
            }
            await websocket.send(json.dumps(respuesta))
    except websockets.exceptions.ConnectionClosed:
        print("Cámara de Unity desconectada.")


async def mostrar_ventanas():
    while True:
        for id_dron, frame in list(ultimos_frames.items()):
            cv2.imshow(f"Dron {id_dron}", frame)
        cv2.waitKey(1)
        await asyncio.sleep(0.03)


async def main():
    # max_size=None: una imagen puede pasar del límite por defecto de 1 MB
    async with websockets.serve(recibir_imagenes, "localhost", PUERTO, max_size=None):
        print(f"Servidor de visión activo en ws://localhost:{PUERTO}")
        try:
            await mostrar_ventanas()
        finally:
            cv2.destroyAllWindows()


if __name__ == "__main__":
    asyncio.run(main())
