using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Captura la cámara del dron cada cierto tiempo y manda la imagen al nodo de visión (YOLO)
/// por WebSocket. Cada mensaje binario es: [1 byte id del dron][bytes del JPG].
/// Python responde con un JSON (resultado de la detección).
/// Va en la raíz del prefab Dron; el id lo asigna ManagerEnjambre al crear el dron.
/// Ver docs/COMUNICACION.md
/// </summary>
public class CamaraWebSocket : MonoBehaviour
{
    [Header("Identificación y red")]
    [Tooltip("1..255. ManagerEnjambre lo asigna a partir del id del dron (MAV_3 → 3).")]
    public byte idDron = 1;
    public string urlServidorVision = "ws://localhost:8766";

    [Header("Cámara")]
    [Tooltip("Si se deja vacío usa la cámara hija del dron.")]
    public Camera camaraDron;
    public int ancho = 1280;
    public int alto = 720;

    [Range(0.1f, 2f)]
    public float intervaloCaptura = 0.5f; // 2 imágenes por segundo por dron

    [Range(10, 100)]
    public int calidadJPG = 75;

    [Header("Depuración")]
    public bool mostrarRespuestas = true;

    private ClientWebSocket ws;
    private CancellationTokenSource cts;
    private RenderTexture renderTexture;
    private Texture2D texturaTemporal;
    private readonly ConcurrentQueue<string> respuestasVision = new ConcurrentQueue<string>();
    private float temporizador;
    private bool enviando;

    private async void Start()
    {
        if (camaraDron == null) camaraDron = GetComponentInChildren<Camera>();
        if (camaraDron == null)
        {
            Debug.LogError($"{name}: no encontré la cámara del dron.");
            enabled = false;
            return;
        }

        // La cámara solo renderiza cuando tomamos una foto (ahorra GPU con varios drones).
        renderTexture = new RenderTexture(ancho, alto, 24) { name = $"RT_{name}" };
        camaraDron.targetTexture = renderTexture;
        camaraDron.enabled = false;
        texturaTemporal = new Texture2D(ancho, alto, TextureFormat.RGB24, false);

        // Desfase aleatorio para que no todos los drones capturen en el mismo frame.
        temporizador = UnityEngine.Random.Range(0f, intervaloCaptura);

        ws = new ClientWebSocket();
        cts = new CancellationTokenSource();
        try
        {
            await ws.ConnectAsync(new Uri(urlServidorVision), cts.Token);
            Debug.Log($"Cámara de {name} (id {idDron}) conectada a {urlServidorVision}");
            _ = EscucharRespuestas();
        }
        catch (Exception e)
        {
            Debug.LogError($"Cámara de {name}: no se pudo conectar a {urlServidorVision}: {e.Message}");
        }
    }

    private void Update()
    {
        while (respuestasVision.TryDequeue(out string json))
            if (mostrarRespuestas) Debug.Log($"Visión ({name}): {json}");

        if (ws == null || ws.State != WebSocketState.Open || enviando) return;

        temporizador += Time.deltaTime;
        if (temporizador >= intervaloCaptura)
        {
            temporizador = 0f;
            CapturarYEnviar();
        }
    }

    private async void CapturarYEnviar()
    {
        enviando = true;
        try
        {
            // 1. Renderizar la cámara a la RenderTexture y copiar los píxeles a la CPU.
            camaraDron.Render();
            RenderTexture anterior = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texturaTemporal.ReadPixels(new Rect(0, 0, ancho, alto), 0, 0);
            texturaTemporal.Apply();
            RenderTexture.active = anterior;

            // 2. Comprimir a JPG y anteponer el id: [idDron, ...jpg]
            byte[] jpg = texturaTemporal.EncodeToJPG(calidadJPG);
            byte[] paquete = new byte[jpg.Length + 1];
            paquete[0] = idDron;
            Buffer.BlockCopy(jpg, 0, paquete, 1, jpg.Length);

            // 3. Enviar como mensaje binario.
            await ws.SendAsync(new ArraySegment<byte>(paquete), WebSocketMessageType.Binary, true, cts.Token);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Cámara de {name}: error enviando imagen: {e.Message}");
        }
        finally
        {
            enviando = false;
        }
    }

    private async Task EscucharRespuestas()
    {
        byte[] buffer = new byte[4096];
        var sb = new StringBuilder();
        try
        {
            while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult res;
                do
                {
                    res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    if (res.MessageType == WebSocketMessageType.Close) return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
                }
                while (!res.EndOfMessage);

                respuestasVision.Enqueue(sb.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"Cámara de {name}: conexión perdida: {e.Message}");
        }
    }

    private async void OnDestroy()
    {
        cts?.Cancel();
        if (ws != null && ws.State == WebSocketState.Open)
        {
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Cerrando", CancellationToken.None); }
            catch { }
        }
        ws?.Dispose();

        if (texturaTemporal != null) Destroy(texturaTemporal);
        if (renderTexture != null) renderTexture.Release();
    }
}
