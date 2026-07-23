using UnityEngine;
using System;
using System.Collections;
using TMPro;

#nullable enable

/// <summary>
/// 被験者実験のメインステートマシン（司令塔）。
/// 全体ステート管理、Block / Trial のルーフ、各コンポーネントの統括、イベント発火を行います。
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

    [Tooltip("入力ハンドラー。未設定時は同一 GameObject から自動取得します。")]
    public EXP_InputHandler? inputHandler;

    [Header("UI References (被験者画面用)")]
    [Tooltip("メッセージ / 教示テキスト (TMP_Text)")]
    public TMP_Text? messageText;

    [Tooltip("固視点オブジェクト（表示 / 非表示を切り替えます）")]
    public GameObject? fixationCross;

    [Header("Debug")]
    [Tooltip("Space キーで実験開始、Escape キーで中断できるようにする（デバッグ用）")]
    public bool debugKeyEnabled = true;

    // =====================================================
    // State (Read-Only)
    // =====================================================

    /// <summary>現在の実験ステート</summary>
    public EXP_ExperimentState CurrentState { get; private set; } = EXP_ExperimentState.Idle;

    /// <summary>現在の試行内フェーズ</summary>
    public EXP_TrialPhase CurrentPhase { get; private set; } = EXP_TrialPhase.ITI;

    /// <summary>実行中のアクティブセッション</summary>
    public EXP_ExperimentSession? CurrentSession { get; private set; }

    /// <summary>実行中の試行データ（非試行時は null）</summary>
    public EXP_TrialData? CurrentTrial { get; private set; }

    /// <summary>被験者画面に表示中のメッセージ</summary>
    public string CurrentMessage { get; private set; } = "";

    // =====================================================
    // Events
    // =====================================================

    /// <summary>ステートが遷移したときに発火 (oldState, newState)</summary>
    public event Action<EXP_ExperimentState, EXP_ExperimentState>? OnStateChanged;

    /// <summary>試行が開始したときに発火</summary>
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

    public void StartExperiment()
    {
        if (CurrentState != EXP_ExperimentState.Idle)
        {
            Debug.LogWarning("[EXP_ExperimentManager] すでに実験が開始されています。");
            return;
        }

        if (config == null)
        {
            Debug.LogError("[EXP_ExperimentManager] ExperimentConfig が設定されていません。");
            return;
        }

        StartCoroutine(MainExperimentLoop());
    }

    public void AbortExperiment()
    {
        if (CurrentState == EXP_ExperimentState.Idle || CurrentState == EXP_ExperimentState.Finished)
            return;

        StopAllCoroutines();

        if (inputHandler != null)
            inputHandler.StopListening();

        if (CurrentSession != null)
        {
            CurrentSession.isFinished = true;
            dataRecorder?.FinalizeSession(CurrentSession);
        }

        eventMarker?.Mark("ExperimentAborted");
        ClearAll();

        TransitionTo(EXP_ExperimentState.Idle);
        OnExperimentAborted?.Invoke();
        Debug.Log("[EXP_ExperimentManager] 実験を中断しました。");
    }

    public void SetMessage(string message)
    {
        CurrentMessage = message ?? "";
        if (messageText != null)
        {
            messageText.text = CurrentMessage;
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(CurrentMessage));
        }
    }

    public void SetFixation(bool visible)
    {
        if (fixationCross != null)
            fixationCross.SetActive(visible);
    }

    public void SetPhase(EXP_TrialPhase phase)
    {
        CurrentPhase = phase;
    }

    public void ClearAll()
    {
        SetMessage("");
        SetFixation(false);
    }

    // =====================================================
    // Main Loop Coroutine
    // =====================================================

    private IEnumerator MainExperimentLoop()
    {
        TransitionTo(EXP_ExperimentState.Instruction);

        CurrentSession = new EXP_ExperimentSession
        {
            participantId   = config.participantId,
            groupLabel      = config.groupLabel,
            sessionStartTime = (double)Time.realtimeSinceStartup,
        };

        if (sequencer != null)
        {
            sequencer.GenerateSequence();
            CurrentSession.totalTrials = sequencer.TotalTrials;
        }

        dataRecorder?.InitializeSession(CurrentSession);
        eventMarker?.Mark($"ExperimentStart_{config.participantId}");

        // --- 1. 教示フェーズ ---
        SetFixation(false);
        SetMessage(
            "【実験の説明】\n\n"
          + "これより触覚刺激の比較実験を開始します。\n"
          + "準備ができたらボタン（または Space キー）を押してください。");

        _responseReceived = false;
        inputHandler?.StartListening();
        yield return WaitForResponse(0f);
        inputHandler?.StopListening();

        ClearAll();

        // --- 2. 練習試行 ---
        if (config.practiceTrialCount > 0)
        {
            TransitionTo(EXP_ExperimentState.Practice);
            eventMarker?.Mark("PracticeStart");

            yield return RunPracticeTrials();

            SetMessage("【練習完了】\n次から本試行を開始します。準備ができたらボタンを押してください。");
            _responseReceived = false;
            inputHandler?.StartListening();
            yield return WaitForResponse(0f);
            inputHandler?.StopListening();

            ClearAll();
        }

        // --- 3. 本試行 ---
        TransitionTo(EXP_ExperimentState.Trial);
        eventMarker?.Mark("MainTrialsStart");

        int totalBlocks = config.blockCount;
        int trialsPerBlock = config.trialsPerBlock;

        for (int block = 0; block < totalBlocks; block++)
        {
            for (int t = 0; t < trialsPerBlock; t++)
            {
                var condition = sequencer?.NextCondition();
                if (condition == null) break;

                int globalTrialIndex = block * trialsPerBlock + t;
                yield return RunTrial(globalTrialIndex, blockIndex: block, isPractice: false, condition);

                if (CurrentState == EXP_ExperimentState.Idle) yield break;
            }

            if (block < totalBlocks - 1 && config.breakDuration > 0f)
            {
                yield return RunBreak(block + 1, totalBlocks);
            }
        }

        // --- 4. 終了フェーズ ---
        TransitionTo(EXP_ExperimentState.Finished);
        CurrentSession.isFinished = true;
        CurrentSession.sessionEndTime = (double)Time.realtimeSinceStartup;

        dataRecorder?.FinalizeSession(CurrentSession);
        eventMarker?.Mark("ExperimentFinished");

        SetMessage(
            "【全試行完了】\n\n"
          + "実験が終了しました。ご協力ありがとうございました。\n"
          + "データは正常に保存されました。");

        OnExperimentFinished?.Invoke(CurrentSession);
        Debug.Log($"[EXP_ExperimentManager] 全試行完了 (総試行数: {CurrentSession.completedTrials})");
    }

    private IEnumerator RunPracticeTrials()
    {
        var seqReadOnly = sequencer!.GetSequence();
        int condCount = seqReadOnly.Count;
        for (int i = 0; i < config.practiceTrialCount; i++)
        {
            if (condCount == 0) break;
            var cond = seqReadOnly[i % condCount];
            yield return RunTrial(i, blockIndex: -1, isPractice: true, cond);
        }

        eventMarker?.Mark("PracticeEnd");
    }

    private IEnumerator RunTrial(
        int trialIndex, int blockIndex, bool isPractice, EXP_BaseCondition condition)
    {
        _responseReceived = false;

        CurrentTrial = new EXP_TrialData
        {
            trialIndex      = trialIndex,
            blockIndex      = blockIndex,
            isPractice      = isPractice,
            conditionName   = condition.conditionName,
            trialStartTime  = (double)Time.realtimeSinceStartup,
        };

        eventMarker?.Mark($"TrialStart_{trialIndex}_{condition.conditionName}");
        OnTrialStarted?.Invoke(CurrentTrial);

        // --- ITI ---
        CurrentPhase = EXP_TrialPhase.ITI;
        SetFixation(true);

        if (config.itiDuration > 0f)
            yield return new WaitForSeconds(config.itiDuration);

        // --- Stimulus ---
        CurrentPhase = EXP_TrialPhase.Stimulus;
        CurrentTrial.stimulusOnsetTime = (double)Time.realtimeSinceStartup;
        eventMarker?.Mark("StimulusOn");

        var stimCoro = condition.StimulusCoroutine(CurrentTrial, this);
        if (stimCoro != null)
        {
            yield return stimCoro;
        }
        else
        {
            condition.Apply(CurrentTrial);
        }

        // --- Response ---
        CurrentPhase = EXP_TrialPhase.Response;
        _responseReceived = false;
        inputHandler?.StartListening();

        if (config.stimulusDuration > 0f)
            yield return new WaitForSeconds(config.stimulusDuration);

        yield return WaitForResponse(config.responseTimeout);
        inputHandler?.StopListening();
        eventMarker?.Mark("StimulusOff");

        if (!_responseReceived)
        {
            CurrentTrial.responseType  = EXP_ResponseType.Timeout;
            CurrentTrial.responseTime  = (double)Time.realtimeSinceStartup;
            CurrentTrial.isCorrect     = false;
            eventMarker?.Mark("Timeout");
        }
        else
        {
            CurrentTrial.isCorrect = condition.EvaluateResponse(CurrentTrial);
            if (CurrentTrial.isCorrect == true)
            {
                CurrentTrial.responseType = EXP_ResponseType.Correct;
            }
            else if (CurrentTrial.isCorrect == false)
            {
                CurrentTrial.responseType = EXP_ResponseType.Incorrect;
            }
        }

        SetFixation(false);
        condition.OnTrialEnd(CurrentTrial);

        dataRecorder?.RecordTrial(CurrentTrial);
        CurrentSession?.trialDataList.Add(CurrentTrial);

        OnTrialCompleted?.Invoke(CurrentTrial);
        eventMarker?.Mark($"TrialEnd_{trialIndex}");
        CurrentTrial = null;
    }

    private IEnumerator RunBreak(int blockIndex, int totalBlocks)
    {
        TransitionTo(EXP_ExperimentState.Break);
        eventMarker?.Mark($"BlockBreak_{blockIndex}");

        SetFixation(false);
        SetMessage(
            $"休憩してください（ブロック {blockIndex} / {totalBlocks} 完了）\n"
          + "準備ができたらボタンを押して再開してください。");

        _responseReceived = false;
        inputHandler?.StartListening();
        yield return WaitForResponse(config.breakDuration);
        inputHandler?.StopListening();

        ClearAll();
        TransitionTo(EXP_ExperimentState.Trial);
    }

    private IEnumerator WaitForResponse(float timeoutSecs)
    {
        float elapsed = 0f;
        while (!_responseReceived)
        {
            if (timeoutSecs > 0f && elapsed >= timeoutSecs)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void HandleResponse(string responseValue)
    {
        if (CurrentTrial != null)
        {
            CurrentTrial.responseValue = responseValue;
            CurrentTrial.responseTime  = (double)Time.realtimeSinceStartup;
            OnResponseReceived?.Invoke(CurrentTrial);
        }

        _responseReceived = true;
    }

    private void TransitionTo(EXP_ExperimentState newState)
    {
        var oldState = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(oldState, newState);
    }

    private T GetOrAdd<T>() where T : Component
    {
        return GetComponent<T>() ?? gameObject.AddComponent<T>();
    }
}
