using UnityEngine;
using static EXP_PanelElementDrawers;
using static EXP_StatusPanelDrawer;
using static EXP_ControlInputPanelDrawer;

#nullable enable

/// <summary>
/// 【実験用コントロールダッシュボード 描画メインオーケストレーター】
/// <see cref="EXP_StatusPanelDrawer"/> と <see cref="EXP_ControlInputPanelDrawer"/> を順次呼び出し、
/// ダッシュボード全体のレイアウトを統括します（200行以内）。
/// </summary>
public static class EXP_ControlPanelDrawer
{
    private static Vector2 _scrollPos;

    /// <summary>
    /// 実験コントロールパネルの全体を描画します。
    /// EditorWindow と In-Game Runtime HUD の両方から呼び出せます。
    /// </summary>
    public static void DrawDashboard(EXP_ExperimentManager manager, bool isEditorWindow = true)
    {
        GUILayout.Space(6);

        // 0. ヘッダー & デバッグ表示モード切替
        DrawHeaderAndDebugToggle(manager);

        GUILayout.Space(6);

        if (!Application.isPlaying)
        {
            DrawMessageBox("💡 Unity の Play ボタンを押すかアプリを起動すると、ダッシュボードがアクティブになります。");
            return;
        }

        _scrollPos = GUILayout.BeginScrollView(_scrollPos);

        // ナビゲーション・操作ガイド & リアルタイム刺激インジケーター
        DrawActionGuideAndIntervalIndicator(manager);

        GUILayout.Space(10);

        // 1. 進捗バー & セッション概要
        DrawSessionProgressSection(manager);

        GUILayout.Space(10);

        // 2. 現在のステータス & フェーズ
        DrawStateAndPhaseSection(manager);

        GUILayout.Space(10);

        // 3. 現在の試行条件パラメータ
        DrawTrialConditionSection(manager);

        GUILayout.Space(10);

        // 4. メイン操作ボタン
        DrawControlButtonsSection(manager);

        GUILayout.Space(12);

        // 5. 参加者応答入力パネル
        DrawResponsePanelSection(manager);

        GUILayout.EndScrollView();
    }
}
