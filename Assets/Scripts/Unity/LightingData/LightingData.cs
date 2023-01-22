using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Container for scene lighting data.
/// </summary>
[CreateAssetMenu(fileName = "Lighting", menuName = "Raymap/Lighting Data", order = 1)]
public class LightingData : ScriptableObject
{
    public float luminosity;
    public float saturate;
    public List<MeshLightingData> meshLightingData;
}
