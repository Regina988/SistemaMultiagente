using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea el material URP del dron a partir de sus texturas PBR y lo asigna al FBX,
/// para que todas las instancias (escena y prefabs) lo usen.
/// Menú: Herramientas → Configurar material del dron.
/// </summary>
public static class ConfigurarMaterialDron
{
    const string Carpeta = "Assets/Models/police-drone";
    const string Fbx = Carpeta + "/source/Drone_Final.fbx";
    const string Tex = Carpeta + "/textures/Final_lambert1_";
    const string RutaMaterial = "Assets/Materials/Dron.mat";
    const string RutaMetalSuave = Tex + "MetallicSmoothness.png";

    [MenuItem("Herramientas/Configurar material del dron")]
    static void Configurar()
    {
        // 1. Tipos de importación correctos para cada textura.
        ConfigurarTextura(Tex + "Normal.png", normal: true, srgb: false);
        ConfigurarTextura(Tex + "Metallic.png", normal: false, srgb: false);
        ConfigurarTextura(Tex + "Roughness.png", normal: false, srgb: false);
        ConfigurarTextura(Tex + "BaseColor.png", normal: false, srgb: true);
        ConfigurarTextura(Tex + "Emissive.png", normal: false, srgb: true);

        // 2. URP Lit usa una sola textura: metal en R y suavidad (1 - rugosidad) en A.
        CrearMetallicSmoothness();
        ConfigurarTextura(RutaMetalSuave, normal: false, srgb: false);

        // 3. Material URP Lit.
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (mat == null)
        {
            mat = new Material(lit);
            AssetDatabase.CreateAsset(mat, RutaMaterial);
        }
        mat.shader = lit;

        mat.SetTexture("_BaseMap", Cargar(Tex + "BaseColor.png"));
        mat.SetColor("_BaseColor", Color.white);

        mat.SetTexture("_BumpMap", Cargar(Tex + "Normal.png"));
        mat.SetFloat("_BumpScale", 1f);
        mat.EnableKeyword("_NORMALMAP");

        mat.SetTexture("_MetallicGlossMap", Cargar(RutaMetalSuave));
        mat.SetFloat("_Metallic", 1f);
        mat.SetFloat("_Smoothness", 1f);
        mat.SetFloat("_SmoothnessTextureChannel", 0f); // alfa de la textura de metal
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");

        mat.SetTexture("_EmissionMap", Cargar(Tex + "Emissive.png"));
        mat.SetColor("_EmissionColor", Color.white);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        // 4. Reasignar todos los materiales del FBX a este material.
        var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(Fbx))
            if (o is Material m)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), mat);
        importer.SaveAndReimport();

        // 5. Por si alguna instancia en escena tiene materiales sobrescritos.
        int instancias = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            GameObject origen = PrefabUtility.GetCorrespondingObjectFromOriginalSource(r.gameObject);
            if (origen == null || AssetDatabase.GetAssetPath(origen) != Fbx) continue;
            Undo.RecordObject(r, "Material del dron");
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            instancias++;
        }

        Selection.activeObject = mat;
        Debug.Log($"Material del dron listo en {RutaMaterial}. Asignado al FBX y a {instancias} renderers en la escena.");
    }

    static Texture2D Cargar(string ruta) => AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);

    static void ConfigurarTextura(string ruta, bool normal, bool srgb)
    {
        var ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (ti == null) { Debug.LogWarning($"No encontré {ruta}"); return; }
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = srgb;
        ti.SaveAndReimport();
    }

    static void CrearMetallicSmoothness()
    {
        // Leer los PNG directo del disco para no depender de "Read/Write" en el importador.
        Texture2D metal = LeerPng(Tex + "Metallic.png");
        Texture2D rugosidad = LeerPng(Tex + "Roughness.png");
        int w = rugosidad.width, h = rugosidad.height;

        var salida = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
        Color[] r = rugosidad.GetPixels();
        Color[] m = metal.GetPixels();
        bool mismoTam = metal.width == w && metal.height == h;
        var px = new Color[r.Length];
        for (int i = 0; i < r.Length; i++)
        {
            float metalico = mismoTam
                ? m[i].r
                : metal.GetPixelBilinear((i % w + 0.5f) / w, (i / w + 0.5f) / h).r;
            px[i] = new Color(metalico, metalico, metalico, 1f - r[i].r);
        }
        salida.SetPixels(px);
        File.WriteAllBytes(RutaMetalSuave, salida.EncodeToPNG());
        AssetDatabase.ImportAsset(RutaMetalSuave);
    }

    static Texture2D LeerPng(string ruta)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        t.LoadImage(File.ReadAllBytes(ruta));
        return t;
    }
}
