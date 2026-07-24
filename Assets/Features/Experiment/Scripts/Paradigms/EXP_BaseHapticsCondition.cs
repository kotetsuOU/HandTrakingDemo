using UnityEngine;

#nullable enable

/// <summary>
/// 【触覚制御を伴う実験条件の共通抽象基底クラス】
/// <see cref="EXP_BaseCondition"/> を継承し、触覚コントローラー（<see cref="HAP_HapticsIllusionFoxFootController"/>）の
/// 参照管理・取得、試行終了時のパラメータリセット、およびハプティクスのバイパス（ON/OFF）制御を一括提供します。
/// </summary>
public abstract class EXP_BaseHapticsCondition : EXP_BaseCondition
{
    // =====================================================
    // Reference
    // =====================================================

    [HideInInspector]
    [System.NonSerialized]
    public HAP_HapticsIllusionFoxFootController? controller;

    /// <summary>
    /// コントローラー参照を取得・自動キャッシュします。
    /// </summary>
    public HAP_HapticsIllusionFoxFootController? GetController()
    {
        if (controller == null)
            controller = Object.FindAnyObjectByType<HAP_HapticsIllusionFoxFootController>();
        return controller;
    }

    // =====================================================
    // Virtual / Abstract Handlers
    // =====================================================

    /// <summary>
    /// 試行終了時にコントローラーのパラメータをリセットする派生クラス固有のロジック。
    /// </summary>
    protected virtual void ResetValueOnTrialEnd(HAP_HapticsIllusionFoxFootController ctrl) { }

    // =====================================================
    // Lifecycle Overrides
    // =====================================================

    /// <summary>
    /// 試行終了時の共通共通後処理。
    /// コントローラーの値を初期化し、触覚出力をバイパス（停止）させます。
    /// </summary>
    public override void OnTrialEnd(EXP_TrialData trial)
    {
        var ctrl = GetController();
        if (ctrl != null)
        {
            ResetValueOnTrialEnd(ctrl);
            SetHapticsBypass(ctrl, true); // 試行終了 → 出力停止
        }
    }

    // =====================================================
    // Haptics Control Helpers
    // =====================================================

    /// <summary>
    /// 触覚出力のバイパス（ON/OFF）を一元制御します。
    /// </summary>
    /// <param name="ctrl">対象のコントローラー</param>
    /// <param name="bypass">true = 停止（バイパス）、false = 照射開始</param>
    protected void SetHapticsBypass(HAP_HapticsIllusionFoxFootController? ctrl, bool bypass)
    {
        if (ctrl == null) ctrl = GetController();
        if (ctrl == null) return;

        if (ctrl.autdController != null)
        {
            ctrl.autdController.bypassHaptics = bypass;
        }
        else
        {
            var mainController = Object.FindAnyObjectByType<HAP_AUTDHapticsController>();
            if (mainController != null)
                mainController.bypassHaptics = bypass;
            else
                ctrl.enabled = !bypass;
        }
    }
}
