using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PCDRendererFeature : ScriptableRendererFeature
{
    public static PCDRendererFeature Instance { get; private set; }

    public enum PCV_OcclusionKernel
    {
        Bouchiba = 0,
        Exponential = 1,
        Linear = 2
    }

    public enum PCV_OcclusionBinning
    {
        Soft = 0,
        Hard = 1
    }

    public enum PCV_OcclusionDirectionCount
    {
        Single = 1,
        Bins3 = 3,
        Bins6 = 6,
        Bins8 = 8 // 8方向分割の追加
    }

    public enum PCV_HoleFillingMethod
    {
        None = 0,
        JointBilateral = 1,
        PullPush = 2,
        Morphology = 3
    }

    [System.Serializable]
    public struct PCDRenderSettings
    {
        public PCV_OcclusionKernel kernelType;
        public PCV_OcclusionBinning binningMethod;
        public PCV_OcclusionDirectionCount directionCount;

        public float exponentAlpha;
        public float densityThreshold_e;
        public float neighborhoodParam_p_prime;
        public bool enableGradientCorrection;
        public float gradientThreshold_g_th;
        [Range(0f, 1f)] public float occlusionThreshold;
        [Range(0f, 1f)] public float occlusionFadeWidth;
        public bool enablePixelTagMap;
        public bool enableOcclusionMap;
        public bool recordOcclusionDebugMap;
        public bool recordPixelTagMap;
        public bool recordIntegratedDepthMap;
        public bool recordNeighborhoodMap;
        public bool recordNeighborCountMap;

        public bool enableVirtualDepthIntegration;

        public bool enableTagBasedOptimization;   // ① タグに基づく探索スキップ
        public bool enableTypeAwareDensity;       // ② 仮想物体を区別した密度計算
        public bool enableSoftOcclusionFade;      // ③ ソフトオクルージョン (FadeWidth)
        public PCV_HoleFillingMethod holeFillingMethod; // ④ エッジ保持型ホールフィリング手法

        [Range(1, 15)] public int morphKernelHalfSize;
        [Range(0, 5)] public int morphErodeIterations;
        [Range(1, 5)] public int morphDilateIterations;

        [HideInInspector] public uint _dynamicMultiplierRuntimeValue;
    }

    // 登録された静的メッシュの情報を保持するためのクラス
    private class RegisteredObject
    {
        public Mesh mesh;
        public Transform transform;
        public PCDProcessingMode mode;
    }

    [Header("Required Assets")]
    public ComputeShader pointCloudCompute;

    // ローカルでのフォールバック用の設定情報
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

    public PCV_OcclusionKernel kernelType
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.kernelType : _fallbackSettings.kernelType;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.kernelType = value;
            else _fallbackSettings.kernelType = value;
        }
    }

    public PCV_OcclusionBinning binningMethod
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.binningMethod : _fallbackSettings.binningMethod;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.binningMethod = value;
            else _fallbackSettings.binningMethod = value;
        }
    }

    public PCV_OcclusionDirectionCount directionCount
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.directionCount : _fallbackSettings.directionCount;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.directionCount = value;
            else _fallbackSettings.directionCount = value;
        }
    }

    public float exponentAlpha
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.exponentAlpha : _fallbackSettings.exponentAlpha;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.exponentAlpha = value;
            else _fallbackSettings.exponentAlpha = value;
        }
    }

    public float densityThreshold_e
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.densityThreshold_e : _fallbackSettings.densityThreshold_e;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.densityThreshold_e = value;
            else _fallbackSettings.densityThreshold_e = value;
        }
    }

    public float neighborhoodParam_p_prime
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.neighborhoodParam_p_prime : _fallbackSettings.neighborhoodParam_p_prime;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.neighborhoodParam_p_prime = value;
            else _fallbackSettings.neighborhoodParam_p_prime = value;
        }
    }

    public bool enableGradientCorrection
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.enableGradientCorrection : _fallbackSettings.enableGradientCorrection;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.enableGradientCorrection = value;
            else _fallbackSettings.enableGradientCorrection = value;
        }
    }

    public float gradientThreshold_g_th
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.gradientThreshold_g_th : _fallbackSettings.gradientThreshold_g_th;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.gradientThreshold_g_th = value;
            else _fallbackSettings.gradientThreshold_g_th = value;
        }
    }

    public float occlusionThreshold
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.occlusionThreshold : _fallbackSettings.occlusionThreshold;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.occlusionThreshold = value;
            else _fallbackSettings.occlusionThreshold = value;
        }
    }

    public float occlusionFadeWidth
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.occlusionFadeWidth : _fallbackSettings.occlusionFadeWidth;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.occlusionFadeWidth = value;
            else _fallbackSettings.occlusionFadeWidth = value;
        }
    }

    public bool enablePixelTagMap
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.enablePixelTagMap : _fallbackSettings.enablePixelTagMap;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.enablePixelTagMap = value;
            else _fallbackSettings.enablePixelTagMap = value;
        }
    }

    public bool enableOcclusionMap
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.enableOcclusionMap : _fallbackSettings.enableOcclusionMap;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.enableOcclusionMap = value;
            else _fallbackSettings.enableOcclusionMap = value;
        }
    }

    public bool recordOcclusionDebugMap
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.recordOcclusionDebugMap : _fallbackSettings.recordOcclusionDebugMap;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.recordOcclusionDebugMap = value;
            else _fallbackSettings.recordOcclusionDebugMap = value;
        }
    }

    public bool recordPixelTagMap
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.recordPixelTagMap : _fallbackSettings.recordPixelTagMap;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.recordPixelTagMap = value;
            else _fallbackSettings.recordPixelTagMap = value;
        }
    }

    public bool recordIntegratedDepthMap
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.recordIntegratedDepthMap : _fallbackSettings.recordIntegratedDepthMap;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.recordIntegratedDepthMap = value;
            else _fallbackSettings.recordIntegratedDepthMap = value;
        }
    }

    public bool recordNeighborhoodMap
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.recordNeighborhoodMap : _fallbackSettings.recordNeighborhoodMap;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.recordNeighborhoodMap = value;
            else _fallbackSettings.recordNeighborhoodMap = value;
        }
    }

    public bool recordNeighborCountMap
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.recordNeighborCountMap : _fallbackSettings.recordNeighborCountMap;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.recordNeighborCountMap = value;
            else _fallbackSettings.recordNeighborCountMap = value;
        }
    }

    public bool enableVirtualDepthIntegration
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.enableVirtualDepthIntegration : _fallbackSettings.enableVirtualDepthIntegration;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.enableVirtualDepthIntegration = value;
            else _fallbackSettings.enableVirtualDepthIntegration = value;
        }
    }

    public bool enableTagBasedOptimization
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.enableTagBasedOptimization : _fallbackSettings.enableTagBasedOptimization;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.enableTagBasedOptimization = value;
            else _fallbackSettings.enableTagBasedOptimization = value;
        }
    }

    public bool enableTypeAwareDensity
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.enableTypeAwareDensity : _fallbackSettings.enableTypeAwareDensity;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.enableTypeAwareDensity = value;
            else _fallbackSettings.enableTypeAwareDensity = value;
        }
    }

    public bool enableSoftOcclusionFade
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.enableSoftOcclusionFade : _fallbackSettings.enableSoftOcclusionFade;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.enableSoftOcclusionFade = value;
            else _fallbackSettings.enableSoftOcclusionFade = value;
        }
    }

    public PCV_HoleFillingMethod holeFillingMethod
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.holeFillingMethod : _fallbackSettings.holeFillingMethod;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.holeFillingMethod = value;
            else _fallbackSettings.holeFillingMethod = value;
        }
    }

    public int morphKernelHalfSize
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.morphKernelHalfSize : _fallbackSettings.morphKernelHalfSize;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.morphKernelHalfSize = value;
            else _fallbackSettings.morphKernelHalfSize = value;
        }
    }

    public int morphErodeIterations
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.morphErodeIterations : _fallbackSettings.morphErodeIterations;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.morphErodeIterations = value;
            else _fallbackSettings.morphErodeIterations = value;
        }
    }

    public int morphDilateIterations
    {
        get => PCDRenderController.Instance != null ? PCDRenderController.Instance.morphDilateIterations : _fallbackSettings.morphDilateIterations;
        set
        {
            if (PCDRenderController.Instance != null) PCDRenderController.Instance.morphDilateIterations = value;
            else _fallbackSettings.morphDilateIterations = value;
        }
    }

    private PCDRenderPass _scriptablePass;

    private bool _useGlobalBufferMode = false;
    public bool IsGlobalBufferMode => _useGlobalBufferMode;

    private static List<RegisteredObject> _persistentObjects = new List<RegisteredObject>();

    public void SetUseGlobalBuffer(bool enable)
    {
        _useGlobalBufferMode = enable;
    }

    // Inspectorで設定されている値を構造体として取得する
    private PCDRenderSettings GetSettings()
    {
        if (PCDRenderController.Instance != null)
        {
            return PCDRenderController.Instance.GetSettings();
        }

        var settings = _fallbackSettings;
        settings._dynamicMultiplierRuntimeValue = _internalDynamicMultiplier;
        return settings;
    }

    [HideInInspector] public uint _internalDynamicMultiplier = 1;

    // レンダラー特徴の初期化時に呼ばれる
    public override void Create()
    {
        Instance = this;

        _scriptablePass?.Cleanup();

        // レンダリングパスのインスタンスを生成し、実行タイミングを設定
        _scriptablePass = new PCDRenderPass(this.pointCloudCompute, GetSettings());
        _scriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

        // パス再生成時にも既存の登録済みメッシュ情報を引き継ぐ
        SyncPersistentObjectsToPass();
    }

    // 内部リストに保持している静的メッシュをスクリプタブルパスへ再登録する
    private void SyncPersistentObjectsToPass()
    {
        if (_scriptablePass == null) return;

        for (int i = _persistentObjects.Count - 1; i >= 0; i--)
        {
            var obj = _persistentObjects[i];
            if (obj.mesh != null && obj.transform != null)
            {
                _scriptablePass.AddStaticMesh(obj.mesh, obj.transform, obj.mode);
            }
            else
            {
                _persistentObjects.RemoveAt(i);
            }
        }
    }

    // オクルージョン用の静的メッシュを追加登録する
    public void AddStaticMesh(Mesh mesh, Transform transform, PCDProcessingMode mode)
    {
        if (mesh == null || transform == null) return;

        // 既に登録されているか確認し、無い場合は追加、ある場合はモードを更新
        var existing = _persistentObjects.Find(x => x.mesh == mesh && x.transform == transform);
        if (existing == null)
        {
            _persistentObjects.Add(new RegisteredObject { mesh = mesh, transform = transform, mode = mode });
        }
        else
        {
            existing.mode = mode;
        }

        // 実際の描画パスにもメッシュ情報を渡す
        _scriptablePass?.AddStaticMesh(mesh, transform, mode);
    }

    // 登録された静的メッシュを削除する
    public void RemoveStaticMesh(Mesh mesh, Transform transform)
    {
        _persistentObjects.RemoveAll(x => x.mesh == mesh && x.transform == transform);
        _scriptablePass?.RemoveStaticMesh(mesh, transform);
    }

    // 動的オブジェクト用にデータ再構築をリクエストする
    public void MarkPointCloudDataDirty()
    {
        _scriptablePass?.MarkPointCloudDataDirty();
    }

    // t[??ARenderGraph?pXGL[
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // 毎フレーム、メッシュのカリング設定やレイヤーを強制適用する
        EnforceSettingsEveryFrame();

        if (pointCloudCompute == null)
        {
            return;
        }

        if (_scriptablePass != null)
        {
            // Inspectorでの変更をパスに反映
            _scriptablePass.UpdateSettings(GetSettings());
            _scriptablePass.SetDebugFlags(enablePixelTagMap, enableOcclusionMap);
        }

        // 常時パスをエンキューし、描画をスキップするかどうかはRecordRenderGraph内や内部ロジックに委ねる
        renderer.EnqueuePass(_scriptablePass);
    }

    // 登録されたすべてのオブジェクトに対して、設定（BoundsやLayer）が正しく適用されているか確認する
    private void EnforceSettingsEveryFrame()
    {
        for (int i = _persistentObjects.Count - 1; i >= 0; i--)
        {
            var obj = _persistentObjects[i];
            // オブジェクトが破棄されていたらリストから削除
            if (obj.mesh == null || obj.transform == null)
            {
                _persistentObjects.RemoveAt(i);
                continue;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _scriptablePass?.Cleanup();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetPointCloudData(PCV_Data data)
    {
        if (_scriptablePass != null)
        {
            _scriptablePass.SetPointCloudData(data);
        }
    }

    public void SetExternalBuffer(ComputeBuffer buffer, int count)
    {
        if (_scriptablePass != null)
        {
            _scriptablePass.SetExternalBuffer(buffer, count);
        }
    }

    public Texture GetDebugDisplayMap() => _scriptablePass?.GetDebugDisplayMap();

    // ==========================================
    // インスペクターの値が変更された時に自動で呼ばれる検証関数
    // ==========================================
    private void OnValidate()
    {
        if (PCDRenderController.Instance != null)
        {
            float maxFade = Mathf.Min(PCDRenderController.Instance.occlusionThreshold, 1.0f - PCDRenderController.Instance.occlusionThreshold) * 2.0f;
            PCDRenderController.Instance.occlusionFadeWidth = Mathf.Clamp(PCDRenderController.Instance.occlusionFadeWidth, 0f, maxFade);
        }
        else
        {
            float maxFadeWidth = Mathf.Min(_fallbackSettings.occlusionThreshold, 1.0f - _fallbackSettings.occlusionThreshold) * 2.0f;
            _fallbackSettings.occlusionFadeWidth = Mathf.Clamp(_fallbackSettings.occlusionFadeWidth, 0f, maxFadeWidth);
        }
    }
}