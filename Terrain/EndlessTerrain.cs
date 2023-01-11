using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessTerrain : MonoBehaviour
{

    public int colliderLODindex;
    const float vieverMoveTresholdForChunkUpdate = 25f;
    const float sqrvieverMoveTresholdForChunkUpdate = vieverMoveTresholdForChunkUpdate * vieverMoveTresholdForChunkUpdate;
    const float colliderGenerationDistanceTreshold = 5f;
    public static float maxViewDistance;
    public Transform viewer;
    static MapGenerator mapGenerator;
    public static Vector2 viewerPosition;
    Vector2 viewerPositionOld;
    public int chunkSize;
    int chunksVisibleInViewDistance;
    public Material mapMaterial;

    public LODInfo[] detailLevels;
   

    Dictionary<Vector2, terrainChunk> terrainChunkDictionary = new Dictionary<Vector2, terrainChunk>();
    static List<terrainChunk> visibleTerrainChunks = new List<terrainChunk>();
    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();
        maxViewDistance = detailLevels[detailLevels.Length-1].visibleDistanceTreshold;
        chunkSize = mapGenerator.mapChunkSize - 1;
        chunksVisibleInViewDistance = Mathf.RoundToInt(maxViewDistance / chunkSize);
        UpdateVisibleChunks();
    }
    private void Update()
    {
        if (viewerPosition != viewerPositionOld)
        {
            foreach(terrainChunk chunk in visibleTerrainChunks)
            {
                chunk.UpdateCollMesh();
            }
        }

        viewerPosition = new Vector2(viewer.position.x, viewer.position.z) / mapGenerator.terrainData.uniformScale;
        if ((viewerPositionOld - viewerPosition).sqrMagnitude > sqrvieverMoveTresholdForChunkUpdate)
        {
            viewerPositionOld = viewerPosition;
            UpdateVisibleChunks();
        }
    }

    void UpdateVisibleChunks()
    {
        HashSet<Vector2> alreadyUpdatedChunkCoords = new HashSet<Vector2>();
        for (int i = visibleTerrainChunks.Count - 1; i >= 0; i--)
        {
            alreadyUpdatedChunkCoords.Add(visibleTerrainChunks[i].coord);
            visibleTerrainChunks[i].UpdateTerrChunk();
        }

        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordY = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksVisibleInViewDistance; yOffset <= chunksVisibleInViewDistance; yOffset++)
        {
            for (int XOffset = -chunksVisibleInViewDistance; XOffset <= chunksVisibleInViewDistance; XOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + XOffset, currentChunkCoordY + yOffset);
                if (!alreadyUpdatedChunkCoords.Contains(viewedChunkCoord))
                {
                    if (terrainChunkDictionary.ContainsKey(viewedChunkCoord))
                    {
                        terrainChunkDictionary[viewedChunkCoord].UpdateTerrChunk();

                    }
                    else
                    {
                        terrainChunkDictionary.Add(viewedChunkCoord, new terrainChunk(viewedChunkCoord, chunkSize, detailLevels, colliderLODindex, transform, mapMaterial));
                    }
                }
            }
        }
    }
    public class terrainChunk
    {
        public Vector2 coord;

        GameObject meshObject;
        Vector2 position;
        Bounds bounds;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        MeshCollider meshCollider;

        LODInfo[] detailLevels;
        LODMesh[] lodMeshes;
        int colliderLODIndex;
        MapData mapData;
        bool mapDataRecived;
        int prevLODIndex = -1;
        bool hasSetCollider;
        public terrainChunk(Vector2 coord, int size,LODInfo[] detailLevels,int colliderLODIndex, Transform parent, Material material)
        {
            lodMeshes = new LODMesh[detailLevels.Length];
            for (int i = 0; i < detailLevels.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod);
                lodMeshes[i].UpdateCallback += UpdateTerrChunk;
                if (i == colliderLODIndex)
                {
                    lodMeshes[i].UpdateCallback += UpdateCollMesh;
                }
                
            }
            this.coord = coord;
            this.colliderLODIndex = colliderLODIndex;
            this.detailLevels = detailLevels;
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 posV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("terrain chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshObject.transform.position = posV3 * mapGenerator.terrainData.uniformScale;
            meshObject.transform.localScale = Vector3.one * mapGenerator.terrainData.uniformScale;
            meshRenderer.material = material;
            meshCollider = meshObject.AddComponent<MeshCollider>();
            meshObject.transform.parent = parent;
            SetVisible(false);

            mapGenerator.requestMapData(position,OnMapDataRecived);
        }
        void OnMapDataRecived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataRecived = true;
            UpdateTerrChunk();
        }
      
        public void UpdateTerrChunk()
        {
            if (mapDataRecived)
            {
                float viewerDstFromNearestEdge = Mathf.Sqrt(bounds.SqrDistance(viewerPosition));
                bool visible = viewerDstFromNearestEdge <= maxViewDistance;
                bool wasVisible = isVisible();
                if (visible)
                {
                    int lodIndex = 0;
                    for (int i = 0; i < detailLevels.Length - 1; i++)
                    {
                        if (viewerDstFromNearestEdge > detailLevels[i].visibleDistanceTreshold)
                        {
                            lodIndex = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (lodIndex != prevLODIndex)
                    {
                        LODMesh lodMesh = lodMeshes[lodIndex];
                        if (lodMesh.hasMesh)
                        {
                            prevLODIndex = lodIndex;
                            meshFilter.mesh = lodMesh.mesh;
                        }
                        else if (!lodMesh.hasRequestedMesh)
                        {
                            lodMesh.RequestMesh(mapData);
                        }
                    }
                    
                    
                }
                if (wasVisible != isVisible())
                {
                    if (visible)
                    {
                        visibleTerrainChunks.Add(this);
                    }
                    else
                    {
                        visibleTerrainChunks.Remove(this);
                    }

                }
                SetVisible(visible);
            }
        }

        public void UpdateCollMesh()
        {
            if (!hasSetCollider)
            {
                float sqrDistanceFromViewToEdge = bounds.SqrDistance(viewerPosition);

                if (sqrDistanceFromViewToEdge < detailLevels[colliderLODIndex].sqrVisibleDistanceTreshold)
                {
                    if (!lodMeshes[colliderLODIndex].hasRequestedMesh)
                    {
                        lodMeshes[colliderLODIndex].RequestMesh(mapData);
                    }
                }

                if (sqrDistanceFromViewToEdge < colliderGenerationDistanceTreshold * colliderGenerationDistanceTreshold)
                {
                    if (lodMeshes[colliderLODIndex].hasMesh)
                    {
                        meshCollider.sharedMesh = lodMeshes[colliderLODIndex].mesh;
                        hasSetCollider = true;
                    }

                }
            }
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }
        public bool isVisible()
        {
            return meshObject.activeSelf;
        }
    }

    class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        int lod;
        public event System.Action UpdateCallback;
        public LODMesh(int lod)
        {
            this.lod = lod;
        }
        void OnMeshDataRecived(MeshData meshdata)
        {
            mesh = meshdata.createMesh();
            hasMesh = true;
            UpdateCallback();
        }
        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataRecived);
        }
    }
    [System.Serializable]
    public struct LODInfo
    {
        [Range(0,MeshGenerator.numSupportedLODs-1)]
        public int lod;
        public float visibleDistanceTreshold;
        public float sqrVisibleDistanceTreshold
        {
            get
            {
                return visibleDistanceTreshold * visibleDistanceTreshold;
            }
        }
    }
}
