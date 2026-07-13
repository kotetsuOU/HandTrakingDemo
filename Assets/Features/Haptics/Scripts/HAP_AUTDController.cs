using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using AUTD3Sharp;
using AUTD3Sharp.Link;
using AUTD3Sharp.Driver.Datagram;
using AUTD3Sharp.Gain;

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
/// 公式AUTD3SharpのC#ネイティブラッパーとして機能します。
/// 
/// ※ ファイル分割構成:
/// - HAP_AUTDController.cs : コアロジック（Inspector設定、Awake/Update/OnDestroy）
/// - HAP_AUTDController_Config.cs : ハードウェア設定の適用（Modulation, Silencer, Fan など）
/// - HAP_AUTDController_Haptics.cs : 接触クラスタからの触覚データ(STM/Sequential)生成
/// - HAP_AUTDController_API.cs : 外部スクリプトからの手動操作API
/// </summary>
public partial class HAP_AUTDController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("接触判定を行う HCD_Pipeline の参照。自動モード時に毎フレームここからクラスタ情報を取得します。")]
    public HCD_Pipeline hcdPipeline = null!;

    [HideInInspector]
    public bool bypassHaptics = false;

    [Header("Link Settings")]
    [Tooltip("AUTDデバイスとの接続方法を選択します")]
    public AUTDLinkType linkType = AUTDLinkType.TwinCAT;

    [Tooltip("SOEM使用時のネットワークアダプタ名（必要であれば指定）")]
    public string soemAdapterName = "";

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

    [Header("Hardware Settings")]
    [Tooltip("環境温度（摂氏）。音速計算に使用され、焦点の正確さに影響します。室温に合わせてください。")]
    public float temperature = 25f;
    
    [Tooltip("デバイス冷却ファンのON/OFF。高出力で長時間使用する場合は ON にしてください。")]
    public bool enableFan = false;

    [Header("Coordinate Settings")]
    [Tooltip("すべての焦点位置に加算されるオフセット。デバイスの原点とUnity上の位置を微調整するのに使います。")]
    public Vector3 offset = Vector3.zero;

    [Header("Directional Grouping")]
    [Tooltip("有効にすると、接触点の法線ベクトル（向き）とAUTDデバイスの向きを比較し、最適なデバイスからのみ超音波を照射します。")]
    public bool enableDirectionalGrouping = false;
    
    [Tooltip("デバイスが面の法線方向から何度まで傾いていても担当として許容するか（0〜90度）。0度で真正面のみ。")]
    [Range(0, 90)]
    public float directionalAngleThreshold = 45.0f;

    [Header("STM Settings (for future extension)")]
    [Tooltip("GainSTM時のモード。通常は PhaseIntensityFull を使用します。")]
    public GainSTMMode gainStmMode = GainSTMMode.PhaseIntensityFull;

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
    public List<AUTD3Device> connectedDevices = new List<AUTD3Device>();

    [HideInInspector]
    public HAP_AUTDDebugDisabler? debugDisabler;

    private Controller? _autd = null;
    private bool _isCurrentlyOff = true;
    
    // スレッドセーフかつ非同期に送信を行うためのロックとタスク
    private readonly object _sendLock = new object();
    private System.Threading.Tasks.Task? _hapticsSendTask = null;

    // 前回の設定を記憶して変更を検知するためのフィールド
    private ModulationMode _prevModMode;
    private float _prevSineFreq;
    private float _prevStaticAmp;
    
    private SilencerMode _prevSilencerMode;
    private ushort _prevSilStepPhase;
    private ushort _prevSilStepAmp;
    
    private bool _prevFanState;
    private float _prevTemperature;

    void Awake()
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

        // シーン内のすべての AUTD3Device コンポーネントを収集し、ID順にソートしてデバイス配置情報を生成
        connectedDevices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None)
            .OrderBy(obj => obj.ID)
            .ToList();

        var devices = connectedDevices.Select(obj => new AUTD3(pos: obj.transform.position, rot: obj.transform.rotation)).ToList();

        Debug.Log($"[HAP_AUTDController] Attempting to connect to AUTD3. Found {devices.Count} AUTD3Device components in the scene.");

        try
        {
            // タイムアウト対策として5秒に延長
            var option = new AUTD3Sharp.SenderOption { Timeout = AUTD3Sharp.Duration.FromMillis(5000) };
            
            switch (linkType)
            {
                case AUTDLinkType.TwinCAT:
                    _autd = Controller.OpenWithOption(devices, new AUTD3Sharp.Link.TwinCAT(), option);
                    Debug.Log("[HAP_AUTDController] Successfully connected to AUTD3 via TwinCAT.");
                    break;
                
                case AUTDLinkType.SOEM:
                    Debug.LogWarning("[HAP_AUTDController] SOEMを使用するには Unity Package Manager から SOEMリンクパッケージ のインストールが必要です。インストール後、スクリプト内のコメントアウトを外してください。");
                    // 【注意】SOEMリンクパッケージをインストール後、以下のコメントアウトを外してください
                    /*
                    var soemLink = new AUTD3Sharp.Link.SOEM();
                    // if (!string.IsNullOrEmpty(soemAdapterName)) { soemLink = soemLink.WithIfname(soemAdapterName); }
                    _autd = Controller.OpenWithOption(devices, soemLink, option);
                    Debug.Log("[HAP_AUTDController] Successfully connected to AUTD3 via SOEM.");
                    */
                    break;
                
                case AUTDLinkType.Simulator:
                    Debug.LogWarning("[HAP_AUTDController] Simulatorを使用するには autd3-server を起動しておく必要があります。(https://github.com/shinolab/autd3-server)");
                    
                    var simLink = new AUTD3Sharp.Link.Remote(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), 8080), new AUTD3Sharp.Link.RemoteOption());
                    _autd = Controller.OpenWithOption(devices, simLink, option);
                    Debug.Log("[HAP_AUTDController] Successfully connected to AUTD3 via Simulator (Remote).");
                    
                    break;
            }

            if (_autd == null)
            {
                Debug.LogWarning("[HAP_AUTDController] Link initialization was skipped or failed. Haptics will be bypassed.");
                return;
            }

            // 初期設定の送信
            ApplyTemperature();
            ApplyModulation();
            ApplySilencer();
            ApplyFan();
            
            // 初期状態はオフ (Null出力)
            _autd.Send(new Null());
            _isCurrentlyOff = true;
            
            Debug.Log("[HAP_AUTDController] Successfully connected to AUTD3 devices via TwinCAT.");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            Debug.LogError("[HAP_AUTDController] Failed to connect to AUTD3 via TwinCAT. Ensure TwinCAT is running in Run Mode.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
            Application.Quit();
#endif
        }
    }

    void Update()
    {
        if (_autd == null) return;

        // プロファイラー設定の同期
        performanceProfiler.Enabled = enableProfiling;
        performanceProfiler.LogEnabled = enableLog;
        performanceProfiler.LogInterval = profilingLogInterval;

        // インスペクターの設定変更を監視して適用（HAP_AUTDController_Config.cs）
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

    private void OnDestroy()
    {
        if (_autd != null)
        {
            _autd.Send(new Null());
            _autd.Close();
            _autd.Dispose();
            _autd = null;
            Debug.Log("[HAP_AUTDController] AUTD3 connection closed.");
        }
    }

    // Modulation overrides logic has been moved to HAP_AUTDController_Haptics.cs
}
