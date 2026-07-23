using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【ABX 識別実験条件の共通基底クラス (ABX Discrimination パラダイム)】
/// <see cref="EXP_BaseCondition"/> を継承し、基準 A、基準 B、および未知の X（AまたはBと同等）を
/// 順次提示して「X は A と B のどちらと同じ（近い）か」を識別させるパラダイムの抽象基底を提供します。
/// </summary>
public abstract class EXP_BaseABXCondition : EXP_BaseCondition
{
    // =====================================================
    // Reference
    // =====================================================

    [HideInInspector]
    [System.NonSerialized]
    public HAP_HapticsIllusionFoxFootController? controller;

    // =====================================================
    // Timing Settings
    // =====================================================

    [Header("Timing Settings")]
    [Tooltip("各インターバル (A, B, X) の刺激提示時間 [秒]")]
    [Min(0.1f)]
    public float intervalDuration = 1.5f;

    [Tooltip("インターバル間の無刺激 ISI [秒]")]
    [Min(0f)]
    public float isiDuration = 0.5f;

    // =====================================================
    // Abstract Interface (派生クラスで実装)
    // =====================================================

    protected abstract float GetValueA();
    protected abstract float GetValueB();
    protected abstract void ApplyValueToController(HAP_HapticsIllusionFoxFootController ctrl, float value);
    protected abstract void ResetValueOnTrialEnd(HAP_HapticsIllusionFoxFootController ctrl);
    protected abstract string FormatValueForDebug(float value);

    // =====================================================
    // Base Implementations
    // =====================================================

    public override void Apply(EXP_TrialData trial) { }

    public override IEnumerator StimulusCoroutine(EXP_TrialData trial, MonoBehaviour runner)
    {
        if (controller == null)
            controller = Object.FindAnyObjectByType<HAP_HapticsIllusionFoxFootController>();

        float valA = GetValueA();
        float valB = GetValueB();
        bool isX_Equal_A = (Random.value < 0.5f);
        float valX = isX_Equal_A ? valA : valB;

        // メタデータ記録
        trial.metadata["valueA"]       = valA.ToString("F4");
        trial.metadata["valueB"]       = valB.ToString("F4");
        trial.metadata["valueX"]       = valX.ToString("F4");
        trial.metadata["correctAnswer"] = isX_Equal_A ? "A" : "B";

        var expManager = runner as EXP_ExperimentManager ?? runner.GetComponent<EXP_ExperimentManager>() ?? Object.FindAnyObjectByType<EXP_ExperimentManager>();
        bool isDebug = expManager != null && expManager.config != null && expManager.config.isDebugMode;

        // ---- Interval A ----
        yield return PresentInterval(expManager, trial, "刺激 A", valA, isDebug);
        yield return PresentISI(expManager, trial);

        // ---- Interval B ----
        yield return PresentInterval(expManager, trial, "刺激 B", valB, isDebug);
        yield return PresentISI(expManager, trial);

        // ---- Interval X ----
        yield return PresentInterval(expManager, trial, "刺激 X (対象)", valX, isDebug);

        // ---- Response Prompt ----
        trial.metadata["currentInterval"] = "応答受付中";
        if (expManager != null)
        {
            expManager.SetMessage("刺激 X は A と B のどちらと同じでしたか？\n【1】刺激 A (Z)   /   【2】刺激 B (X)");
            expManager.SetPhase(EXP_TrialPhase.Response);
        }
    }

    public override void OnTrialEnd(EXP_TrialData trial)
    {
        if (controller != null)
        {
            ResetValueOnTrialEnd(controller);
            SetHapticsBypass(controller, false);
        }
    }

    public override bool? EvaluateResponse(EXP_TrialData trial)
    {
        if (string.IsNullOrEmpty(trial.responseValue)) return null;
        string correct = trial.metadata.TryGetValue("correctAnswer", out var ans) ? ans : "";

        if (trial.responseValue == "Z" || trial.responseValue == "1") return correct == "A";
        if (trial.responseValue == "X" || trial.responseValue == "2") return correct == "B";
        return null;
    }

    private IEnumerator PresentInterval(EXP_ExperimentManager? manager, EXP_TrialData trial, string name, float val, bool isDebug)
    {
        string debugStr = FormatValueForDebug(val);
        string label = isDebug ? $"{name} ({debugStr})" : name;
        trial.metadata["currentInterval"] = label;
        manager?.SetMessage(isDebug ? $"【 {name} 】 ({debugStr})" : $"【 {name} 】");

        if (controller != null)
        {
            ApplyValueToController(controller, val);
            SetHapticsBypass(controller, false);
        }

        yield return new WaitForSeconds(intervalDuration);
    }

    private IEnumerator PresentISI(EXP_ExperimentManager? manager, EXP_TrialData trial)
    {
        trial.metadata["currentInterval"] = "無刺激間隔 (ISI)";
        manager?.SetMessage("・ ・ ・");
        if (controller != null) SetHapticsBypass(controller, true);
        if (isiDuration > 0f) yield return new WaitForSeconds(isiDuration);
    }

    protected static void SetHapticsBypass(HAP_HapticsIllusionFoxFootController ctrl, bool bypass)
    {
        if (ctrl.autdController != null) ctrl.autdController.bypassHaptics = bypass;
        else ctrl.enabled = !bypass;
    }
}
