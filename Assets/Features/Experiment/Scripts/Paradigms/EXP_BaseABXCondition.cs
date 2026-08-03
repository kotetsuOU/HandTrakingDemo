using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【ABX 識別実験条件の共通基底クラス (ABX Discrimination パラダイム)】
/// <see cref="EXP_BaseCondition"/> を継承し、基準 A、基準 B、および未知の X（AまたはBと同等）を
/// 順次提示して「X は A と B のどちらと同じ（近い）か」を識別させるパラダイムの抽象基底を提供します。
/// </summary>
public abstract class EXP_BaseABXCondition : EXP_BaseHapticsCondition
{
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
    protected abstract string FormatValueForDebug(float value);

    // =====================================================
    // Base Implementations
    // =====================================================

    public override string ParadigmType => "ABX";

    public override void Apply(EXP_TrialData trial) { }

    public override IEnumerator StimulusCoroutine(EXP_TrialData trial, MonoBehaviour runner)
    {
        var ctrl = GetController();

        float valA = GetValueA();
        float valB = GetValueB();
        bool isX_Equal_A = (Random.value < 0.5f);
        float valX = isX_Equal_A ? valA : valB;

        // 共通フィールド & メタデータ記録
        trial.paradigmType              = ParadigmType;
        trial.stimulusVal1              = valA;
        trial.stimulusVal2              = valB;
        trial.comparisonDetail          = $"A: {valA:F4} vs B: {valB:F4} (Target X: {valX:F4} [={(isX_Equal_A ? "A" : "B")}])";

        trial.metadata["valueA"]        = valA.ToString("F4");
        trial.metadata["valueB"]        = valB.ToString("F4");
        trial.metadata["valueX"]        = valX.ToString("F4");
        trial.metadata["correctAnswer"] = isX_Equal_A ? "A" : "B";

        var expManager = runner as EXP_ExperimentManager ?? runner.GetComponent<EXP_ExperimentManager>() ?? Object.FindAnyObjectByType<EXP_ExperimentManager>();
        bool isDebug = expManager != null && expManager.isDebugMode;

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

    public override string FormatResponseValue(EXP_TrialData trial, string rawResponse)
    {
        trial.metadata["rawKey"] = rawResponse;
        string upper = rawResponse.ToUpperInvariant();
        bool isChoiceA = (upper == "CHOICE1" || upper == "Z" || upper == "1" || upper == "A");
        bool isChoiceB = (upper == "CHOICE2" || upper == "X" || upper == "2" || upper == "B");

        if (isChoiceA)
        {
            trial.metadata["selectedChoice"] = "A";
            string selectedVal = trial.metadata.TryGetValue("valueA", out string? valStr) ? valStr : trial.stimulusVal1.ToString("F4");
            trial.metadata["selectedStimulusValue"] = selectedVal;
            return selectedVal;
        }
        else if (isChoiceB)
        {
            trial.metadata["selectedChoice"] = "B";
            string selectedVal = trial.metadata.TryGetValue("valueB", out string? valStr) ? valStr : trial.stimulusVal2.ToString("F4");
            trial.metadata["selectedStimulusValue"] = selectedVal;
            return selectedVal;
        }

        return rawResponse;
    }

    public override bool? EvaluateResponse(EXP_TrialData trial)
    {
        if (string.IsNullOrEmpty(trial.responseValue)) return null;
        string correct = trial.metadata.TryGetValue("correctAnswer", out var ans) ? ans : "";
        if (trial.metadata.TryGetValue("selectedChoice", out var choice))
        {
            return choice == correct;
        }

        if (trial.responseValue == "A" || trial.responseValue == "Z" || trial.responseValue == "1" || trial.responseValue == "Choice1") return correct == "A";
        if (trial.responseValue == "B" || trial.responseValue == "X" || trial.responseValue == "2" || trial.responseValue == "Choice2") return correct == "B";
        return null;
    }

    private IEnumerator PresentInterval(EXP_ExperimentManager? manager, EXP_TrialData trial, string name, float val, bool isDebug)
    {
        string debugStr = FormatValueForDebug(val);
        string label = isDebug ? $"{name} ({debugStr})" : name;
        trial.metadata["currentInterval"] = label;
        manager?.SetMessage(isDebug ? $"【 {name} 】 ({debugStr})" : $"【 {name} 】");

        var ctrl = GetController();
        if (ctrl != null)
        {
            ApplyValueToController(ctrl, val);
            SetHapticsBypass(ctrl, false);
        }

        yield return new WaitForSeconds(intervalDuration);
    }

    private IEnumerator PresentISI(EXP_ExperimentManager? manager, EXP_TrialData trial)
    {
        trial.metadata["currentInterval"] = "無刺激間隔 (ISI)";
        manager?.SetMessage("・ ・ ・");
        SetHapticsBypass(GetController(), true);
        if (isiDuration > 0f) yield return new WaitForSeconds(isiDuration);
    }
}
