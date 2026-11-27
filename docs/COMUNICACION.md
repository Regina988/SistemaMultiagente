# Comunicación Unity ↔ Python

Unity y Python se comunican por **WebSockets**. Python es el **servidor** y Unity se conecta como **cliente** al darle Play. Hay dos canales:

| Canal | Puerto | Dirección | Formato | Script en Unity |
|---|---|---|---|---|
| Enjambre (AgentPy) | `8767` | Python → Unity | JSON (texto) | `ManagerEnjambre.cs` |
| Visión (YOLO / OpenVINO) | `8766` | Unity → Python, con respuesta | Binario (imagen) → JSON | `CamaraWebSocket.cs` |

```
 AgentPy ──(8767) posiciones y estados ──▶ Unity: crea y mueve los drones
 Unity ──(8766) imagen de cada cámara ──▶ YOLO ──▶ respuesta JSON ──▶ Unity
```

## Canal 1: Enjambre (puerto 8767)

Python manda el estado completo del enjambre, idealmente **unas 10 veces por segundo**. Unity solo usa el mensaje más reciente.

```json
{
  "drones": [
    { "id": "MAV_1", "x": 10.0, "y": 30.0, "z": -5.0, "estado": "patrullando" },
    { "id": "MAV_2", "x": -40.0, "y": 30.0, "z": 20.0, "estado": "duda" }
  ],
  "enlaces": [
    { "origen": "MAV_2", "destino": "MAV_1" }
  ]
}
```

| Campo | Significado |
|---|---|
| `id` | Nombre del dron. Si es nuevo, Unity lo crea desde el prefab `Dron`. Si termina en número (`MAV_3`), ese número es su `id_dron` en el canal de visión. |
| `x`, `z` | Posición horizontal en metros. El terreno va de **-100 a 100** en X y en Z (200 × 200 m, centrado en el origen). |
| `y` | **Altura sobre el suelo** en metros (por ejemplo 30). Unity le suma la altura del terreno. Se puede cambiar a altura absoluta desmarcando *Altura Relativa Al Suelo* en `ManagerEnjambre`. |
| `estado` | `patrullando` (esfera verde), `duda` (amarilla) o `confirmado` (roja). |
| `enlaces` | Pares de drones que se están comunicando. Unity dibuja una línea entre ellos. Puede ir vacío: `[]`. |

Unity mueve cada dron suavemente hacia la posición recibida. Python no necesita mandar rotaciones.

## Canal 2: Visión (puerto 8766)

Cada dron abre su propia conexión y manda una imagen cada `intervaloCaptura` segundos (0.5 s por defecto).

**Mensaje de Unity → Python (binario):**

```
[ byte 0 ] [ bytes 1 … N ]
  id_dron    imagen JPG (1280×720 por defecto)
```

Ejemplo de lectura en Python:

```python
id_dron = mensaje[0]
frame = cv2.imdecode(np.frombuffer(mensaje[1:], np.uint8), cv2.IMREAD_COLOR)
```

**Respuesta de Python → Unity (JSON).** Por ahora Unity solo la muestra en la consola:

```json
{ "id_dron": 2, "detectado": true, "clase": "persona", "confianza": 0.87, "latencia_ms": 34.1 }
```

> El servidor debe aceptar mensajes grandes: `websockets.serve(..., max_size=None)`. El límite por defecto (1 MB) puede cortar imágenes.

## Probar el lado de Unity sin AgentPy ni YOLO

En `python/` hay dos servidores de prueba:

```bash
cd python
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt

python servidor_enjambre.py   # terminal 1: 3 drones volando en círculo
python servidor_vision.py     # terminal 2: muestra lo que ve cada dron
```

Después, dar **Play** en Unity con la escena `Bosque`. Deben aparecer 3 drones y una ventana de OpenCV por cada uno.

## Configuración en Unity

- **`ManagerEnjambre`**: GameObject vacío en la escena. Campo *Prefab Dron* = `Assets/Prefabs/Dron.prefab`.
- **Prefab `Dron`**: lleva `VisualDron` (movimiento, esfera de estado y conteo de colisiones) y `CamaraWebSocket` (captura y envío de imágenes).
- Las colisiones aparecen en la consola como advertencias `¡Colisión! …` y se cuentan en el campo *Colisiones* de `VisualDron`.
