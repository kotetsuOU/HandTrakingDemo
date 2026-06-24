// =============================================================================
// PCD_RenderPass_BindParams.cs
// -----------------------------------------------------------------------------
// RecordRenderGraph から呼ばれ、ComputePassData に必要なパラメータを転写する。
//
// このメソッドでは以下をセットする:
//   - カメラの行列情報（View / Projection）
//   - ハーフミラー対応のX軸反転処理
//   - 全カーネルIDの転写
//   - バッファ管理情報（外部/内部/結合バッファ）
//   - 仮想深度・DepthMapOnly モードのフラグ
// =============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public partial class PCDRenderPass
{
    /// <summary>
    /// ComputePassData にレンダリングに必要なすべてのパラメータを転写する。
    /// カメラ行列の計算やハーフミラー用のX反転もここで行う。
    /// </summary>
    private void BindComputePassData(ref ComputePassData data, Camera camera, int screenWidth, int screenHeight, int activeCount, ComputeBuffer activeBuffer, bool depthMapOnlyMode, UniversalResourceData resourceData)
    {
        data.computeShader = pointCloudCompute;
        data.pointCount = activeCount;
        data.screenParams = new Vector4(screenWidth, screenHeight, 0, 0);

        Matrix4x4 vMatrix = camera.worldToCameraMatrix;
        var adjuster = camera.GetComponent<CameraAdjuster>();
        if (adjuster != null && adjuster.isHalfMirrorEnabled)
        {
            if (adjuster.displayTransform != null)
            {
                // 鏡面世界（ハーフミラー）用に、Display中心で点群をローカルX軸方向に反転させます
                Vector3 center = adjuster.displayTransform.position;
                Quaternion rotation = adjuster.displayTransform.rotation;
                Matrix4x4 displayTRS = Matrix4x4.TRS(center, rotation, Vector3.one);
                Matrix4x4 flipX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
                Matrix4x4 displayInverse = displayTRS.inverse;
                
                vMatrix = vMatrix * displayTRS * flipX * displayInverse;
            }
            else
            {
                // displayTransformが未設定の場合は、ワールド原点中心でX反転
                vMatrix = vMatrix * Matrix4x4.Scale(new Vector3(-1, 1, 1));
            }
        }
        data.viewMatrix = vMatrix;

        data.projectionMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
        data.settings = _settings;
        data.kernelClear = _kernelClear;
        data.kernelClearCounter = _kernelClearCounter;
        data.kernelProject = _kernelProject;
        data.kernelCalcGridZMin = _kernelCalcGridZMin;
        data.kernelCalcDensity = _kernelCalcDensity;
        data.kernelCalcGridLevel = _kernelCalcGridLevel;
        data.kernelGridMedianFilter = _kernelGridMedianFilter;
        data.kernelCalcNeighborhoodSize = _kernelCalcNeighborhoodSize;
        data.kernelFillNeighborhoodSizeWithMinLevel = _kernelFillNeighborhoodSizeWithMinLevel;
        data.kernelBuildDepthPyramidL1 = _kernelBuildDepthPyramidL1;
        data.kernelBuildDepthPyramidL2 = _kernelBuildDepthPyramidL2;
        data.kernelBuildDepthPyramidL3 = _kernelBuildDepthPyramidL3;
        data.kernelBuildDepthPyramidL4 = _kernelBuildDepthPyramidL4;
        data.kernelBuildDepthPyramidL5 = _kernelBuildDepthPyramidL5;
        data.kernelBuildDepthPyramidL6 = _kernelBuildDepthPyramidL6;
        data.kernelApplyGradient = _kernelApplyGradient;
        data.kernelComputeOcclusion = _kernelComputeOcclusion;
        data.kernelCopyColorToOcclusion = _kernelCopyColorToOcclusion;
        data.kernelFillHoles = _kernelFillHoles;
        data.kernelFillHolesPullPushInit = _kernelFillHolesPullPushInit;
        data.kernelFillHolesPull = _kernelFillHolesPull;
        data.kernelFillHolesPush = _kernelFillHolesPush;
        data.kernelFillHolesPullPushFinalize = _kernelFillHolesPullPushFinalize;
        data.kernelInterpolate = _kernelInterpolate;
        data.kernelMerge = _kernelMerge;
        data.kernelInitFromCamera = _kernelInitFromCamera;
        data.kernelVisualizeOcclusionDebug = _kernelVisualizeOcclusionDebug;
        data.kernelMorphologyErode = _kernelMorphologyErode;
        data.kernelMorphologyDilate = _kernelMorphologyDilate;
        data.kernelMorphologyCopy = _kernelMorphologyCopy;
        data.kernelBuildMorphPyramidL1 = _kernelBuildMorphPyramidL1;
        data.kernelBuildMorphPyramidL2 = _kernelBuildMorphPyramidL2;
        data.kernelBuildMorphPyramidL3 = _kernelBuildMorphPyramidL3;
        data.kernelBuildMorphPyramidL4 = _kernelBuildMorphPyramidL4;
        data.kernelBuildMorphPyramidL5 = _kernelBuildMorphPyramidL5;
        data.kernelBuildMorphPyramidL6 = _kernelBuildMorphPyramidL6;
        data.useExternal = _bufferManager.UseExternalBuffer;
        data.externalBuffer = _bufferManager.ExternalPointBuffer;
        data.internalBuffer = _bufferManager.PointBuffer;
        data.externalCount = _bufferManager.ExternalPointCount;
        data.internalCount = _bufferManager.PointCount;
        data.combinedBuffer = _bufferManager.CombinedBuffer;
        data.pointBuffer = activeBuffer;
        data.staticMeshCounterBuffer = _staticMeshCounterBuffer;
        data.hasVirtualDepth = resourceData.cameraDepthTexture.IsValid();
        data.depthMapOnlyMode = depthMapOnlyMode;
        data.inverseProjectionMatrix = camera.projectionMatrix.inverse;
    }
}
