using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
//
// [CustomEditor(typeof(TerrainChunk)), CanEditMultipleObjects]
// public class TerrainChunkEditor : Editor
// {
//     public override void OnInspectorGUI()
//     {
//         DrawDefaultInspector();
//         if (GUILayout.Button("Reload"))
//         {
//             foreach (Object terrain in targets)
//             {
//                 ((TerrainChunk)terrain).ReloadChunk();
//             }
//         }
//     }
// }

public struct ChunkData
{
    public int globalResolution;
    public int smoothRange;
    public Vector2Int gridPos;
    public Vector2 uvStart;
    public Vector2 uvSize;
    public float size;
    public Material material;
    public Texture2D sharedHeights;
    public float heightMultiplier;
}

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TerrainChunk : MonoBehaviour
{
    private Vector3[] _vertices;
    private Vector2[] _uvs;
    private int[] _indices;

    private Transform _transform;
    private Mesh _mesh;
    private ChunkData _chunkData;

    public void Init(ChunkData chunkData)
    {
        Debug.Assert(chunkData.sharedHeights.isReadable);

        _chunkData = chunkData;
        _transform = transform;
        _transform.position =
            new Vector3(_chunkData.gridPos.x * _chunkData.size, 0, _chunkData.gridPos.y * _chunkData.size);

        GetComponent<MeshRenderer>().sharedMaterial = _chunkData.material;
        ReloadChunk();
    }

    public void ReloadChunk()
    {
        if (_mesh)
            _mesh.Clear();

        _mesh = new Mesh();
        PlaneGeneration(_chunkData.globalResolution + 2);
        GetComponent<MeshFilter>().mesh = _mesh;
    }

    private void PlaneGeneration(int resolution)
    {
        int vertexCount = resolution * resolution;

        if (_vertices == null || _vertices.Length != vertexCount)
        {
            _vertices = new Vector3[vertexCount];
            _uvs = new Vector2[vertexCount];
            _indices = new int[(vertexCount - 1) * 6];
        }

        Vector2Int texOffset = _chunkData.gridPos * (resolution - 1);
        Color[] levels =
            _chunkData.sharedHeights.GetPixels(texOffset.x, texOffset.y, resolution, resolution);

        int curIndex = 0;
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                Vector2 delta = new Vector2(x / (resolution - 1f), y / (resolution - 1f));
                Vector2 uvPos = _chunkData.uvStart + delta * _chunkData.uvSize;
                Color level = levels[x + y * resolution];
                float height = level.r * _chunkData.heightMultiplier;
                _vertices[curIndex] = new Vector3(_chunkData.size * delta.x, height, _chunkData.size * delta.y);
                _uvs[curIndex++] = uvPos;
            }
        }

        curIndex = 0;
        for (int column = 0; column < resolution - 1; column++)
        {
            for (int row = 0; row < resolution - 1; row++)
            {
                int i = Utility.GetIndexFromGridPos(column, row, resolution);
                _indices[curIndex++] = i;
                _indices[curIndex++] = i + 1;
                _indices[curIndex++] = i + resolution;

                _indices[curIndex++] = i + 1;
                _indices[curIndex++] = i + resolution + 1;
                _indices[curIndex++] = i + resolution;
            }
        }

        _mesh.indexFormat = IndexFormat.UInt32;
        _mesh.vertices = _vertices;
        _mesh.uv = _uvs;
        _mesh.triangles = _indices;
        _mesh.RecalculateNormals();
    }

/*
3 7 11
2 6 10
1 5 9
0 4 8
*/

/*
4 9 14 19 24
3 8 13 18 23
2 7 12 17 22
1 6 11 16 21
0 5 10 15 20
*/
}