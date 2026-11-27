using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de editor para aplanar el terreno del escenario.
/// Menú: Herramientas → Editar Terreno.
/// Incluye además una sección para reducir árboles y abrir claros.
///
/// Mezcla cada punto del heightmap hacia una altura objetivo (el promedio del terreno),
/// vuelve a asentar los árboles sobre el suelo y mueve los objetos de la escena
/// (rocas, troncos, personas...) para que no queden flotando ni enterrados.
/// Se puede deshacer con Cmd/Ctrl + Z.
/// </summary>
public class AplanarTerreno : EditorWindow
{
    [Tooltip("0 = sin cambios, 1 = completamente plano")]
    float intensidad = 1f;

    Terrain terreno;

    [MenuItem("Herramientas/Editar Terreno")]
    static void Abrir() => GetWindow<AplanarTerreno>("Editar Terreno");

    void OnEnable() => terreno = Terrain.activeTerrain;

    void OnGUI()
    {
        terreno = (Terrain)EditorGUILayout.ObjectField("Terreno", terreno, typeof(Terrain), true);
        intensidad = EditorGUILayout.Slider(
            new GUIContent("Intensidad", "0 = sin cambios, 1 = completamente plano"), intensidad, 0f, 1f);

        if (terreno == null)
        {
            EditorGUILayout.HelpBox("Selecciona el Terrain de la escena.", MessageType.Warning);
            return;
        }

        var (min, max, promedio) = RangoAlturas(terreno.terrainData);
        EditorGUILayout.HelpBox(
            $"Relieve actual: {min:F1} m a {max:F1} m (diferencia {max - min:F1} m).\n" +
            $"Altura objetivo (promedio): {promedio:F1} m.",
            MessageType.Info);

        if (GUILayout.Button("Aplanar"))
            Aplanar(terreno, intensidad);

        DibujarSeccionArboles();
    }

    // ───────────────────────────── Árboles ─────────────────────────────
    //
    // En el terreno, "árbol" (TreeInstance) incluye también rocas, hojas secas, troncos y plantas.
    // Estas herramientas solo tocan los árboles de verdad (prototipos altos); los objetos de suelo
    // se conservan tal cual.

    const float AlturaMinimaArbol = 4f; // metros; lo más bajo se considera objeto de suelo

    float conservar = 0.5f;
    bool quitarSinCollider = true;
    int numClaros = 4;
    float radioClaro = 12f;
    int semilla = 42;
    float tamCelda = 10f;
    float densidad = 6f;
    float separacionMin = 3f;
    TerrainData origenSuelo;

    void DibujarSeccionArboles()
    {
        TerrainData datos = terreno.terrainData;
        bool[] esArbol = ClasificarPrototipos(datos);
        int numArboles = 0;
        foreach (TreeInstance a in datos.treeInstances)
            if (esArbol[a.prototypeIndex]) numArboles++;

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Árboles", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            $"Árboles: {numArboles}   ·   Objetos de suelo: {datos.treeInstanceCount - numArboles}\n" +
            $"Árboles: {NombresPrototipos(datos, esArbol, true)}\n" +
            $"Suelo (no se tocan): {NombresPrototipos(datos, esArbol, false)}",
            MessageType.Info);

        semilla = EditorGUILayout.IntField(
            new GUIContent("Semilla", "Mismo número = mismo resultado"), semilla);

        quitarSinCollider = EditorGUILayout.Toggle(
            new GUIContent("Quitar árboles sin collider",
                "Quita los árboles que los drones atravesarían (CherryBlossom, DawnRedwood...)"),
            quitarSinCollider);
        conservar = EditorGUILayout.Slider(
            new GUIContent("Conservar", "Porcentaje de árboles que se quedan (al azar)"), conservar, 0.05f, 1f);
        if (GUILayout.Button($"Reducir árboles (conservar {conservar:P0})"))
            ReducirArboles(datos, conservar, quitarSinCollider, semilla);

        EditorGUILayout.Space(6);
        float area = datos.size.x * datos.size.z;
        EditorGUILayout.LabelField($"Densidad actual: {numArboles / area * 100f:F2} árboles / 100 m²");
        tamCelda = EditorGUILayout.Slider(
            new GUIContent("Tamaño de celda (m)", "Más chico = más parejo; más grande = más natural"), tamCelda, 5f, 30f);
        densidad = EditorGUILayout.Slider(
            new GUIContent("Árboles / 100 m²", "Densidad objetivo en cada celda"), densidad, 0.5f, 15f);
        separacionMin = EditorGUILayout.Slider(
            new GUIContent("Separación mínima (m)", "Distancia mínima al agregar árboles nuevos"), separacionMin, 1f, 8f);
        if (GUILayout.Button("Igualar densidad"))
            IgualarDensidad(datos, tamCelda, densidad, separacionMin, semilla);

        EditorGUILayout.Space(6);
        numClaros = EditorGUILayout.IntSlider(
            new GUIContent("Número de claros", "Zonas circulares sin árboles en lugares al azar"), numClaros, 1, 15);
        radioClaro = EditorGUILayout.Slider(new GUIContent("Radio del claro (m)"), radioClaro, 3f, 40f);
        if (GUILayout.Button("Abrir claros"))
            AbrirClaros(datos, numClaros, radioClaro, semilla);

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("Objetos de suelo", EditorStyles.boldLabel);
        if (origenSuelo == null)
            origenSuelo = AssetDatabase.LoadAssetAtPath<TerrainData>(
                "Assets/Polyeler/EssentialNaturePack/Terrain/New Terrain.asset");
        origenSuelo = (TerrainData)EditorGUILayout.ObjectField(
            new GUIContent("Copiar desde", "Terreno original del que se copian rocas, hojas, troncos y plantas"),
            origenSuelo, typeof(TerrainData), false);
        using (new EditorGUI.DisabledScope(origenSuelo == null || origenSuelo == datos))
            if (GUILayout.Button("Restaurar objetos de suelo del original"))
                RestaurarSuelo(datos, origenSuelo);

        EditorGUILayout.Space(6);
        int numSuelo = datos.treeInstanceCount - numArboles;
        EditorGUILayout.LabelField($"Densidad de suelo actual: {numSuelo / area * 100f:F2} objetos / 100 m²");
        densidadSuelo = EditorGUILayout.Slider(
            new GUIContent("Objetos de suelo / 100 m²",
                "Mínimo por celda; solo se agregan donde faltan, nunca se quitan"), densidadSuelo, 0.5f, 30f);
        if (GUILayout.Button("Rellenar zonas vacías"))
            RellenarSuelo(datos, tamCelda, densidadSuelo, semilla);
    }

    float densidadSuelo = 8f;

    /// En cada celda con menos objetos de suelo que el mínimo, agrega copias de objetos de suelo
    /// existentes (elegidos al azar, así se respeta la proporción actual de rocas/hojas/plantas)
    /// sin encimarlos con troncos de árboles.
    static void RellenarSuelo(TerrainData datos, float tamCelda, float densidadSuelo, int semilla)
    {
        Undo.RegisterCompleteObjectUndo(datos, "Rellenar zonas vacías");
        var azar = new System.Random(semilla + 1);
        Vector3 tam = datos.size;
        int celdasX = Mathf.Max(1, Mathf.RoundToInt(tam.x / tamCelda));
        int celdasZ = Mathf.Max(1, Mathf.RoundToInt(tam.z / tamCelda));
        float anchoX = tam.x / celdasX, anchoZ = tam.z / celdasZ;
        bool[] esArbol = ClasificarPrototipos(datos);

        var plantillas = new List<TreeInstance>();
        var conteo = new int[celdasX, celdasZ];
        var troncos = new List<Vector2>[celdasX, celdasZ];
        for (int x = 0; x < celdasX; x++)
            for (int z = 0; z < celdasZ; z++)
                troncos[x, z] = new List<Vector2>();

        var resultado = new List<TreeInstance>(datos.treeInstances);
        foreach (TreeInstance a in datos.treeInstances)
        {
            int cx = Mathf.Clamp((int)(a.position.x * celdasX), 0, celdasX - 1);
            int cz = Mathf.Clamp((int)(a.position.z * celdasZ), 0, celdasZ - 1);
            if (esArbol[a.prototypeIndex])
                troncos[cx, cz].Add(new Vector2(a.position.x * tam.x, a.position.z * tam.z));
            else
            {
                conteo[cx, cz]++;
                plantillas.Add(a);
            }
        }
        if (plantillas.Count == 0)
        {
            Debug.LogWarning("No hay objetos de suelo que copiar. Usa primero «Restaurar objetos de suelo».");
            return;
        }

        const float separacionTronco = 1.5f;
        float objetivoExacto = densidadSuelo * anchoX * anchoZ / 100f;
        int agregados = 0;

        for (int x = 0; x < celdasX; x++)
        for (int z = 0; z < celdasZ; z++)
        {
            int objetivo = (int)objetivoExacto + (azar.NextDouble() < objetivoExacto % 1f ? 1 : 0);
            int faltan = objetivo - conteo[x, z];
            int intentos = 0;
            while (faltan > 0 && intentos++ < objetivo * 20)
            {
                var p = new Vector2(
                    (x + (float)azar.NextDouble()) * anchoX,
                    (z + (float)azar.NextDouble()) * anchoZ);
                bool muyCerca = false;
                foreach (Vector2 t in troncos[x, z])
                    if (Vector2.Distance(p, t) < separacionTronco) { muyCerca = true; break; }
                if (muyCerca) continue;

                TreeInstance nuevo = plantillas[azar.Next(plantillas.Count)];
                float nx = p.x / tam.x, nz = p.y / tam.z;
                nuevo.position = new Vector3(nx, datos.GetInterpolatedHeight(nx, nz) / tam.y, nz);
                nuevo.rotation = (float)(azar.NextDouble() * Mathf.PI * 2);
                float escala = Mathf.Lerp(0.8f, 1.2f, (float)azar.NextDouble());
                nuevo.widthScale *= escala;
                nuevo.heightScale *= escala;

                resultado.Add(nuevo);
                faltan--;
                agregados++;
            }
        }

        Guardar(datos, resultado);
        Debug.Log($"Zonas vacías rellenadas: +{agregados} objetos de suelo " +
                  $"(mínimo {densidadSuelo:F1} / 100 m² en celdas de {anchoX:F0}×{anchoZ:F0} m).");
    }

    /// true = árbol de verdad, false = objeto de suelo (roca, hojas, tronco caído, planta).
    static bool[] ClasificarPrototipos(TerrainData datos)
    {
        TreePrototype[] prototipos = datos.treePrototypes;
        var resultado = new bool[prototipos.Length];
        for (int i = 0; i < prototipos.Length; i++)
            resultado[i] = AlturaPrefab(prototipos[i].prefab) >= AlturaMinimaArbol;
        return resultado;
    }

    static float AlturaPrefab(GameObject prefab)
    {
        if (prefab == null) return 0f;
        // Altura real en Y considerando rotación y escala de cada hijo
        // (los FBX exportados de Blender suelen venir rotados -90° en X).
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (MeshFilter mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds b = mf.sharedMesh.bounds;
            Matrix4x4 m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var esquina = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                float y = m.MultiplyPoint3x4(esquina).y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }
        return maxY > minY ? maxY - minY : 0f;
    }

    static string NombresPrototipos(TerrainData datos, bool[] esArbol, bool arboles)
    {
        var nombres = new List<string>();
        TreePrototype[] prototipos = datos.treePrototypes;
        for (int i = 0; i < prototipos.Length; i++)
            if (esArbol[i] == arboles && prototipos[i].prefab != null)
                nombres.Add(prototipos[i].prefab.name);
        return nombres.Count > 0 ? string.Join(", ", nombres) : "(ninguno)";
    }

    static bool TieneCollider(TreePrototype p) =>
        p.prefab != null && p.prefab.GetComponentInChildren<Collider>() != null;

    static void ReducirArboles(TerrainData datos, float conservar, bool quitarSinCollider, int semilla)
    {
        Undo.RegisterCompleteObjectUndo(datos, "Reducir árboles");
        var azar = new System.Random(semilla);
        TreePrototype[] prototipos = datos.treePrototypes;
        bool[] esArbol = ClasificarPrototipos(datos);

        var resultado = new List<TreeInstance>();
        int quitadosEspecie = 0;
        foreach (TreeInstance a in datos.treeInstances)
        {
            if (!esArbol[a.prototypeIndex]) { resultado.Add(a); continue; } // suelo: no se toca
            if (quitarSinCollider && !TieneCollider(prototipos[a.prototypeIndex])) { quitadosEspecie++; continue; }
            if (azar.NextDouble() < conservar) resultado.Add(a);
        }

        int antes = datos.treeInstanceCount;
        Guardar(datos, resultado);
        Debug.Log($"Instancias: {antes} → {resultado.Count} " +
                  $"({quitadosEspecie} árboles quitados por no tener collider; objetos de suelo intactos).");
    }

    /// Divide el terreno en celdas cuadradas y deja cada una con la misma cantidad de árboles:
    /// quita al azar donde sobran y agrega copias de árboles existentes (solo especies con collider)
    /// donde faltan. Los objetos de suelo no se tocan.
    static void IgualarDensidad(TerrainData datos, float tamCelda, float densidad, float separacionMin, int semilla)
    {
        Undo.RegisterCompleteObjectUndo(datos, "Igualar densidad");
        var azar = new System.Random(semilla);
        Vector3 tam = datos.size;
        int celdasX = Mathf.Max(1, Mathf.RoundToInt(tam.x / tamCelda));
        int celdasZ = Mathf.Max(1, Mathf.RoundToInt(tam.z / tamCelda));
        float anchoX = tam.x / celdasX, anchoZ = tam.z / celdasZ;
        TreePrototype[] prototipos = datos.treePrototypes;
        bool[] esArbol = ClasificarPrototipos(datos);

        // Una plantilla por especie de árbol con collider, para copiar escala y color.
        var plantillas = new List<TreeInstance>();
        var yaTiene = new HashSet<int>();
        var resultado = new List<TreeInstance>();
        var celdas = new List<TreeInstance>[celdasX, celdasZ];
        for (int x = 0; x < celdasX; x++)
            for (int z = 0; z < celdasZ; z++)
                celdas[x, z] = new List<TreeInstance>();

        foreach (TreeInstance a in datos.treeInstances)
        {
            if (!esArbol[a.prototypeIndex]) { resultado.Add(a); continue; } // suelo: no se toca
            if (TieneCollider(prototipos[a.prototypeIndex]) && yaTiene.Add(a.prototypeIndex)) plantillas.Add(a);
            int cx = Mathf.Clamp((int)(a.position.x * celdasX), 0, celdasX - 1);
            int cz = Mathf.Clamp((int)(a.position.z * celdasZ), 0, celdasZ - 1);
            celdas[cx, cz].Add(a);
        }

        float objetivoExacto = densidad * anchoX * anchoZ / 100f;
        int quitados = 0, agregados = 0;

        for (int x = 0; x < celdasX; x++)
        for (int z = 0; z < celdasZ; z++)
        {
            List<TreeInstance> lista = celdas[x, z];
            // Redondeo aleatorio para que densidades fraccionarias salgan bien en promedio.
            int objetivo = (int)objetivoExacto + (azar.NextDouble() < objetivoExacto % 1f ? 1 : 0);

            // Sobran: barajar y quedarse con los primeros.
            for (int i = lista.Count - 1; i > 0; i--)
            {
                int j = azar.Next(i + 1);
                (lista[i], lista[j]) = (lista[j], lista[i]);
            }
            if (lista.Count > objetivo)
            {
                quitados += lista.Count - objetivo;
                lista.RemoveRange(objetivo, lista.Count - objetivo);
            }

            // Faltan: agregar en posiciones al azar respetando la separación mínima.
            var posiciones = lista.ConvertAll(a => new Vector2(a.position.x * tam.x, a.position.z * tam.z));
            int intentos = 0;
            while (lista.Count < objetivo && plantillas.Count > 0 && intentos++ < objetivo * 20)
            {
                var p = new Vector2(
                    (x + (float)azar.NextDouble()) * anchoX,
                    (z + (float)azar.NextDouble()) * anchoZ);
                bool muyCerca = false;
                foreach (Vector2 o in posiciones)
                    if (Vector2.Distance(p, o) < separacionMin) { muyCerca = true; break; }
                if (muyCerca) continue;

                TreeInstance nuevo = plantillas[azar.Next(plantillas.Count)];
                float nx = p.x / tam.x, nz = p.y / tam.z;
                nuevo.position = new Vector3(nx, datos.GetInterpolatedHeight(nx, nz) / tam.y, nz);
                nuevo.rotation = (float)(azar.NextDouble() * Mathf.PI * 2);
                float escala = Mathf.Lerp(0.85f, 1.15f, (float)azar.NextDouble());
                nuevo.widthScale *= escala;
                nuevo.heightScale *= escala;

                lista.Add(nuevo);
                posiciones.Add(p);
                agregados++;
            }
            resultado.AddRange(lista);
        }

        Guardar(datos, resultado);
        Debug.Log($"Densidad igualada a {densidad:F2} árboles/100 m² en celdas de {anchoX:F0}×{anchoZ:F0} m " +
                  $"(−{quitados}, +{agregados} árboles; objetos de suelo intactos).");
    }

    static void AbrirClaros(TerrainData datos, int numClaros, float radio, int semilla)
    {
        Undo.RegisterCompleteObjectUndo(datos, "Abrir claros");
        var azar = new System.Random(semilla);
        Vector3 tam = datos.size;
        bool[] esArbol = ClasificarPrototipos(datos);

        // Centros al azar dejando un margen para que el claro quede dentro del terreno.
        var centros = new Vector2[numClaros];
        for (int i = 0; i < numClaros; i++)
            centros[i] = new Vector2(
                Mathf.Lerp(radio, tam.x - radio, (float)azar.NextDouble()),
                Mathf.Lerp(radio, tam.z - radio, (float)azar.NextDouble()));

        var resultado = new List<TreeInstance>();
        int quitados = 0;
        foreach (TreeInstance a in datos.treeInstances)
        {
            if (!esArbol[a.prototypeIndex]) { resultado.Add(a); continue; } // suelo: se queda en el claro
            var p = new Vector2(a.position.x * tam.x, a.position.z * tam.z);
            bool dentro = false;
            foreach (Vector2 c in centros)
                if (Vector2.Distance(p, c) < radio) { dentro = true; break; }
            if (dentro) quitados++;
            else resultado.Add(a);
        }

        Guardar(datos, resultado);
        Debug.Log($"Claros abiertos: {numClaros} de {radio:F0} m de radio ({quitados} árboles quitados).");
    }

    /// Reemplaza los objetos de suelo actuales por los del terreno original,
    /// asentándolos sobre la altura actual del suelo. Los árboles actuales no se tocan.
    static void RestaurarSuelo(TerrainData datos, TerrainData origen)
    {
        Undo.RegisterCompleteObjectUndo(datos, "Restaurar objetos de suelo");
        bool[] esArbol = ClasificarPrototipos(datos);
        TreePrototype[] destino = datos.treePrototypes;
        TreePrototype[] fuente = origen.treePrototypes;

        // Mapear prototipos del original a los del terreno actual por prefab (no por índice).
        var mapa = new int[fuente.Length];
        for (int i = 0; i < fuente.Length; i++)
        {
            mapa[i] = -1;
            for (int j = 0; j < destino.Length; j++)
                if (fuente[i].prefab != null && fuente[i].prefab == destino[j].prefab) { mapa[i] = j; break; }
        }

        var resultado = new List<TreeInstance>();
        int quitados = 0;
        foreach (TreeInstance a in datos.treeInstances)
        {
            if (esArbol[a.prototypeIndex]) resultado.Add(a);
            else quitados++;
        }

        int restaurados = 0;
        foreach (TreeInstance a in origen.treeInstances)
        {
            int idx = mapa[a.prototypeIndex];
            if (idx < 0 || esArbol[idx]) continue;
            TreeInstance copia = a;
            copia.prototypeIndex = idx;
            Vector3 p = copia.position;
            p.y = datos.GetInterpolatedHeight(p.x, p.z) / datos.size.y;
            copia.position = p;
            resultado.Add(copia);
            restaurados++;
        }

        Guardar(datos, resultado);
        Debug.Log($"Objetos de suelo restaurados: {restaurados} (se reemplazaron {quitados}).");
    }

    static void Guardar(TerrainData datos, List<TreeInstance> arboles)
    {
        datos.treeInstances = arboles.ToArray();
        EditorUtility.SetDirty(datos);
        AssetDatabase.SaveAssetIfDirty(datos);
        foreach (Terrain t in Terrain.activeTerrains) t.Flush();
    }

    static (float min, float max, float promedio) RangoAlturas(TerrainData datos)
    {
        int res = datos.heightmapResolution;
        float[,] h = datos.GetHeights(0, 0, res, res);
        float min = float.MaxValue, max = float.MinValue, suma = 0f;
        foreach (float v in h)
        {
            if (v < min) min = v;
            if (v > max) max = v;
            suma += v;
        }
        float escala = datos.size.y;
        return (min * escala, max * escala, suma / h.Length * escala);
    }

    static void Aplanar(Terrain terreno, float intensidad)
    {
        TerrainData datos = terreno.terrainData;

        // Objetos de la escena apoyados en el terreno (raíces de prefabs que no son el terreno).
        var objetos = new List<Transform>();
        var alturasAntes = new List<float>();
        foreach (GameObject go in terreno.gameObject.scene.GetRootGameObjects())
            ColectarObjetos(go.transform, terreno, objetos);
        foreach (Transform t in objetos)
            alturasAntes.Add(terreno.SampleHeight(t.position));

        Undo.RegisterCompleteObjectUndo(datos, "Aplanar terreno");
        Undo.RecordObjects(objetos.ConvertAll(t => (Object)t).ToArray(), "Aplanar terreno");

        // 1. Heightmap: mezclar hacia el promedio.
        int res = datos.heightmapResolution;
        float[,] h = datos.GetHeights(0, 0, res, res);
        float promedio = 0f;
        foreach (float v in h) promedio += v;
        promedio /= h.Length;

        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                h[y, x] = Mathf.Lerp(h[y, x], promedio, intensidad);
        datos.SetHeights(0, 0, h);

        // 2. Árboles pintados: volver a asentarlos sobre el suelo.
        TreeInstance[] arboles = datos.treeInstances;
        for (int i = 0; i < arboles.Length; i++)
        {
            Vector3 p = arboles[i].position; // normalizada 0..1
            p.y = datos.GetInterpolatedHeight(p.x, p.z) / datos.size.y;
            arboles[i].position = p;
        }
        datos.treeInstances = arboles;

        // 3. Objetos de la escena: moverlos lo mismo que cambió el suelo debajo de ellos.
        for (int i = 0; i < objetos.Count; i++)
        {
            float delta = terreno.SampleHeight(objetos[i].position) - alturasAntes[i];
            objetos[i].position += Vector3.up * delta;
        }

        terreno.Flush();
        EditorUtility.SetDirty(datos);
        AssetDatabase.SaveAssetIfDirty(datos);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);

        Debug.Log($"Terreno aplanado (intensidad {intensidad:P0}). " +
                  $"{arboles.Length} árboles y {objetos.Count} objetos reajustados. " +
                  "Guarda la escena (Cmd/Ctrl + S).");
    }

    // Toma la raíz de cada prefab (o cada objeto con Renderer) sin bajar a sus hijos,
    // para no mover dos veces el mismo objeto. Ignora el terreno, cámaras, luces y volúmenes.
    static void ColectarObjetos(Transform t, Terrain terreno, List<Transform> salida)
    {
        if (t == terreno.transform) return;
        if (t.GetComponent<Camera>() || t.GetComponent<Light>() ||
            t.GetComponent<UnityEngine.Rendering.Volume>()) return;

        bool esRaizPrefab = PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject);
        bool tieneRender = t.GetComponent<Renderer>() != null;
        if (esRaizPrefab || tieneRender)
        {
            salida.Add(t);
            return;
        }
        foreach (Transform hijo in t)
            ColectarObjetos(hijo, terreno, salida);
    }
}
