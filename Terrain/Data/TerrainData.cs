using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName ="TerrainData_",menuName = "Scriptable/Terrain/TerrainData")]
public class TerrainData : UpdatableData
{
    public AnimationCurve meshHightCurve;
    public float meshHeightMultiplier;
    public bool useFalloff;
    public bool useFlatShadeing;
    public float uniformScale = 5f;

    public float minHeight
    {
        get
        {
            return uniformScale * meshHeightMultiplier * meshHightCurve.Evaluate(0);
        }
    }
    public float maxHeight
    {
        get
        {
            return uniformScale * meshHeightMultiplier * meshHightCurve.Evaluate(1);
        }
    }
}
