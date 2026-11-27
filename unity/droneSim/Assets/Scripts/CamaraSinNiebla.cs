using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Desactiva la niebla de la escena solo mientras renderiza esta cámara.
/// Sirve para la cámara general: está a más de 100 m del bosque y la niebla lo taparía,
/// mientras que las cámaras de los drones (cerca del suelo) siguen viendo con niebla.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class CamaraSinNiebla : MonoBehaviour
{
    private Camera cam;
    private bool nieblaOriginal;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        RenderPipelineManager.beginCameraRendering += AntesDeRenderizar;
        RenderPipelineManager.endCameraRendering += DespuesDeRenderizar;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= AntesDeRenderizar;
        RenderPipelineManager.endCameraRendering -= DespuesDeRenderizar;
    }

    private void AntesDeRenderizar(ScriptableRenderContext contexto, Camera camara)
    {
        if (camara != cam) return;
        nieblaOriginal = RenderSettings.fog;
        RenderSettings.fog = false;
    }

    private void DespuesDeRenderizar(ScriptableRenderContext contexto, Camera camara)
    {
        if (camara != cam) return;
        RenderSettings.fog = nieblaOriginal;
    }
}
