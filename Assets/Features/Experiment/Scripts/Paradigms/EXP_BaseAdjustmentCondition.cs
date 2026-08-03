using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【調整法実験条件の共通基底クラス (Method of Adjustment パラダイム)】
/// <see cref="EXP_BaseCondition"/> を継承し、被験者がリアルタイムに入力で物理値を上下調整し、
/// 主観的等価点 (PSE: Point of Subjective Equality) や閾値を探索・確定するパラダイムの抽象基底を提供します。
/// </summary>
public abstract class EXP_BaseAdjustmentCondition : EXP_BaseHapticsCondition
{
    // =====================================================
    // Adjustment Parameters
    // =====================================================

    [Header("Adjustment Settings")]
    [Tooltip("1回のキー入力で変化させる物理ステップ量")]
    public float stepSize = 1.0f;

    [Tooltip("物理値の最小可変限界値")]
    public float minLimit = 0.0f;

    [Tooltip("物理値の最大可変限界値")]
    public float maxLimit = 100.0f;

    // =====================================================
    // Abstract Interface (派生クラスで実装)
    // =====================================================

    protected abstract float GetInitialValue();
    protected abstract void ApplyValueToController(HAP_HapticsIllusionFoxFootController ctrl, float value);
    protected abstract string FormatValueForDebug(float value);

    // =====================================================
    // Base Implementations
    // =====================================================

    public override string ParadigmType => "Adjustment";

    public override void Apply(EXP_TrialData trial) { }

    public override IEnumerator StimulusCoroutine(EXP_TrialData trial, MonoBehaviour runner)
    {
        var ctrl = GetController();

        float initialValue = GetInitialValue();
        float currentValue = initialValue;

        trial.paradigmType = ParadigmType;
        trial.stimulusVal1 = initialValue;
        trial.metadata["initialValue"] = initialValue.ToString("F4");

        var expManager = runner as EXP_ExperimentManager ?? runner.GetComponent<EXP_ExperimentManager>() ?? Object.FindAnyObjectByType<EXP_ExperimentManager>();
        bool isDebug = expManager != null && expManager.isDebugMode;

        if (expManager != null)
            expManager.SetPhase(EXP_TrialPhase.Response);

        // 調整ループ（確定キーが押されるまでリアルタイム受付）
        bool confirmed = false;
        while (!confirmed)
        {
            if (ctrl != null)
            {
                ApplyValueToController(ctrl, currentValue);
                SetHapticsBypass(ctrl, false);
            }

            string debugStr = FormatValueForDebug(currentValue);
            string msg = isDebug
                ? $"【 調整中 】 現在値: {debugStr}\n【↑/↓】調整   /   【Space】確定"
                : "【 調整中 】 感覚が一致する位置に調整してください\n【↑/↓】調整   /   【Space】確定";

            trial.metadata["currentValue"] = currentValue.ToString("F4");
            trial.metadata["currentInterval"] = isDebug ? $"調整中 ({debugStr})" : "調整中";
            expManager?.SetMessage(msg);

            // リアルタイムキー監視
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                currentValue = Mathf.Clamp(currentValue + stepSize, minLimit, maxLimit);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                currentValue = Mathf.Clamp(currentValue - stepSize, minLimit, maxLimit);
            }
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                confirmed = true;
            }

            yield return null;
        }

        // 確定値を最終記録
        trial.stimulusVal2 = currentValue;
        trial.comparisonDetail = $"Initial: {initialValue:F4} -> Adjusted: {currentValue:F4}";
        trial.responseValue = currentValue.ToString("F4");

        trial.metadata["finalAdjustedValue"] = currentValue.ToString("F4");
        trial.metadata["adjustmentDelta"]    = (currentValue - initialValue).ToString("F4");
        expManager?.SetMessage("✅ 調整確定");
    }

    public override string FormatResponseValue(EXP_TrialData trial, string rawResponse)
    {
        if (!string.IsNullOrEmpty(trial.responseValue))
            return trial.responseValue;
        return rawResponse;
    }

    public override bool? EvaluateResponse(EXP_TrialData trial) => null;
}
