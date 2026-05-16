using System;
using UnityEditor;
using UnityEngine;
using Unity.Collections;

[CustomEditor(typeof(TerrainGeneration))]
[CanEditMultipleObjects]
public class TerrainGenerationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        TerrainGeneration terrain = (TerrainGeneration)target;
        if (GUILayout.Button("Reload")) terrain.ReloadTerrain();
    }
}

public class TerrainGeneration : MonoBehaviour
{
    [SerializeField] [Min(.5f)] private float terrainSize;
    [SerializeField] [Min(1)] private int chunkCount;
    [SerializeField] [Min(0)] private int globalResolution;
    [SerializeField] private Material material;
    [SerializeField] private Texture2D heightMap;
    [SerializeField] private float globalHeightMultiplier;
    [SerializeField] [Range(0, 10)] private int smoothRange;
    [SerializeField] [Range(0, 10)] private int smoothCount;
    private int _previousChunkCount;
    private Texture2D _smoothedHeightMap;

    public int GlobalResolution => globalResolution;

    public void ReloadTerrain()
    {
        _previousChunkCount = (int)Mathf.Sqrt(transform.childCount);

        if (_smoothedHeightMap)
        {
            DestroyImmediate(_smoothedHeightMap);
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        _smoothedHeightMap = SmoothHeightmap();
        UpdateTerrain();
        GenerateTerrain();

        if (chunkCount < _previousChunkCount)
            TrimTerrain();
    }

    private Texture2D RescaledTexture(Texture2D texture, int newSize)
    {
        RenderTexture rt = RenderTexture.GetTemporary(newSize, newSize, 0, RenderTextureFormat.RFloat);
        Graphics.Blit(texture, rt);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D rescaled = new(newSize, newSize, texture.format, false);
        rescaled.ReadPixels(new Rect(0, 0, newSize, newSize), 0, 0);
        rescaled.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        return rescaled;
    }

    private Texture2D SmoothHeightmap()
    {
        int newMapSize = chunkCount * (globalResolution + 2);
        Texture2D newMap = RescaledTexture(heightMap, newMapSize);

        if (smoothRange == 0 || smoothCount == 0)
            return newMap;

        Debug.Assert(newMap.format == TextureFormat.RFloat);
        Span<float> originalPixels = newMap.GetPixelData<float>(0).AsSpan();
        float[] smoothedPixels = new float[originalPixels.Length];

        int smoothPixelsSize = (smoothRange + 1) * 2 - 1;
        Vector2Int[] offsets = new Vector2Int[smoothPixelsSize * smoothPixelsSize];

        int index = 0;
        for (int x = -smoothRange; x <= smoothRange; x++)
        {
            for (int y = -smoothRange; y <= smoothRange; y++)
            {
                offsets[index++] = new Vector2Int(x, y);
            }
        }

        for (int i = 0; i < smoothCount; i++)
        {
            for (int x = 0; x < newMapSize; x++)
            {
                for (int y = 0; y < newMapSize; y++)
                {
                    float r = 0;
                    int divider = 0;

                    foreach (Vector2Int offset in offsets)
                    {
                        int sampleX = x + offset.x;
                        int sampleY = y + offset.y;

                        if (sampleX >= 0 && sampleX < newMapSize && sampleY >= 0 && sampleY < newMapSize)
                        {
                            r += originalPixels[sampleX + sampleY * newMapSize];
                            divider++;
                        }
                    }

                    smoothedPixels[x + y * newMapSize] = r / divider;
                }
            }

            originalPixels = smoothedPixels.AsSpan();
        }

        newMap.SetPixelData(smoothedPixels, 0);
        newMap.Apply();
        return newMap;
    }

    private ChunkData GetChunkData(int x, int y, float size)
    {
        return new ChunkData
        {
            globalResolution = globalResolution,
            gridPos = new Vector2Int(x, y),
            uvStart = new Vector2(x / (float)chunkCount, y / (float)chunkCount),
            uvSize = new Vector2(1f / chunkCount, 1f / chunkCount),
            size = size,
            material = material,
            sharedHeights = _smoothedHeightMap,
            heightMultiplier = globalHeightMultiplier,
            smoothRange = smoothRange
        };
    }

    private void CreateChunk(int x, int y, float size)
    {
        GameObject newChild = new($"Chunk ({x}, {y})");
        newChild.transform.SetParent(transform, false);
        newChild.transform.SetSiblingIndex(Utility.GetIndexFromGridPos(x, y, chunkCount));
        newChild.AddComponent<TerrainChunk>().Init(GetChunkData(x, y, size));
    }

    private void UpdateChunk(int x, int y, float size)
    {
        int index = Utility.GetIndexFromGridPos(x, y, _previousChunkCount);
        transform.GetChild(index).GetComponent<TerrainChunk>().Init(GetChunkData(x, y, size));
    }

    private void UpdateTerrain()
    {
        float chunkSize = terrainSize / chunkCount;

        for (int x = 0; x < chunkCount; x++)
        for (int y = 0; y < chunkCount; y++)
            if (x < _previousChunkCount && y < _previousChunkCount)
                UpdateChunk(x, y, chunkSize);
    }

    private void GenerateTerrain()
    {
        float chunkSize = terrainSize / chunkCount;

        for (int x = 0; x < chunkCount; x++)
        for (int y = 0; y < chunkCount; y++)
            if (x >= _previousChunkCount || y >= _previousChunkCount)
                CreateChunk(x, y, chunkSize);
    }

    private void TrimTerrain()
    {
        if (_previousChunkCount <= chunkCount)
            return;

        for (int x = _previousChunkCount - 1; x >= 0; --x)
        for (int y = _previousChunkCount - 1; y >= 0; --y)
            if (x >= chunkCount || y >= chunkCount)
                DestroyImmediate(transform.GetChild(Utility.GetIndexFromGridPos(x, y, _previousChunkCount)).gameObject);
    }
}