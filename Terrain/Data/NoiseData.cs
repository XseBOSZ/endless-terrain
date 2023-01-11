using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "NoiseData_",menuName = "Scriptable/Terrain/NoiseData")]
public class NoiseData : UpdatableData
{
    public float noiseScale;
    public int octaves;
    public int seed;
    [Range(0, 1)]
    public float persistance;
    public float lacunarity;
    public Vector2 offset;
    public Noise.NormalizeMode normalizedMode;

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        if (lacunarity < 1)
        {
            lacunarity = 1;
        }
        if (octaves < 0)
        {
            octaves = 0;
        }

        base.OnValidate();
    }
#endif
}
