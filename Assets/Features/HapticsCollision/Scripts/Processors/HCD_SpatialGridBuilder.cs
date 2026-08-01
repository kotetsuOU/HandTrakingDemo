using UnityEngine;

namespace Features.HapticsCollision.Processors
{
    /// <summary>
    /// GPU側 8x8x8 空間グリッドのバッファ管理およびグリッドビルドディスパッチを担当するクラス。
    /// </summary>
    public class HCD_SpatialGridBuilder
    {
        private ComputeBuffer _meshVerticesBuffer;
        private ComputeBuffer _meshNormalsBuffer;
        private ComputeBuffer _meshIndicesBuffer;
        private ComputeBuffer _gridBuffer;

        public ComputeBuffer MeshVerticesBuffer => _meshVerticesBuffer;
        public ComputeBuffer MeshNormalsBuffer => _meshNormalsBuffer;
        public ComputeBuffer MeshIndicesBuffer => _meshIndicesBuffer;
        public ComputeBuffer GridBuffer => _gridBuffer;

        public Vector3 GridMin { get; private set; }
        public Vector3 CellSize { get; private set; }
        public int[] GridResolution { get; } = new int[] { 8, 8, 8 };
        public float TotalPadding { get; private set; }

        public void Setup()
        {
            if (_gridBuffer == null)
            {
                _gridBuffer = new ComputeBuffer(512 * 32, sizeof(int));
            }
        }

        public void BuildGrid(
            ComputeShader computeShader,
            int kernelClearGrid,
            int kernelBuildGrid,
            Vector3[] vertices,
            Vector3[] normals,
            int[] triangles,
            Bounds bounds,
            Transform targetTransform,
            float maxThreshold)
        {
            if (vertices == null || triangles == null) return;

            int trianglesCount = triangles.Length / 3;

            if (_meshVerticesBuffer == null || _meshVerticesBuffer.count != vertices.Length)
            {
                _meshVerticesBuffer?.Release();
                _meshNormalsBuffer?.Release();
                _meshVerticesBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
                _meshNormalsBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            }

            if (_meshIndicesBuffer == null || _meshIndicesBuffer.count != triangles.Length)
            {
                _meshIndicesBuffer?.Release();
                _meshIndicesBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
            }

            _meshVerticesBuffer.SetData(vertices);
            if (normals != null && normals.Length == vertices.Length)
            {
                _meshNormalsBuffer.SetData(normals);
            }
            _meshIndicesBuffer.SetData(triangles);

            TotalPadding = maxThreshold + 0.1f;
            Vector3 gridMin = bounds.min - new Vector3(TotalPadding, TotalPadding, TotalPadding);
            Vector3 gridMax = bounds.max + new Vector3(TotalPadding, TotalPadding, TotalPadding);
            Vector3 gridSize = gridMax - gridMin;

            GridMin = gridMin;
            CellSize = new Vector3(gridSize.x / 8f, gridSize.y / 8f, gridSize.z / 8f);

            // 1. Clear Grid
            computeShader.SetBuffer(kernelClearGrid, "GridBuffer", _gridBuffer);
            computeShader.Dispatch(kernelClearGrid, Mathf.CeilToInt(512 / 256.0f), 1, 1);

            // 2. Build Grid
            computeShader.SetBuffer(kernelBuildGrid, "GridBuffer", _gridBuffer);
            computeShader.SetBuffer(kernelBuildGrid, "MeshVerticesBuffer", _meshVerticesBuffer);
            computeShader.SetBuffer(kernelBuildGrid, "MeshIndicesBuffer", _meshIndicesBuffer);
            computeShader.SetInt("MeshTrianglesCount", trianglesCount);
            computeShader.SetMatrix("LocalToWorldMatrix", targetTransform.localToWorldMatrix);
            computeShader.SetVector("GridBoundsMin", GridMin);
            computeShader.SetVector("GridCellSize", CellSize);
            computeShader.SetInts("GridResolution", GridResolution);

            computeShader.Dispatch(kernelBuildGrid, Mathf.CeilToInt(trianglesCount / 256.0f), 1, 1);
        }

        public void Release()
        {
            _meshVerticesBuffer?.Release();
            _meshNormalsBuffer?.Release();
            _meshIndicesBuffer?.Release();
            _gridBuffer?.Release();
        }
    }
}
