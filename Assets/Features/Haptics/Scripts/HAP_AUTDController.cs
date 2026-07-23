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

/// <summary>
/// HCD_Pipeline によって計算された接触重心を受け取り、
/// AUTD3デバイス群に GSPAT (Acoustic Holography) 等を用いてマルチフォーカス出力を行うコントローラー。
/// 
/// 内部ロジックは非MonoBehaviourな純粋C#サービスクラス (HAP_AUTDLinkService / HAP_AUTDModulationService) にカプセル化され、
/// 神クラス化を防ぎつつ Serialize 項目の肥大化を防止します。
/// </summary>
public partial class HAP_AUTDController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("接触判定を行う HCD_Pipeline の参照。自動モード時に毎フレームここからクラスタ情報を取得します。")]
    public HCD_Pipeline hcdPipeline = null!;

    [UnityEngine.Serialization.FormerlySerializedAs("foxFootHapticsController")]
    [Tooltip("オブジェクトのハプティクス制御コンポーネント。これがアタッチされ有効な場合、標準の衝突判定による触覚の代わりにオブジェクトの位置へ照射します。")]
    public HAP_BaseObjectHapticsController? objectHapticsController;

    [HideInInspector]
    public bool bypassHaptics = false;

    [Header("Link Settings")]
    [Tooltip("AUTDデバイスとの接続方法を選択します")]
    public AUTDLinkType linkType = AUTDLinkType.TwinCAT;

    [Tooltip("SOEM使用時のネットワークアダプタ名（必要であれば指定）")]
    public string soemAdapterName = "";

    [Header("Hardware Environment")]
    [Tooltip("環境温度（摂氏）。音速計算に使用され、焦点の正確さに影響します。室温に合わせてください。")]
    public float temperature = 25f;

    [Tooltip("デバイス冷却ファンのON/OFF。高出力で長時間使用する場合は ON にしてください。")]
    public bool enableFan = false;

    [Header("Modulation Settings")]
    [Tooltip("変調モード。\nSine: 指定周波数で明滅（ブーンという感触）。\nStatic: 連続出力（押される感触）。")]
    public ModulationMode modulationMode = ModulationMode.Sine;

    [Tooltip("サイン波の変調周波数 (Hz)。一般的に人間の皮膚は 150〜200Hz で最も感度が高くなります。")]
    public float sineFrequency = 150f;

    [Tooltip("定常波(Static)の振幅 (0.0〜1.0)。通常は1.0を使用します。")]
    public float staticAmplitude = 1.0f;

    [Header("Silencer Settings")]
    [Tooltip("サイレンサーのモード。可聴ノイズ（ジージー音）を減らします。\nFixedUpdateRate: 強度と位相のステップで指定。\nFixedCompletionTime: 完了時間で指定。")]
    public SilencerMode silencerMode = SilencerMode.FixedUpdateRate;

    [Tooltip("位相の変化ステップ。小さいほど静かになりますが、応答が遅れます。")]
    public ushort silencerStepPhase = 500;

    [Tooltip("振幅の変化ステップ。小さいほど静かになりますが、応答が遅れます。")]
    public ushort silencerStepAmplitude = 65535;

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

    [HideInInspector]
    public HAP_AUTDDebugDisabler? debugDisabler;

    // 非Serializeなプライベートサービスクラスインスタンス
    private readonly HAP_AUTDLinkService _linkService = new HAP_AUTDLinkService();
    private HAP_AUTDModulationService? _configService;

    public HAP_AUTDLinkService LinkService => _linkService;

    // 後方互換性のための内部プロパティアクセス
    public object _sendLock => _linkService.SendLock;
    public List<AUTD3Device> connectedDevices => _linkService.ConnectedDevices;

#if !USE_AUTD3_LEGACY
    public Client? _client => _linkService.Client;
    public Geometry? geometry => _linkService.Geometry;
#else
    public Controller? _autd => _linkService.Autd;
#endif

    public void ApplyModulation() => _configService?.ApplyModulation(modulationMode, sineFrequency, staticAmplitude);
    public void ApplySilencer() => _configService?.ApplySilencer(silencerMode, silencerStepPhase, silencerStepAmplitude);
    public void ApplyFan() => _configService?.ApplyFan(enableFan);
    public void ApplyTemperature() => _configService?.ApplyTemperature(temperature);

    private bool _isCurrentlyOff = true;
    private System.Threading.Tasks.Task? _hapticsSendTask = null;

#if !USE_AUTD3_LEGACY
    async void Awake()
#else
    void Awake()
#endif
    {
        debugDisabler = GetComponent<HAP_AUTDDebugDisabler>();

        if (hcdPipeline == null)
        {
            hcdPipeline = FindAnyObjectByType<HCD_Pipeline>();
            if (hcdPipeline == null)
            {
                Debug.LogWarning("[HAP_AUTDController] HCD_Pipeline is not assigned and could not be found in the scene.");
            }
        }

        _configService = new HAP_AUTDModulationService(_linkService);

#if !USE_AUTD3_LEGACY
        await _linkService.OpenAsync(linkType, soemAdapterName);
#else
        _linkService.Open(linkType, soemAdapterName);
#endif

        if (_linkService.IsConnected)
        {
            _configService.CheckAndApply(
                modulationMode, sineFrequency, staticAmplitude,
                silencerMode, silencerStepPhase, silencerStepAmplitude,
                enableFan, temperature);
        }
    }

    void Update()
    {
        if (!_linkService.IsConnected) return;

        // プロファイラー設定の同期
        performanceProfiler.Enabled = enableProfiling;
        performanceProfiler.LogEnabled = enableLog;
        performanceProfiler.LogInterval = profilingLogInterval;

        // ハードウェア設定の差分監視・自動送信
        _configService?.CheckAndApply(
            modulationMode, sineFrequency, staticAmplitude,
            silencerMode, silencerStepPhase, silencerStepAmplitude,
            enableFan, temperature);

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

#if !USE_AUTD3_LEGACY
    private async void OnDestroy()
    {
        await _linkService.CloseAsync();
    }
#else
    private void OnDestroy()
    {
        _linkService.Close();
    }
#endif
}
