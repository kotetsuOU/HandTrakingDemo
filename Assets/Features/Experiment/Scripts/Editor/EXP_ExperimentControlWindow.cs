using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

#nullable enable

/// <summary>
/// 【被験者実験 コントロールダッシュボード】（大文字＆2AFCリアルタイム逐次インジケーター付き）
/// Tools -> EXP -> 実験コントロールパネル から開けます。
/// <para>
/// 逐次比較法（2AFC）で「第1刺激提示中」「無刺激間隔(ISI)」「第2刺激提示中」「応答受付中」を
/// ド派手な巨大リアルタイムインジケーターと大きなフォントで一目で把握できます。
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
        window.minSize = new Vector2(440, 620);
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
        EditorGUILayout.Space(10);

        // 大フォント用スタイル定義
        var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
        var sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };

        // ヘッダー
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("被験者実験 コントロールダッシュボード", titleStyle);
            GUILayout.FlexibleSpace();
            if (EditorApplication.isPlaying)
            {
                DrawBadge("実行中 (PLAY)", new Color(0.1f, 0.75f, 0.2f), 13, 24);
            }
            else
            {
                DrawBadge("停止中 (STOP)", new Color(0.5f, 0.5f, 0.5f), 13, 24);
            }
        }

        EditorGUILayout.Space(8);

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
        // 0. ナビゲーション・操作ガイド & リアルタイム刺激インジケーター（超巨大表示）
        // =====================================================
        DrawActionGuideAndIntervalIndicator(manager);

        EditorGUILayout.Space(12);

        // =====================================================
        // 1. 進捗バー & セッション概要
        // =====================================================
        DrawSectionHeader("1. 実験の進捗・セッション概要", sectionStyle);

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            int completed = manager.CurrentSession?.completedTrials ?? 0;
            int total = manager.CurrentSession?.totalTrials ?? (manager.sequencer != null ? manager.sequencer.TotalTrials : 0);
            float progress = total > 0 ? (float)completed / total : 0f;

            string progressText = $"試行進捗: {completed} / {total} 試行完了 ({progress:P0})";
            Rect progressRect = GUILayoutUtility.GetRect(18, 30, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(progressRect, progress, progressText);

            EditorGUILayout.Space(8);

            var largeLabelStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("被験者 ID:", manager.CurrentSession?.participantId ?? "-", largeLabelStyle);
                EditorGUILayout.LabelField("正答率:", manager.CurrentSession != null ? $"{manager.CurrentSession.accuracy:P1}" : "-", largeLabelStyle);
            }
        }

        EditorGUILayout.Space(12);

        // =====================================================
        // 2. 現在のステータス & フェーズ
        // =====================================================
        DrawSectionHeader("2. 現在のステータスとフェーズ", sectionStyle);

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("全体状態:", GUILayout.Width(75));
                DrawStateBadge(manager.CurrentState);

                GUILayout.Space(10);

                EditorGUILayout.LabelField("試行フェーズ:", GUILayout.Width(85));
                DrawPhaseBadge(manager.CurrentPhase);
            }

            // 被験者に提示中のメッセージ（特大表示）
            var uiCtrl = manager.uiController ?? manager.GetComponent<EXP_UIController>();
            if (uiCtrl != null && uiCtrl.messageText != null && !string.IsNullOrEmpty(uiCtrl.messageText.text))
            {
                EditorGUILayout.Space(10);
                GUILayout.Label("💬 被験者画面に表示中のメッセージ:", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    var msgStyle = new GUIStyle(EditorStyles.wordWrappedLabel)
                    {
                        fontSize = 15,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    EditorGUILayout.SelectableLabel(uiCtrl.messageText.text, msgStyle, GUILayout.Height(50));
                }
            }
        }

        EditorGUILayout.Space(12);

        // =====================================================
        // 3. 現在の試行条件パラメータ
        // =====================================================
        DrawSectionHeader("3. 実行中の試行条件パラメータ", sectionStyle);

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            if (manager.CurrentTrial != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var paramHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                    EditorGUILayout.LabelField("現在の条件名:", manager.CurrentTrial.conditionName, paramHeaderStyle);
                    if (manager.CurrentTrial.isPractice)
                    {
                        DrawBadge("練習試行", new Color(0.9f, 0.55f, 0.1f), 12, 22);
                    }
                    else
                    {
                        DrawBadge("本試行", new Color(0.2f, 0.6f, 0.9f), 12, 22);
                    }
                }

                if (manager.CurrentTrial.metadata.Count > 0)
                {
                    EditorGUILayout.Space(6);
                    GUILayout.Label("詳細パラメータ:", EditorStyles.boldLabel);
                    var itemLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 12 };
                    var valLabelStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };

                    foreach (var kv in manager.CurrentTrial.metadata)
                    {
                        string japaneseKey = TranslateMetadataKey(kv.Key);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField($"  • {japaneseKey}", itemLabelStyle, GUILayout.Width(230));
                            EditorGUILayout.LabelField(kv.Value, valLabelStyle);
                        }
                    }
                }
            }
            else
            {
                var emptyStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 12 };
                EditorGUILayout.LabelField("現在実行中の試行はありません（待機中）", emptyStyle);
            }
        }

        EditorGUILayout.Space(12);

        // =====================================================
        // 4. メイン操作ボタン（開始・中断・進行）
        // =====================================================
        DrawSectionHeader("4. 実験コントロール操作", sectionStyle);

        var btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold
        };

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = (manager.CurrentState == EXP_ExperimentState.Idle);
            var prevBg = GUI.backgroundColor;
            if (GUI.enabled) GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);

            if (GUILayout.Button("▶ 実験を開始する (Space)", btnStyle, GUILayout.Height(44)))
            {
                manager.StartExperiment();
            }

            GUI.backgroundColor = prevBg;
            GUI.enabled = (manager.CurrentState != EXP_ExperimentState.Idle && manager.CurrentState != EXP_ExperimentState.Finished);
            if (GUI.enabled) GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);

            if (GUILayout.Button("■ 実験を中断する (Esc)", btnStyle, GUILayout.Height(44)))
            {
                manager.AbortExperiment();
            }

            GUI.backgroundColor = prevBg;
            GUI.enabled = true;
        }

        EditorGUILayout.Space(14);

        // =====================================================
        // 5. 参加者応答パネル（超巨大入力ボタン）
        // =====================================================
        DrawSectionHeader("5. 参加者応答入力パネル", sectionStyle);

        var inputHandler = manager.inputHandler ?? manager.GetComponent<EXP_InputHandler>();
        bool isListening = inputHandler != null && inputHandler.IsListening &&
                           (!inputHandler.blockAfterFirstResponse || !inputHandler.HasResponded);

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var inputHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
                EditorGUILayout.LabelField("入力受付状態:", inputHeaderStyle, GUILayout.Width(100));
                if (isListening)
                {
                    DrawBadge("● 応答受付中 (ボタン/キー有効)", new Color(0.1f, 0.8f, 0.3f), 13, 24);
                }
                else
                {
                    DrawBadge("入力ロック (待機中)", new Color(0.5f, 0.5f, 0.5f), 13, 24);
                }
            }

            EditorGUILayout.Space(10);

            GUI.enabled = isListening;
            var defaultBg = GUI.backgroundColor;

            if (isListening) GUI.backgroundColor = new Color(0.55f, 0.88f, 1.0f);

            var bigChoiceBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };

            // 教示中や休憩中用の決定・次へボタン
            if (manager.CurrentState == EXP_ExperimentState.Instruction || manager.CurrentState == EXP_ExperimentState.Break)
            {
                if (GUILayout.Button("👉 次へ進む / 準備完了 (クリック)", bigChoiceBtnStyle, GUILayout.Height(55)))
                {
                    inputHandler?.TriggerResponse("Space");
                }
            }
            else
            {
                // 2AFC 専用巨大応答ボタン
                GUILayout.Label("重さ比較の判断選択 (直接クリックで回答):", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("【 1 】 第 1 刺激が重い\n(Z キー / クリック)", bigChoiceBtnStyle, GUILayout.Height(62)))
                    {
                        inputHandler?.TriggerResponse("Z");
                    }
                    if (GUILayout.Button("【 2 】 第 2 刺激が重い\n(X キー / クリック)", bigChoiceBtnStyle, GUILayout.Height(62)))
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
    // アクションガイド & 2AFC リアルタイム刺激提示インジケーター
    // =====================================================

    private static void DrawActionGuideAndIntervalIndicator(EXP_ExperimentManager manager)
    {
        string guideText;
        MessageType messageType = MessageType.Info;

        switch (manager.CurrentState)
        {
            case EXP_ExperimentState.Idle:
                guideText = "👉 【準備完了】上の「▶ 実験を開始する (Space)」ボタンを押してください。";
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

        // 2AFC リアルタイム逐次刺激インジケーター（刺激提示中 / 応答待機中 に特大バッジを表示）
        if (manager.CurrentState == EXP_ExperimentState.Trial && manager.CurrentTrial != null)
        {
            string currentInterval = manager.CurrentTrial.metadata.GetValueOrDefault("currentInterval", "");
            if (!string.IsNullOrEmpty(currentInterval))
            {
                EditorGUILayout.Space(4);
                Color badgeBg = currentInterval.Contains("第 1") ? new Color(0.9f, 0.25f, 0.25f)
                              : currentInterval.Contains("第 2") ? new Color(0.25f, 0.55f, 0.95f)
                              : currentInterval.Contains("応答") ? new Color(0.15f, 0.8f, 0.35f)
                              : new Color(0.6f, 0.6f, 0.6f);

                DrawBadge($"⚡ 現在の刺激フェーズ: {currentInterval}", badgeBg, 15, 34);
            }
        }
    }

    // =====================================================
    // UI ヘルパー & バッジ描画
    // =====================================================

    private static void DrawSectionHeader(string title, GUIStyle style)
    {
        EditorGUILayout.LabelField(title, style);
    }

    private static void DrawBadge(string text, Color color, int fontSize = 12, float height = 22)
    {
        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        var prevColor = GUI.color;
        GUI.color = color;
        GUILayout.Box(text, style, GUILayout.Height(height));
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
        DrawBadge(label, color, 12, 22);
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
        DrawBadge(label, color, 12, 22);
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
            "currentInterval" => "現在の刺激提示フェーズ",
            _ => key
        };
    }
}
