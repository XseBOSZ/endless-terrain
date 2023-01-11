using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
public class MapGenerator : MonoBehaviour
{
    //Ints
    public int mapChunkSize
    {
        get
        {
            if (terrainData.useFlatShadeing)
            {
                return MeshGenerator.supportedFlatShadedChunkSizez[flatShadedChunkSizeIndex]-1;
            }
            else
            {
                return MeshGenerator.supportedChunkSizes[chunkSizeIndex]-1;
            }
        }
    }
    [Range(0,MeshGenerator.numSupportedLODs-1)]
    public int EditorLOD;
    [Range(0,MeshGenerator.numSupportedChunkSizes-1)]
    public int chunkSizeIndex;
    [Range(0, MeshGenerator.numSupportedFlatShadedChunkSizes - 1)]
    public int flatShadedChunkSizeIndex;

    //materials
    public Material terrainMaterial;

    //floats
    float[,] falloffMap;

    
    //bools
    public bool autoUpdate;

    //datas
    public NoiseData noiseData;
    public TerrainData terrainData;
    public TextureData textureData;


    //Enums
    public enum DrawMode { NoiseMap, Mesh, FalloffMap };
    public DrawMode drawMode;

    Queue<mapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<mapThreadInfo<MapData>>();
    Queue<mapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<mapThreadInfo<MeshData>>();
    private void Awake()
    {
        textureData.ApplyToMaterial(terrainMaterial);
        textureData.UpdateMeshHeights(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
    }
    void OnValuesUpdated()
    {
        textureData.ApplyToMaterial(terrainMaterial);
        if (!Application.isPlaying)
        {
            DrawMapInEditor();
        }
    }

    public void requestMapData(Vector2 Center, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(Center, callback);
        };
        new Thread(threadStart).Start();
    }
    void MapDataThread(Vector2 Center, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(Center);
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new mapThreadInfo<MapData>(callback, mapData));
        }
    }
    public void RequestMeshData( MapData mapData,int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData,lod, callback);
        };
        new Thread(threadStart).Start();
    }
    void MeshDataThread(MapData mapData,int lod, Action<MeshData> callback)
    {
        MeshData meshData = MeshGenerator.GenerateMesh(mapData.heightMap, terrainData.meshHeightMultiplier, terrainData.meshHightCurve, lod,terrainData.useFlatShadeing);
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new mapThreadInfo<MeshData>(callback, meshData));
        }
    }
    private void Update()
    {
        if(mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                mapThreadInfo<MapData> thredinfo = mapDataThreadInfoQueue.Dequeue();
                thredinfo.callback(thredinfo.parameter);
            }
        }
        if(meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                mapThreadInfo<MeshData> thredInfo = meshDataThreadInfoQueue.Dequeue();
                thredInfo.callback(thredInfo.parameter);
            }
        }
    }

    public void DrawMapInEditor()
    {
        textureData.UpdateMeshHeights(terrainMaterial, terrainData.minHeight, terrainData.maxHeight);
        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay display = FindObjectOfType<MapDisplay>();
        if (drawMode == DrawMode.NoiseMap)
        {
            display.DrawTextureMethod(TextureGeneration.TextureFromHeightMap(mapData.heightMap));
        }
        else if (drawMode == DrawMode.Mesh)
        {
            display.DrawMesh(MeshGenerator.GenerateMesh(mapData.heightMap, terrainData.meshHeightMultiplier, terrainData.meshHightCurve, EditorLOD, terrainData.useFlatShadeing));

        }else if(drawMode == DrawMode.FalloffMap)
        {
            display.DrawTextureMethod(TextureGeneration.TextureFromHeightMap(Falloff.GenerateFalloff(mapChunkSize)));
        }
    }

     MapData GenerateMapData(Vector2 center)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(noiseData.normalizedMode, mapChunkSize + 2, mapChunkSize + 2, noiseData.seed, noiseData.noiseScale, noiseData.octaves, noiseData.persistance, noiseData.lacunarity, center + noiseData.offset);

        if (terrainData.useFalloff)
        {
            if(falloffMap == null)
            {
                falloffMap = Falloff.GenerateFalloff(mapChunkSize + 2);
            }
            for (int y = 0; y < mapChunkSize+2; y++)
            {
                for (int x = 0; x < mapChunkSize+2; x++)
                {
                    if (terrainData.useFalloff)
                    {
                        noiseMap[x, y] = Mathf.Clamp(noiseMap[x, y] - falloffMap[x, y], 0, 1);
                    }

                }
            }
        }
       
        return new MapData(noiseMap);
    }
    private void OnValidate()
    {
        if (terrainData != null)
        {
            terrainData.OnValuesUpdatet -= OnValuesUpdated;
            terrainData.OnValuesUpdatet += OnValuesUpdated;
        }
        if (noiseData != null)
        {
            noiseData.OnValuesUpdatet -= OnValuesUpdated;
            noiseData.OnValuesUpdatet += OnValuesUpdated;
        }
        if (textureData != null)
        {
            textureData.OnValuesUpdatet -= OnValuesUpdated;
            textureData.OnValuesUpdatet += OnValuesUpdated;
        }
    }

    struct mapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public mapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

[System.Serializable]


public struct MapData{
    public readonly float[,] heightMap;


    public MapData(float[,] heightMap )
    {
        this.heightMap = heightMap;

    }
}