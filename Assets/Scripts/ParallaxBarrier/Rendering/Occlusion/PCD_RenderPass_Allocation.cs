using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;

public partial class PCDRenderPass
{
    private int _lastScreenWidth = -1;
    private int _lastScreenHeight = -1;

    private void AllocateInternalHandles(int screenWidth, int screenHeight)
    {
        bool sizeChanged = (_lastScreenWidth != screenWidth || _lastScreenHeight != screenHeight);

        if (_bufferManager._colorMapHandle == null || sizeChanged)
        {
            ReleaseInternalHandles();

            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;

            var colorFormatARGB = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.ARGBFloat, false);
            var colorFormatRFloat = GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.RFloat, false);

            _bufferManager._colorMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: colorFormatARGB, enableRandomWrite: true, name: "PCD_ColorMap");
            _bufferManager._depthMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, enableRandomWrite: true, name: "PCD_DepthMap");
            _bufferManager._viewPositionMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: colorFormatARGB, enableRandomWrite: true, name: "PCD_ViewPosMap");
            _bufferManager._originTypeMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, enableRandomWrite: true, name: "PCD_OriginTypeMap");

            int gridGroupsX = (screenWidth + (int)_settings.gridSize - 1) / (int)_settings.gridSize;
            int gridGroupsY = (screenHeight + (int)_settings.gridSize - 1) / (int)_settings.gridSize;

            _bufferManager._gridZMinMapHandle = RTHandles.Alloc(gridGroupsX, gridGroupsY, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, enableRandomWrite: true, name: "PCD_GridZMin");
            _bufferManager._densityMapHandle = RTHandles.Alloc(gridGroupsX, gridGroupsY, colorFormat: colorFormatRFloat, enableRandomWrite: true, name: "PCD_Density");
            _bufferManager._gridLevelMapHandle = RTHandles.Alloc(gridGroupsX, gridGroupsY, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SInt, enableRandomWrite: true, name: "PCD_GridLevel");
            _bufferManager._filteredGridLevelMapHandle = RTHandles.Alloc(gridGroupsX, gridGroupsY, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SInt, enableRandomWrite: true, name: "PCD_FilteredGridLevel");

            _bufferManager._neighborhoodSizeMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SInt, enableRandomWrite: true, name: "PCD_Neighborhood");
            _bufferManager._correctedNeighborhoodSizeMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SInt, enableRandomWrite: true, name: "PCD_CorrectedNeighborhood");

            _bufferManager._occlusionResultMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: colorFormatARGB, enableRandomWrite: true, name: "PCD_OcclusionResult");
            _bufferManager._occlusionValueMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: GraphicsFormatUtility.GetGraphicsFormat(RenderTextureFormat.RGFloat, false), enableRandomWrite: true, name: "PCD_OcclusionValue");
            _bufferManager._debugDisplayMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: colorFormatARGB, enableRandomWrite: true, name: "PCD_DebugDisplay");
            _bufferManager._neighborCountMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, enableRandomWrite: true, name: "PCD_NeighborCountMapDebug");
            _bufferManager._integratedDepthMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, enableRandomWrite: true, name: "PCD_IntegratedDepthMap");
            _bufferManager._neighborhoodMapHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SInt, enableRandomWrite: true, name: "PCD_NeighborhoodMapDebug");
            _bufferManager._finalImageHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: colorFormatARGB, enableRandomWrite: true, name: "PCD_FinalImage");

            int l1w = Mathf.Max(1, screenWidth / 2); int l1h = Mathf.Max(1, screenHeight / 2);
            int l2w = Mathf.Max(1, l1w / 2); int l2h = Mathf.Max(1, l1h / 2);
            int l3w = Mathf.Max(1, l2w / 2); int l3h = Mathf.Max(1, l2h / 2);
            int l4w = Mathf.Max(1, l3w / 2); int l4h = Mathf.Max(1, l3h / 2);
            int l5w = Mathf.Max(1, l4w / 2); int l5h = Mathf.Max(1, l4h / 2);
            int l6w = Mathf.Max(1, l5w / 2); int l6h = Mathf.Max(1, l5h / 2);

            var fmtR16 = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            _bufferManager.depthPyramidL1 = RTHandles.Alloc(l1w, l1h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_DepthPyramidL1");
            _bufferManager.depthPyramidL2 = RTHandles.Alloc(l2w, l2h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_DepthPyramidL2");
            _bufferManager.depthPyramidL3 = RTHandles.Alloc(l3w, l3h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_DepthPyramidL3");
            _bufferManager.depthPyramidL4 = RTHandles.Alloc(l4w, l4h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_DepthPyramidL4");
            _bufferManager.depthPyramidL5 = RTHandles.Alloc(l5w, l5h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_DepthPyramidL5");
            _bufferManager.depthPyramidL6 = RTHandles.Alloc(l6w, l6h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_DepthPyramidL6");

            _bufferManager.pullPushPyramid = new RTHandle[5];
            int pw = screenWidth; int ph = screenHeight;
            for(int i = 0; i < 5; i++) {
                _bufferManager.pullPushPyramid[i] = RTHandles.Alloc(pw, ph, colorFormat: colorFormatARGB, enableRandomWrite: true, name: "PCD_PP_" + i);
                pw = Mathf.Max(1, (pw + 1) / 2); ph = Mathf.Max(1, (ph + 1) / 2);
            }
            
            _bufferManager._morphColorTempHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: colorFormatARGB, enableRandomWrite: true, name: "PCD_MorphColorTemp");
            _bufferManager._morphTypeTempHandle = RTHandles.Alloc(screenWidth, screenHeight, colorFormat: UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, enableRandomWrite: true, name: "PCD_MorphTypeTemp");
            var fmtUInt = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt;
            _bufferManager.morphTypePyramidL1 = RTHandles.Alloc(l1w, l1h, colorFormat: fmtUInt, enableRandomWrite: true, name: "PCD_MorphTL1");
            _bufferManager.morphTypePyramidL2 = RTHandles.Alloc(l2w, l2h, colorFormat: fmtUInt, enableRandomWrite: true, name: "PCD_MorphTL2");
            _bufferManager.morphTypePyramidL3 = RTHandles.Alloc(l3w, l3h, colorFormat: fmtUInt, enableRandomWrite: true, name: "PCD_MorphTL3");
            _bufferManager.morphTypePyramidL4 = RTHandles.Alloc(l4w, l4h, colorFormat: fmtUInt, enableRandomWrite: true, name: "PCD_MorphTL4");
            _bufferManager.morphTypePyramidL5 = RTHandles.Alloc(l5w, l5h, colorFormat: fmtUInt, enableRandomWrite: true, name: "PCD_MorphTL5");
            _bufferManager.morphTypePyramidL6 = RTHandles.Alloc(l6w, l6h, colorFormat: fmtUInt, enableRandomWrite: true, name: "PCD_MorphTL6");

            _bufferManager.morphColorPyramidL1 = RTHandles.Alloc(l1w, l1h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_MorphCL1");
            _bufferManager.morphColorPyramidL2 = RTHandles.Alloc(l2w, l2h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_MorphCL2");
            _bufferManager.morphColorPyramidL3 = RTHandles.Alloc(l3w, l3h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_MorphCL3");
            _bufferManager.morphColorPyramidL4 = RTHandles.Alloc(l4w, l4h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_MorphCL4");
            _bufferManager.morphColorPyramidL5 = RTHandles.Alloc(l5w, l5h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_MorphCL5");
            _bufferManager.morphColorPyramidL6 = RTHandles.Alloc(l6w, l6h, colorFormat: fmtR16, enableRandomWrite: true, name: "PCD_MorphCL6");
        }
    }

    private void ReleaseInternalHandles()
    {
        _bufferManager._colorMapHandle?.Release();
        _bufferManager._depthMapHandle?.Release();
        _bufferManager._viewPositionMapHandle?.Release();
        _bufferManager._originTypeMapHandle?.Release();
        _bufferManager._gridZMinMapHandle?.Release();
        _bufferManager._densityMapHandle?.Release();
        _bufferManager._gridLevelMapHandle?.Release();
        _bufferManager._filteredGridLevelMapHandle?.Release();
        _bufferManager._neighborhoodSizeMapHandle?.Release();
        _bufferManager._correctedNeighborhoodSizeMapHandle?.Release();
        _bufferManager._occlusionResultMapHandle?.Release();
        _bufferManager._occlusionValueMapHandle?.Release();
        _bufferManager._debugDisplayMapHandle?.Release();
        _bufferManager._neighborCountMapHandle?.Release();
        _bufferManager._integratedDepthMapHandle?.Release();
        _bufferManager._neighborhoodMapHandle?.Release();
        _bufferManager._finalImageHandle?.Release();
        _bufferManager.depthPyramidL1?.Release();
        _bufferManager.depthPyramidL2?.Release();
        _bufferManager.depthPyramidL3?.Release();
        _bufferManager.depthPyramidL4?.Release();
        _bufferManager.depthPyramidL5?.Release();
        _bufferManager.depthPyramidL6?.Release();

        if (_bufferManager.pullPushPyramid != null) {
            foreach (var handle in _bufferManager.pullPushPyramid) {
                handle?.Release();
            }
        }

        _bufferManager._morphColorTempHandle?.Release();
        _bufferManager._morphTypeTempHandle?.Release();

        _bufferManager.morphTypePyramidL1?.Release();
        _bufferManager.morphTypePyramidL2?.Release();
        _bufferManager.morphTypePyramidL3?.Release();
        _bufferManager.morphTypePyramidL4?.Release();
        _bufferManager.morphTypePyramidL5?.Release();
        _bufferManager.morphTypePyramidL6?.Release();

        _bufferManager.morphColorPyramidL1?.Release();
        _bufferManager.morphColorPyramidL2?.Release();
        _bufferManager.morphColorPyramidL3?.Release();
        _bufferManager.morphColorPyramidL4?.Release();
        _bufferManager.morphColorPyramidL5?.Release();
        _bufferManager.morphColorPyramidL6?.Release();
    }
}

