using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class RestoreLighting : MonoBehaviour
{
    public LightingData lightingData;

    private void Awake()
    {
        // If null, script probably just got added by exporter.
        // No need to restore lighting.
        if(lightingData != null)
        {
            RestoreLightingRuntime();
        }
    }

    /// <summary>
    /// Restores lighting data on each mesh.
    /// Data is set using MaterialPropertyBlocks.
    /// </summary>
    public void RestoreLightingRuntime()
    {
        Shader.SetGlobalFloat("_Luminosity", lightingData.luminosity);
        Shader.SetGlobalFloat("_Saturate", lightingData.saturate);

        foreach (var mld in lightingData.meshLightingData)
        {
            var obj = GameObject.Find(mld.objName);
            if(obj != null)
            {
                var mr = obj.GetComponent<MeshRenderer>();

                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                mr.GetPropertyBlock(mpb);

                mpb.SetFloat("_StaticLightCount", mld.staticLightCount);
                if(mld.staticLightCount > 0)
                {
                    mpb.SetVectorArray("_StaticLightPos", mld.staticLightPos.staticLightPos);
                    mpb.SetVectorArray("_StaticLightDir", mld.staticLightDir.staticLightDir);
                    mpb.SetVectorArray("_StaticLightCol", mld.staticLightCol.staticLightCol);
                    mpb.SetVectorArray("_StaticLightParams", mld.staticLightParams.staticLightParams);
                }

                mr.SetPropertyBlock(mpb);
            } else
            {
                Debug.LogWarning("Object not found, unable to restore ligthing: " + mld.objName);
            }
        }
    }

    #if UNITY_EDITOR
    /// <summary>
    /// Convenient menu item to restore lighting in editor.
    /// </summary>
    [MenuItem("Raymap/Restore Lighting")]
    public static void RestoreLightingEditor()
    {
        var ld = GameObject.Find("LightingData");
        if(ld == null)
        {
            Debug.LogError("LightingData object could not be found!");
        } else
        {
            ld.GetComponent<RestoreLighting>().RestoreLightingRuntime();
        }
    }
    #endif
}
