using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【実験条件】STM 周波数の知覚重さ比較（2AFC）。
/// <para>
/// 2つの刺激（第1刺激・第2刺激）を順次提示し、「どちらが重く感じたか」を参加者に判断させます。
/// <see cref="afcMode"/> により「基準 vs 可変」「完全ランダムペア（一対比較）」「固定ペア」を切替可能です。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "STMFrequencyCondition", menuName = "EXP/Conditions/STMFrequencyCondition")]
public class EXP_STMFrequencyCondition : EXP_BaseCondition
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
           + "• FixedPair: 指定の referenceFrequency vs comparisonFrequency の固定ペア")]
    public EXP_2AFCMode afcMode = EXP_2AFCMode.RandomPair;

    [Header("Frequency Settings")]
    [Tooltip("基準刺激の STM 周波数 [Hz]（ReferenceVsComparison モードで使用）")]
    [Min(1f)]
    public float referenceFrequency = 80f;

    [Tooltip("固定の比較刺激 STM 周波数 [Hz]（FixedPair モードで使用）")]
    [Min(1f)]
    public float comparisonFrequency = 120f;

    [Tooltip("周波数候補のリスト [Hz]（ReferenceVsComparison / RandomPair モードで使用）")]
    public float[] candidateFrequencies = new float[] { 20f, 40f, 60f, 80f, 100f, 120f, 140f, 160f };

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
            Debug.LogError("[EXP_STMFrequencyCondition] HAP_HapticsIllusionFoxFootController が見つかりません。");
            yield break;
        }

        // モードに応じた周波数ペアの決定
        (float freq1, float freq2, string refPos) = DetermineFrequencyPair();

        // メタデータに記録
        trial.metadata["afcMode"]            = afcMode.ToString();
        trial.metadata["interval1Frequency"] = freq1.ToString("F2");
        trial.metadata["interval2Frequency"] = freq2.ToString("F2");
        trial.metadata["frequencyDelta"]     = Mathf.Abs(freq1 - freq2).ToString("F2");
        trial.metadata["referencePosition"]  = refPos;

        float originalFrequency = controller.stmFrequency;

        // ---- Interval 1 ----
        trial.metadata["currentInterval"] = $"第 1 刺激 ({freq1:F0} Hz)";
        var uiCtrl = runner.GetComponent<EXP_UIController>() ?? Object.FindAnyObjectByType<EXP_UIController>();
        uiCtrl?.SetMessage($"【 第 1 刺激 】提示中 ({freq1:F0} Hz)");
        yield return RunInterval(controller, freq1, "Interval1", cueDuration, intervalDuration);

        // ---- ISI ----
        trial.metadata["currentInterval"] = "無刺激間隔 (ISI)";
        uiCtrl?.SetMessage("・ ・ ・");
        StopHaptics(controller, originalFrequency);
        if (isiDuration > 0f)
            yield return new WaitForSeconds(isiDuration);

        // ---- Interval 2 ----
        trial.metadata["currentInterval"] = $"第 2 刺激 ({freq2:F0} Hz)";
        uiCtrl?.SetMessage($"【 第 2 刺激 】提示中 ({freq2:F0} Hz)");
        yield return RunInterval(controller, freq2, "Interval2", cueDuration, intervalDuration);

        // ---- Response Prompt ----
        trial.metadata["currentInterval"] = "応答受付中";
        uiCtrl?.SetMessage("どちらが重かったですか？\n【1】第1刺激 (Z)   /   【2】第2刺激 (X)");
    }

    public override void OnTrialEnd(EXP_TrialData trial)
    {
        if (controller == null) return;
        ApplyFrequencyToAllControllers(controller, referenceFrequency);
        SetHapticsBypass(controller, false);
    }

    public override bool? EvaluateResponse(EXP_TrialData trial) => null;

    // =====================================================
    // Private Helpers
    // =====================================================

    private (float freq1, float freq2, string refPos) DetermineFrequencyPair()
    {
        bool swap = randomizePresentationOrder && (Random.value < 0.5f);

        if (afcMode == EXP_2AFCMode.RandomPair && candidateFrequencies != null && candidateFrequencies.Length >= 2)
        {
            // 候補から重複しない異なる2つの周波数をランダム抽出
            int idxA = Random.Range(0, candidateFrequencies.Length);
            int idxB;
            do { idxB = Random.Range(0, candidateFrequencies.Length); } while (idxB == idxA);

            float fA = candidateFrequencies[idxA];
            float fB = candidateFrequencies[idxB];
            return swap ? (fB, fA, "N/A (RandomPair)") : (fA, fB, "N/A (RandomPair)");
        }
        else if (afcMode == EXP_2AFCMode.ReferenceVsComparison && candidateFrequencies != null && candidateFrequencies.Length > 0)
        {
            // 基準値 vs 候補からランダム抽出した比較値
            float cmpFreq = candidateFrequencies[Random.Range(0, candidateFrequencies.Length)];
            return swap
                ? (cmpFreq, referenceFrequency, "Interval2")
                : (referenceFrequency, cmpFreq, "Interval1");
        }
        else
        {
            // 固定ペア
            return swap
                ? (comparisonFrequency, referenceFrequency, "Interval2")
                : (referenceFrequency, comparisonFrequency, "Interval1");
        }
    }

    private IEnumerator RunInterval(
        HAP_HapticsIllusionFoxFootController ctrl,
        float frequency,
        string label,
        float cueSecs,
        float durationSecs)
    {
        ApplyFrequencyToAllControllers(ctrl, frequency);
        SetHapticsBypass(ctrl, false);

        if (cueSecs > 0f)
            yield return new WaitForSeconds(cueSecs);

        yield return new WaitForSeconds(durationSecs);
    }

    private void StopHaptics(HAP_HapticsIllusionFoxFootController ctrl, float originalFrequency)
    {
        SetHapticsBypass(ctrl, true);
        ApplyFrequencyToAllControllers(ctrl, originalFrequency);
    }

    private static void ApplyFrequencyToAllControllers(HAP_HapticsIllusionFoxFootController ctrl, float freq)
    {
        ctrl.stmFrequency = freq;
        ctrl.sequentialSTMFrequency = freq;

        if (ctrl.autdController != null)
        {
            ctrl.autdController.stmFrequency = freq;
        }

        var mainController = Object.FindAnyObjectByType<HAP_AUTDHapticsController>();
        if (mainController != null)
        {
            mainController.stmFrequency = freq;
        }
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
