using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【実験条件】OppositeOffset Y 値の知覚重さ比較（2AFC）。
/// <para>
/// 2つの刺激（第1刺激・第2刺激）を順次提示し、「どちらが重く感じたか」を参加者に判断させます。
/// <see cref="afcMode"/> により「基準 vs 可変」「完全ランダムペア（一対比較）」「固定ペア」を切替可能です。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "OppositeOffsetCondition", menuName = "EXP/Conditions/OppositeOffsetCondition")]
public class EXP_OppositeOffsetCondition : EXP_BaseCondition
{
    // =====================================================
    // Reference
    // =====================================================

    [HideInInspector]
    [System.NonSerialized]
    public HAP_HapticsIllusionFoxFootController? controller;

    // =====================================================
    // Condition Parameters
    // =====================================================

    [Header("2AFC Pair Mode")]
    [Tooltip("2AFC 刺激ペアの構成モード:\n"
           + "• ReferenceVsComparison: 基準値(固定) vs 候補リストから選出\n"
           + "• RandomPair: 候補リストから2つの異なる刺激を完全にランダム抽出 (一対比較法)\n"
           + "• FixedPair: 指定の referenceOffsetY vs comparisonOffsetY の固定ペア")]
    public EXP_2AFCMode afcMode = EXP_2AFCMode.RandomPair;

    [Header("Offset Settings")]
    [Tooltip("基準刺激の OppositeOffset Y 値 [m]（ReferenceVsComparison モードで使用、例: 0.03 = 3cm）")]
    public float referenceOffsetY = 0.03f;

    [Tooltip("固定の比較刺激 OppositeOffset Y 値 [m]（FixedPair モードで使用）")]
    public float comparisonOffsetY = 0.0f;

    [Tooltip("Y オフセット候補のリスト [m]（ReferenceVsComparison / RandomPair モードで使用）\n（例: -0.04 〜 0.02 m）")]
    public float[] candidateOffsetsY = new float[] { -0.04f, -0.03f, -0.02f, -0.01f, 0.0f, 0.01f, 0.02f };

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
    // Apply (2AFC なので StimulusCoroutine を使用)
    // =====================================================

    public override void Apply(EXP_TrialData trial) { }

    /// <summary>
    /// 2AFC 刺激提示コルーチン。
    /// Interval 1 → ISI → Interval 2 の順に実行します。
    /// </summary>
    public override IEnumerator StimulusCoroutine(EXP_TrialData trial, MonoBehaviour runner)
    {
        if (controller == null)
            controller = Object.FindAnyObjectByType<HAP_HapticsIllusionFoxFootController>();

        if (controller == null)
        {
            Debug.LogError("[EXP_OppositeOffsetCondition] HAP_HapticsIllusionFoxFootController が見つかりません。");
            yield break;
        }

        // モードに応じた Y オフセットペアの決定
        (float y1, float y2, string refPos) = DetermineOffsetPair();

        // メタデータに記録
        trial.metadata["afcMode"]           = afcMode.ToString();
        trial.metadata["interval1Y"]        = y1.ToString("F4");
        trial.metadata["interval2Y"]        = y2.ToString("F4");
        trial.metadata["offsetDelta"]       = Mathf.Abs(y1 - y2).ToString("F4");
        trial.metadata["referencePosition"] = refPos;

        float originalY = controller.oppositeOffset.y;

        var expManager = runner.GetComponent<EXP_ExperimentManager>() ?? Object.FindAnyObjectByType<EXP_ExperimentManager>();
        bool isDebug = expManager != null && expManager.config != null && expManager.config.isDebugMode;

        // ---- Interval 1 ----
        string label1 = isDebug ? $"第 1 刺激 (Y: {y1 * 100:F1} cm)" : "第 1 刺激";
        string msg1 = isDebug ? $"【 第 1 刺激 】 (Y: {y1 * 100:F1} cm)" : "【 第 1 刺激 】";
        trial.metadata["currentInterval"] = label1;
        var uiCtrl = runner.GetComponent<EXP_UIController>() ?? Object.FindAnyObjectByType<EXP_UIController>();
        uiCtrl?.SetMessage(msg1);
        yield return RunInterval(controller, y1, originalY, "Interval1", cueDuration, intervalDuration);

        // ---- ISI ----
        trial.metadata["currentInterval"] = "無刺激間隔 (ISI)";
        uiCtrl?.SetMessage("・ ・ ・");
        StopHaptics(controller, originalY);
        if (isiDuration > 0f)
            yield return new WaitForSeconds(isiDuration);

        // ---- Interval 2 ----
        string label2 = isDebug ? $"第 2 刺激 (Y: {y2 * 100:F1} cm)" : "第 2 刺激";
        string msg2 = isDebug ? $"【 第 2 刺激 】 (Y: {y2 * 100:F1} cm)" : "【 第 2 刺激 】";
        trial.metadata["currentInterval"] = label2;
        uiCtrl?.SetMessage(msg2);
        yield return RunInterval(controller, y2, originalY, "Interval2", cueDuration, intervalDuration);

        // ---- Response Prompt ----
        trial.metadata["currentInterval"] = "応答受付中";
        uiCtrl?.SetMessage("どちらが重かったですか？\n【1】第1刺激 (Z)   /   【2】第2刺激 (X)");
    }

    public override void OnTrialEnd(EXP_TrialData trial)
    {
        if (controller == null) return;
        var off = controller.oppositeOffset;
        off.y = referenceOffsetY;
        controller.oppositeOffset = off;
        SetHapticsBypass(controller, false);
    }

    public override bool? EvaluateResponse(EXP_TrialData trial) => null;

    // =====================================================
    // Private Helpers
    // =====================================================

    private (float y1, float y2, string refPos) DetermineOffsetPair()
    {
        bool swap = randomizePresentationOrder && (Random.value < 0.5f);

        if (afcMode == EXP_2AFCMode.RandomPair && candidateOffsetsY != null && candidateOffsetsY.Length >= 2)
        {
            // 候補から重複しない異なる2つの Y オフセットをランダム抽出
            int idxA = Random.Range(0, candidateOffsetsY.Length);
            int idxB;
            do { idxB = Random.Range(0, candidateOffsetsY.Length); } while (idxB == idxA);

            float yA = candidateOffsetsY[idxA];
            float yB = candidateOffsetsY[idxB];
            return swap ? (yB, yA, "N/A (RandomPair)") : (yA, yB, "N/A (RandomPair)");
        }
        else if (afcMode == EXP_2AFCMode.ReferenceVsComparison && candidateOffsetsY != null && candidateOffsetsY.Length > 0)
        {
            // 基準値 vs 候補からランダム抽出した比較値
            float cmpY = candidateOffsetsY[Random.Range(0, candidateOffsetsY.Length)];
            return swap
                ? (cmpY, referenceOffsetY, "Interval2")
                : (referenceOffsetY, cmpY, "Interval1");
        }
        else
        {
            // 固定ペア
            return swap
                ? (comparisonOffsetY, referenceOffsetY, "Interval2")
                : (referenceOffsetY, comparisonOffsetY, "Interval1");
        }
    }

    private IEnumerator RunInterval(
        HAP_HapticsIllusionFoxFootController ctrl,
        float offsetY,
        float originalY,
        string label,
        float cueSecs,
        float durationSecs)
    {
        var off = ctrl.oppositeOffset;
        off.y = offsetY;
        ctrl.oppositeOffset = off;
        SetHapticsBypass(ctrl, false);

        if (cueSecs > 0f)
            yield return new WaitForSeconds(cueSecs);

        yield return new WaitForSeconds(durationSecs);
    }

    private void StopHaptics(HAP_HapticsIllusionFoxFootController ctrl, float originalY)
    {
        SetHapticsBypass(ctrl, true);
        var off = ctrl.oppositeOffset;
        off.y = originalY;
        ctrl.oppositeOffset = off;
    }

    private static void SetHapticsBypass(HAP_HapticsIllusionFoxFootController ctrl, bool bypass)
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
