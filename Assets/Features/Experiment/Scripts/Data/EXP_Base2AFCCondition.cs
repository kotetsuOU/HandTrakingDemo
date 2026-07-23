using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【2AFC 実験条件の共通基底クラス】
/// <see cref="EXP_BaseCondition"/> を継承し、2AFC（二選択強制選択）パラダイムにおける
/// 共通のペア構成モード（<see cref="afcMode"/>）、時間順序カウンターバランス、タイミング制御、
/// インターバル提示フロー、およびブラインド / デバッグ表示制御を抽象化・提供します。
/// </summary>
public abstract class EXP_Base2AFCCondition : EXP_BaseCondition
{
    // =====================================================
    // Reference
    // =====================================================

    [HideInInspector]
    [System.NonSerialized]
    public HAP_HapticsIllusionFoxFootController? controller;

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

    /// <summary>基準物理値（Reference）を取得します。</summary>
    protected abstract float GetReferenceValue();

    /// <summary>固定比較物理値（Fixed Comparison）を取得します。</summary>
    protected abstract float GetFixedComparisonValue();

    /// <summary>候補物理値リスト（Candidates）を取得します。</summary>
    protected abstract float[] GetCandidateValues();

    /// <summary>物理値をコントローラーやハードウェアへ適用します。</summary>
    protected abstract void ApplyValueToController(HAP_HapticsIllusionFoxFootController ctrl, float value);

    /// <summary>試行終了時の物理値リセット処理を行います。</summary>
    protected abstract void ResetValueOnTrialEnd(HAP_HapticsIllusionFoxFootController ctrl);

    /// <summary>デバッグ表示用の値文字列（例: "80 Hz" や "Y: -2.0 cm"）を取得します。</summary>
    protected abstract string FormatValueForDebug(float value);

    // =====================================================
    // Base Implementations
    // =====================================================

    public override void Apply(EXP_TrialData trial) { }

    public override IEnumerator StimulusCoroutine(EXP_TrialData trial, MonoBehaviour runner)
    {
        if (controller == null)
            controller = Object.FindAnyObjectByType<HAP_HapticsIllusionFoxFootController>();

        if (controller == null)
        {
            Debug.LogError($"[{GetType().Name}] HAP_HapticsIllusionFoxFootController が見つかりません。");
            yield break;
        }

        // ペアの物理決定
        (float val1, float val2, string refPos) = DeterminePairValues();

        // メタデータ記録
        trial.metadata["afcMode"]           = afcMode.ToString();
        trial.metadata["interval1Value"]    = val1.ToString("F4");
        trial.metadata["interval2Value"]    = val2.ToString("F4");
        trial.metadata["valueDelta"]        = Mathf.Abs(val1 - val2).ToString("F4");
        trial.metadata["referencePosition"] = refPos;

        var expManager = runner.GetComponent<EXP_ExperimentManager>() ?? Object.FindAnyObjectByType<EXP_ExperimentManager>();
        bool isDebug = expManager != null && expManager.config != null && expManager.config.isDebugMode;

        var uiCtrl = runner.GetComponent<EXP_UIController>() ?? Object.FindAnyObjectByType<EXP_UIController>();

        // ---- Interval 1 ----
        string debugStr1 = FormatValueForDebug(val1);
        string label1 = isDebug ? $"第 1 刺激 ({debugStr1})" : "第 1 刺激";
        string msg1 = isDebug ? $"【 第 1 刺激 】 ({debugStr1})" : "【 第 1 刺激 】";
        trial.metadata["currentInterval"] = label1;
        uiCtrl?.SetMessage(msg1);

        yield return RunSingleInterval(controller, val1, cueDuration, intervalDuration);

        // ---- ISI ----
        trial.metadata["currentInterval"] = "無刺激間隔 (ISI)";
        uiCtrl?.SetMessage("・ ・ ・");
        StopHaptics(controller);
        if (isiDuration > 0f)
            yield return new WaitForSeconds(isiDuration);

        // ---- Interval 2 ----
        string debugStr2 = FormatValueForDebug(val2);
        string label2 = isDebug ? $"第 2 刺激 ({debugStr2})" : "第 2 刺激";
        string msg2 = isDebug ? $"【 第 2 刺激 】 ({debugStr2})" : "【 第 2 刺激 】";
        trial.metadata["currentInterval"] = label2;
        uiCtrl?.SetMessage(msg2);

        yield return RunSingleInterval(controller, val2, cueDuration, intervalDuration);

        // ---- Response Prompt ----
        trial.metadata["currentInterval"] = "応答受付中";
        uiCtrl?.SetMessage("どちらが重かったですか？\n【1】第 1 刺激 (Z)   /   【2】第 2 刺激 (X)");
    }

    public override void OnTrialEnd(EXP_TrialData trial)
    {
        if (controller == null) return;
        ResetValueOnTrialEnd(controller);
        SetHapticsBypass(controller, false);
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
        HAP_HapticsIllusionFoxFootController ctrl,
        float value,
        float cueSecs,
        float durationSecs)
    {
        ApplyValueToController(ctrl, value);
        SetHapticsBypass(ctrl, false);

        if (cueSecs > 0f)
            yield return new WaitForSeconds(cueSecs);

        yield return new WaitForSeconds(durationSecs);
    }

    protected void StopHaptics(HAP_HapticsIllusionFoxFootController ctrl)
    {
        SetHapticsBypass(ctrl, true);
    }

    protected static void SetHapticsBypass(HAP_HapticsIllusionFoxFootController ctrl, bool bypass)
    {
        if (ctrl.autdController != null)
        {
            ctrl.autdController.bypassHaptics = bypass;
        }
        else
        {
            var mainController = Object.FindAnyObjectByType<HAP_AUTDHapticsController>();
            if (mainController != null)
            {
                mainController.bypassHaptics = bypass;
            }
            else
            {
                ctrl.enabled = !bypass;
            }
        }
    }
}
