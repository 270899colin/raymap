using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mesh lighting data, located in the MaterialPropertyBlock in the mesh renderer.
/// </summary>
[System.Serializable]
public class MeshLightingData
{
    public string objName;
    public bool skinned;
    public float staticLightCount;
    // Need container objects to be serializable.
    public StaticLightPos staticLightPos;
    public StaticLightDir staticLightDir;
    public StaticLightCol staticLightCol;
    public StaticLightParams staticLightParams;
    public Vector4 sectorFog;
    public Vector4 sectorFogParams;
}
