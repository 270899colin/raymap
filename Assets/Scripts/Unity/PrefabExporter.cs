using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utility class for exporting a map to Unity prefab, along with textures, materials and lighting data.
/// </summary>
public class PrefabExporter : MonoBehaviour
{
    /// <summary>
    /// Dictionary of object sets to export.
    /// An object set is defined as a list of objects with the same identifier/obj name (they are duplicates).
    /// They contain the same mesh/texture data, but may contain different lighting data (MPB).
    /// Key is name of the original object.
    /// </summary>
    private Dictionary<string, List<Transform>> objectsToExport = new Dictionary<string, List<Transform>>();

    /// <summary>
    /// Cache saved textures (using hash) so we don't save the same texture multiple times.
    /// Key is hash (gen using GetPixels), value is file name.
    /// </summary>
    private Dictionary<string, string> savedTextures = new Dictionary<string, string>();

    /// <summary>
    /// Directory to export to, relative to Assets directory, set in SetupFolders().
    /// Default: Export/(time) -> Assets/Export/(time)
    /// </summary>
    private string exportDir;

    /// <summary>
    /// Export current scene, scene must be fully loaded by Raymap first.
    /// Exports the following: meshes, materials, textures, prefab, lighting data.
    /// </summary>
    public void Export()
    {
        SectorManager sm = GameObject.Find("SectorManager").GetComponent<SectorManager>();
        if(sm.displayInactiveSectors == false)
        {
            Debug.LogWarning("[PrefabExporter] Display inactive sectors in SectorManager is false, some objects may not be exported.");
        }

        SetupFolders();

        GameObject world;

        if(UnitySettings.ExportStaticMeshesOnly)
        {
            // Only export static meshes (terrain).
            world = GameObject.Find("Father Sector");
        } else
        {
            // Also include dynamic objects (lums, enemies, etc).
            world = GameObject.Find("Actual World");
        } 

        // We only need the objects containing the mesh & material data.
        var objects = world.GetComponentsInChildren<Transform>().Where(x => x.name.StartsWith("Submesh"));

        // Sort objects into object sets.
        foreach (var obj in objects)
        {
            var objName = obj.name;
            if(objectsToExport.ContainsKey(objName))
            {
                // Rename duplicates to avoid issues with restoring lighting.
                obj.name = obj.name + "_" + objectsToExport[objName].Count;
                objectsToExport[objName].Add(obj);
            } else
            {
                objectsToExport.Add(objName, new List<Transform> {obj});
            }
        }

        List<MeshLightingData> mld = new List<MeshLightingData>();

        // Export data from each object set.
        foreach (var pair in objectsToExport)
        {
            var objSet = pair.Value;

            ExportMesh(objSet);
            ExportMaterial(objSet);
            mld.AddRange(ExportMeshLightingData(objSet));          
        }

        SaveLightingData(mld, world.transform);

        if(UnitySettings.RemoveRaymapScripts)
        {
            RemoveRaymapScripts(world);
        }

        PrefabUtility.SaveAsPrefabAsset(world, "Assets/" + exportDir + "/Prefabs/" + UnitySettings.MapName + ".prefab");
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
    /// Export mesh of an object set to the Meshes folder.
    /// </summary>
    /// <param name="objSet">Object set to export.</param>
    private void ExportMesh(List<Transform> objSet)
    {
        var toExport = objSet.First();
        var isSkinnedMesh = IsSkinnedMesh(toExport);

        Mesh mesh;

        if(!isSkinnedMesh)
        {
            var mf = toExport.GetComponent<MeshFilter>();
            mesh = mf.mesh;
        } else
        {
            var smr = toExport.GetComponent<SkinnedMeshRenderer>();
            mesh = smr.sharedMesh;
        }

        var saveName = GetMeshFriendlyName(toExport.name) + ".asset";
        AssetDatabase.CreateAsset(mesh, "Assets/" + exportDir + "/Meshes/" + saveName);
        AssetDatabase.Refresh();

        // Set mesh & collider for all duplicates
        foreach (var obj in objSet)
        {
            if(!isSkinnedMesh)
            {
                var filter = obj.GetComponent<MeshFilter>();
                filter.mesh = mesh;
                var col = GetComponent<MeshCollider>();
                if (col != null) { col.sharedMesh = mesh; }
            } else
            {
                var smr = obj.GetComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
            }           
        }
    }

    /// <summary>
    /// Export material of an object set to the Materials folder.
    /// </summary>
    /// <param name="objSet">Object set to export.</param>
    private void ExportMaterial(List<Transform> objSet)
    {
        var toExport = objSet.First();
        var isSkinnedMesh = IsSkinnedMesh(toExport);

        Material material;
        if (!isSkinnedMesh)
        {
            var mr = toExport.GetComponent<MeshRenderer>();
            material = new Material(mr.sharedMaterial);
        } else
        {
            var smr = toExport.GetComponent<SkinnedMeshRenderer>();
            material = new Material(smr.sharedMaterial);
        }

        ExportTextures(material);

        var saveName = GetMeshFriendlyName(toExport.name) + ".mat";

        AssetDatabase.CreateAsset(material, "Assets/" + exportDir + "/Materials/" + saveName);
        AssetDatabase.Refresh();

        // Set saved material on duplicates.
        foreach (var obj in objSet)
        {
            if(!isSkinnedMesh)
            {
                obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            } else
            {
                obj.GetComponent<SkinnedMeshRenderer>().sharedMaterial = material;
            }          
        }
    }

    /// <summary>
    /// Export lighting data of each object in an object set.
    /// Data is retrieved from the MaterialPropertyBlock of each renderer.
    /// </summary>
    /// <param name="objSet">Objects to export.</param>
    /// <returns>List of mesh lighting data.</returns>
    private List<MeshLightingData> ExportMeshLightingData(List<Transform> objSet)
    {
        List<MeshLightingData> mld = new List<MeshLightingData>();

        var isSkinnedMesh = IsSkinnedMesh(objSet.First());

        foreach (var obj in objSet)
        {
            MeshLightingData data = new MeshLightingData();

            data.objName = obj.name;
            data.skinned = isSkinnedMesh;

            var mpb = new MaterialPropertyBlock();

            if (!isSkinnedMesh)
            {
                var mr = obj.GetComponent<MeshRenderer>();
                mr.GetPropertyBlock(mpb);
            } else
            {
                var smr = obj.GetComponent<SkinnedMeshRenderer>();
                smr.GetPropertyBlock(mpb);
            }
                   
            // Lights data.
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

            // Fog data.
            data.sectorFog = mpb.GetVector("_SectorFog");
            data.sectorFogParams = mpb.GetVector("_SectorFogParams");

            mld.Add(data);
        }

        return mld;
    }

    /// <summary>
    /// Saves the lighting data to a ScriptableObject and JSON.
    /// Also adds a script to the world object to restore lighting at runtime and in editor.
    /// </summary>
    /// <param name="mld">Lighting data of all meshes.</param>
    /// <param name="world">World root object.</param>
    private void SaveLightingData(List<MeshLightingData> mld, Transform world)
    {
        var lightingData = ScriptableObject.CreateInstance<LightingData>();

        lightingData.luminosity = Shader.GetGlobalFloat("_Luminosity");
        lightingData.saturate = Shader.GetGlobalFloat("_Saturate");
        lightingData.meshLightingData = mld;

        // Save ScriptableObject.
        AssetDatabase.CreateAsset(lightingData, "Assets/" + exportDir + "/Prefabs/" + "Lighting.asset");
        AssetDatabase.Refresh();

        var ldo = new GameObject("LightingData");
        ldo.transform.parent = world.transform;

        // Script for restoring lighting.
        var ldscript = ldo.AddComponent<RestoreLighting>();
        ldscript.lightingData = lightingData;

        // Also save to JSON.
        var jsonPath = Application.dataPath + "/" + exportDir + "/Prefabs/Lighting.json";
        var json = JsonUtility.ToJson(lightingData);
        File.WriteAllText(jsonPath, json);
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
        RemoveComponentsInChildren<PortalBehaviour>(world);
        RemoveComponentsInChildren<PersoBehaviour>(world);
        RemoveComponentsInChildren<BrainComponent>(world);
        RemoveComponentsInChildren<DsgVarComponent>(world);
        RemoveComponentsInChildren<DynamicsMechanicsComponent>(world);
        RemoveComponentsInChildren<MindComponent>(world);
        RemoveComponentsInChildren<CustomBitsComponent>(world);
        RemoveComponentsInChildren<ScriptComponent>(world);

        // TODO: Investigate these later
        RemoveComponentsInChildren<MultiTextureMaterial>(world);
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

    /// <summary>
    /// Checks if an object has a skinned mesh renderer.
    /// </summary>
    /// <param name="obj">Object to check.</param>
    /// <returns>True if skinned mesh renderer is present.</returns>
    private bool IsSkinnedMesh(Transform obj)
    {
        return obj.GetComponent<SkinnedMeshRenderer>() != null;
    }
}