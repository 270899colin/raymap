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

public class PrefabExporter
{
    // Cache saved textures (using hash) so we don't save the same texture multiple times.
    private Dictionary<string, string> savedTextures = new Dictionary<string, string>();

    public void Export()
    {
        var world = GameObject.Find("Father Sector");

        var renderers = world.GetComponentsInChildren<Renderer>();
        var meshRenderers = renderers.Where(x => x.gameObject.name.StartsWith("Submesh")).ToList();
        var meshFilters = meshRenderers.Select(x => x.gameObject.GetComponent<MeshFilter>());

        ExportMeshes(meshFilters);
        ExportMaterials(meshRenderers);

        PrefabUtility.SaveAsPrefabAsset(world, "Assets/Export/Prefabs/world.prefab");
        AssetDatabase.Refresh();
    }

    private void ExportMeshes(IEnumerable<MeshFilter> filters)
    {
        foreach (var mf in filters)
        {
            var mesh = mf.mesh;
            var saveName = GetMeshFriendlyName(mf.gameObject.name) + ".asset";
            AssetDatabase.CreateAsset(mesh, "Assets/Export/Meshes/" + saveName);
            AssetDatabase.Refresh();
        }
    }

    private void ExportMaterials(IEnumerable<Renderer> renderers)
    {
        foreach (var mr in renderers)
        {
            var material = new Material(mr.sharedMaterial);

            ExportTextures(material);

            var saveName = GetMeshFriendlyName(mr.gameObject.name) + ".mat";

            AssetDatabase.CreateAsset(material, "Assets/Export/Materials/" + saveName);
            AssetDatabase.Refresh();

            mr.sharedMaterial = material;
        }

        // Save MPB
    }

    private void ExportTextures(Material mat)
    {
        var textures = new Dictionary<string, Texture>();
        // var numTextures = mat.GetFloat("_NumTextures");

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

            // Texture.imageContentsHash is null so can't be used here.
            // Not great performance, but should be fine with rayman's small texture size.
            var hash = new Hash128();
            hash.Append(tx.GetPixels());

            if (!savedTextures.ContainsKey(hash.ToString()))
            {
                // Save texture
                var fileName = "Texture" + savedTextures.Count() + ".png";
                var savePath = Application.dataPath + "/Export/Textures/" + fileName;
                byte[] bytes = tx.EncodeToPNG();
                File.WriteAllBytes(savePath, bytes);
                AssetDatabase.Refresh();
                savedTextures.Add(hash.ToString(), fileName);
            } else
            {
                // Texture already saved, set ref to already saved instance.
                var fileName = savedTextures[hash.ToString()];
                var savedTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/Export/Textures/" + fileName, typeof(Texture2D));
                mat.SetTexture(texture.Key, savedTexture);
            }
        }
    }

    private string GetMeshFriendlyName(string objName)
    {
        string pattern = @"\[(.+?)\]";
        Regex regex = new Regex(pattern);
        var matches = regex.Match(objName);
        return matches.Groups[1].Value;
    }
}
