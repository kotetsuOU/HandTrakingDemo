using UnityEngine;
using System;
using System.Collections;
using TMPro;

#nullable enable

/// <summary>
/// 被験者実験のメインステートマネージャー（統括窓口）。
/// 実験設定（<see cref="EXP_SessionSettings"/>）、コンポーネント保持、ステート状態管理、および外部 API を統括します。
/// </summary>
public class EXP_ExperimentManager : MonoBehaviour
{
    // =====================================================
    // Inspector Settings
    // =====================================================

    [Header("Experiment Settings (Foldable)")]
    public EXP_SessionSettings settings = new EXP_SessionSettings();

    [Header("Sub Components & Text Assets")]
    public EXP_TrialSequencer? sequencer;
    public EXP_DataRecorder? dataRecorder;
    public EXP_EventMarker? eventMarker;
    public EXP_InputHandler? inputHandler;

    [Tooltip("教示・同意・説明文章アセット（空の場合は標準デフォルト文章が適用されます）")]
    public EXP_InstructionTextConfig? instructionText;

    [Header("UI References")]
    public TMP_Text? messageText;
    public GameObject? fixationCross;

    [Header("Debug Control")]
    public bool debugKeyEnabled = true;

    // Direct Access Properties (Convenience Accessors)
    public string participantId { get => settings.participantId; set => settings.participantId = value; }
    public string participantName { get => settings.participantName; set => settings.participantName = value; }
    public string groupLabel { get => settings.groupLabel; set => settings.groupLabel = value; }
    public bool isDebugMode { get => settings.isDebugMode; set => settings.isDebugMode = value; }
    public int trialsPerBlock { get => settings.trialsPerBlock; set => settings.trialsPerBlock = value; }
    public int blockCount { get => settings.blockCount; set => settings.blockCount = value; }
    public int practiceTrialCount { get => settings.practiceTrialCount; set => settings.practiceTrialCount = value; }
    public float itiDuration { get => settings.itiDuration; set => settings.itiDuration = value; }
    public float stimulusDuration { get => settings.stimulusDuration; set => settings.stimulusDuration = value; }
    public float responseTimeout { get => settings.responseTimeout; set => settings.responseTimeout = value; }
    public float breakDuration { get => settings.breakDuration; set => settings.breakDuration = value; }

    // =====================================================
    // State (Read-Only)
    // =====================================================

    public EXP_ExperimentState CurrentState { get; private set; } = EXP_ExperimentState.Idle;
    public EXP_TrialPhase CurrentPhase { get; private set; } = EXP_TrialPhase.ITI;
    public EXP_ExperimentSession? CurrentSession { get; private set; }
    public EXP_TrialData? CurrentTrial { get; private set; }
    public string CurrentMessage { get; private set; } = "";

    // =====================================================
    // Events
    // =====================================================

    public event Action<EXP_ExperimentState, EXP_ExperimentState>? OnStateChanged;
    public event Action<EXP_TrialData>? OnTrialStarted;
    public event Action<EXP_TrialData>? OnResponseReceived;
    public event Action<EXP_TrialData>? OnTrialCompleted;
    public event Action<EXP_ExperimentSession>? OnExperimentFinished;
    public event Action? OnExperimentAborted;

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
        if (inputHandler != null) inputHandler.OnResponse += HandleResponse;
    }

    void OnDisable()
    {
        if (inputHandler != null) inputHandler.OnResponse -= HandleResponse;
    }

    void Update()
    {
        if (!debugKeyEnabled) return;
        if (Input.GetKeyDown(KeyCode.Space) && CurrentState == EXP_ExperimentState.Idle) StartExperiment();
        if (Input.GetKeyDown(KeyCode.Escape) && CurrentState != EXP_ExperimentState.Idle && CurrentState != EXP_ExperimentState.Finished) AbortExperiment();
    }

    // =====================================================
    // Public API
    // =====================================================

    public void StartExperiment()
    {
        if (CurrentState != EXP_ExperimentState.Idle) return;
        StartCoroutine(EXP_ExperimentFlowController.RunMainLoop(this));
    }

    public void AbortExperiment()
    {
        if (CurrentState == EXP_ExperimentState.Idle || CurrentState == EXP_ExperimentState.Finished) return;
        StopAllCoroutines();

        if (inputHandler != null) inputHandler.StopListening();
        if (CurrentSession != null)
        {
            CurrentSession.isFinished = true;
            dataRecorder?.FinalizeSession(CurrentSession);
        }

        eventMarker?.Mark("ExperimentAborted");
        ClearAll();

        TransitionTo(EXP_ExperimentState.Idle);
        OnExperimentAborted?.Invoke();
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

    public void SetFixation(bool visible) { if (fixationCross != null) fixationCross.SetActive(visible); }
    public void SetPhase(EXP_TrialPhase phase) => CurrentPhase = phase;
    public void ClearAll() { SetMessage(""); SetFixation(false); }

    public void TransitionTo(EXP_ExperimentState newState)
    {
        var oldState = CurrentState;
        CurrentState = newState;
        OnStateChanged?.Invoke(oldState, newState);
    }

    public void SetCurrentSession(EXP_ExperimentSession? session) => CurrentSession = session;
    public void SetCurrentTrial(EXP_TrialData? trial) => CurrentTrial = trial;
    public void ResetResponseReceived() => _responseReceived = false;
    public bool HasReceivedResponse() => _responseReceived;

    public IEnumerator WaitForResponse(float timeoutSecs)
    {
        float elapsed = 0f;
        while (!_responseReceived)
        {
            if (timeoutSecs > 0f && elapsed >= timeoutSecs) yield break;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    public void InvokeTrialStarted(EXP_TrialData trial) => OnTrialStarted?.Invoke(trial);
    public void InvokeTrialCompleted(EXP_TrialData trial) => OnTrialCompleted?.Invoke(trial);
    public void InvokeExperimentFinished(EXP_ExperimentSession session) => OnExperimentFinished?.Invoke(session);

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

    private T GetOrAdd<T>() where T : Component => GetComponent<T>() ?? gameObject.AddComponent<T>();
}
