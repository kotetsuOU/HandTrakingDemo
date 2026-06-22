using UnityEngine;

public class HCD_DistanceProcessor : MonoBehaviour, IHCD_Processor
{
    public string ProcessorName => "DistanceCalculator";

    public enum DetectionMode { TransformOnly, SkinnedMeshRenderer }
    [Header("Mode Settings")]
    public DetectionMode detectionMode;

    [Header("Transform Mode Settings")]
    public Transform targetObject;

    [Header("SkinnedMesh Mode Settings")]
    public SkinnedMeshRenderer targetSkinnedMesh;

    [Header("Collision Parameters")]
    [Tooltip("これより近いと接触と判定する距離(m)")]
    public float surfaceDistanceThreshold = 0.02f;
    [Tooltip("これより深くめり込むと貫通として無視する距離(m)")]
    public float backfaceDistanceThreshold = 0.05f;

    public ComputeShader collisionComputeShader;

    public const string ResultBufferName = "CollisionResultBuffer";

    private HCD_Pipeline _pipeline;
    private ComputeBuffer _resultBuffer;
    private int _kernelTransform;
    private int _kernelMesh;

    private Mesh _bakedMesh;
    private int _kernelClearGrid;
    private int _kernelBuildGrid;

    private ComputeBuffer _meshVerticesBuffer;
    private ComputeBuffer _meshNormalsBuffer;
    private ComputeBuffer _meshIndicesBuffer;
    private ComputeBuffer _gridBuffer;
    private Vector3[] _meshVertices;
    private Vector3[] _meshNormals;
    private int[] _meshIndices;

    // Struct size: int(4) + float3(12) + float3(12) = 28 bytes
    private const int STRIDE = 28;

    public void Setup(HCD_Pipeline pipeline)
    {
        _pipeline = pipeline;
        if (collisionComputeShader != null)
        {
            _kernelTransform = collisionComputeShader.FindKernel("CheckCollisionTransform");
            _kernelMesh = collisionComputeShader.FindKernel("CheckCollisionMesh");
            _kernelClearGrid = collisionComputeShader.FindKernel("ClearMeshGrid");
            _kernelBuildGrid = collisionComputeShader.FindKernel("BuildMeshGrid");
        }
        
        // Voxel Grid: 8x8x8 = 512 cells, max 31 triangles per cell (32 ints)
        _gridBuffer = new ComputeBuffer(512 * 32, sizeof(int));
    }

    public void Dispatch(ComputeBuffer pointCloudBuffer, int pointCount)
    {
        if (collisionComputeShader == null || pointCount == 0) return;

        // 点群数に合わせてバッファをリサイズ
        if (_resultBuffer == null || _resultBuffer.count != pointCount)
        {
            _resultBuffer?.Release();
            _resultBuffer = new ComputeBuffer(pointCount, STRIDE);
            _pipeline.SetSharedBuffer(ResultBufferName, _resultBuffer);
        }

        if (detectionMode == DetectionMode.TransformOnly)
        {
            if (targetObject == null) return;

            collisionComputeShader.SetBuffer(_kernelTransform, "PointCloudBuffer", pointCloudBuffer);
            collisionComputeShader.SetBuffer(_kernelTransform, "ResultBuffer", _resultBuffer);
            collisionComputeShader.SetInt("PointsCount", pointCount);
            collisionComputeShader.SetVector("TargetPosition", targetObject.position);
            collisionComputeShader.SetFloat("SurfaceDistanceThreshold", surfaceDistanceThreshold);
            collisionComputeShader.SetFloat("BackfaceDistanceThreshold", backfaceDistanceThreshold);

            int threadGroups = Mathf.CeilToInt(pointCount / 256.0f);
            collisionComputeShader.Dispatch(_kernelTransform, threadGroups, 1, 1);
        }
        else if (detectionMode == DetectionMode.SkinnedMeshRenderer)
        {
            if (targetSkinnedMesh == null) return;

            if (_bakedMesh == null) _bakedMesh = new Mesh();
            targetSkinnedMesh.BakeMesh(_bakedMesh, true);

            _meshVertices = _bakedMesh.vertices;
            _meshNormals = _bakedMesh.normals;
            _meshIndices = _bakedMesh.triangles;
            int trianglesCount = _meshIndices.Length / 3;

            if (_meshVertices == null || _meshVertices.Length == 0 || _meshIndices == null || _meshIndices.Length == 0) return;

            if (_meshVerticesBuffer == null || _meshVerticesBuffer.count != _meshVertices.Length)
            {
                _meshVerticesBuffer?.Release();
                _meshNormalsBuffer?.Release();
                _meshVerticesBuffer = new ComputeBuffer(_meshVertices.Length, sizeof(float) * 3);
                _meshNormalsBuffer = new ComputeBuffer(_meshVertices.Length, sizeof(float) * 3);
            }

            if (_meshIndicesBuffer == null || _meshIndicesBuffer.count != _meshIndices.Length)
            {
                _meshIndicesBuffer?.Release();
                _meshIndicesBuffer = new ComputeBuffer(_meshIndices.Length, sizeof(int));
            }

            _meshVerticesBuffer.SetData(_meshVertices);
            if (_meshNormals != null && _meshNormals.Length == _meshVertices.Length)
            {
                _meshNormalsBuffer.SetData(_meshNormals);
            }
            _meshIndicesBuffer.SetData(_meshIndices);

            // Setup Grid Parameters
            Bounds bounds = targetSkinnedMesh.bounds;
            float maxThresh = Mathf.Max(surfaceDistanceThreshold, backfaceDistanceThreshold);
            float totalPadding = maxThresh + 0.1f; // safe margin

            Vector3 gridMin = bounds.min - new Vector3(totalPadding, totalPadding, totalPadding);
            Vector3 gridMax = bounds.max + new Vector3(totalPadding, totalPadding, totalPadding);
            Vector3 gridSize = gridMax - gridMin;
            Vector3 cellSize = new Vector3(gridSize.x / 8f, gridSize.y / 8f, gridSize.z / 8f);
            int[] gridRes = new int[] { 8, 8, 8 };

            // 1. Clear Grid
            collisionComputeShader.SetBuffer(_kernelClearGrid, "GridBuffer", _gridBuffer);
            collisionComputeShader.Dispatch(_kernelClearGrid, Mathf.CeilToInt(512 / 256.0f), 1, 1);

            // 2. Build Grid
            collisionComputeShader.SetBuffer(_kernelBuildGrid, "GridBuffer", _gridBuffer);
            collisionComputeShader.SetBuffer(_kernelBuildGrid, "MeshVerticesBuffer", _meshVerticesBuffer);
            collisionComputeShader.SetBuffer(_kernelBuildGrid, "MeshIndicesBuffer", _meshIndicesBuffer);
            collisionComputeShader.SetInt("MeshTrianglesCount", trianglesCount);
            collisionComputeShader.SetMatrix("LocalToWorldMatrix", targetSkinnedMesh.transform.localToWorldMatrix);
            collisionComputeShader.SetVector("GridBoundsMin", gridMin);
            collisionComputeShader.SetVector("GridCellSize", cellSize);
            collisionComputeShader.SetInts("GridResolution", gridRes);

            collisionComputeShader.Dispatch(_kernelBuildGrid, Mathf.CeilToInt(trianglesCount / 256.0f), 1, 1);

            // 3. Distance Check
            collisionComputeShader.SetBuffer(_kernelMesh, "PointCloudBuffer", pointCloudBuffer);
            collisionComputeShader.SetBuffer(_kernelMesh, "ResultBuffer", _resultBuffer);
            collisionComputeShader.SetBuffer(_kernelMesh, "MeshVerticesBuffer", _meshVerticesBuffer);
            collisionComputeShader.SetBuffer(_kernelMesh, "MeshNormalsBuffer", _meshNormalsBuffer);
            collisionComputeShader.SetBuffer(_kernelMesh, "MeshIndicesBuffer", _meshIndicesBuffer);
            collisionComputeShader.SetBuffer(_kernelMesh, "GridBuffer", _gridBuffer);

            collisionComputeShader.SetInt("PointsCount", pointCount);
            collisionComputeShader.SetInt("MeshTrianglesCount", trianglesCount);
            collisionComputeShader.SetMatrix("LocalToWorldMatrix", targetSkinnedMesh.transform.localToWorldMatrix);

            collisionComputeShader.SetVector("MeshBoundsMin", bounds.min - new Vector3(totalPadding, totalPadding, totalPadding));
            collisionComputeShader.SetVector("MeshBoundsMax", bounds.max + new Vector3(totalPadding, totalPadding, totalPadding));
            
            collisionComputeShader.SetVector("GridBoundsMin", gridMin);
            collisionComputeShader.SetVector("GridCellSize", cellSize);
            collisionComputeShader.SetInts("GridResolution", gridRes);
            
            collisionComputeShader.SetFloat("SurfaceDistanceThreshold", surfaceDistanceThreshold);
            collisionComputeShader.SetFloat("BackfaceDistanceThreshold", backfaceDistanceThreshold);

            int threadGroups = Mathf.CeilToInt(pointCount / 256.0f);
            collisionComputeShader.Dispatch(_kernelMesh, threadGroups, 1, 1);
        }
    }

    public void Release()
    {
        _resultBuffer?.Release();
        _meshVerticesBuffer?.Release();
        _meshNormalsBuffer?.Release();
        _meshIndicesBuffer?.Release();
        _gridBuffer?.Release();
        if (_bakedMesh != null) Destroy(_bakedMesh);
    }
}
