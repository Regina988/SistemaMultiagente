using UnityEngine;

/// <summary>
/// Mueve el dron suavemente hacia la posición que manda Python, muestra su estado con una
/// esfera de color encima y cuenta colisiones. Va en la raíz del prefab Dron.
///
/// Estados: "patrullando" (verde), "duda" (amarillo), "confirmado" (rojo).
/// </summary>
public class VisualDron : MonoBehaviour
{
    [Header("Movimiento")]
    public float velocidadMovimiento = 6f;
    public float velocidadGiro = 5f;

    [Header("Indicador de estado")]
    public float alturaIndicador = 0.8f;
    public float tamanoIndicador = 0.35f;

    [Header("Colisiones (solo lectura)")]
    public int colisiones;

    private Vector3 destino;
    private string estadoActual = "";
    private Renderer indicador;

    private void Awake()
    {
        destino = transform.position;
        CrearIndicador();

        // El collider se usa como trigger: el Rigidbody es cinemático (lo mueve el código),
        // así Unity avisa cuando el dron toca un árbol, el suelo u otro dron.
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void CrearIndicador()
    {
        GameObject esfera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        esfera.name = "IndicadorEstado";
        Destroy(esfera.GetComponent<Collider>());
        esfera.transform.SetParent(transform, false);
        esfera.transform.localPosition = Vector3.up * alturaIndicador;
        esfera.transform.localScale = Vector3.one * tamanoIndicador;

        // Unlit para que el color se vea igual con sol o sombra.
        indicador = esfera.GetComponent<Renderer>();
        indicador.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        indicador.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        PintarIndicador("patrullando");
    }

    public void ActualizarDatos(Vector3 nuevaPosicion, string nuevoEstado)
    {
        destino = nuevaPosicion;
        if (nuevoEstado != estadoActual) PintarIndicador(nuevoEstado);
    }

    private void PintarIndicador(string estado)
    {
        estadoActual = estado;
        if (indicador == null) return;

        Color color;
        switch (estado)
        {
            case "duda": color = Color.yellow; break;
            case "confirmado": color = Color.red; break;
            default: color = Color.green; break; // "patrullando"
        }
        indicador.material.SetColor("_BaseColor", color);
    }

    private void Update()
    {
        transform.position = Vector3.Lerp(transform.position, destino, Time.deltaTime * velocidadMovimiento);

        // Girar solo en horizontal para que la cámara no cambie de inclinación.
        Vector3 direccion = destino - transform.position;
        direccion.y = 0f;
        if (direccion.sqrMagnitude > 0.01f)
        {
            Quaternion objetivo = Quaternion.LookRotation(direccion);
            transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, Time.deltaTime * velocidadGiro);
        }
    }

    private void OnTriggerEnter(Collider otro)
    {
        colisiones++;
        Debug.LogWarning($"¡Colisión! {name} tocó '{otro.name}' en {transform.position}. Total: {colisiones}");
    }
}
