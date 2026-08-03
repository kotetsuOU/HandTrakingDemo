using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
/// <summary>
/// RenderGraph のパス登録前に、毎フレーム必要なカメラ行列の計算、
/// 点群バッファの調停、描画スキップ判定などを事前に行うビルダークラスです。
/// </summary>
internal class PCDContextBuilder
{
    private PCDPointBufferManager.Point[] _virtualContactPointsArray;

    public struct PreComputeData
    {
        public bool ShouldSkip;
        
        public Camera Camera;
        public UniversalResourceData ResourceData;
        public int ScreenWidth;
        public int ScreenHeight;

        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 InverseProjectionMatrix;

        public ComputeBuffer ActiveBuffer;
        public int ActiveCount;

        public bool HasVirtualObjects;
        public bool HasVirtualDepth;
    }

    public PreComputeData BuildPreComputeData(
        ContextContainer frameData,
        PCDRendererFeature.PCDRenderSettings settings,
        PCDPointBufferManager bufferManager)
    {
        var data = new PreComputeData();

        if (!Application.isPlaying)
        {
            data.ShouldSkip = true;
            return data;
        }

        // =========================================================================
        // 仮想接触ポイントの生成
        // =========================================================================
        bool shouldUseExternal = (PCDRendererFeature.Instance != null && PCDRendererFeature.Instance.IsGlobalBufferMode) || (RsGlobalPointCloudManager.Instance != null);

        if (settings.enableVirtualContactOcclusion && HCD_Pipeline.Instance != null)
        {
            var trackedClusters = HCD_Pipeline.Instance.GetTrackedClusters();
            int estimatedMaxPoints = 0;
            float radius = settings.virtualContactRadius;
            float spacing = Mathf.Max(0.001f, settings.virtualContactSpacing);
            int pointsPerCluster = (int)(Mathf.PI * radius * radius / (spacing * spacing)) + 100;

            foreach (var c in trackedClusters)
            {
                if (c.IsAlive) estimatedMaxPoints += pointsPerCluster;
            }

            if (_virtualContactPointsArray == null || _virtualContactPointsArray.Length < estimatedMaxPoints)
            {
                _virtualContactPointsArray = new PCDPointBufferManager.Point[Mathf.Max(1024, estimatedMaxPoints * 2)];
            }

            int idx = 0;
            foreach (var c in trackedClusters)
            {
                if (!c.IsAlive) continue;

                Vector3 centroid = c.Centroid;
                Vector3 normal = c.Normal.normalized;
                if (normal.sqrMagnitude < 0.1f) normal = Vector3.up;

                float offset = HCD_Pipeline.Instance.distanceProcessor != null ? HCD_Pipeline.Instance.distanceProcessor.surfaceDistanceThreshold : 0f;
                centroid += normal * offset;

                Vector3 tangent = Vector3.Cross(normal, Vector3.up);
                if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.Cross(normal, Vector3.right);
                tangent.Normalize();
                Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

                int steps = Mathf.CeilToInt(radius / spacing);
                for (int x = -steps; x <= steps; x++)
                {
                    for (int y = -steps; y <= steps; y++)
                    {
                        float dx = x * spacing;
                        float dy = y * spacing;
                        if (dx * dx + dy * dy <= radius * radius)
                        {
                            if (idx >= _virtualContactPointsArray.Length) break;
                            _virtualContactPointsArray[idx++] = new PCDPointBufferManager.Point
                            {
                                position = centroid + tangent * dx + bitangent * dy,
                                color = new Vector3(settings.virtualContactColor.r, settings.virtualContactColor.g, settings.virtualContactColor.b),
                                originType = 0
                            };
                        }
                    }
                }
            }
            bufferManager.SetVirtualContactPoints(idx > 0 ? _virtualContactPointsArray : null, idx);
        }
        else
        {
            bufferManager.SetVirtualContactPoints(null, 0);
        }

        // =========================================================================
        // 外部バッファの設定
        // =========================================================================
        if (shouldUseExternal && RsGlobalPointCloudManager.Instance != null)
        {
            var globalBuffer = RsGlobalPointCloudManager.Instance.GetOcclusionGlobalBuffer();
            var globalCount = RsGlobalPointCloudManager.Instance.OcclusionTotalCount;
            bufferManager.SetExternalBuffer(globalBuffer, globalCount);
        }
        else
        {
            bufferManager.SetExternalBuffer(null, 0);
        }

        bufferManager.Update();

        // =========================================================================
        // アクティブバッファの決定
        // =========================================================================
        if (bufferManager.UseExternalBuffer && bufferManager.ExternalPointBuffer != null)
        {
            int extCount = bufferManager.ExternalPointCount >= 0 ? bufferManager.ExternalPointCount : bufferManager.ExternalPointBuffer.count;
            if (bufferManager.PointCount > 0)
            {
                int totalCount = extCount + bufferManager.PointCount;
                bufferManager.EnsureCombinedBuffer(totalCount);
                data.ActiveBuffer = bufferManager.CombinedBuffer;
                data.ActiveCount = totalCount;
            }
            else
            {
                data.ActiveBuffer = bufferManager.ExternalPointBuffer;
                data.ActiveCount = extCount;
            }
        }
        else
        {
            data.ActiveBuffer = bufferManager.PointBuffer;
            data.ActiveCount = bufferManager.PointCount;
        }

        // =========================================================================
        // スキップ判定
        // =========================================================================
        bool hasStaticMeshes = bufferManager.HasStaticMeshes();
        bool pointCloudHasData = data.ActiveBuffer != null && data.ActiveCount > 0 && data.ActiveBuffer.IsValid();

        if (!pointCloudHasData && !hasStaticMeshes)
        {
            data.ShouldSkip = true;
            return data;
        }

        // =========================================================================
        // カメラ・リソース情報の取得と行列計算
        // =========================================================================
        var cameraData = frameData.Get<UniversalCameraData>();
        data.ResourceData = frameData.Get<UniversalResourceData>();
        data.Camera = cameraData.camera;
        data.ScreenWidth = cameraData.cameraTargetDescriptor.width;
        data.ScreenHeight = cameraData.cameraTargetDescriptor.height;

        data.HasVirtualDepth = data.ResourceData.cameraDepthTexture.IsValid();
        data.HasVirtualObjects = hasStaticMeshes;
        if (data.HasVirtualDepth && settings.enableVirtualDepthIntegration)
        {
            if (PCDRendererFeature.Instance != null)
            {
                data.HasVirtualObjects = data.HasVirtualObjects || (PCDRendererFeature.Instance.LastFrameVirtualMeshPixelCount > 0);
            }
        }

        Matrix4x4 vMatrix = data.Camera.worldToCameraMatrix;
#if UNITY_2023_1_OR_NEWER
        var adjuster = data.Camera.GetComponent<CameraAdjuster>() ?? Object.FindFirstObjectByType<CameraAdjuster>();
#else
        var adjuster = data.Camera.GetComponent<CameraAdjuster>() ?? Object.FindObjectOfType<CameraAdjuster>();
#endif
        if (adjuster != null && adjuster.isHalfMirrorEnabled)
        {
            if (adjuster.displayTransform != null)
            {
                Vector3 center = adjuster.displayTransform.position;
                Quaternion rotation = adjuster.displayTransform.rotation;
                Matrix4x4 displayTRS = Matrix4x4.TRS(center, rotation, Vector3.one);
                Matrix4x4 flipX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                Matrix4x4 displayInverse = displayTRS.inverse;
                vMatrix = vMatrix * displayTRS * flipX * displayInverse;
            }
            else
            {
                vMatrix = vMatrix * Matrix4x4.Scale(new Vector3(-1, 1, 1));
            }
        }

        data.ViewMatrix = vMatrix;
        data.ProjectionMatrix = GL.GetGPUProjectionMatrix(data.Camera.projectionMatrix, false);
        data.InverseProjectionMatrix = data.Camera.projectionMatrix.inverse;

        data.ShouldSkip = false;
        return data;
    }
}
