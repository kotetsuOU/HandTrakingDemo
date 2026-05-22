using UnityEngine;
using static PCDRendererFeature;

public class PCDSettingsBridge
{
    private PCDRenderSettings _fallbackSettings = new PCDRenderSettings
    {
        kernelType = PCV_OcclusionKernel.Bouchiba,
        binningMethod = PCV_OcclusionBinning.Soft,
        directionCount = PCV_OcclusionDirectionCount.Single,
        exponentAlpha = 0f,
        densityThreshold_e = 0.04f,
        neighborhoodParam_p_prime = 4.8f,
        enableGradientCorrection = true,
        gradientThreshold_g_th = 0.05f,
        occlusionThreshold = 0.8f,
        occlusionFadeWidth = 0.1f,
        enablePixelTagMap = false,
        enableOcclusionMap = false,
        recordOcclusionDebugMap = false,
        recordPixelTagMap = false,
        recordIntegratedDepthMap = false,
        recordNeighborhoodMap = false,
        recordNeighborCountMap = false,
        enableVirtualDepthIntegration = true,
        enableTagBasedOptimization = true,
        enableTypeAwareDensity = true,
        enableSoftOcclusionFade = true,
        holeFillingMethod = PCV_HoleFillingMethod.JointBilateral,
        morphKernelHalfSize = 1,
        morphErodeIterations = 0,
        morphDilateIterations = 1
    };

    private PCDOcclusionPipelineController Controller => PCDOcclusionPipelineController.Instance;

    public PCV_OcclusionKernel kernelType
    {
        get => Controller != null ? Controller.kernelType : _fallbackSettings.kernelType;
        set
        {
            if (Controller != null) Controller.kernelType = value;
            else _fallbackSettings.kernelType = value;
        }
    }

    public PCV_OcclusionBinning binningMethod
    {
        get => Controller != null ? Controller.binningMethod : _fallbackSettings.binningMethod;
        set
        {
            if (Controller != null) Controller.binningMethod = value;
            else _fallbackSettings.binningMethod = value;
        }
    }

    public PCV_OcclusionDirectionCount directionCount
    {
        get => Controller != null ? Controller.directionCount : _fallbackSettings.directionCount;
        set
        {
            if (Controller != null) Controller.directionCount = value;
            else _fallbackSettings.directionCount = value;
        }
    }

    public float exponentAlpha
    {
        get => Controller != null ? Controller.exponentAlpha : _fallbackSettings.exponentAlpha;
        set
        {
            if (Controller != null) Controller.exponentAlpha = value;
            else _fallbackSettings.exponentAlpha = value;
        }
    }

    public float densityThreshold_e
    {
        get => Controller != null ? Controller.densityThreshold_e : _fallbackSettings.densityThreshold_e;
        set
        {
            if (Controller != null) Controller.densityThreshold_e = value;
            else _fallbackSettings.densityThreshold_e = value;
        }
    }

    public float neighborhoodParam_p_prime
    {
        get => Controller != null ? Controller.neighborhoodParam_p_prime : _fallbackSettings.neighborhoodParam_p_prime;
        set
        {
            if (Controller != null) Controller.neighborhoodParam_p_prime = value;
            else _fallbackSettings.neighborhoodParam_p_prime = value;
        }
    }

    public bool enableGradientCorrection
    {
        get => Controller != null ? Controller.enableGradientCorrection : _fallbackSettings.enableGradientCorrection;
        set
        {
            if (Controller != null) Controller.enableGradientCorrection = value;
            else _fallbackSettings.enableGradientCorrection = value;
        }
    }

    public float gradientThreshold_g_th
    {
        get => Controller != null ? Controller.gradientThreshold_g_th : _fallbackSettings.gradientThreshold_g_th;
        set
        {
            if (Controller != null) Controller.gradientThreshold_g_th = value;
            else _fallbackSettings.gradientThreshold_g_th = value;
        }
    }

    public float occlusionThreshold
    {
        get => Controller != null ? Controller.occlusionThreshold : _fallbackSettings.occlusionThreshold;
        set
        {
            if (Controller != null) Controller.occlusionThreshold = value;
            else _fallbackSettings.occlusionThreshold = value;
        }
    }

    public float occlusionFadeWidth
    {
        get => Controller != null ? Controller.occlusionFadeWidth : _fallbackSettings.occlusionFadeWidth;
        set
        {
            if (Controller != null) Controller.occlusionFadeWidth = value;
            else _fallbackSettings.occlusionFadeWidth = value;
        }
    }

    public bool enablePixelTagMap
    {
        get => Controller != null ? Controller.enablePixelTagMap : _fallbackSettings.enablePixelTagMap;
        set
        {
            if (Controller != null) Controller.enablePixelTagMap = value;
            else _fallbackSettings.enablePixelTagMap = value;
        }
    }

    public bool enableOcclusionMap
    {
        get => Controller != null ? Controller.enableOcclusionMap : _fallbackSettings.enableOcclusionMap;
        set
        {
            if (Controller != null) Controller.enableOcclusionMap = value;
            else _fallbackSettings.enableOcclusionMap = value;
        }
    }

    public bool recordOcclusionDebugMap
    {
        get => Controller != null ? Controller.recordOcclusionDebugMap : _fallbackSettings.recordOcclusionDebugMap;
        set
        {
            if (Controller != null) Controller.recordOcclusionDebugMap = value;
            else _fallbackSettings.recordOcclusionDebugMap = value;
        }
    }

    public bool recordPixelTagMap
    {
        get => Controller != null ? Controller.recordPixelTagMap : _fallbackSettings.recordPixelTagMap;
        set
        {
            if (Controller != null) Controller.recordPixelTagMap = value;
            else _fallbackSettings.recordPixelTagMap = value;
        }
    }

    public bool recordIntegratedDepthMap
    {
        get => Controller != null ? Controller.recordIntegratedDepthMap : _fallbackSettings.recordIntegratedDepthMap;
        set
        {
            if (Controller != null) Controller.recordIntegratedDepthMap = value;
            else _fallbackSettings.recordIntegratedDepthMap = value;
        }
    }

    public bool recordNeighborhoodMap
    {
        get => Controller != null ? Controller.recordNeighborhoodMap : _fallbackSettings.recordNeighborhoodMap;
        set
        {
            if (Controller != null) Controller.recordNeighborhoodMap = value;
            else _fallbackSettings.recordNeighborhoodMap = value;
        }
    }

    public bool recordNeighborCountMap
    {
        get => Controller != null ? Controller.recordNeighborCountMap : _fallbackSettings.recordNeighborCountMap;
        set
        {
            if (Controller != null) Controller.recordNeighborCountMap = value;
            else _fallbackSettings.recordNeighborCountMap = value;
        }
    }

    public bool enableVirtualDepthIntegration
    {
        get => Controller != null ? Controller.enableVirtualDepthIntegration : _fallbackSettings.enableVirtualDepthIntegration;
        set
        {
            if (Controller != null) Controller.enableVirtualDepthIntegration = value;
            else _fallbackSettings.enableVirtualDepthIntegration = value;
        }
    }

    public bool enableTagBasedOptimization
    {
        get => Controller != null ? Controller.enableTagBasedOptimization : _fallbackSettings.enableTagBasedOptimization;
        set
        {
            if (Controller != null) Controller.enableTagBasedOptimization = value;
            else _fallbackSettings.enableTagBasedOptimization = value;
        }
    }

    public bool enableTypeAwareDensity
    {
        get => Controller != null ? Controller.enableTypeAwareDensity : _fallbackSettings.enableTypeAwareDensity;
        set
        {
            if (Controller != null) Controller.enableTypeAwareDensity = value;
            else _fallbackSettings.enableTypeAwareDensity = value;
        }
    }

    public bool enableSoftOcclusionFade
    {
        get => Controller != null ? Controller.enableSoftOcclusionFade : _fallbackSettings.enableSoftOcclusionFade;
        set
        {
            if (Controller != null) Controller.enableSoftOcclusionFade = value;
            else _fallbackSettings.enableSoftOcclusionFade = value;
        }
    }

    public PCV_HoleFillingMethod holeFillingMethod
    {
        get => Controller != null ? Controller.holeFillingMethod : _fallbackSettings.holeFillingMethod;
        set
        {
            if (Controller != null) Controller.holeFillingMethod = value;
            else _fallbackSettings.holeFillingMethod = value;
        }
    }

    public int morphKernelHalfSize
    {
        get => Controller != null ? Controller.morphKernelHalfSize : _fallbackSettings.morphKernelHalfSize;
        set
        {
            if (Controller != null) Controller.morphKernelHalfSize = value;
            else _fallbackSettings.morphKernelHalfSize = value;
        }
    }

    public int morphErodeIterations
    {
        get => Controller != null ? Controller.morphErodeIterations : _fallbackSettings.morphErodeIterations;
        set
        {
            if (Controller != null) Controller.morphErodeIterations = value;
            else _fallbackSettings.morphErodeIterations = value;
        }
    }

    public int morphDilateIterations
    {
        get => Controller != null ? Controller.morphDilateIterations : _fallbackSettings.morphDilateIterations;
        set
        {
            if (Controller != null) Controller.morphDilateIterations = value;
            else _fallbackSettings.morphDilateIterations = value;
        }
    }

    public PCDRenderSettings GetSettings(uint internalDynamicMultiplier)
    {
        if (Controller != null)
        {
            return Controller.GetSettings();
        }

        var settings = _fallbackSettings;
        settings._dynamicMultiplierRuntimeValue = internalDynamicMultiplier;
        return settings;
    }

    public void OnValidate()
    {
        if (Controller != null)
        {
            float maxFade = Mathf.Min(Controller.occlusionThreshold, 1.0f - Controller.occlusionThreshold) * 2.0f;
            Controller.occlusionFadeWidth = Mathf.Clamp(Controller.occlusionFadeWidth, 0f, maxFade);
        }
        else
        {
            float maxFadeWidth = Mathf.Min(_fallbackSettings.occlusionThreshold, 1.0f - _fallbackSettings.occlusionThreshold) * 2.0f;
            _fallbackSettings.occlusionFadeWidth = Mathf.Clamp(_fallbackSettings.occlusionFadeWidth, 0f, maxFadeWidth);
        }
    }
}
