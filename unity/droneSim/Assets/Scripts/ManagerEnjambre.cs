using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// ─────────────── Mensaje que manda Python (AgentPy) a Unity ───────────────
// {
//   "drones":  [ { "id": "MAV_1", "x": 10, "y": 30, "z": -5, "estado": "patrullando" }, ... ],
//   "enlaces": [ { "origen": "MAV_2", "destino": "MAV_1" }, ... ]
// }
// Ver docs/COMUNICACION.md

[Serializable]
public class DatosDron
{
    public string id;
    public float x;
    public float y;
    public float z;
    public string estado;
}

[Serializable]
public class EnlaceComunicacion
{
    public string origen;
    public string destino;
}

[Serializable]
public class EstadoEnjambre
{
    public List<DatosDron> drones;
    public List<EnlaceComunicacion> enlaces;
}

/// <summary>
/// Recibe por WebSocket el estado del enjambre desde Python, crea un dron por cada id nuevo
/// (a partir del prefab) y actualiza su posición, estado y los enlaces de comunicación.
/// Va en un GameObject vacío de la escena.
/// </summary>
public class ManagerEnjambre : MonoBehaviour
{
    [Header("Conexión")]
    public string urlServidor = "ws://localhost:8767";

    [Header("Drones")]
    public GameObject prefabDron;

    [Tooltip("Si está activo, la 'y' que manda Python es la altura sobre el suelo, no la altura absoluta.")]
    public bool alturaRelativaAlSuelo = true;

    [Tooltip("Terreno para calcular la altura del suelo. Si se deja vacío usa el terreno activo.")]
    public Terrain terreno;

    [Header("Enlaces")]
    public bool mostrarEnlaces = true;
    public float grosorEnlace = 0.15f;

    private ClientWebSocket ws;
    private CancellationTokenSource cts;
    private readonly ConcurrentQueue<string> colaEstados = new ConcurrentQueue<string>();

    private readonly Dictionary<string, VisualDron> dronesActivos = new Dictionary<string, VisualDron>();
    private readonly List<LineRenderer> lineas = new List<LineRenderer>();
    private Material materialLineas;
    private byte siguienteIdNumerico = 1;

    private void Awake()
    {
        // Seguir simulando aunque Unity no tenga el foco (p. ej. mientras se ven las ventanas
        // de Python). Sin esto la simulación se pausa y se cortan los mensajes.
        Application.runInBackground = true;
    }

    private async void Start()
    {
        if (terreno == null) terreno = Terrain.activeTerrain;
        materialLineas = new Material(Shader.Find("Sprites/Default"));

        ws = new ClientWebSocket();
        cts = new CancellationTokenSource();

        try
        {
            await ws.ConnectAsync(new Uri(urlServidor), cts.Token);
            Debug.Log($"ManagerEnjambre conectado a {urlServidor}");
            _ = EscucharServidor();
        }
        catch (Exception e)
        {
            Debug.LogError($"ManagerEnjambre: no se pudo conectar a {urlServidor}: {e.Message}");
        }
    }

    private void Update()
    {
        // Solo importa el estado más reciente; los anteriores se descartan.
        string ultimoJson = null;
        while (colaEstados.TryDequeue(out string json)) ultimoJson = json;
        if (ultimoJson == null) return;

        EstadoEnjambre estado;
        try
        {
            estado = JsonUtility.FromJson<EstadoEnjambre>(ultimoJson);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"ManagerEnjambre: JSON inválido ({e.Message}): {ultimoJson}");
            return;
        }

        ActualizarDrones(estado);
        ActualizarEnlaces(estado);
    }

    private void ActualizarDrones(EstadoEnjambre estado)
    {
        if (estado.drones == null) return;

        foreach (DatosDron d in estado.drones)
        {
            Vector3 pos = new Vector3(d.x, d.y, d.z);
            if (alturaRelativaAlSuelo && terreno != null)
                pos.y += terreno.SampleHeight(pos) + terreno.transform.position.y;

            if (!dronesActivos.ContainsKey(d.id))
                CrearDron(d.id, pos);

            dronesActivos[d.id].ActualizarDatos(pos, d.estado);
        }
    }

    private void CrearDron(string id, Vector3 pos)
    {
        GameObject obj = Instantiate(prefabDron, pos, Quaternion.identity);
        obj.name = id;

        VisualDron visual = obj.GetComponent<VisualDron>();
        if (visual == null) visual = obj.AddComponent<VisualDron>();
        dronesActivos[id] = visual;

        // La cámara identifica sus imágenes con un número de 1 byte (1..255).
        CamaraWebSocket camara = obj.GetComponent<CamaraWebSocket>();
        if (camara != null)
        {
            camara.idDron = IdNumerico(id);
            Debug.Log($"Dron '{id}' creado. Sus imágenes llegan a Python con id_dron = {camara.idDron}.");
        }
    }

    // "MAV_3" → 3. Si el id no termina en número, se asigna el siguiente libre.
    private byte IdNumerico(string id)
    {
        int i = id.Length;
        while (i > 0 && char.IsDigit(id[i - 1])) i--;
        if (i < id.Length && int.TryParse(id.Substring(i), out int n) && n >= 1 && n <= 255)
            return (byte)n;
        return siguienteIdNumerico++;
    }

    private void ActualizarEnlaces(EstadoEnjambre estado)
    {
        int usados = 0;
        if (mostrarEnlaces && estado.enlaces != null)
        {
            foreach (EnlaceComunicacion e in estado.enlaces)
            {
                if (!dronesActivos.TryGetValue(e.origen, out VisualDron a) ||
                    !dronesActivos.TryGetValue(e.destino, out VisualDron b))
                    continue;

                LineRenderer linea = ObtenerLinea(usados++);
                linea.positionCount = 2;
                linea.SetPosition(0, a.transform.position);
                linea.SetPosition(1, b.transform.position);
            }
        }

        for (int i = usados; i < lineas.Count; i++)
            lineas[i].positionCount = 0;
    }

    private LineRenderer ObtenerLinea(int indice)
    {
        while (lineas.Count <= indice)
        {
            var go = new GameObject($"Enlace_{lineas.Count}");
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.material = materialLineas; // Sprites/Default evita la línea rosa
            lr.startWidth = lr.endWidth = grosorEnlace;
            lr.startColor = Color.cyan;
            lr.endColor = Color.yellow;
            lr.positionCount = 0;
            lineas.Add(lr);
        }
        return lineas[indice];
    }

    private async Task EscucharServidor()
    {
        byte[] buffer = new byte[8192];
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

                colaEstados.Enqueue(sb.ToString());
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            Debug.LogWarning($"ManagerEnjambre: conexión perdida: {e.Message}");
        }
    }

    private async void OnApplicationQuit()
    {
        cts?.Cancel();
        if (ws != null && ws.State == WebSocketState.Open)
        {
            try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Cerrando", CancellationToken.None); }
            catch { }
        }
        ws?.Dispose();
    }
}
