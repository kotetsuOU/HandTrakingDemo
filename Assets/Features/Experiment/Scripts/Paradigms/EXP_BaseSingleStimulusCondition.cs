using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【単一刺激実験条件の共通基底クラス (Detection / Rating パラダイム)】
/// <see cref="EXP_BaseCondition"/> を継承し、単一の刺激を提示して検出の有無 (Yes/No) や
/// マグニチュード評定 (Rating) を行う実験パラダイムの共通ロジックを提供します。
/// </summary>
public abstract class EXP_BaseSingleStimulusCondition : EXP_BaseHapticsCondition
{
    // =====================================================
    // Timing Settings
    // =====================================================

    [Header("Timing Settings")]
    [Tooltip("刺激提示時間 [秒]")]
    [Min(0.1f)]
    public float stimulusDuration = 2.0f;

    [Tooltip("刺激提示前の合図（Cue）表示時間 [秒]")]
    [Min(0f)]
    public float cueDuration = 0.3f;

    // =====================================================
    // Abstract Interface (派生クラスで実装)
    // =====================================================

    protected abstract float GetTargetValue();
    protected abstract void ApplyValueToController(HAP_HapticsIllusionFoxFootController ctrl, float value);
    protected abstract string FormatValueForDebug(float value);

    // =====================================================
    // Base Implementations
    // =====================================================

    public override string ParadigmType => "SingleStimulus";

    public override void Apply(EXP_TrialData trial) { }

    public override IEnumerator StimulusCoroutine(EXP_TrialData trial, MonoBehaviour runner)
    {
        var ctrl = GetController();

        float val = GetTargetValue();

        // 共通フィールド & メタデータ記録
        trial.paradigmType              = ParadigmType;
        trial.stimulusVal1              = val;
        trial.stimulusVal2              = 0.0;
        trial.comparisonDetail          = $"Stimulus: {val:F4}";

        trial.metadata["stimulusValue"] = val.ToString("F4");

        var expManager = runner as EXP_ExperimentManager ?? runner.GetComponent<EXP_ExperimentManager>() ?? Object.FindAnyObjectByType<EXP_ExperimentManager>();
        bool isDebug = expManager != null && expManager.isDebugMode;

        string debugStr = FormatValueForDebug(val);
        string label = isDebug ? $"刺激提示中 ({debugStr})" : "刺激提示中";
        string msg = isDebug ? $"【 刺激提示中 】 ({debugStr})" : "【 刺激提示中 】";

        trial.metadata["currentInterval"] = label;
        expManager?.SetMessage(msg);

        // 刺激提示
        if (ctrl != null)
        {
            ApplyValueToController(ctrl, val);
            SetHapticsBypass(ctrl, false);
        }

        if (cueDuration > 0f)
            yield return new WaitForSeconds(cueDuration);

        yield return new WaitForSeconds(stimulusDuration);

        // 刺激提示終了後、応答受付に入る前にハプティクス出力を停止
        StopHaptics(ctrl);

        // 応答受付メッセージ
        trial.metadata["currentInterval"] = "応答受付中";
        if (expManager != null)
        {
            expManager.SetMessage("刺激を感じましたか？\n【1】はい (Z)   /   【2】いいえ (X)");
            expManager.SetPhase(EXP_TrialPhase.Response);
        }
    }

    public override string FormatResponseValue(EXP_TrialData trial, string rawResponse)
    {
        trial.metadata["rawKey"] = rawResponse;
        string upper = rawResponse.ToUpperInvariant();
        bool isYes = (upper == "CHOICE1" || upper == "Z" || upper == "1" || upper == "YES");
        bool isNo  = (upper == "CHOICE2" || upper == "X" || upper == "2" || upper == "NO");

        if (isYes)
        {
            trial.metadata["selectedResponse"] = "Yes";
            string valStr = trial.metadata.TryGetValue("stimulusValue", out string? sVal) ? sVal : trial.stimulusVal1.ToString("F4");
            return valStr;
        }
        else if (isNo)
        {
            trial.metadata["selectedResponse"] = "No";
            return "0.0000";
        }

        return rawResponse;
    }

    public override bool? EvaluateResponse(EXP_TrialData trial) => null;
}
