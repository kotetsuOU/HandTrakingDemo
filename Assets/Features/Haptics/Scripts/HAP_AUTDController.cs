using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
#if !USE_AUTD3_LEGACY
using System.Threading.Tasks;
using AUTD3;
using AUTD3.Holo;
#else
using AUTD3Sharp;
using AUTD3Sharp.Link;
using AUTD3Sharp.Driver.Datagram;
using AUTD3Sharp.Gain;
#endif

#nullable enable

public enum HapticsGenerationMode
{
    Simplified,
    Precision
}

public enum AUTDLinkType
{
    TwinCAT,
    SOEM,
    Simulator
}

/// <summary>
/// HCD_Pipeline によって計算された接触重心を受け取り、
/// AUTD3デバイス群に GSPAT (Acoustic Holography) 等を用いてマルチフォーカス出力を行うコントローラー。
/// 
/// 公式AUTD3SharpのC#ネイティブラッパーとして機能し、ハードウェア接続・制御は HAP_AUTDHardwareManager に委譲します。
/// 
/// ※ ファイル分割構成:
/// - HAP_AUTDController.cs : コアロジック（Inspector設定、Awake/Update）
/// - HAP_AUTDController_Config.cs : ハードウェア設定監視の委譲
/// - HAP_AUTDController_Haptics.cs : 接触クラスタからの触覚データ(STM/Sequential)生成・送信
/// - HAP_AUTDController_API.cs : 外部スクリプトからの手動操作API
/// </summary>
public partial class HAP_AUTDController : MonoBehaviour
{
    [Header("Hardware Reference")]
    [Tooltip("AUTDデバイスの物理接続およびハードウェア設定を管理するマネージャー。未指定時は自動取得します。")]
    public HAP_AUTDHardwareManager hardwareManager = null!;

    [Header("Dependencies")]
    [Tooltip("接触判定を行う HCD_Pipeline の参照。自動モード時に毎フレームここからクラスタ情報を取得します。")]
    public HCD_Pipeline hcdPipeline = null!;

    [UnityEngine.Serialization.FormerlySerializedAs("foxFootHapticsController")]
    [Tooltip("オブジェクトのハプティクス制御コンポーネント。これがアタッチされ有効な場合、標準の衝突判定による触覚の代わりにオブジェクトの位置へ照射します。")]
    public HAP_BaseObjectHapticsController? objectHapticsController;

    [HideInInspector]
    public bool bypassHaptics = false;

    // 後方互換性のための HardwareManager 委譲プロパティ
    public AUTDLinkType linkType
    {
        get => hardwareManager != null ? hardwareManager.linkType : AUTDLinkType.TwinCAT;
        set { if (hardwareManager != null) hardwareManager.linkType = value; }
    }
    public string soemAdapterName
    {
        get => hardwareManager != null ? hardwareManager.soemAdapterName : "";
        set { if (hardwareManager != null) hardwareManager.soemAdapterName = value; }
    }
    public float temperature
    {
        get => hardwareManager != null ? hardwareManager.temperature : 25f;
        set { if (hardwareManager != null) hardwareManager.temperature = value; }
    }
    public bool enableFan
    {
        get => hardwareManager != null ? hardwareManager.enableFan : false;
        set { if (hardwareManager != null) hardwareManager.enableFan = value; }
    }
    public ModulationMode modulationMode
    {
        get => hardwareManager != null ? hardwareManager.modulationMode : ModulationMode.Sine;
        set { if (hardwareManager != null) hardwareManager.modulationMode = value; }
    }
    public float sineFrequency
    {
        get => hardwareManager != null ? hardwareManager.sineFrequency : 150f;
        set { if (hardwareManager != null) hardwareManager.sineFrequency = value; }
    }
    public float staticAmplitude
    {
        get => hardwareManager != null ? hardwareManager.staticAmplitude : 1.0f;
        set { if (hardwareManager != null) hardwareManager.staticAmplitude = value; }
    }
    public SilencerMode silencerMode
    {
        get => hardwareManager != null ? hardwareManager.silencerMode : SilencerMode.FixedUpdateRate;
        set { if (hardwareManager != null) hardwareManager.silencerMode = value; }
    }
    public ushort silencerStepPhase
    {
        get => hardwareManager != null ? hardwareManager.silencerStepPhase : (ushort)500;
        set { if (hardwareManager != null) hardwareManager.silencerStepPhase = value; }
    }
    public ushort silencerStepAmplitude
    {
        get => hardwareManager != null ? hardwareManager.silencerStepAmplitude : (ushort)65535;
        set { if (hardwareManager != null) hardwareManager.silencerStepAmplitude = value; }
    }

    private readonly object _fallbackLock = new object();
    public object _sendLock => hardwareManager != null ? hardwareManager.SendLock : _fallbackLock;

    public void ApplyModulation() { if (hardwareManager != null) hardwareManager.ApplyModulation(); }
    public void ApplySilencer() { if (hardwareManager != null) hardwareManager.ApplySilencer(); }
    public void ApplyFan() { if (hardwareManager != null) hardwareManager.ApplyFan(); }
#if USE_AUTD3_LEGACY
    public void ApplyTemperature() { if (hardwareManager != null) hardwareManager.ApplyTemperature(); }
#endif

    [Header("Operation Settings")]
    [Tooltip("Simplified: 1クラスタ1点の単純出力(軽量)。\nPrecision: 楕円やランダムノイズなどリッチな表現を使用します。")]
    public HapticsGenerationMode generationMode = HapticsGenerationMode.Simplified;

    [Header("Precision Sources")]
    [Tooltip("接触領域の「重心」に対して基本的な超音波の焦点を生成するソース設定")]
    public HAP_HapticsCentroidSource centroidSource = new HAP_HapticsCentroidSource();
    
    [Tooltip("接触領域の「形状」を主成分分析(PCA)し、楕円状になぞるSTMを生成するソース設定")]
    public HAP_HapticsEllipseSource ellipseSource = new HAP_HapticsEllipseSource();
    
    [Tooltip("接触領域内でランダムに16点をサンプリングし、不規則に飛び回るSTM（ザラザラ感）を生成するソース設定")]
    public HAP_HapticsRandomSource randomSource = new HAP_HapticsRandomSource();

    [Header("Acoustic Settings")]
    [Tooltip("ホログラフィアルゴリズム。\nGSPAT: 高速・高品質。\nNaive: 計算は軽いが音圧や精度が落ちます。")]
    public HoloAlgorithm holoAlgorithm = HoloAlgorithm.GSPAT;
    
    [Tooltip("超音波の出力強度 (Pascal)。最大で 10000 程度。大きすぎるとデバイスの保護回路が働くか、クリッピングが発生します。")]
    public float focusIntensityPascal = 10000f;

    [Header("Coordinate Settings")]
    [Tooltip("すべての焦点位置に加算されるオフセット。デバイスの原点とUnity上の位置を微調整するのに使います。")]
    public Vector3 offset = Vector3.zero;

    [Header("Directional Grouping")]
    [Tooltip("有効にすると、接触点の法線ベクトル（向き）とAUTDデバイスの向きを比較し、最適なデバイスからのみ超音波を照射します。")]
    public bool enableDirectionalGrouping = false;
    
    [Tooltip("デバイスが面の法線方向から何度まで傾いていても担当として許容するか（0〜90度）。0度で真正面のみ。")]
    [Range(0, 90)]
    public float directionalAngleThreshold = 45.0f;

    [Header("STM Settings")]
    [Tooltip("STMの種類を選択。FociSTM(ハードウェア計算・単焦点)、GainSTM(CPU計算・GSPAT等の複数焦点に対応)")]
    public HapticsSTMMode stmMode = HapticsSTMMode.FociSTM;

    [Tooltip("STMの再生周波数 (Hz)。")]
    public float stmFrequency = 150f;

    [Tooltip("単焦点計算に使用する内部ソルバー（Customアルゴリズム選択時等のフォールバック）。\nNaive: 単焦点向けに最適で素子数にO(N)。\nGSPAT: 多焦点向けの反復最適化計算で負荷が高い。")]
    public HoloSolverAlgorithm customInnerAlgorithm = HoloSolverAlgorithm.Naive;

#if USE_AUTD3_LEGACY
    [Tooltip("GainSTM時のモード。通常は PhaseIntensityFull を使用します。")]
    public GainSTMMode gainStmMode = GainSTMMode.PhaseIntensityFull;
#endif

    [Header("Debug")]
    [Tooltip("エディタ上でデバイスのサイズと位置を Gizmo (青色の枠) で表示します。")]
    public bool visualizeDevices = true;

    [Header("Performance Profiling")]
    [Tooltip("有効にすると、ハプティクスパイプラインの処理時間を計測します（Unity ProfilerMarker には毎フレーム記録されます）。")]
    public bool enableProfiling = false;

    [Tooltip("有効にすると、Sendを含む全処理をメインスレッドで同期実行します。\n" +
             "Profile Analyzer で CPU Usage の中央値を取る際に使用してください。\n" +
             "※ 有効時はフレームレートが低下します。論文計測時のみONにしてください。")]
    public bool synchronousSend = false;

    [Tooltip("Debug.Log への結果出力を有効にします。")]
    public bool enableLog = true;
    
    [Tooltip("Debug.Log に処理時間を出力する間隔（フレーム数）。1で毎フレーム出力。")]
    [Range(1, 600)]
    public int profilingLogInterval = 60;

    /// <summary>
    /// パフォーマンスプロファイラー。外部からも参照可能です。
    /// </summary>
    [HideInInspector]
    public HAP_AUTDPerformanceProfiler performanceProfiler = new HAP_AUTDPerformanceProfiler();

    public List<AUTD3Device> connectedDevices => hardwareManager != null ? hardwareManager.connectedDevices : new List<AUTD3Device>();

    [HideInInspector]
    public HAP_AUTDDebugDisabler? debugDisabler;

#if !USE_AUTD3_LEGACY
    public Client? _client => hardwareManager != null ? hardwareManager.Client : null;
    public Geometry? geometry => hardwareManager != null ? hardwareManager.Geometry : null;
#else
    public Controller? _autd => hardwareManager != null ? hardwareManager.Autd : null;
#endif

    private bool _isCurrentlyOff = true;
    
    private System.Threading.Tasks.Task? _hapticsSendTask = null;

    void Awake()
    {
        debugDisabler = GetComponent<HAP_AUTDDebugDisabler>();

        if (hardwareManager == null)
        {
            hardwareManager = GetComponent<HAP_AUTDHardwareManager>();
            if (hardwareManager == null)
            {
                hardwareManager = FindAnyObjectByType<HAP_AUTDHardwareManager>();
                if (hardwareManager == null)
                {
                    Debug.LogWarning("[HAP_AUTDController] HAP_AUTDHardwareManager is not assigned and was not found in the scene. Adding component automatically.");
                    hardwareManager = gameObject.AddComponent<HAP_AUTDHardwareManager>();
                }
            }
        }

        if (hcdPipeline == null)
        {
            hcdPipeline = FindAnyObjectByType<HCD_Pipeline>();
            if (hcdPipeline == null)
            {
                Debug.LogWarning("[HAP_AUTDController] HCD_Pipeline is not assigned and could not be found in the scene.");
            }
        }
    }

    void Update()
    {
        if (hardwareManager == null || !hardwareManager.IsConnected) return;

        // プロファイラー設定の同期
        performanceProfiler.Enabled = enableProfiling;
        performanceProfiler.LogEnabled = enableLog;
        performanceProfiler.LogInterval = profilingLogInterval;

        // インスペクターの設定変更を監視して適用（HAP_AUTDController_Config.cs -> hardwareManager）
        CheckForConfigChanges();
        
        // Modulation Override の解決
        ResolveModulationOverrides();

        UpdateHaptics();
    }

    private void OnDrawGizmos()
    {
        if (!visualizeDevices) return;

        var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None);
        
#if UNITY_EDITOR
        var disabler = debugDisabler != null ? debugDisabler : GetComponent<HAP_AUTDDebugDisabler>();
        HAP_GizmoVisualizer.DrawDevicesAndGroupings(
            devices, 
            enableDirectionalGrouping, 
            directionalAngleThreshold, 
            hcdPipeline,
            disabler
        );
#endif
    }
}

