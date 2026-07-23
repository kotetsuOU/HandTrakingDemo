using UnityEngine;
using System;
using System.Collections;
using System.IO;

#nullable enable

/// <summary>
/// 実験全体のステートマシン。被験者実験の司令塔コンポーネント。
/// <para>
/// 依存コンポーネント（<see cref="EXP_TrialSequencer"/>, <see cref="EXP_DataRecorder"/>,
/// <see cref="EXP_EventMarker"/>, <see cref="EXP_UIController"/>, <see cref="EXP_InputHandler"/>）
/// は同一 GameObject 上に配置するか、Inspector で参照を設定してください。
/// 未設定の場合は Awake() 時に自動取得または自動追加されます。
/// </para>
/// <para>
/// 最小セットアップ:
/// <list type="number">
/// <item><see cref="EXP_ExperimentConfig"/> を作成して <see cref="config"/> に設定</item>
/// <item><see cref="EXP_BaseCondition"/> を継承した条件を <see cref="sequencer"/>.conditions に登録</item>
/// <item>Play Mode で Space キー、またはコードから <see cref="StartExperiment"/> を呼ぶ</item>
/// </list>
/// </para>
/// </summary>
public class EXP_ExperimentManager : MonoBehaviour
{
    // =====================================================
    // Inspector Settings
    // =====================================================

    [Header("Configuration")]
    [Tooltip("実験設定 ScriptableObject（必須）")]
    public EXP_ExperimentConfig config = null!;

    [Header("Sub Components")]
    [Tooltip("試行シーケンス管理。未設定時は同一 GameObject から自動取得します。")]
    public EXP_TrialSequencer? sequencer;

    [Tooltip("データ記録。未設定時は同一 GameObject から自動取得します。")]
    public EXP_DataRecorder? dataRecorder;

    [Tooltip("イベントマーカー。未設定時は同一 GameObject から自動取得します。")]
    public EXP_EventMarker? eventMarker;

    [Tooltip("UI コントローラー。未設定時は同一 GameObject から自動取得します。")]
    public EXP_UIController? uiController;

    [Tooltip("入力ハンドラー。未設定時は同一 GameObject から自動取得します。")]
    public EXP_InputHandler? inputHandler;

    [Header("Debug")]
    [Tooltip("Space キーで実験開始、Escape キーで中断できるようにする（デバッグ用）")]
    public bool debugKeyEnabled = true;

    // =====================================================
    // State (Read-Only)
    // =====================================================

    /// <summary>現在の実験ステート</summary>
    public EXP_ExperimentState CurrentState { get; private set; } = EXP_ExperimentState.Idle;

    /// <summary>現在の試行フェーズ</summary>
    public EXP_TrialPhase CurrentPhase { get; private set; } = EXP_TrialPhase.ITI;

    /// <summary>現在実行中のセッション情報（実験開始後に設定されます）</summary>
    public EXP_ExperimentSession? CurrentSession { get; private set; }

    /// <summary>現在実行中の試行データ（試行中のみ非 null）</summary>
    public EXP_TrialData? CurrentTrial { get; private set; }

    // =====================================================
    // Events
    // =====================================================

    /// <summary>実験ステートが変化したときに発火</summary>
    public event Action<EXP_ExperimentState>? OnStateChanged;

    /// <summary>試行が開始されたときに発火</summary>
    public event Action<EXP_TrialData>? OnTrialStarted;

    /// <summary>参加者が応答したときに発火（応答情報はすでに trial に書き込まれています）</summary>
    public event Action<EXP_TrialData>? OnResponseReceived;

    /// <summary>試行が完了したときに発火（データ記録後）</summary>
    public event Action<EXP_TrialData>? OnTrialCompleted;

    /// <summary>実験が正常終了したときに発火</summary>
    public event Action<EXP_ExperimentSession>? OnExperimentFinished;

    /// <summary>実験が中断されたときに発火</summary>
    public event Action? OnExperimentAborted;

    // =====================================================
    // Private Fields
    // =====================================================

    private bool _responseReceived = false;

    // =====================================================
    // Unity Lifecycle
    // =====================================================

    void Awake()
    {
        sequencer    ??= GetOrAdd<EXP_TrialSequencer>();
        dataRecorder ??= GetOrAdd<EXP_DataRecorder>();
        eventMarker  ??= GetOrAdd<EXP_EventMarker>();
        uiController ??= GetOrAdd<EXP_UIController>();
        inputHandler ??= GetOrAdd<EXP_InputHandler>();
    }

    void OnEnable()
    {
        if (inputHandler != null)
            inputHandler.OnResponse += HandleResponse;
    }

    void OnDisable()
    {
        if (inputHandler != null)
            inputHandler.OnResponse -= HandleResponse;
    }

    void Update()
    {
        if (!debugKeyEnabled) return;

        if (Input.GetKeyDown(KeyCode.Space) && CurrentState == EXP_ExperimentState.Idle)
            StartExperiment();

        if (Input.GetKeyDown(KeyCode.Escape) && CurrentState != EXP_ExperimentState.Idle
                                             && CurrentState != EXP_ExperimentState.Finished)
            AbortExperiment();
    }

    // =====================================================
    // Public API
    // =====================================================

    /// <summary>
    /// 実験を開始します。
    /// <see cref="config"/> が未設定の場合はエラーログを出して何もしません。
    /// </summary>
    public void StartExperiment()
    {
        if (config == null)
        {
            Debug.LogError("[EXP_ExperimentManager] config が設定されていません。");
            return;
        }
        if (CurrentState != EXP_ExperimentState.Idle)
        {
            Debug.LogWarning("[EXP_ExperimentManager] 実験はすでに開始されています。");
            return;
        }

        StartCoroutine(RunExperiment());
    }

    /// <summary>
    /// 実験を中断します。ここまでのデータは保存されます。
    /// </summary>
    public void AbortExperiment()
    {
        StopAllCoroutines();
        inputHandler?.StopListening();

        if (CurrentSession != null && dataRecorder != null)
        {
            CurrentSession.Finalize();
            dataRecorder.SaveAll(CurrentSession);
        }

        eventMarker?.Mark("ExperimentAborted");
        TransitionTo(EXP_ExperimentState.Finished);
        uiController?.SetMessage("実験を中断しました。");
        OnExperimentAborted?.Invoke();
        Debug.LogWarning("[EXP_ExperimentManager] 実験が中断されました。");
    }

    // =====================================================
    // Main Coroutine
    // =====================================================

    private IEnumerator RunExperiment()
    {
        // --- セッション初期化 ---
        CurrentSession = EXP_ExperimentSession.Create(config.participantId, config.groupLabel);

        string resolvedDir = string.IsNullOrEmpty(config.outputDirectory)
            ? Path.Combine(Application.persistentDataPath, "ExperimentData")
            : config.outputDirectory;

        // サブコンポーネントの設定を config に同期
        dataRecorder!.dataFormat       = config.dataFormat;
        dataRecorder!.outputDirectory  = resolvedDir;
        dataRecorder!.filePrefix       = config.filePrefix;
        dataRecorder!.Initialize(CurrentSession);

        eventMarker!.Initialize(resolvedDir, CurrentSession.sessionId);
        eventMarker!.logToConsole = !config.suppressLogs;

        inputHandler!.inputDevice    = config.inputDevice;
        inputHandler!.responseKeys   = config.responseKeys;
        inputHandler!.gamepadButtons = config.gamepadButtons;

        uiController!.useUnityUI = config.useUnityUI;

        // 乱数シード
        if (config.randomSeed >= 0)
            UnityEngine.Random.InitState(config.randomSeed);

        // シーケンス構築
        sequencer!.randomSeed = config.randomSeed;
        sequencer!.BuildSequence();
        CurrentSession.totalTrials = sequencer.TotalTrials;

        eventMarker.Mark("ExperimentStart");
        Log($"実験開始: 参加者 {config.participantId}, セッション {CurrentSession.sessionId}");

        // --- 教示 ---
        yield return RunInstruction(
            "実験を開始します。\n準備ができたらボタンを押してください。");

        // --- 練習試行 ---
        if (config.practiceTrialCount > 0)
        {
            yield return RunPracticeBlock();
            yield return RunInstruction(
                "練習が終わりました。本番を始めます。\nボタンを押して続けてください。");
            // 本番用に再シャッフル
            sequencer.BuildSequence();
        }

        // --- 本試行 ---
        TransitionTo(EXP_ExperimentState.Trial);
        eventMarker.Mark("MainTrialsStart");

        int totalTrials = sequencer.TotalTrials;
        int blockSize   = config.trialsPerBlock > 0 ? config.trialsPerBlock : totalTrials;
        int trialIndex  = 0;
        int blockIndex  = 0;

        while (!sequencer.IsFinished)
        {
            var condition = sequencer.GetNextCondition();
            if (condition == null) break;

            yield return RunTrial(trialIndex, blockIndex, isPractice: false, condition);
            CurrentSession.completedTrials++;
            uiController.SetProgress((float)(trialIndex + 1) / totalTrials);
            trialIndex++;

            // ブロック境界でブレイク挿入（最終試行は除く）
            bool isBlockEnd = (config.blockCount > 1)
                           && (trialIndex % blockSize == 0)
                           && !sequencer.IsFinished;
            if (isBlockEnd)
            {
                blockIndex++;
                yield return RunBreak(blockIndex, config.blockCount);
            }
        }

        // --- 終了 ---
        eventMarker.Mark("ExperimentEnd");
        CurrentSession.Finalize();
        dataRecorder.SaveAll(CurrentSession);

        TransitionTo(EXP_ExperimentState.Finished);
        uiController.SetMessage("実験が終了しました。\nご参加ありがとうございました。");
        OnExperimentFinished?.Invoke(CurrentSession);

        Log($"実験完了: {CurrentSession.completedTrials} 試行, 正答率 {CurrentSession.accuracy:P1}");
    }

    // =====================================================
    // Phase Coroutines
    // =====================================================

    private IEnumerator RunInstruction(string message)
    {
        TransitionTo(EXP_ExperimentState.Instruction);
        uiController!.SetMessage(message);
        eventMarker!.Mark("InstructionStart");

        inputHandler!.StartListening();
        yield return WaitForResponse(timeoutSecs: 0f);   // タイムアウトなし（必ず応答待ち）
        inputHandler.StopListening();

        uiController.ClearAll();
        eventMarker.Mark("InstructionEnd");
    }

    private IEnumerator RunPracticeBlock()
    {
        TransitionTo(EXP_ExperimentState.Practice);
        eventMarker!.Mark("PracticeStart");

        // 練習は conditions リストの先頭から循環して使用
        var seqReadOnly = sequencer!.GetSequence();
        int condCount = seqReadOnly.Count;
        for (int i = 0; i < config.practiceTrialCount; i++)
        {
            if (condCount == 0) break;
            var cond = seqReadOnly[i % condCount];
            yield return RunTrial(i, blockIndex: -1, isPractice: true, cond);
        }

        eventMarker.Mark("PracticeEnd");
    }

    private IEnumerator RunTrial(
        int trialIndex, int blockIndex, bool isPractice, EXP_BaseCondition condition)
    {
        // --- 試行データ初期化 ---
        CurrentTrial = new EXP_TrialData
        {
            trialIndex      = trialIndex,
            blockIndex      = blockIndex,
            isPractice      = isPractice,
            conditionName   = condition.conditionName,
            trialStartTime  = (double)Time.realtimeSinceStartup,
        };

        eventMarker!.Mark($"TrialStart_{trialIndex}_{condition.conditionName}");
        OnTrialStarted?.Invoke(CurrentTrial);

        // --- ITI ---
        CurrentPhase = EXP_TrialPhase.ITI;
        uiController!.SetFixation(true);
        uiController.SetFeedback("");
        if (config.itiDuration > 0f)
            yield return new WaitForSeconds(config.itiDuration);

        // --- Stimulus ---
        CurrentPhase = EXP_TrialPhase.Stimulus;
        CurrentTrial.stimulusOnsetTime = (double)Time.realtimeSinceStartup;
        eventMarker.Mark("StimulusOn");
        condition.Apply(CurrentTrial);

        // --- Response ---
        CurrentPhase = EXP_TrialPhase.Response;
        _responseReceived = false;
        inputHandler!.StartListening();

        // 刺激が固定時間で消えるケース
        if (config.stimulusDuration > 0f)
            yield return new WaitForSeconds(config.stimulusDuration);

        // 応答またはタイムアウトまで待機
        yield return WaitForResponse(config.responseTimeout);
        inputHandler.StopListening();
        eventMarker.Mark("StimulusOff");

        // タイムアウト判定
        if (!_responseReceived)
        {
            CurrentTrial.responseType  = EXP_ResponseType.Timeout;
            CurrentTrial.responseTime  = (double)Time.realtimeSinceStartup;
            CurrentTrial.isCorrect     = false;
            eventMarker.Mark("Timeout");
        }
        else
        {
            // 条件クラスに正誤判定を委ねる
            CurrentTrial.isCorrect = condition.EvaluateResponse(CurrentTrial);
            if (CurrentTrial.isCorrect == true)
            {
                CurrentTrial.responseType = EXP_ResponseType.Correct;
                CurrentSession!.correctTrials++;
            }
            else if (CurrentTrial.isCorrect == false)
            {
                CurrentTrial.responseType = EXP_ResponseType.Incorrect;
            }
            // null = 正誤判定なし → responseType は None のまま（response は記録済み）
        }

        // --- Feedback ---
        if (config.feedbackDuration > 0f && config.showFeedback)
        {
            CurrentPhase = EXP_TrialPhase.Feedback;
            uiController.ShowFeedback(CurrentTrial.responseType);
            yield return new WaitForSeconds(config.feedbackDuration);
            uiController.SetFeedback("");
        }

        uiController.SetFixation(false);
        condition.OnTrialEnd(CurrentTrial);

        // --- 記録 ---
        dataRecorder!.RecordTrial(CurrentTrial);
        CurrentSession?.trialDataList.Add(CurrentTrial);

        OnTrialCompleted?.Invoke(CurrentTrial);
        eventMarker.Mark($"TrialEnd_{trialIndex}");
        CurrentTrial = null;
    }

    private IEnumerator RunBreak(int blockIndex, int totalBlocks)
    {
        TransitionTo(EXP_ExperimentState.Break);
        eventMarker!.Mark($"BlockBreak_{blockIndex}");

        uiController!.SetFixation(false);
        uiController.SetMessage(
            $"休憩してください（ブロック {blockIndex} / {totalBlocks} 完了）\n"
          + "準備ができたらボタンを押して再開してください。");

        inputHandler!.StartListening();
        yield return WaitForResponse(config.breakDuration);   // タイムアウトで自動再開
        inputHandler.StopListening();

        uiController.ClearAll();
        TransitionTo(EXP_ExperimentState.Trial);
    }

    // =====================================================
    // Helpers
    // =====================================================

    /// <summary>
    /// 応答またはタイムアウトまでフレームを待機します。
    /// <paramref name="timeoutSecs"/> が 0 以下の場合はタイムアウトなし（応答まで無限待機）。
    /// </summary>
    private IEnumerator WaitForResponse(float timeoutSecs)
    {
        float elapsed = 0f;
        while (!_responseReceived)
        {
            if (timeoutSecs > 0f && elapsed >= timeoutSecs) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void HandleResponse(string responseValue)
    {
        if (CurrentTrial == null) return;
        _responseReceived          = true;
        CurrentTrial.responseValue = responseValue;
        CurrentTrial.responseTime  = (double)Time.realtimeSinceStartup;
        eventMarker?.Mark($"Response_{responseValue}");
        OnResponseReceived?.Invoke(CurrentTrial);
    }

    private void TransitionTo(EXP_ExperimentState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
        Log($"State → {newState}");
    }

    private T GetOrAdd<T>() where T : Component
    {
        var component = GetComponent<T>();
        if (component == null) component = gameObject.AddComponent<T>();
        return component;
    }

    private void Log(string message)
    {
        if (config != null && config.suppressLogs) return;
        Debug.Log($"[EXP_ExperimentManager] {message}");
    }
}
