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
using AUTD3Sharp.Driver.Datagram;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
#endif

#nullable enable

/// <summary>
/// HCD_Pipeline やオブジェクト接触判定から焦点位置（Foci / STM）をリアルタイム計算し、
/// HAP_AUTDHardwareController 経由でマルチフォーカス超音波を出力する触覚生成パイプラインコントローラー。
/// </summary>
public class HAP_AUTDHapticsController : MonoBehaviour
{
    [Header("Hardware Reference")]
    [Tooltip("物理通信接続および送信を担当する HAP_AUTDHardwareController の参照。未指定時は自動取得します。")]
    public HAP_AUTDHardwareController hardwareController = null!;

    [Tooltip("配置・座標オフセットを管理する HAP_AUTDTransformLoader の参照。未指定時は自動取得します。")]
    public HAP_AUTDTransformLoader transformLoader = null!;

    [Header("Operation Settings")]
    [Tooltip("触覚出力のターゲットデータソース（AutoHCD: 手の接触クラスタ、ObjectTarget: オブジェクト部位ターゲット、Manual: 手動API）")]
    public HapticsSourceMode sourceMode = HapticsSourceMode.AutoHCD;

    [Header("Dependencies")]
    [Tooltip("接触判定を行う HCD_Pipeline の参照。自動モード時に毎フレームここからクラスタ情報を取得します。")]
    public HCD_Pipeline hcdPipeline = null!;

    [Tooltip("HCDの焦点生成設定を行う HAP_HCDFociSettings の参照。未指定時は自動取得します。")]
    public HAP_HCDFociSettings hcdFociSettings = null!;

    [Tooltip("オブジェクトのハプティクス制御コンポーネントのリスト。アタッチされている場合、特定オブジェクト位置へ照射します。")]
    public List<HAP_BaseObjectHapticsController> objectHapticsControllers = new List<HAP_BaseObjectHapticsController>();

    [Tooltip("現在アクティブなコントローラーのインデックス（0以上）。選択されたオブジェクトのみ enabled=true に同期されます。")]
    public int activeObjectControllerIndex = 0;

    /// <summary>
    /// 単一オブジェクトコントローラーとの互換用アクセサ。最初の要素を返します/セットします。
    /// </summary>
    public HAP_BaseObjectHapticsController? objectHapticsController
    {
        get => objectHapticsControllers.FirstOrDefault(c => c != null && c.enabled);
        set
        {
            if (value != null && !objectHapticsControllers.Contains(value))
            {
                objectHapticsControllers.Add(value);
            }
        }
    }

    /// <summary>
    /// 指定されたインデックスのコントローラーのみを enabled / GameObject.SetActive = true にし、他を非アクティブへ連動切り替えします。
    /// </summary>
    public void SetActiveControllerIndex(int index)
    {
        objectHapticsControllers.RemoveAll(c => c == null);
        if (objectHapticsControllers.Count == 0)
        {
            activeObjectControllerIndex = 0;
            return;
        }

        activeObjectControllerIndex = Mathf.Clamp(index, 0, objectHapticsControllers.Count - 1);

        for (int i = 0; i < objectHapticsControllers.Count; i++)
        {
            var ctrl = objectHapticsControllers[i];
            if (ctrl != null)
            {
                bool isActive = (i == activeObjectControllerIndex);
                ctrl.enabled = isActive;

                // 自身とは別のGameObjectにアタッチされている場合、GameObject自体の SetActive も同期
                if (ctrl.gameObject != this.gameObject)
                {
                    ctrl.gameObject.SetActive(isActive);
                }
            }
        }
    }

    [HideInInspector]
    public bool bypassHaptics = false;

    [Header("Acoustic Settings")]
    [Tooltip("ホログラフィアルゴリズム。\nGSPAT: 高速・高品質。\nNaive: 計算は軽いが音圧や精度が落ちます。")]
    public HoloAlgorithm holoAlgorithm = HoloAlgorithm.GSPAT;
    
    [Tooltip("超音波の出力強度 (Pascal)。最大で 10000 程度。大きすぎるとデバイスの保護回路が働くか、クリッピングが発生します。")]
    public float focusIntensityPascal = 10000f;

    [Header("Directional Grouping")]
    [Tooltip("有効にすると、接触点の法線ベクトル（向き）とAUTDデバイスの向きを比較し、最適なデバイスからのみ超音波を照射します。")]
    public bool enableDirectionalGrouping = false;
    
    [Tooltip("デバイスが面の法線方向から何度まで傾いていても担当として許容するか（0〜90度）。0度で真正面のみ。")]
    [Range(0, 90)]
    public float directionalAngleThreshold = 45.0f;

    [Header("STM Settings")]
    [Tooltip("STMの種類を選択。FociSTM(ハードウェア計算・単焦点)、GainSTM(CPU計算・GSPAT等の複数焦点に対応)")]
    public HapticsSTMMode stmMode = HapticsSTMMode.FociSTM;

    [Tooltip("STMの再生周波数 (Hz)。単焦点・多焦点いずれのSTMでも再生速度として利用されます。")]
    public float stmFrequency = 150f;

#if USE_AUTD3_LEGACY
    [Tooltip("GainSTM時のモード。通常は PhaseIntensityFull を使用します。")]
    public GainSTMMode gainStmMode = GainSTMMode.PhaseIntensityFull;
#endif

    [Header("Debug")]
    [Tooltip("エディタ上でデバイスのサイズと位置を Gizmo (青色の枠) で表示します。")]
    public bool visualizeDevices = true;

    [Header("Performance Profiling")]
    [Tooltip("有効にすると、ハプティクスパイプラインの処理時間を計測します。")]
    public bool enableProfiling = false;

    [Tooltip("有効にすると、Sendを含む全処理をメインスレッドで同期実行します。")]
    public bool synchronousSend = false;

    [Tooltip("Debug.Log への結果出力を有効にします。")]
    public bool enableLog = true;
    
    [Tooltip("Debug.Log に処理時間を出力する間隔（フレーム数）。")]
    [Range(1, 600)]
    public int profilingLogInterval = 60;

    /// <summary>
    /// パフォーマンスプロファイラー。外部からも参照可能です。
    /// </summary>
    [HideInInspector]
    public HAP_AUTDPerformanceProfiler performanceProfiler = new HAP_AUTDPerformanceProfiler();

    [HideInInspector]
    public HAP_AUTDDebugDisabler? debugDisabler;

    private bool _isCurrentlyOff = true;
#if !USE_AUTD3_LEGACY
    private System.Threading.Tasks.Task? _hapticsSendTask = null;
#endif

    void Awake()
    {
        debugDisabler = GetComponent<HAP_AUTDDebugDisabler>();

        if (hardwareController == null)
        {
            hardwareController = GetComponent<HAP_AUTDHardwareController>();
            if (hardwareController == null)
            {
                hardwareController = FindAnyObjectByType<HAP_AUTDHardwareController>();
                if (hardwareController == null)
                {
                    hardwareController = gameObject.AddComponent<HAP_AUTDHardwareController>();
                }
            }
        }

        if (transformLoader == null)
        {
            transformLoader = GetComponent<HAP_AUTDTransformLoader>();
            if (transformLoader == null)
            {
                transformLoader = FindAnyObjectByType<HAP_AUTDTransformLoader>();
            }
        }

        if (hcdPipeline == null)
        {
            hcdPipeline = FindAnyObjectByType<HCD_Pipeline>();
        }

        if (hcdFociSettings == null)
        {
            if (hcdPipeline != null)
            {
                hcdFociSettings = hcdPipeline.GetComponent<HAP_HCDFociSettings>();
            }
            if (hcdFociSettings == null)
            {
                hcdFociSettings = FindAnyObjectByType<HAP_HCDFociSettings>();
            }
        }
    }

    void Update()
    {
        if (hardwareController == null || !hardwareController.IsConnected) return;

        performanceProfiler.Enabled = enableProfiling;
        performanceProfiler.LogEnabled = enableLog;
        performanceProfiler.LogInterval = profilingLogInterval;

        UpdateHaptics();
    }

    /// <summary>
    /// HCD_Pipelineから接触クラスタを取得し、最適なHaptics信号（GSPATなど）を生成・送信します。
    /// </summary>
    private void UpdateHaptics()
    {
        // バイパス時: ハードウェアがまだ出力中なら一度だけNullを送って停止させる
        if (bypassHaptics || sourceMode == HapticsSourceMode.Manual)
        {
            if (!_isCurrentlyOff && hardwareController != null && hardwareController.IsConnected)
            {
                hardwareController.SetNull();
                _isCurrentlyOff = true;
            }
            return;
        }

        Vector3 currentOffset = transformLoader != null ? transformLoader.offset : Vector3.zero;
        bool hasActiveTargets = false;
        List<TrackedCluster> activeClusters = new List<TrackedCluster>();
        List<HAP_FociGenerator.ClusterFociData> clusterFociList = new List<HAP_FociGenerator.ClusterFociData>();

        if (sourceMode == HapticsSourceMode.ObjectTarget)
        {
            objectHapticsControllers.RemoveAll(c => c == null);
            if (objectHapticsControllers.Count > 0)
            {
                int validIdx = Mathf.Clamp(activeObjectControllerIndex, 0, objectHapticsControllers.Count - 1);
                var activeCtrl = objectHapticsControllers[validIdx];
                if (activeCtrl != null && activeCtrl.enabled && activeCtrl.HasActiveTargets())
                {
                    var foci = activeCtrl.GetHapticsTargets(focusIntensityPascal, currentOffset);
                    clusterFociList.AddRange(foci);
                }
            }
            hasActiveTargets = clusterFociList.Count > 0;
        }
        else if (sourceMode == HapticsSourceMode.AutoHCD)
        {
            if (hcdPipeline != null)
            {
                var trackedClusters = hcdPipeline.GetTrackedClusters();
                activeClusters = trackedClusters.Where(c => c.IsAlive && c.Force > 0.01f).ToList();
                hasActiveTargets = activeClusters.Count > 0;
            }
        }

        HoloAlgorithm effectiveAlgorithm = (stmMode == HapticsSTMMode.FociSTM)
            ? HoloAlgorithm.Naive
            : holoAlgorithm;

        if (hasActiveTargets)
        {
            var profiler = performanceProfiler;
            profiler.BeginTotal();

            try
            {
                profiler.BeginFociGenerate();
                if (sourceMode == HapticsSourceMode.AutoHCD)
                {
                    if (hcdFociSettings != null)
                    {
                        clusterFociList = hcdFociSettings.GenerateFoci(
                            activeClusters,
                            focusIntensityPascal,
                            currentOffset,
                            stmMode,
                            stmFrequency);
                    }
                    else
                    {
                        // フォールバック: 設定オブジェクトが見つからない場合は Simplified モードで計算
                        clusterFociList = HAP_FociGenerator.Generate(
                            activeClusters,
                            HapticsGenerationMode.Simplified,
                            new HAP_HapticsCentroidSource(),
                            new HAP_HapticsEllipseSource(),
                            new HAP_HapticsRandomSource(),
                            focusIntensityPascal,
                            currentOffset,
                            stmMode,
                            stmFrequency);
                    }
                }
                profiler.EndFociGenerate();

#if !USE_AUTD3_LEGACY
                if (hardwareController.Client != null && hardwareController.Geometry != null)
                {
                    profiler.BeginDeviceAllocate();
                    var builder = hardwareController.Client.DatagramBuilder();
                    HAP_GSPATDeviceAllocator.Allocate(
                        builder,
                        clusterFociList,
                        hardwareController.ConnectedDevices,
                        hardwareController.Geometry,
                        enableDirectionalGrouping,
                        directionalAngleThreshold,
                        effectiveAlgorithm,
                        debugDisabler
                    );
                    profiler.EndDeviceAllocate();

                    profiler.BeginDatagramBuild();
                    var frames = builder.Build();
                    profiler.EndDatagramBuild();

                    profiler.BeginSend();
                    if (synchronousSend)
                    {
                        foreach (var frame in frames)
                        {
                            hardwareController.Client.SendCheckedAsync(frame).GetAwaiter().GetResult();
                        }
                        profiler.EndSend();
                    }
                    else
                    {
                        if (_hapticsSendTask == null || _hapticsSendTask.IsCompleted)
                        {
                            _hapticsSendTask = System.Threading.Tasks.Task.Run(async () =>
                            {
                                try
                                {
                                    foreach (var frame in frames)
                                    {
                                        await hardwareController.Client.SendCheckedAsync(frame);
                                    }
                                }
                                finally
                                {
                                    profiler.EndSend();
                                }
                            });
                        }
                        else
                        {
                            profiler.EndSend();
                        }
                    }

                    _isCurrentlyOff = false;
                }
#else
                if (hardwareController.Autd != null)
                {
                    profiler.BeginDeviceAllocate();
                    lock (hardwareController.SendLock)
                    {
                        var datagram = HAP_GSPATDeviceAllocator.Allocate(
                            clusterFociList,
                            hardwareController.ConnectedDevices,
                            effectiveAlgorithm,
                            enableDirectionalGrouping,
                            directionalAngleThreshold,
                            focusIntensityPascal,
                            debugDisabler
                        );
                        hardwareController.Send(datagram);
                    }
                    profiler.EndDeviceAllocate();
                    _isCurrentlyOff = false;
                }
#endif
            }
            finally
            {
                profiler.EndTotal();
            }
        }
        else
        {
            if (!_isCurrentlyOff)
            {
                hardwareController.SetNull();
                _isCurrentlyOff = true;
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!visualizeDevices) return;

        var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None);
        
#if UNITY_EDITOR
        // 実行中/非実行中どちらでも確実に取得できるよう GetComponent を直接使用
        var disabler = GetComponent<HAP_AUTDDebugDisabler>();

        // ObjectTarget モード時、アクティブな IllusionController があれば渡す
        HAP_HapticsIllusionCustomController? illusionCtrl = null;
        if (sourceMode == HapticsSourceMode.ObjectTarget && objectHapticsControllers != null)
        {
            objectHapticsControllers.RemoveAll(c => c == null);
            if (objectHapticsControllers.Count > 0)
            {
                int validIdx = Mathf.Clamp(activeObjectControllerIndex, 0, objectHapticsControllers.Count - 1);
                illusionCtrl = objectHapticsControllers[validIdx] as HAP_HapticsIllusionCustomController;
            }
        }

        HAP_GizmoVisualizer.DrawDevicesAndGroupings(
            devices, 
            enableDirectionalGrouping, 
            directionalAngleThreshold, 
            hcdPipeline,
            disabler,
            illusionCtrl
        );
#endif
    }
}
