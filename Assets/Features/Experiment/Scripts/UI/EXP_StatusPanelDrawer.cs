using UnityEngine;
using static EXP_PanelElementDrawers;

#nullable enable

/// <summary>
/// コントロールパネルのステータス・進捗・ヘッダー描画コンポーネント。
/// </summary>
public static class EXP_StatusPanelDrawer
{
    public static void DrawHeaderAndDebugToggle(EXP_ExperimentManager manager)
    {
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("被験者実験 コントロールダッシュボード", GetTitleStyle());
            GUILayout.FlexibleSpace();
            if (Application.isPlaying)
            {
                DrawBadge("実行中 (PLAY)", new Color(0.1f, 0.75f, 0.2f), 13, 26);
            }
            else
            {
                DrawBadge("停止中 (STOP)", new Color(0.5f, 0.5f, 0.5f), 13, 26);
            }
        }

        GUILayout.Space(4);

        if (Application.isPlaying && manager.config != null)
        {
            using (new GUILayout.HorizontalScope(GUI.skin.box))
            {
                bool isDebugMode = manager.config.isDebugMode;
                bool newDebugMode = GUILayout.Toggle(isDebugMode, " 🐞 デバッグ表示モード (DebugPlay: 被験者画面やパネルに詳細数値を表示)", GetBoldLabelStyle());
                if (newDebugMode != isDebugMode)
                {
                    manager.config.isDebugMode = newDebugMode;
                }

                GUILayout.FlexibleSpace();
                if (isDebugMode)
                {
                    DrawBadge("🐞 DEBUG MODE", new Color(0.85f, 0.45f, 0.1f), 11, 20);
                }
                else
                {
                    DrawBadge("🔒 BLIND (本番)", new Color(0.15f, 0.65f, 0.35f), 11, 20);
                }
            }
        }
    }

    public static void DrawActionGuideAndIntervalIndicator(EXP_ExperimentManager manager)
    {
        string guideText = manager.CurrentState switch
        {
            EXP_ExperimentState.Idle => "👉 【準備完了】「▶ 実験を開始する」ボタンを押してください。",
            EXP_ExperimentState.Instruction => "👉 【教示中】被験者に説明が表示されています。「次へ進む」を押してください。",
            EXP_ExperimentState.Practice => "👉 【練習試行中】練習セッションを実行中です。",
            EXP_ExperimentState.Trial => manager.CurrentPhase switch
            {
                EXP_TrialPhase.ITI => "⏳ 【試行間隔 (ITI)】次の試行を開始する準備をしています...",
                EXP_TrialPhase.Stimulus => "🔊 【刺激提示中】触覚刺激を照射しています。提示終了までお待ちください...",
                EXP_TrialPhase.Response => "🎯 【応答受付中】被験者に『第1刺激(Z)』か『第2刺激(X)』かを選んでもらってください。",
                EXP_TrialPhase.Feedback => "💬 【フィードバック提示中】結果を表示しています...",
                _ => "進行中..."
            },
            EXP_ExperimentState.Break => "☕ 【ブロック休憩中】準備ができたら「次へ進む」を押してください。",
            EXP_ExperimentState.Finished => "🎉 【全試行完了】実験が正常に終了しました！データは保存済みです。",
            _ => "待機中..."
        };

        DrawMessageBox(guideText);

        if (manager.CurrentState == EXP_ExperimentState.Trial && manager.CurrentTrial != null)
        {
            string currentInterval = manager.CurrentTrial.metadata.TryGetValue("currentInterval", out var val) ? val : "";
            if (!string.IsNullOrEmpty(currentInterval))
            {
                GUILayout.Space(6);
                Color badgeBg = currentInterval.Contains("第 1") ? new Color(0.9f, 0.25f, 0.25f)
                              : currentInterval.Contains("第 2") ? new Color(0.25f, 0.55f, 0.95f)
                              : currentInterval.Contains("応答") ? new Color(0.12f, 0.78f, 0.32f)
                              : new Color(0.55f, 0.55f, 0.55f);

                DrawBadge($"⚡ 現在の刺激フェーズ: {currentInterval}", badgeBg, 15, 36);
            }
        }
    }

    public static void DrawSessionProgressSection(EXP_ExperimentManager manager)
    {
        DrawSectionHeader("1. 実験の進捗・セッション概要");
        using (new GUILayout.VerticalScope(GUI.skin.box))
        {
            int completed = manager.CurrentSession?.completedTrials ?? 0;
            int total = manager.CurrentSession?.totalTrials ?? (manager.sequencer != null ? manager.sequencer.TotalTrials : 0);
            float progress = total > 0 ? (float)completed / total : 0f;

            string progressText = $"試行進捗: {completed} / {total} 試行完了 ({progress:P0})";
            DrawProgressBar(progress, progressText);

            GUILayout.Space(6);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label($"被験者 ID: {manager.CurrentSession?.participantId ?? "-"}", GetBoldLabelStyle());
                GUILayout.FlexibleSpace();
                GUILayout.Label($"正答率: {(manager.CurrentSession != null ? $"{manager.CurrentSession.accuracy:P1}" : "-")}", GetBoldLabelStyle());
            }
        }
    }

    public static void DrawStateAndPhaseSection(EXP_ExperimentManager manager)
    {
        DrawSectionHeader("2. 現在のステータスとフェーズ");
        using (new GUILayout.VerticalScope(GUI.skin.box))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("全体状態 (State):", GetBoldLabelStyle(), GUILayout.Width(130));
                DrawStateBadge(manager.CurrentState);
            }

            GUILayout.Space(6);

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("試行フェーズ (Phase):", GetBoldLabelStyle(), GUILayout.Width(130));
                DrawPhaseBadge(manager.CurrentPhase, manager);
            }

            if (!string.IsNullOrEmpty(manager.CurrentMessage))
            {
                GUILayout.Space(8);
                GUILayout.Label("💬 被験者画面に表示中のメッセージ:", GetBoldLabelStyle());
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label(manager.CurrentMessage, GetCenterBoldStyle());
                }
            }
        }
    }

    private static void DrawStateBadge(EXP_ExperimentState state)
    {
        (string label, Color color) = state switch
        {
            EXP_ExperimentState.Idle => ("未開始 (IDLE)", new Color(0.5f, 0.5f, 0.5f)),
            EXP_ExperimentState.Instruction => ("教示表示中 (INSTRUCTION)", new Color(0.2f, 0.6f, 0.9f)),
            EXP_ExperimentState.Practice => ("練習試行中 (PRACTICE)", new Color(0.9f, 0.55f, 0.1f)),
            EXP_ExperimentState.Trial => ("本試行中 (TRIAL)", new Color(0.2f, 0.75f, 0.3f)),
            EXP_ExperimentState.Break => ("ブロック休憩中 (BREAK)", new Color(0.85f, 0.7f, 0.15f)),
            EXP_ExperimentState.Finished => ("実験終了 (FINISHED)", new Color(0.6f, 0.3f, 0.85f)),
            _ => ("不明", Color.gray)
        };
        DrawBadge(label, color, 13, 28);
    }

    private static void DrawPhaseBadge(EXP_TrialPhase phase, EXP_ExperimentManager? manager)
    {
        string label;
        Color color;

        switch (phase)
        {
            case EXP_TrialPhase.ITI:
                label = "試行間隔 (ITI)";
                color = new Color(0.5f, 0.5f, 0.5f);
                break;

            case EXP_TrialPhase.Stimulus:
                string currentInterval = manager?.CurrentTrial?.metadata.TryGetValue("currentInterval", out var val) == true ? val : "";
                if (currentInterval.Contains("第 1") || currentInterval.Contains("Interval 1"))
                {
                    label = "🔊 第 1 刺激 提示中 (STIMULUS)";
                    color = new Color(0.9f, 0.25f, 0.25f);
                }
                else if (currentInterval.Contains("第 2") || currentInterval.Contains("Interval 2"))
                {
                    label = "🔊 第 2 刺激 提示中 (STIMULUS)";
                    color = new Color(0.25f, 0.55f, 0.95f);
                }
                else if (currentInterval.Contains("ISI") || currentInterval.Contains("無刺激"))
                {
                    label = "⏳ 無刺激間隔 (ISI)";
                    color = new Color(0.55f, 0.55f, 0.55f);
                }
                else
                {
                    label = "🔊 刺激提示中 (STIMULUS)";
                    color = new Color(0.9f, 0.3f, 0.3f);
                }
                break;

            case EXP_TrialPhase.Response:
                label = "🎯 応答受付中 (RESPONSE)";
                color = new Color(0.15f, 0.8f, 0.35f);
                break;

            case EXP_TrialPhase.Feedback:
                label = "💬 フィードバック (FEEDBACK)";
                color = new Color(0.25f, 0.7f, 0.9f);
                break;

            default:
                label = "-";
                color = Color.gray;
                break;
        }

        DrawBadge(label, color, 13, 28);
    }
}
