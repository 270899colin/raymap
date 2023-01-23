using OpenSpace;
using OpenSpace.Object;
using OpenSpace.Visual;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utility class for exporting a map to Unity prefab, along with textures and materials.
/// </summary>
public class PrefabExporter : MonoBehaviour
{
    // Cache saved textures (using hash) so we don't save the same texture multiple times.
    // Key is hash (gen using GetPixels), value is file name.
    private Dictionary<string, string> savedTextures = new Dictionary<string, string>();

    // Assets/Export/(time)
    private string exportDir;

    /// <summary>
    /// Export current scene, scene must be fully loaded by Raymap first.
    /// Exports the following: meshes, materials, textures, prefab.
    /// </summary>
    public void Export()
    {
        SectorManager sm = GameObject.Find("SectorManager").GetComponent<SectorManager>();
        if(sm.displayInactiveSectors == false)
        {
            Debug.LogWarning("[PrefabExporter] Display inactive sectors in SectorManager is false, some objects may not be exported.");
        }

        SetupFolders();

        // Static meshes only for now.
        var world = GameObject.Find("Father Sector");

        var renderers = world.GetComponentsInChildren<Renderer>();
        // Do not include collide submeshes.
        var meshRenderers = renderers.Where(x => x.gameObject.name.StartsWith("Submesh")).ToList();
        var meshFilters = meshRenderers.Select(x => x.gameObject.GetComponent<MeshFilter>());

        ExportMeshes(meshFilters);
        ExportMaterials(meshRenderers);
        ExportLightingData(meshRenderers);

        RemoveRaymapScripts(world);

        PrefabUtility.SaveAsPrefabAsset(world, "Assets/" + exportDir + "/Prefabs/world.prefab");
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Create necessary folders in the asset database for export.
    /// </summary>
    private void SetupFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Export"))
        {
            AssetDatabase.CreateFolder("Assets", "Export");
        }

        var time = DateTime.Now.ToString("dd-MM-yy-HH-mm");
        AssetDatabase.CreateFolder("Assets/Export", time);
        exportDir = "Export/" + time;

        AssetDatabase.CreateFolder("Assets/" + exportDir, "Meshes");
        AssetDatabase.CreateFolder("Assets/" + exportDir, "Textures");
        AssetDatabase.CreateFolder("Assets/" + exportDir, "Materials");
        AssetDatabase.CreateFolder("Assets/" + exportDir, "Prefabs");
    }

    /// <summary>
    /// Export all meshes in the mesh filters to the Meshes folder.
    /// </summary>
    /// <param name="filters">MeshFilters of all meshes in the scene.</param>
    private void ExportMeshes(IEnumerable<MeshFilter> filters)
    {
        foreach (var mf in filters)
        {
            var mesh = mf.mesh;
            var saveName = GetMeshFriendlyName(mf.gameObject.name) + ".asset";
            AssetDatabase.CreateAsset(mesh, "Assets/" + exportDir + "/Meshes/" + saveName);
            AssetDatabase.Refresh();

            // Fix collision
            var mc = mf.GetComponent<MeshCollider>();
            if(mc != null)
            {
                mc.sharedMesh = mesh;
            }
        }
    }

    /// <summary>
    /// Export all materials in the (mesh) renderers to the Materials folder.
    /// </summary>
    /// <param name="renderers">MeshRenderers of all meshes in the scene.</param>
    private void ExportMaterials(IEnumerable<Renderer> renderers)
    {
        foreach (var mr in renderers)
        {
            var material = new Material(mr.sharedMaterial);

            ExportTextures(material);

            var saveName = GetMeshFriendlyName(mr.gameObject.name) + ".mat";

            AssetDatabase.CreateAsset(material, "Assets/" + exportDir + "/Materials/" + saveName);
            AssetDatabase.Refresh();

            mr.sharedMaterial = material;
        }
    }

    /// <summary>
    /// Export lighting data of each mesh.
    /// Data is retrieved from the MaterialPropertyBlock of each renderer.
    /// </summary>
    /// <param name="renderers">MeshRenderers of all meshes in the scene.</param>
    private void ExportLightingData(IEnumerable<Renderer> renderers)
    {
        List<MeshLightingData> mld = new List<MeshLightingData>();

        foreach (var mr in renderers)
        {
            MeshLightingData data = new MeshLightingData();

            data.objName = mr.gameObject.name;
            var mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);

            data.staticLightCount = mpb.GetFloat("_StaticLightCount");

            if (data.staticLightCount > 0)
            {
                data.staticLightPos = new StaticLightPos();
                data.staticLightDir = new StaticLightDir();
                data.staticLightCol = new StaticLightCol();
                data.staticLightParams = new StaticLightParams();

                data.staticLightPos.staticLightPos = mpb.GetVectorArray("_StaticLightPos");
                data.staticLightDir.staticLightDir = mpb.GetVectorArray("_StaticLightDir");
                data.staticLightCol.staticLightCol = mpb.GetVectorArray("_StaticLightCol");
                data.staticLightParams.staticLightParams = mpb.GetVectorArray("_StaticLightParams");
            }

            mld.Add(data);
        }
        
        var lightingData = ScriptableObject.CreateInstance<LightingData>();
        
        lightingData.luminosity = Shader.GetGlobalFloat("_Luminosity");
        lightingData.saturate = Shader.GetGlobalFloat("_Saturate");
        lightingData.meshLightingData = mld;

        AssetDatabase.CreateAsset(lightingData, "Assets/" + exportDir + "/Prefabs/" + "Lighting.asset");
    }

    /// <summary>
    /// Export all textures of a material to the textures folder.
    /// The hash of each exported texture is cached, if a texture is already exported, assign cached texture.
    /// </summary>
    /// <param name="mat">Material to export textures from.</param>
    private void ExportTextures(Material mat)
    {
        var textures = new Dictionary<string, Texture>();

        var tex0 = mat.GetTexture("_Tex0");
        if(tex0 != null)
        {
            textures.Add("_Tex0", tex0);
        }

        var tex1 = mat.GetTexture("_Tex1");
        if (tex1 != null)
        {
            textures.Add("_Tex1", tex1);
        }

        var tex2 = mat.GetTexture("_Tex2");
        if (tex2 != null)
        {
            textures.Add("_Tex2", tex2);
        }

        var tex3 = mat.GetTexture("_Tex3");
        if (tex3 != null)
        {
            textures.Add("_Tex3", tex3);
        }

        foreach (var texture in textures)
        {
            var tx = (Texture2D)texture.Value;

            // tx.imageContentsHash is null so can't be used here.
            // Not great performance, but should be fine with Rayman's small texture size.
            var hash = new Hash128();
            hash.Append(tx.GetPixels());

            string fileName;

            if (!savedTextures.ContainsKey(hash.ToString()))
            {
                // Save texture
                fileName = "Texture" + savedTextures.Count() + ".png";
                var savePath = Application.dataPath + "/" + exportDir + "/Textures/" + fileName;
                byte[] bytes = tx.EncodeToPNG();
                // AssetDatabase.CreateAsset can't be used for textures.
                File.WriteAllBytes(savePath, bytes);
                AssetDatabase.Refresh();
                savedTextures.Add(hash.ToString(), fileName);
            } else
            {
                // Texture already saved, use saved texture.
                fileName = savedTextures[hash.ToString()];
            }
            // Assign exported texture to mat.
            var savedTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/" + exportDir + "/Textures/" + fileName, typeof(Texture2D));
            mat.SetTexture(texture.Key, savedTexture);
        }
    }

    /// <summary>
    /// Removes Raymap scripts in all child objects.
    /// </summary>
    /// <param name="world">World GameObject.</param>
    private void RemoveRaymapScripts(GameObject world)
    {
        // Unity heavily recommends NOT using DestroyImmediate but Destroy does not seem
        // to persist when saving the prefab for some reason.
        DestroyImmediate(world.GetComponent<SuperObjectComponent>());
        DestroyImmediate(world.GetComponent<Moddable>());

        RemoveComponentsInChildren<ExportableModel>(world);
        RemoveComponentsInChildren<SuperObjectComponent>(world);
        RemoveComponentsInChildren<Moddable>(world);
        RemoveComponentsInChildren<SectorComponent>(world);
        RemoveComponentsInChildren<BillboardBehaviour>(world);
        RemoveComponentsInChildren<CollideComponent>(world);
    }

    /// <summary>
    /// Remove all instances of a component in each child object.
    /// </summary>
    /// <typeparam name="T">Component to remove.</typeparam>
    /// <param name="parent">Parent object.</param>
    private void RemoveComponentsInChildren<T>(GameObject parent)
    {
        var components = parent.GetComponentsInChildren<T>();
        foreach (var comp in components)
        {
            DestroyImmediate(comp as Component);
        }
    }

    /// <summary>
    /// Shortens name of submesh object names.
    /// </summary>
    /// <param name="objName">Name of submesh object.</param>
    /// <returns>Shortened name.</returns>
    private string GetMeshFriendlyName(string objName)
    {
        return objName.Substring(objName.LastIndexOf('|') + 1);
    }
}