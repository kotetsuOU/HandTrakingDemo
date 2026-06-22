using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class PCDRendererFeature : ScriptableRendererFeature
{
    public static PCDRendererFeature Instance { get; private set; }

    public enum PCD_OcclusionKernel
    {
        Bouchiba = 0,
        Exponential = 1,
        Linear = 2,
        Skip = 3,
        DepthOnly = 4
    }

    public enum PCD_OcclusionEvaluationMode
    {
        Average = 0,
        SectorThreshold = 1
    }

    public enum PCD_HoleFillingMethod
    {
        None = 0,
        JointBilateral = 1,
        PullPush = 2,
        Morphology_OC = 3, // Opening-Closing
        Morphology_CO = 4  // Closing-Opening
    }

    public enum PCD_GridSize
    {
        Grid8x8 = 8,
        Grid16x16 = 16,
        Grid32x32 = 32
    }

    [System.Serializable]
    public struct PCDRenderSettings
    {
        public PCD_OcclusionKernel kernelType;
        public PCD_OcclusionEvaluationMode evaluationMode;
        [Range(1, 8)] public int minOccludedSectors;
        [Range(0, 6)] public int minSearchLevel;

        public float exponentAlpha;
        public float densityThreshold_e;
        public float neighborhoodParam_p_prime;
        public bool enableDensityBasedLOD;
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
        public PCD_HoleFillingMethod holeFillingMethod; // ④ エッジ保持型ホールフィリング手法

        public PCD_GridSize gridSize;             // グリッドサイズ (最適化・検証用)

        [Range(1, 25)] public int morphKernelHalfSize;
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

    public PCDSettingsBridge settings { get; private set; }

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
        if (settings == null)
        {
            settings = new PCDSettingsBridge();
        }
        return settings.GetSettings(_internalDynamicMultiplier);
    }

    [HideInInspector] public uint _internalDynamicMultiplier = 1;
    public uint LastFrameVirtualMeshPixelCount { get; set; } = 1;

    // レンダラー特徴の初期化時に呼ばれる
    public override void Create()
    {
        Instance = this;

        if (settings == null)
        {
            settings = new PCDSettingsBridge();
        }

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
        // Sceneビューなど解像度の異なるカメラが混ざることで、RTHandleが毎フレーム破棄・再構築されるのを防ぐため、Game/VRのみ許可する
        if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.VR)
        {
            return;
        }

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
            if (settings == null)
            {
                settings = new PCDSettingsBridge();
            }
            _scriptablePass.SetDebugFlags(settings.enablePixelTagMap, settings.enableOcclusionMap);
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
        if (settings == null)
        {
            settings = new PCDSettingsBridge();
        }
        settings.OnValidate();
    }
}