using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealSense.DummyPointCloud
{
    public enum PointDensityUnit
    {
        PointsPerMm2,   // 1 mm^2 あたりの点数
        PointsPerCm2,   // 1 cm^2 あたりの点数
        PointSpacingMm, // 点間隔 (mm)
        TotalPointCount // 指定の合計点数
    }

    public enum PointColorMode
    {
        SolidColor,    // 単色指定
        MaterialColor, // マテリアル/メインカラー
        VertexColor    // メッシュの頂点カラー
    }

    public struct SampledPointCloudData
    {
        public Vector3[] Positions; // ワールド座標
        public Color[] Colors;      // 各点の色
        public int PointCount;
    }

    public class RsMeshPointCloudSampler
    {
        private Mesh _sharedBakedMesh;

        public RsMeshPointCloudSampler()
        {
            _sharedBakedMesh = new Mesh();
        }

        ~RsMeshPointCloudSampler()
        {
            if (_sharedBakedMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(_sharedBakedMesh);
                _sharedBakedMesh = null;
            }
        }

        /// <summary>
        /// 指定された GameObject のリスト配下から Mesh を取得し、
        /// 物理密度およびカラー設定に従って点群データをサンプリングします。
        /// </summary>
        public SampledPointCloudData SamplePointCloud(
            List<GameObject> rootObjects,
            bool includeChildren,
            PointDensityUnit densityUnit,
            float densityValue,
            PointColorMode colorMode,
            Color solidColor)
        {
            if (rootObjects == null || rootObjects.Count == 0)
            {
                return new SampledPointCloudData { Positions = Array.Empty<Vector3>(), Colors = Array.Empty<Color>(), PointCount = 0 };
            }

            List<Renderer> allRenderers = new List<Renderer>();

            foreach (var rootObj in rootObjects)
            {
                if (rootObj == null) continue;

                var renderers = includeChildren
                    ? rootObj.GetComponentsInChildren<Renderer>()
                    : rootObj.GetComponents<Renderer>();

                foreach (var r in renderers)
                {
                    if (r != null && r.enabled && r.gameObject.activeInHierarchy && !allRenderers.Contains(r))
                    {
                        allRenderers.Add(r);
                    }
                }
            }

            List<Vector3> allPositions = new List<Vector3>();
            List<Color> allColors = new List<Color>();

            foreach (var renderer in allRenderers)
            {
                if (renderer is SkinnedMeshRenderer skinnedRenderer)
                {
                    SampleFromSkinnedMesh(skinnedRenderer, densityUnit, densityValue, colorMode, solidColor, allPositions, allColors);
                }
                else if (renderer is MeshRenderer meshRenderer)
                {
                    var meshFilter = renderer.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        SampleFromStaticMesh(meshFilter.sharedMesh, meshRenderer, densityUnit, densityValue, colorMode, solidColor, allPositions, allColors);
                    }
                }
            }

            return new SampledPointCloudData
            {
                Positions = allPositions.ToArray(),
                Colors = allColors.ToArray(),
                PointCount = allPositions.Count
            };
        }

        private void SampleFromSkinnedMesh(
            SkinnedMeshRenderer skinnedRenderer,
            PointDensityUnit densityUnit,
            float densityValue,
            PointColorMode colorMode,
            Color solidColor,
            List<Vector3> outPositions,
            List<Color> outColors)
        {
            if (skinnedRenderer.sharedMesh == null) return;

            if (_sharedBakedMesh == null) _sharedBakedMesh = new Mesh();
            _sharedBakedMesh.Clear();
            skinnedRenderer.BakeMesh(_sharedBakedMesh);

            Matrix4x4 localToWorld = skinnedRenderer.transform.localToWorldMatrix;
            SampleMeshInternal(_sharedBakedMesh, skinnedRenderer, localToWorld, densityUnit, densityValue, colorMode, solidColor, outPositions, outColors);
        }

        private void SampleFromStaticMesh(
            Mesh mesh,
            MeshRenderer meshRenderer,
            PointDensityUnit densityUnit,
            float densityValue,
            PointColorMode colorMode,
            Color solidColor,
            List<Vector3> outPositions,
            List<Color> outColors)
        {
            Matrix4x4 localToWorld = meshRenderer.transform.localToWorldMatrix;
            SampleMeshInternal(mesh, meshRenderer, localToWorld, densityUnit, densityValue, colorMode, solidColor, outPositions, outColors);
        }

        private void SampleMeshInternal(
            Mesh mesh,
            Renderer renderer,
            Matrix4x4 localToWorld,
            PointDensityUnit densityUnit,
            float densityValue,
            PointColorMode colorMode,
            Color solidColor,
            List<Vector3> outPositions,
            List<Color> outColors)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Color[] vertexColors = mesh.colors;

            if (vertices == null || vertices.Length == 0 || triangles == null || triangles.Length == 0)
                return;

            bool hasVertexColors = vertexColors != null && vertexColors.Length == vertices.Length;
            Color baseMaterialColor = solidColor;

            if (colorMode == PointColorMode.MaterialColor && renderer.sharedMaterial != null)
            {
                if (renderer.sharedMaterial.HasProperty("_Color"))
                {
                    baseMaterialColor = renderer.sharedMaterial.color;
                }
                else if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    baseMaterialColor = renderer.sharedMaterial.GetColor("_BaseColor");
                }
            }

            int triCount = triangles.Length / 3;
            float[] triAreas = new float[triCount];
            float totalWorldAreaSquareMeters = 0f;

            for (int i = 0; i < triCount; i++)
            {
                Vector3 p0 = localToWorld.MultiplyPoint3x4(vertices[triangles[i * 3]]);
                Vector3 p1 = localToWorld.MultiplyPoint3x4(vertices[triangles[i * 3 + 1]]);
                Vector3 p2 = localToWorld.MultiplyPoint3x4(vertices[triangles[i * 3 + 2]]);

                float area = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                triAreas[i] = area;
                totalWorldAreaSquareMeters += area;
            }

            if (totalWorldAreaSquareMeters <= 1e-8f) return;

            float areaMm2 = totalWorldAreaSquareMeters * 1000000f;
            float areaCm2 = totalWorldAreaSquareMeters * 10000f;

            int targetTotalPoints = 0;
            switch (densityUnit)
            {
                case PointDensityUnit.PointsPerMm2:
                    targetTotalPoints = Mathf.Max(1, Mathf.RoundToInt(areaMm2 * Mathf.Max(0.0001f, densityValue)));
                    break;
                case PointDensityUnit.PointsPerCm2:
                    targetTotalPoints = Mathf.Max(1, Mathf.RoundToInt(areaCm2 * Mathf.Max(0.0001f, densityValue)));
                    break;
                case PointDensityUnit.PointSpacingMm:
                    float spacingMm = Mathf.Max(0.1f, densityValue);
                    float pointsPerMm2 = 1f / (spacingMm * spacingMm);
                    targetTotalPoints = Mathf.Max(1, Mathf.RoundToInt(areaMm2 * pointsPerMm2));
                    break;
                case PointDensityUnit.TotalPointCount:
                    targetTotalPoints = Mathf.Max(1, Mathf.RoundToInt(densityValue));
                    break;
            }

            targetTotalPoints = Mathf.Min(targetTotalPoints, 500000);

            System.Random rand = new System.Random(42);

            for (int i = 0; i < triCount; i++)
            {
                float areaFrac = triAreas[i] / totalWorldAreaSquareMeters;
                int pointsForThisTri = Mathf.RoundToInt(targetTotalPoints * areaFrac);

                if (pointsForThisTri <= 0 && targetTotalPoints > 0 && rand.NextDouble() < areaFrac * targetTotalPoints)
                {
                    pointsForThisTri = 1;
                }

                if (pointsForThisTri <= 0) continue;

                int i0 = triangles[i * 3];
                int i1 = triangles[i * 3 + 1];
                int i2 = triangles[i * 3 + 2];

                Vector3 v0 = localToWorld.MultiplyPoint3x4(vertices[i0]);
                Vector3 v1 = localToWorld.MultiplyPoint3x4(vertices[i1]);
                Vector3 v2 = localToWorld.MultiplyPoint3x4(vertices[i2]);

                Color c0 = (colorMode == PointColorMode.SolidColor) ? solidColor : ((colorMode == PointColorMode.VertexColor && hasVertexColors) ? vertexColors[i0] : baseMaterialColor);
                Color c1 = (colorMode == PointColorMode.SolidColor) ? solidColor : ((colorMode == PointColorMode.VertexColor && hasVertexColors) ? vertexColors[i1] : baseMaterialColor);
                Color c2 = (colorMode == PointColorMode.SolidColor) ? solidColor : ((colorMode == PointColorMode.VertexColor && hasVertexColors) ? vertexColors[i2] : baseMaterialColor);

                for (int p = 0; p < pointsForThisTri; p++)
                {
                    float r1 = (float)rand.NextDouble();
                    float r2 = (float)rand.NextDouble();

                    float sqrtR1 = Mathf.Sqrt(r1);
                    float u = 1f - sqrtR1;
                    float v = r2 * sqrtR1;
                    float w = 1f - u - v;

                    Vector3 pos = u * v0 + v * v1 + w * v2;
                    Color col = u * c0 + v * c1 + w * c2;

                    outPositions.Add(pos);
                    outColors.Add(col);
                }
            }
        }
    }
}
