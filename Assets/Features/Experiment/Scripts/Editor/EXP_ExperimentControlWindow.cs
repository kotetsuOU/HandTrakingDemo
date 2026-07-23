using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#nullable enable

/// <summary>
/// 【実験用コントロールダッシュボード】（日本語版）
/// Unity Editor メニュー Tools -> EXP -> 実験コントロールパネル から開ける独立した EditorWindow。
/// <para>
/// Build しなくても Unity Editor の Play モード中に別ウィンドウ（独立パネル）としてポップアウト・サブモニター等に配置し、
/// 今何を行うべきかのガイド付きで、実験の全ステータス（進捗バー、フェーズ、刺激パラメータ、被験者向けメッセージ）を日本語でリアルタイム表示・操作できます。
/// </para>
/// </summary>
public class EXP_ExperimentControlWindow : EditorWindow
{
    private Vector2 _scrollPosition;

    [MenuItem("Tools/EXP/実験コントロールパネル")]
    [MenuItem("Tools/EXP/Experiment Control Panel")]
    public static void OpenWindow()
    {
        var window = GetWindow<EXP_ExperimentControlWindow>("実験コントロールパネル");
        window.minSize = new Vector2(380, 560);
        window.Show();
    }

    void OnEnable()
    {
        EditorApplication.update += Repaint;
    }

    void OnDisable()
    {
        EditorApplication.update -= Repaint;
    }

    void OnGUI()
    {
        EditorGUILayout.Space(8);

        // ヘッダー
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("被験者実験 コントロールダッシュボード", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (EditorApplication.isPlaying)
            {
                DrawBadge("実行中 (PLAY)", new Color(0.1f, 0.75f, 0.2f));
            }
            else
            {
                DrawBadge("停止中 (STOP)", new Color(0.5f, 0.5f, 0.5f));
            }
        }

        EditorGUILayout.Space(6);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("💡 Unity の Play ボタンを押すと、ダッシュボードがアクティブになり操作可能になります。", MessageType.Info);
            return;
        }

        var manager = Object.FindAnyObjectByType<EXP_ExperimentManager>();
        if (manager == null)
        {
            EditorGUILayout.HelpBox("❌ シーン内に EXP_ExperimentManager が見つかりません。ヒエラルキーを確認してください。", MessageType.Error);
            return;
        }

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        // =====================================================
        // 0. ナビゲーション・操作ガイド（今何をすべきか？）
        // =====================================================
        DrawActionGuide(manager);

        EditorGUILayout.Space(10);

        // =====================================================
        // 1. 進捗バー & セッション概要
        // =====================================================
        DrawSectionHeader("1. 実験の進捗・セッション概要");

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            int completed = manager.CurrentSession?.completedTrials ?? 0;
            int total = manager.CurrentSession?.totalTrials ?? (manager.sequencer != null ? manager.sequencer.TotalTrials : 0);
            float progress = total > 0 ? (float)completed / total : 0f;

            string progressText = $"試行進捗: {completed} / {total} 試行完了 ({progress:P0})";
            Rect progressRect = GUILayoutUtility.GetRect(18, 24, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(progressRect, progress, progressText);

            EditorGUILayout.Space(6);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("被験者 ID:", manager.CurrentSession?.participantId ?? "-", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("正答率:", manager.CurrentSession != null ? $"{manager.CurrentSession.accuracy:P1}" : "-", EditorStyles.boldLabel);
            }
        }

        EditorGUILayout.Space(10);

        // =====================================================
        // 2. 現在のステータス & フェーズ
        // =====================================================
        DrawSectionHeader("2. 現在のステータスとフェーズ");

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("全体状態:", GUILayout.Width(70));
                DrawStateBadge(manager.CurrentState);

                GUILayout.Space(10);

                EditorGUILayout.LabelField("試行フェーズ:", GUILayout.Width(80));
                DrawPhaseBadge(manager.CurrentPhase);
            }

            // 被験者に提示中のメッセージ
            var uiCtrl = manager.uiController ?? manager.GetComponent<EXP_UIController>();
            if (uiCtrl != null && uiCtrl.messageText != null && !string.IsNullOrEmpty(uiCtrl.messageText.text))
            {
                EditorGUILayout.Space(8);
                GUILayout.Label("💬 被験者画面に表示中のメッセージ:", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.SelectableLabel(uiCtrl.messageText.text, EditorStyles.wordWrappedLabel, GUILayout.Height(38));
                }
            }
        }

        EditorGUILayout.Space(10);

        // =====================================================
        // 3. 現在の試行条件パラメータ
        // =====================================================
        DrawSectionHeader("3. 実行中の試行条件パラメータ");

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            if (manager.CurrentTrial != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("現在の条件名:", manager.CurrentTrial.conditionName, EditorStyles.boldLabel);
                    if (manager.CurrentTrial.isPractice)
                    {
                        DrawBadge("練習試行", new Color(0.9f, 0.55f, 0.1f));
                    }
                    else
                    {
                        DrawBadge("本試行", new Color(0.2f, 0.6f, 0.9f));
                    }
                }

                if (manager.CurrentTrial.metadata.Count > 0)
                {
                    EditorGUILayout.Space(4);
                    GUILayout.Label("詳細パラメータ:", EditorStyles.miniBoldLabel);
                    foreach (var kv in manager.CurrentTrial.metadata)
                    {
                        string japaneseKey = TranslateMetadataKey(kv.Key);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"  • {japaneseKey} ({kv.Key})", GUILayout.Width(220));
                            EditorGUILayout.LabelField(kv.Value, EditorStyles.boldLabel);
                        }
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("現在実行中の試行はありません（待機中）", EditorStyles.centeredGreyMiniLabel);
            }
        }

        EditorGUILayout.Space(10);

        // =====================================================
        // 4. メイン操作ボタン（開始・中断・進行）
        // =====================================================
        DrawSectionHeader("4. 実験コントロール操作");

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = (manager.CurrentState == EXP_ExperimentState.Idle);
            var prevBg = GUI.backgroundColor;
            if (GUI.enabled) GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);

            if (GUILayout.Button("▶ 実験を開始する (Space)", GUILayout.Height(40)))
            {
                manager.StartExperiment();
            }

            GUI.backgroundColor = prevBg;
            GUI.enabled = (manager.CurrentState != EXP_ExperimentState.Idle && manager.CurrentState != EXP_ExperimentState.Finished);
            if (GUI.enabled) GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);

            if (GUILayout.Button("■ 実験を中断する (Esc)", GUILayout.Height(40)))
            {
                manager.AbortExperiment();
            }

            GUI.backgroundColor = prevBg;
            GUI.enabled = true;
        }

        EditorGUILayout.Space(12);

        // =====================================================
        // 5. 参加者応答パネル（入力ボタン）
        // =====================================================
        DrawSectionHeader("5. 参加者応答入力パネル");

        var inputHandler = manager.inputHandler ?? manager.GetComponent<EXP_InputHandler>();
        bool isListening = inputHandler != null && inputHandler.IsListening &&
                           (!inputHandler.blockAfterFirstResponse || !inputHandler.HasResponded);

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("入力受付状態:", EditorStyles.boldLabel, GUILayout.Width(90));
                if (isListening)
                {
                    DrawBadge("● 応答受付中 (ボタン/キー有効)", new Color(0.1f, 0.8f, 0.3f));
                }
                else
                {
                    DrawBadge("入力ロック (待機中)", new Color(0.5f, 0.5f, 0.5f));
                }
            }

            EditorGUILayout.Space(8);

            GUI.enabled = isListening;
            var defaultBg = GUI.backgroundColor;

            if (isListening) GUI.backgroundColor = new Color(0.55f, 0.88f, 1.0f);

            // 教示中や休憩中用の決定・次へボタン
            if (manager.CurrentState == EXP_ExperimentState.Instruction || manager.CurrentState == EXP_ExperimentState.Break)
            {
                if (GUILayout.Button("👉 次へ進む / 準備完了 (クリック)", GUILayout.Height(48)))
                {
                    inputHandler?.TriggerResponse("Space");
                }
            }
            else
            {
                // 2AFC 専用巨大応答ボタン
                GUILayout.Label("重さ比較の判断選択 (直接クリックで回答):", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("【 1 】 第 1 刺激が重い\n(Z キー / クリック)", GUILayout.Height(54)))
                    {
                        inputHandler?.TriggerResponse("Z");
                    }
                    if (GUILayout.Button("【 2 】 第 2 刺激が重い\n(X キー / クリック)", GUILayout.Height(54)))
                    {
                        inputHandler?.TriggerResponse("X");
                    }
                }
            }

            GUI.backgroundColor = defaultBg;
            GUI.enabled = true;
        }

        EditorGUILayout.EndScrollView();
    }

    // =====================================================
    // アクションガイド（今何をすべきかの案内メッセージ）
    // =====================================================

    private static void DrawActionGuide(EXP_ExperimentManager manager)
    {
        string guideText;
        MessageType messageType = MessageType.Info;

        switch (manager.CurrentState)
        {
            case EXP_ExperimentState.Idle:
                guideText = "👉 【準備完了】下の「▶ 実験を開始する (Space)」ボタン、または Space キーを押して実験を開始してください。";
                messageType = MessageType.Info;
                break;

            case EXP_ExperimentState.Instruction:
                guideText = "👉 【教示中】被験者に説明が表示されています。準備ができたら被験者にキーを押してもらうか、下の「次へ進む」ボタンをクリックしてください。";
                messageType = MessageType.Warning;
                break;

            case EXP_ExperimentState.Practice:
                guideText = "👉 【練習試行中】練習セッションを実行中です。被験者の回答を入力してください。";
                messageType = MessageType.Info;
                break;

            case EXP_ExperimentState.Trial:
                guideText = manager.CurrentPhase switch
                {
                    EXP_TrialPhase.ITI => "⏳ 【試行間隔 (ITI)】次の試行を開始する準備をしています...",
                    EXP_TrialPhase.Stimulus => "🔊 【刺激提示中】触覚刺激を照射しています。刺激終了までお待ちください...",
                    EXP_TrialPhase.Response => "🎯 【応答受付中】被験者に『第1刺激(Z)』と『第2刺激(X)』のどちらが重かったかを選んでもらってください。",
                    EXP_TrialPhase.Feedback => "💬 【フィードバック提示中】結果を表示しています...",
                    _ => "進行中..."
                };
                messageType = (manager.CurrentPhase == EXP_TrialPhase.Response) ? MessageType.Warning : MessageType.Info;
                break;

            case EXP_ExperimentState.Break:
                guideText = "☕ 【ブロック休憩中】被験者が休憩中です。再開準備ができたらキーを押すか、下のボタンをクリックしてください。";
                messageType = MessageType.Warning;
                break;

            case EXP_ExperimentState.Finished:
                guideText = "🎉 【全試行完了】実験が正常に終了しました！データは保存済みです。";
                messageType = MessageType.Info;
                break;

            default:
                guideText = "待機中...";
                break;
        }

        EditorGUILayout.HelpBox(guideText, messageType);
    }

    // =====================================================
    // UI ヘルパー & バッジ描画
    // =====================================================

    private static void DrawSectionHeader(string title)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private static void DrawBadge(string text, Color color)
    {
        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        var prevColor = GUI.color;
        GUI.color = color;
        GUILayout.Box(text, style, GUILayout.Height(20));
        GUI.color = prevColor;
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
        DrawBadge(label, color);
    }

    private static void DrawPhaseBadge(EXP_TrialPhase phase)
    {
        (string label, Color color) = phase switch
        {
            EXP_TrialPhase.ITI => ("試行間隔 (ITI)", new Color(0.5f, 0.5f, 0.5f)),
            EXP_TrialPhase.Stimulus => ("刺激提示中 (STIMULUS)", new Color(0.9f, 0.3f, 0.3f)),
            EXP_TrialPhase.Response => ("応答受付中 (RESPONSE)", new Color(0.15f, 0.8f, 0.35f)),
            EXP_TrialPhase.Feedback => ("フィードバック (FEEDBACK)", new Color(0.25f, 0.7f, 0.9f)),
            _ => ("-", Color.gray)
        };
        DrawBadge(label, color);
    }

    private static string TranslateMetadataKey(string key)
    {
        return key switch
        {
            "referenceOffsetY" => "基準刺激 Y オフセット [m]",
            "comparisonOffsetY" => "比較刺激 Y オフセット [m]",
            "refFirst" => "第1刺激が基準刺激か",
            "interval1Y" => "第1刺激の Y オフセット [m]",
            "interval2Y" => "第2刺激の Y オフセット [m]",
            "referenceFrequency" => "基準 STM 周波数 [Hz]",
            "comparisonFrequency" => "比較 STM 周波数 [Hz]",
            "interval1Frequency" => "第1刺激の STM 周波数 [Hz]",
            "interval2Frequency" => "第2刺激の STM 周波数 [Hz]",
            _ => key
        };
    }
}
