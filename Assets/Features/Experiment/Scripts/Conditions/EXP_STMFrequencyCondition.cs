using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【実験条件】STM 周波数の知覚重さ比較（2AFC）。
/// <para>
/// Reference 刺激（基準周波数）と Comparison 刺激（比較周波数）を2インターバルで順次提示し、
/// 「どちらが重く感じたか」を参加者に判断させます（Two-Alternative Forced Choice）。
/// </para>
/// <para>
/// <b>使い方:</b>
/// <list type="number">
/// <item>
///   このアセットを Project ウィンドウで比較周波数の数だけ作成し、
///   それぞれの <see cref="comparisonFrequency"/> に比較したい Hz 値を設定します。
/// </item>
/// <item>全アセットを <c>EXP_TrialSequencer.conditions</c> に登録します。</item>
/// <item>応答キーは「1つ目が重い: Z」「2つ目が重い: X」などを <c>EXP_ExperimentConfig</c> で設定します。</item>
/// </list>
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

    [Header("2AFC Parameters")]
    [Tooltip("基準刺激の STM 周波数 [Hz]（常に固定）")]
    [Min(1f)]
    public float referenceFrequency = 80f;

    [Tooltip("比較刺激の STM 周波数 [Hz]（この条件で変化させる値）")]
    [Min(1f)]
    public float comparisonFrequency = 120f;

    [Tooltip("インターバル1とインターバル2を参加者ごとにカウンターバランスする\n"
           + "（true: ランダムに提示順序を入れ替え、metadata に記録します）")]
    public bool counterbalanceOrder = true;

    [Header("Timing")]
    [Tooltip("各インターバルの刺激提示時間 [秒]")]
    [Min(0.1f)]
    public float intervalDuration = 2.0f;

    [Tooltip("インターバル間の無刺激 ISI (inter-stimulus interval) [秒]")]
    [Min(0f)]
    public float isiDuration = 0.5f;

    [Tooltip("インターバル開始直前の合図を表示する時間 [秒]（0 = 表示なし）")]
    [Min(0f)]
    public float cueDuration = 0.3f;

    // =====================================================
    // Apply (2AFC なので StimulusCoroutine を使用)
    // =====================================================

    /// <summary>StimulusCoroutine を使用するため空実装。</summary>
    public override void Apply(EXP_TrialData trial) { }

    /// <summary>
    /// 2AFC 刺激提示コルーチン。
    /// Interval 1 → ISI → Interval 2 の順に実行します。
    /// </summary>
    public override IEnumerator StimulusCoroutine(EXP_TrialData trial, MonoBehaviour runner)
    {
        // コントローラーを自動取得
        if (controller == null)
            controller = Object.FindAnyObjectByType<HAP_HapticsIllusionFoxFootController>();

        if (controller == null)
        {
            Debug.LogError("[EXP_STMFrequencyCondition] HAP_HapticsIllusionFoxFootController が見つかりません。");
            yield break;
        }

        // 提示順序の決定（counterbalance）
        bool refFirst = !counterbalanceOrder || (Random.value >= 0.5f);
        float freq1 = refFirst ? referenceFrequency : comparisonFrequency;
        float freq2 = refFirst ? comparisonFrequency : referenceFrequency;

        // メタデータに記録
        trial.metadata["referenceFrequency"]  = referenceFrequency.ToString("F2");
        trial.metadata["comparisonFrequency"] = comparisonFrequency.ToString("F2");
        trial.metadata["refFirst"]            = refFirst.ToString();
        trial.metadata["interval1Frequency"]  = freq1.ToString("F2");
        trial.metadata["interval2Frequency"]  = freq2.ToString("F2");

        float originalFrequency = controller.stmFrequency;

        // ---- Interval 1 ----
        trial.metadata["currentInterval"] = "第 1 刺激 (Interval 1)";
        var uiCtrl = runner.GetComponent<EXP_UIController>() ?? Object.FindAnyObjectByType<EXP_UIController>();
        uiCtrl?.SetMessage("【 第 1 刺激 】提示中");
        yield return RunInterval(controller, freq1, "Interval1", cueDuration, intervalDuration);

        // ---- ISI ----
        trial.metadata["currentInterval"] = "無刺激間隔 (ISI)";
        uiCtrl?.SetMessage("・ ・ ・");
        StopHaptics(controller, originalFrequency);
        if (isiDuration > 0f)
            yield return new WaitForSeconds(isiDuration);

        // ---- Interval 2 ----
        trial.metadata["currentInterval"] = "第 2 刺激 (Interval 2)";
        uiCtrl?.SetMessage("【 第 2 刺激 】提示中");
        yield return RunInterval(controller, freq2, "Interval2", cueDuration, intervalDuration);

        // ---- Response Prompt ----
        trial.metadata["currentInterval"] = "応答受付中";
        uiCtrl?.SetMessage("どちらが重かったですか？\n【1】第1刺激 (Z)   /   【2】第2刺激 (X)");
    }

    public override void OnTrialEnd(EXP_TrialData trial)
    {
        if (controller == null) return;

        // 周波数を基準値に戻す
        controller.stmFrequency = referenceFrequency;
        SetHapticsBypass(controller, false);
    }

    /// <summary>正誤判定なし（閾値推定実験では常に null）。</summary>
    public override bool? EvaluateResponse(EXP_TrialData trial) => null;

    // =====================================================
    // Private Helpers
    // =====================================================

    private IEnumerator RunInterval(
        HAP_HapticsIllusionFoxFootController ctrl,
        float frequency,
        string label,
        float cueSecs,
        float durationSecs)
    {
        ctrl.stmFrequency = frequency;
        SetHapticsBypass(ctrl, false);

        if (cueSecs > 0f)
            yield return new WaitForSeconds(cueSecs);

        yield return new WaitForSeconds(durationSecs);
    }

    private void StopHaptics(HAP_HapticsIllusionFoxFootController ctrl, float originalFrequency)
    {
        SetHapticsBypass(ctrl, true);
        ctrl.stmFrequency = originalFrequency;
    }

    private void SetHapticsBypass(HAP_HapticsIllusionFoxFootController ctrl, bool bypass)
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
