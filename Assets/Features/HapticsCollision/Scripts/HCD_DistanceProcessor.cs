using UnityEngine;
using System;

[Serializable]
public class HCD_DistanceProcessor : IHCD_Processor
{
    public string ProcessorName => "DistanceCalculator";

    public enum DetectionMode { TransformOnly, SkinnedMeshRenderer, MeshFilter }
    public enum DistanceMode { MeshSurface, ViewDirection }

    [Header("Mode Settings")]
    public DetectionMode detectionMode;

    [Header("Distance Mode Settings")]
    [Tooltip("距離判定モード (MeshSurface: メッシュ表面基準の手前奥 / ViewDirection: 視線方向基準の手前奥)")]
    public DistanceMode distanceMode = DistanceMode.MeshSurface;

    [Tooltip("視線方向モードで基準とするカメラ。null の場合は Camera.main を使用します。")]
    public Camera viewCamera;

    [Header("Transform Mode Settings")]
    public Transform targetObject;

    [Header("SkinnedMesh Mode Settings")]
    public SkinnedMeshRenderer[] targetSkinnedMeshes;

    [Header("MeshFilter Mode Settings")]
    public MeshFilter[] targetMeshFilters;

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

    private Mesh[] _tempBakedMeshes;
    private CombineInstance[] _combineInstances;

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

        Vector3 cameraPos = Vector3.zero;
        if (distanceMode == DistanceMode.ViewDirection)
        {
            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam != null) cameraPos = cam.transform.position;
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
            collisionComputeShader.SetInt("DistanceMode", (int)distanceMode);
            collisionComputeShader.SetVector("CameraPosition", cameraPos);

            int threadGroups = Mathf.CeilToInt(pointCount / 256.0f);
            collisionComputeShader.Dispatch(_kernelTransform, threadGroups, 1, 1);
        }
        else if (detectionMode == DetectionMode.SkinnedMeshRenderer || detectionMode == DetectionMode.MeshFilter)
        {
            Transform targetTransform = null;
            Bounds bounds = default;
            bool boundsInitialized = false;

            if (_bakedMesh == null) _bakedMesh = new Mesh();

            if (detectionMode == DetectionMode.SkinnedMeshRenderer)
            {
                if (targetSkinnedMeshes == null || targetSkinnedMeshes.Length == 0) return;
                
                targetTransform = targetSkinnedMeshes[0].transform;

                if (_tempBakedMeshes == null || _tempBakedMeshes.Length != targetSkinnedMeshes.Length)
                {
                    if (_tempBakedMeshes != null)
                    {
                        foreach (var m in _tempBakedMeshes) if (m != null) UnityEngine.Object.Destroy(m);
                    }
                    _tempBakedMeshes = new Mesh[targetSkinnedMeshes.Length];
                    for (int i = 0; i < targetSkinnedMeshes.Length; i++) _tempBakedMeshes[i] = new Mesh();
                }

                if (_combineInstances == null || _combineInstances.Length != targetSkinnedMeshes.Length)
                {
                    _combineInstances = new CombineInstance[targetSkinnedMeshes.Length];
                }

                for (int i = 0; i < targetSkinnedMeshes.Length; i++)
                {
                    var smr = targetSkinnedMeshes[i];
                    if (smr == null) continue;

                    smr.BakeMesh(_tempBakedMeshes[i], true);
                    
                    _combineInstances[i].mesh = _tempBakedMeshes[i];
                    _combineInstances[i].transform = targetTransform.worldToLocalMatrix * smr.transform.localToWorldMatrix;

                    if (!boundsInitialized)
                    {
                        bounds = smr.bounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(smr.bounds);
                    }
                }

                _bakedMesh.CombineMeshes(_combineInstances, true, true);
                
                _meshVertices = _bakedMesh.vertices;
                _meshNormals = _bakedMesh.normals;
                _meshIndices = _bakedMesh.triangles;
            }
            else
            {
                if (targetMeshFilters == null || targetMeshFilters.Length == 0) return;
                
                targetTransform = targetMeshFilters[0].transform;

                var validInstances = new System.Collections.Generic.List<CombineInstance>();

                for (int i = 0; i < targetMeshFilters.Length; i++)
                {
                    var mf = targetMeshFilters[i];
                    if (mf == null || mf.sharedMesh == null) continue;
                    
                    if (!mf.sharedMesh.isReadable)
                    {
                        Debug.LogWarning($"[HCD_DistanceProcessor] Mesh '{mf.sharedMesh.name}' on '{mf.gameObject.name}' is not readable. Please enable 'Read/Write Enabled' in the import settings. Skipping this mesh for haptic collision.");
                        continue;
                    }

                    CombineInstance ci = new CombineInstance();
                    ci.mesh = mf.sharedMesh;
                    ci.transform = targetTransform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    validInstances.Add(ci);

                    var renderer = mf.GetComponent<MeshRenderer>();
                    var smrBounds = renderer != null ? renderer.bounds : new Bounds(mf.transform.position, mf.transform.lossyScale);

                    if (!boundsInitialized)
                    {
                        bounds = smrBounds;
                        boundsInitialized = true;
                    }
                    else
                    {
                        bounds.Encapsulate(smrBounds);
                    }
                }

                _combineInstances = validInstances.ToArray();
                _bakedMesh.CombineMeshes(_combineInstances, true, true);
                
                _meshVertices = _bakedMesh.vertices;
                _meshNormals = _bakedMesh.normals;
                _meshIndices = _bakedMesh.triangles;
            }

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
            // Bounds already obtained above
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
            collisionComputeShader.SetMatrix("LocalToWorldMatrix", targetTransform.localToWorldMatrix);
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
            collisionComputeShader.SetMatrix("LocalToWorldMatrix", targetTransform.localToWorldMatrix);

            collisionComputeShader.SetVector("MeshBoundsMin", bounds.min - new Vector3(totalPadding, totalPadding, totalPadding));
            collisionComputeShader.SetVector("MeshBoundsMax", bounds.max + new Vector3(totalPadding, totalPadding, totalPadding));
            
            collisionComputeShader.SetVector("GridBoundsMin", gridMin);
            collisionComputeShader.SetVector("GridCellSize", cellSize);
            collisionComputeShader.SetInts("GridResolution", gridRes);
            
            collisionComputeShader.SetFloat("SurfaceDistanceThreshold", surfaceDistanceThreshold);
            collisionComputeShader.SetFloat("BackfaceDistanceThreshold", backfaceDistanceThreshold);
            collisionComputeShader.SetInt("DistanceMode", (int)distanceMode);
            collisionComputeShader.SetVector("CameraPosition", cameraPos);

            int threadGroups = Mathf.CeilToInt(pointCount / 64.0f);
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
        if (_bakedMesh != null) UnityEngine.Object.Destroy(_bakedMesh);
        if (_tempBakedMeshes != null)
        {
            foreach (var m in _tempBakedMeshes) if (m != null) UnityEngine.Object.Destroy(m);
        }
    }
}
