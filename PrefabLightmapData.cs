#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
#endif
using UnityEngine;
using System.Collections.Generic;

public class PrefabLightmapData : MonoBehaviour
{
    [System.Serializable]
    struct RendererInfo
    {
        public Renderer renderer;
        public int lightmapIndex;
        public Vector4 lightmapOffsetScale;
    }

    [SerializeField]
    RendererInfo[] m_RendererInfo;
    [SerializeField]
    Texture2D[] m_Lightmaps;
    [SerializeField]
    Texture2D[] m_Lightmaps2;

    public const string LIGHTMAP_RESOURCE_PATH = "Assets/Scenes/Resources/Lightmaps/";

    [System.Serializable]
    struct Texture2D_Remap
    {
        public int originalLightmapIndex;
        public Texture2D originalLightmap;
        public Texture2D lightmap;
        public Texture2D lightmap2;
    }

    static List<Texture2D_Remap> sceneLightmaps = new List<Texture2D_Remap>();

    void Awake()
    {
        ApplyLightmaps(m_RendererInfo, m_Lightmaps, m_Lightmaps2);
    }

    static void ApplyLightmaps(RendererInfo[] rendererInfo, Texture2D[] lightmaps, Texture2D[] lightmaps2)
    {
        bool existsAlready = false;
        int counter = 0;
        int[] lightmapArrayOffsetIndex;

        if (rendererInfo == null || rendererInfo.Length == 0)
            return;

        var settingslightmaps = LightmapSettings.lightmaps;
        var combinedLightmaps = new List<LightmapData>();
        lightmapArrayOffsetIndex = new int[lightmaps.Length];

        for (int i = 0; i < lightmaps.Length; i++)
        {
            existsAlready = false;
            for (int j = 0; j < settingslightmaps.Length; j++)
            {
                if (lightmaps[i] == settingslightmaps[j].lightmapColor)
                {
                    lightmapArrayOffsetIndex[i] = j;
                    existsAlready = true;
                }
            }

            if (!existsAlready)
            {
                lightmapArrayOffsetIndex[i] = counter + settingslightmaps.Length;
                var newLightmapData = new LightmapData();
                newLightmapData.lightmapColor = lightmaps[i];
                newLightmapData.lightmapDir = lightmaps2[i];
                combinedLightmaps.Add(newLightmapData);
                ++counter;
            }
        }

        var combinedLightmaps2 = new LightmapData[settingslightmaps.Length + counter];
        settingslightmaps.CopyTo(combinedLightmaps2, 0);

        if (counter > 0)
        {
            for (int i = 0; i < combinedLightmaps.Count; i++)
            {
                combinedLightmaps2[i + settingslightmaps.Length] = new LightmapData();
                combinedLightmaps2[i + settingslightmaps.Length].lightmapColor = combinedLightmaps[i].lightmapColor;
                combinedLightmaps2[i + settingslightmaps.Length].lightmapDir = combinedLightmaps[i].lightmapDir;
            }
        }

        ApplyRendererInfo(rendererInfo, lightmapArrayOffsetIndex);

        LightmapSettings.lightmaps = combinedLightmaps2;
    }

    static void ApplyRendererInfo(RendererInfo[] infos, int[] arrayOffsetIndex)
    {
        for (int i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            info.renderer.lightmapIndex = arrayOffsetIndex[info.lightmapIndex];
            info.renderer.lightmapScaleOffset = info.lightmapOffsetScale;
        }
    }

#if UNITY_EDITOR
    [MenuItem("VR_Rehearsal_app/Update Scene with Prefab Lightmaps")]
    static void UpdateLightmaps()
    {
        PrefabLightmapData[] prefabs = FindObjectsOfType<PrefabLightmapData>();

        foreach (var instance in prefabs)
        {
            ApplyLightmaps(instance.m_RendererInfo, instance.m_Lightmaps, instance.m_Lightmaps2);
        }

        Debug.Log("Prefab lightmaps updated");
    }

    [MenuItem("VR_Rehearsal_app/Bake Prefab Lightmaps")]
    static void GenerateLightmapInfo()
    {
        if (UnityEditor.Lightmapping.giWorkflowMode != UnityEditor.Lightmapping.GIWorkflowMode.OnDemand)
        {
            Debug.LogError("ExtractLightmapData requires that you have baked you lightmaps and Auto mode is disabled.");
            return;
        }
        UnityEditor.Lightmapping.Bake();

        PrefabLightmapData[] prefabs = FindObjectsOfType<PrefabLightmapData>();

        foreach (var instance in prefabs)
        {
            var gameObject = instance.gameObject;
            var rendererInfos = new List<RendererInfo>();
            var lightmaps = new List<Texture2D>();

            GenerateLightmapInfo(gameObject, rendererInfos, lightmaps);

            instance.m_RendererInfo = rendererInfos.ToArray();
            instance.m_Lightmaps = lightmaps.ToArray();

            var targetPrefab = UnityEditor.PrefabUtility.GetPrefabParent(gameObject) as GameObject;
            if (targetPrefab != null)
            {
                //UnityEditor.Prefab
                UnityEditor.PrefabUtility.ReplacePrefab(gameObject, targetPrefab);
            }
        }
    }

    static void GenerateLightmapInfo(GameObject root, List<RendererInfo> rendererInfos, List<Texture2D> lightmaps)
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer.lightmapIndex != -1)
            {
                RendererInfo info = new RendererInfo();
                info.renderer = renderer;
                info.lightmapOffsetScale = renderer.lightmapScaleOffset;

                Texture2D lightmap = LightmapSettings.lightmaps[renderer.lightmapIndex].lightmapColor;

                info.lightmapIndex = lightmaps.IndexOf(lightmap);
                if (info.lightmapIndex == -1)
                {
                    info.lightmapIndex = lightmaps.Count;
                    lightmaps.Add(lightmap);
                }

                rendererInfos.Add(info);
            }
        }
}

    static int AddLightmap(string scenePath, string resourcePath, int originalLightmapIndex, Texture2D lightmap, Texture2D lightmap2)
    {
        int newIndex = -1;

        for (int i = 0; i < sceneLightmaps.Count; i++)
        {
            if (sceneLightmaps[i].originalLightmapIndex == originalLightmapIndex)
            {
                return i;
            }
        }

        if (newIndex == -1)
        {
            var lightmap_Remap = new Texture2D_Remap();
            lightmap_Remap.originalLightmapIndex = originalLightmapIndex;
            lightmap_Remap.originalLightmap = lightmap;

            var filename = scenePath + "Lightmap-" + originalLightmapIndex;

            string path = FileUtil.GetProjectRelativePath(EditorUtility.SaveFilePanel("Lightmap Far Path",
                LIGHTMAP_RESOURCE_PATH, SceneManager.GetActiveScene().name + "_light-" + originalLightmapIndex, "asset"));

            lightmap_Remap.lightmap = GetLightmapAsset(filename + "_comp_light.exr", path, lightmap);
            if (lightmap2 != null)
            {
                path = FileUtil.GetProjectRelativePath(EditorUtility.SaveFilePanel("Lightmap Near Path",
                    LIGHTMAP_RESOURCE_PATH, SceneManager.GetActiveScene().name + "_dir-" + originalLightmapIndex, "asset"));
                lightmap_Remap.lightmap2 = GetLightmapAsset(filename + "_comp_dir.exr", path, lightmap2);
            }

            sceneLightmaps.Add(lightmap_Remap);
            newIndex = sceneLightmaps.Count - 1;
        }

        return newIndex;
    }

    static Texture2D GetLightmapAsset(string filename, string assetPath, Texture2D lightmap)
    {
        AssetDatabase.ImportAsset(filename, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(filename) as TextureImporter;
        importer.isReadable = true;
        AssetDatabase.ImportAsset(filename, ImportAssetOptions.ForceUpdate);

        var assetLightmap = AssetDatabase.LoadAssetAtPath<Texture2D>(filename);

        var newLightmap = Instantiate<Texture2D>(assetLightmap);

        AssetDatabase.CreateAsset(newLightmap, assetPath);

        newLightmap = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

        importer.isReadable = false;
        AssetDatabase.ImportAsset(filename, ImportAssetOptions.ForceUpdate);

        return newLightmap;
    }
#endif

}