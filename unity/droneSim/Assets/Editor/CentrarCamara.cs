using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Coloca la cámara seleccionada (o la cámara general de la escena) apuntando al centro del
/// terreno, inclinada y a la distancia justa para que se vea el terreno completo.
/// Menú: Herramientas → Centrar cámara sobre el terreno.
/// </summary>
public static class CentrarCamara
{
    const float Inclinacion = 60f; // grados hacia abajo (90 = vista cenital)

    [MenuItem("Herramientas/Centrar cámara sobre el terreno")]
    static void Centrar()
    {
        Terrain terreno = Terrain.activeTerrain;
        if (terreno == null) { Debug.LogError("No hay terreno en la escena."); return; }

        Camera cam = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<Camera>()
            : null;
        if (cam == null)
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.GetComponentInParent<CamaraWebSocket>() == null) { cam = c; break; } // ignorar cámaras de drones
        if (cam == null) { Debug.LogError("Selecciona la cámara que quieres centrar."); return; }

        // Centro del terreno, a la altura del suelo.
        Vector3 tam = terreno.terrainData.size;
        Vector3 centro = terreno.transform.position + new Vector3(tam.x / 2f, 0f, tam.z / 2f);
        centro.y = terreno.SampleHeight(centro) + terreno.transform.position.y;

        // Distancia mínima para que quepan el ancho (horizontal) y la profundidad del terreno
        // vista en diagonal (vertical), con un 5 % de margen.
        const float margen = 1.05f;
        float fovV = cam.fieldOfView * Mathf.Deg2Rad;
        float fovH = Camera.VerticalToHorizontalFieldOfView(cam.fieldOfView, Mathf.Max(cam.aspect, 0.01f)) * Mathf.Deg2Rad;
        float porAncho = tam.x / 2f * margen / Mathf.Tan(fovH / 2f);
        float porProfundidad = tam.z / 2f * Mathf.Sin(Inclinacion * Mathf.Deg2Rad) * margen / Mathf.Tan(fovV / 2f);
        float distancia = Mathf.Max(porAncho, porProfundidad);
        float radio = 0.5f * Mathf.Sqrt(tam.x * tam.x + tam.z * tam.z);

        Undo.RecordObject(cam.transform, "Centrar cámara");
        Undo.RecordObject(cam, "Centrar cámara");
        Quaternion rot = Quaternion.Euler(Inclinacion, 0f, 0f); // mirando hacia +Z (norte)
        cam.transform.SetPositionAndRotation(centro - rot * Vector3.forward * distancia, rot);
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, distancia + radio * 2f);

        // Que se vea con la misma iluminación y post-processing que las cámaras de los drones.
        var datos = cam.GetComponent<UniversalAdditionalCameraData>();
        if (datos != null)
        {
            Undo.RecordObject(datos, "Centrar cámara");
            datos.renderPostProcessing = true;
        }

        // Sin niebla para esta cámara: a esta distancia taparía todo el bosque.
        if (cam.GetComponent<CamaraSinNiebla>() == null)
            Undo.AddComponent<CamaraSinNiebla>(cam.gameObject);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cam.gameObject.scene);
        Debug.Log($"Cámara '{cam.name}' centrada: apunta a {centro} desde {distancia:F0} m, " +
                  $"inclinada {Inclinacion}°. Guarda la escena (Cmd/Ctrl + S).");
    }
}
