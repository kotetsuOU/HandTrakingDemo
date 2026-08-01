using UnityEngine;
using System.Collections;
using Core.Logging;
using Features.Experiment.Debug;

#nullable enable

/// <summary>
/// 【2AFC 実験条件の共通基底クラス】
/// <see cref="EXP_BaseCondition"/> を継承し、2AFC（二選択強制選択）パラダイムにおける
/// 共通のペア構成モード（<see cref="afcMode"/>）、時間順序カウンターバランス、タイミング制御、
/// インターバル提示フロー、およびブラインド / デバッグ表示制御を抽象化・提供します。
/// </summary>
public abstract class EXP_Base2AFCCondition : EXP_BaseHapticsCondition
{
    // =====================================================
    // Common 2AFC Parameters
    // =====================================================

    [Header("2AFC Pair Mode")]
    [Tooltip("2AFC 刺激ペアの構成モード:\n"
           + "• ReferenceVsComparison: 基準値(固定) vs 候補リストから選出\n"
           + "• RandomPair: 候補リストから2つの異なる刺激を完全にランダム抽出 (一対比較法)\n"
           + "• FixedPair: 指定の reference vs comparison の固定ペア")]
    public EXP_2AFCMode afcMode = EXP_2AFCMode.RandomPair;

    [Header("Presentation Order Randomization")]
    [Tooltip("有効にすると、第1刺激と第2刺激の提示順序を毎試行 50% の確率でランダム反転させます（時間順序効果の防止）")]
    public bool randomizePresentationOrder = true;

    [Header("Timing")]
    [Tooltip("各インターバルの刺激提示時間 [秒]")]
    [Min(0.1f)]
    public float intervalDuration = 2.0f;

    [Tooltip("インターバル間の無刺激 ISI (inter-stimulus interval) [秒]")]
    [Min(0f)]
    public float isiDuration = 0.5f;

    [Tooltip("合図を表示する時間 [秒]（0 = 表示なし）")]
    [Min(0f)]
    public float cueDuration = 0.3f;

    // =====================================================
    // Abstract Interface (派生クラスで実装)
    // =====================================================

    protected abstract float GetReferenceValue();
    protected abstract float GetFixedComparisonValue();
    protected abstract float[] GetCandidateValues();
    protected abstract void ApplyValueToController(HAP_HapticsIllusionFoxFootController ctrl, float value);
    protected abstract string FormatValueForDebug(float value);

    // =====================================================
    // Base Implementations
    // =====================================================

    public override string ParadigmType => "2AFC";

    public override void Apply(EXP_TrialData trial) { }

    public override IEnumerator StimulusCoroutine(EXP_TrialData trial, MonoBehaviour runner)
    {
        var ctrl = GetController();
        if (ctrl == null)
        {
            AppLogger.LogWarning(runner, EXP_LogTriggers.TagFlowController, $"[{GetType().Name}] HAP_HapticsIllusionFoxFootController が見つかりません（ダミータイマーで動作します）。");
        }

        // ペアの物理値決定
        (float val1, float val2, string refPos) = DeterminePairValues();

        // 共通フィールド & メタデータ記録
        trial.paradigmType                  = ParadigmType;
        trial.stimulusVal1                  = val1;
        trial.stimulusVal2                  = val2;
        string refLabel1 = (refPos == "Interval1") ? "Ref" : (refPos == "Interval2") ? "Cmp" : "Pair1";
        string refLabel2 = (refPos == "Interval2") ? "Ref" : (refPos == "Interval1") ? "Cmp" : "Pair2";
        trial.comparisonDetail              = $"Interval1: {val1:F4} ({refLabel1}) vs Interval2: {val2:F4} ({refLabel2})";

        trial.metadata["afcMode"]           = afcMode.ToString();
        trial.metadata["interval1Value"]    = val1.ToString("F4");
        trial.metadata["interval2Value"]    = val2.ToString("F4");
        trial.metadata["valueDelta"]        = Mathf.Abs(val1 - val2).ToString("F4");
        trial.metadata["referencePosition"] = refPos;

        var expManager = runner as EXP_ExperimentManager ?? runner.GetComponent<EXP_ExperimentManager>() ?? Object.FindAnyObjectByType<EXP_ExperimentManager>();
        bool isDebug = expManager != null && expManager.isDebugMode;

        // ---- Interval 1 ----
        string debugStr1 = FormatValueForDebug(val1);
        string label1 = isDebug ? $"第 1 刺激 ({debugStr1})" : "第 1 刺激";
        string msg1 = isDebug ? $"【 第 1 刺激 】 ({debugStr1})" : "【 第 1 刺激 】";
        trial.metadata["currentInterval"] = label1;
        expManager?.SetMessage(msg1);

        yield return RunSingleInterval(ctrl, val1, cueDuration, intervalDuration);

        // ---- ISI ----
        trial.metadata["currentInterval"] = "無刺激間隔 (ISI)";
        expManager?.SetMessage("・ ・ ・");
        StopHaptics(ctrl);
        if (isiDuration > 0f)
            yield return new WaitForSeconds(isiDuration);

        // ---- Interval 2 ----
        string debugStr2 = FormatValueForDebug(val2);
        string label2 = isDebug ? $"第 2 刺激 ({debugStr2})" : "第 2 刺激";
        string msg2 = isDebug ? $"【 第 2 刺激 】 ({debugStr2})" : "【 第 2 刺激 】";
        trial.metadata["currentInterval"] = label2;
        expManager?.SetMessage(msg2);

        yield return RunSingleInterval(ctrl, val2, cueDuration, intervalDuration);

        // 第2刺激終了後、応答受付に入る前にハプティクス出力を停止
        StopHaptics(ctrl);

        // ---- Response Prompt ----
        trial.metadata["currentInterval"] = "応答受付中";
        if (expManager != null)
        {
            expManager.SetMessage("どちらが重かったですか？\n【1】第 1 刺激 (Z)   /   【2】第 2 刺激 (X)");
            expManager.SetPhase(EXP_TrialPhase.Response);
        }
    }

    public override string FormatResponseValue(EXP_TrialData trial, string rawResponse)
    {
        trial.metadata["rawKey"] = rawResponse;
        string upper = rawResponse.ToUpperInvariant();
        bool isInterval1 = (upper == "CHOICE1" || upper == "Z" || upper == "1");
        bool isInterval2 = (upper == "CHOICE2" || upper == "X" || upper == "2");

        if (isInterval1)
        {
            trial.metadata["selectedInterval"] = "Interval1";
            if (trial.metadata.TryGetValue("referencePosition", out string? refPos))
            {
                if (refPos == "Interval1") trial.metadata["selectedStimulus"] = "Reference";
                else if (refPos == "Interval2") trial.metadata["selectedStimulus"] = "Comparison";
            }
            
            string selectedVal = trial.metadata.TryGetValue("interval1Value", out string? valStr)
                ? valStr
                : trial.stimulusVal1.ToString("F4");
            trial.metadata["selectedValue"] = selectedVal;
            return selectedVal;
        }
        else if (isInterval2)
        {
            trial.metadata["selectedInterval"] = "Interval2";
            if (trial.metadata.TryGetValue("referencePosition", out string? refPos))
            {
                if (refPos == "Interval2") trial.metadata["selectedStimulus"] = "Reference";
                else if (refPos == "Interval1") trial.metadata["selectedStimulus"] = "Comparison";
            }

            string selectedVal = trial.metadata.TryGetValue("interval2Value", out string? valStr)
                ? valStr
                : trial.stimulusVal2.ToString("F4");
            trial.metadata["selectedValue"] = selectedVal;
            return selectedVal;
        }

        return rawResponse;
    }

    public override bool? EvaluateResponse(EXP_TrialData trial) => null;

    // =====================================================
    // Internal Helpers
    // =====================================================

    protected virtual (float val1, float val2, string refPos) DeterminePairValues()
    {
        bool swap = randomizePresentationOrder && (Random.value < 0.5f);
        float[] candidates = GetCandidateValues();
        float refVal = GetReferenceValue();
        float fixCmpVal = GetFixedComparisonValue();

        if (afcMode == EXP_2AFCMode.RandomPair && candidates != null && candidates.Length >= 2)
        {
            int idxA = Random.Range(0, candidates.Length);
            int idxB;
            do { idxB = Random.Range(0, candidates.Length); } while (idxB == idxA);

            float vA = candidates[idxA];
            float vB = candidates[idxB];
            return swap ? (vB, vA, "N/A (RandomPair)") : (vA, vB, "N/A (RandomPair)");
        }
        else if (afcMode == EXP_2AFCMode.ReferenceVsComparison && candidates != null && candidates.Length > 0)
        {
            float cmpVal = candidates[Random.Range(0, candidates.Length)];
            return swap ? (cmpVal, refVal, "Interval2") : (refVal, cmpVal, "Interval1");
        }
        else
        {
            return swap ? (fixCmpVal, refVal, "Interval2") : (refVal, fixCmpVal, "Interval1");
        }
    }

    protected IEnumerator RunSingleInterval(
        HAP_HapticsIllusionFoxFootController? ctrl,
        float value,
        float cueSecs,
        float durationSecs)
    {
        if (ctrl != null)
        {
            ApplyValueToController(ctrl, value);
            SetHapticsBypass(ctrl, false);
        }

        if (cueSecs > 0f)
            yield return new WaitForSeconds(cueSecs);

        yield return new WaitForSeconds(durationSecs);
    }
}
