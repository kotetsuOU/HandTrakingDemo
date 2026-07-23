using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【調整法実験条件の共通基底クラス (Method of Adjustment パラダイム)】
/// <see cref="EXP_BaseCondition"/> を継承し、被験者がリアルタイムに入力で物理値を上下調整し、
/// 主観的等価点 (PSE: Point of Subjective Equality) や閾値を探索・確定するパラダイムの抽象基底を提供します。
/// </summary>
public abstract class EXP_BaseAdjustmentCondition : EXP_BaseCondition
{
    // =====================================================
    // Reference
    // =====================================================

    [HideInInspector]
    [System.NonSerialized]
    public HAP_HapticsIllusionFoxFootController? controller;

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

        float currentValue = GetInitialValue();
        trial.metadata["initialValue"] = currentValue.ToString("F4");

        var expManager = runner as EXP_ExperimentManager ?? runner.GetComponent<EXP_ExperimentManager>() ?? Object.FindAnyObjectByType<EXP_ExperimentManager>();
        bool isDebug = expManager != null && expManager.isDebugMode;

        if (expManager != null)
            expManager.SetPhase(EXP_TrialPhase.Response);

        // 調整ループ（確定キーが押されるまでリアルタイム受付）
        bool confirmed = false;
        while (!confirmed)
        {
            if (controller != null)
            {
                ApplyValueToController(controller, currentValue);
                SetHapticsBypass(controller, false);
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
        trial.metadata["finalAdjustedValue"] = currentValue.ToString("F4");
        expManager?.SetMessage("✅ 調整確定");
    }

    public override void OnTrialEnd(EXP_TrialData trial)
    {
        if (controller != null)
        {
            ResetValueOnTrialEnd(controller);
            SetHapticsBypass(controller, false);
        }
    }

    public override bool? EvaluateResponse(EXP_TrialData trial) => null;

    protected static void SetHapticsBypass(HAP_HapticsIllusionFoxFootController ctrl, bool bypass)
    {
        if (ctrl.autdController != null) ctrl.autdController.bypassHaptics = bypass;
        else ctrl.enabled = !bypass;
    }
}
