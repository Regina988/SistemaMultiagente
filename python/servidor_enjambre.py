"""Servidor de PRUEBA del enjambre (sustituye a AgentPy mientras se integra).

Manda a Unity, 10 veces por segundo, la posición y el estado de 3 drones que vuelan
en círculos sobre el bosque, con un ciclo de demostración patrullando → duda → confirmado.

Uso:
    python servidor_enjambre.py
Luego dar Play en Unity (escena Bosque). Protocolo: docs/COMUNICACION.md
"""

import asyncio
import json
import math

import websockets

PUERTO = 8767
ALTURA_VUELO = 30.0  # metros sobre el suelo (Unity suma la altura del terreno)
RADIO = 50.0         # el terreno va de -100 a 100 en X y Z


async def simular_enjambre(websocket):
    print("Unity conectado al simulador de enjambre.")
    paso = 0
    try:
        while True:
            paso += 1
            t = paso * 0.02
            fase = paso % 150

            if fase < 60:
                estados = ["patrullando", "patrullando", "patrullando"]
                enlaces = []
            elif fase < 100:
                estados = ["patrullando", "duda", "patrullando"]
                enlaces = [{"origen": "MAV_2", "destino": "MAV_1"}]
            else:
                estados = ["confirmado", "confirmado", "patrullando"]
                enlaces = [
                    {"origen": "MAV_2", "destino": "MAV_1"},
                    {"origen": "MAV_2", "destino": "MAV_3"},
                ]

            drones = []
            for i in range(3):
                angulo = t + i * 2 * math.pi / 3
                drones.append({
                    "id": f"MAV_{i + 1}",
                    "x": round(math.cos(angulo) * RADIO, 2),
                    "y": ALTURA_VUELO,
                    "z": round(math.sin(angulo) * RADIO, 2),
                    "estado": estados[i],
                })

            await websocket.send(json.dumps({"drones": drones, "enlaces": enlaces}))
            await asyncio.sleep(0.1)
    except websockets.exceptions.ConnectionClosed:
        print("Unity desconectado del enjambre.")


async def main():
    async with websockets.serve(simular_enjambre, "localhost", PUERTO):
        print(f"Servidor de enjambre activo en ws://localhost:{PUERTO}")
        await asyncio.Future()


if __name__ == "__main__":
    asyncio.run(main())
