using UnityEngine;
using static EXP_PanelElementDrawers;

#nullable enable

/// <summary>
/// コントロールパネルの条件表示・ボタン操作・応答入力パネル描画コンポーネント（200行以内）。
/// </summary>
public static class EXP_ControlInputPanelDrawer
{
    public static void DrawTrialConditionSection(EXP_ExperimentManager manager)
    {
        DrawSectionHeader("3. 実行中の試行条件パラメータ");
        using (new GUILayout.VerticalScope(GUI.skin.box))
        {
            if (manager.CurrentTrial != null)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label($"現在の条件名: {manager.CurrentTrial.conditionName}", GetBoldLabelStyle());
                    GUILayout.FlexibleSpace();
                    if (manager.CurrentTrial.isPractice)
                    {
                        DrawBadge("練習試行", new Color(0.9f, 0.55f, 0.1f), 12, 24);
                    }
                    else
                    {
                        DrawBadge("本試行", new Color(0.2f, 0.6f, 0.9f), 12, 24);
                    }
                }

                if (manager.CurrentTrial.metadata.Count > 0)
                {
                    GUILayout.Space(6);
                    bool isDebug = manager.config != null && manager.config.isDebugMode;

                    if (isDebug)
                    {
                        GUILayout.Label("詳細パラメータ (デバッグ表示中):", GetBoldLabelStyle());
                        foreach (var kv in manager.CurrentTrial.metadata)
                        {
                            string japaneseKey = EXP_MetadataTranslator.TranslateKey(kv.Key);
                            using (new GUILayout.HorizontalScope())
                            {
                                GUILayout.Label($"  • {japaneseKey}", GUILayout.Width(230));
                                GUILayout.Label(kv.Value, GetBoldLabelStyle());
                            }
                        }
                    }
                    else
                    {
                        DrawMessageBox("🔒 本番ブラインドモード有効中: 被験者への数値漏洩を防止するため物理数値は非表示です。");
                    }
                }
            }
            else
            {
                GUILayout.Label("現在実行中の試行はありません（待機中）", GetMiniLabelStyle());
            }
        }
    }

    public static void DrawControlButtonsSection(EXP_ExperimentManager manager)
    {
        DrawSectionHeader("4. 実験コントロール操作");
        using (new GUILayout.HorizontalScope())
        {
            GUI.enabled = (manager.CurrentState == EXP_ExperimentState.Idle);
            var prevBg = GUI.backgroundColor;
            if (GUI.enabled) GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);

            if (GUILayout.Button("▶ 実験を開始する (Space)", GetBigButtonStyle(), GUILayout.Height(44)))
            {
                manager.StartExperiment();
            }

            GUI.backgroundColor = prevBg;
            GUI.enabled = (manager.CurrentState != EXP_ExperimentState.Idle && manager.CurrentState != EXP_ExperimentState.Finished);
            if (GUI.enabled) GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);

            if (GUILayout.Button("■ 実験を中断する (Esc)", GetBigButtonStyle(), GUILayout.Height(44)))
            {
                manager.AbortExperiment();
            }

            GUI.backgroundColor = prevBg;
            GUI.enabled = true;
        }
    }

    public static void DrawResponsePanelSection(EXP_ExperimentManager manager)
    {
        DrawSectionHeader("5. 参加者応答入力パネル");
        var inputHandler = manager.inputHandler ?? manager.GetComponent<EXP_InputHandler>();
        bool isListening = inputHandler != null && inputHandler.IsListening &&
                           (!inputHandler.blockAfterFirstResponse || !inputHandler.HasResponded);

        using (new GUILayout.VerticalScope(GUI.skin.box))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("入力受付状態:", GetBoldLabelStyle(), GUILayout.Width(100));
                if (isListening)
                {
                    DrawBadge("● 応答受付中 (ボタン/キー有効)", new Color(0.1f, 0.78f, 0.3f), 13, 28);
                }
                else
                {
                    DrawBadge("🔒 入力ロック中 (待機中 / 刺激照射中)", new Color(0.5f, 0.5f, 0.5f), 13, 28);
                }
            }

            GUILayout.Space(8);

            GUI.enabled = isListening;
            var defaultBg = GUI.backgroundColor;

            if (isListening) GUI.backgroundColor = new Color(0.55f, 0.88f, 1.0f);

            if (manager.CurrentState == EXP_ExperimentState.Instruction || manager.CurrentState == EXP_ExperimentState.Break)
            {
                if (GUILayout.Button("👉 次へ進む / 準備完了 (クリック)", GetBigChoiceButtonStyle(), GUILayout.Height(55)))
                {
                    inputHandler?.TriggerResponse("Space");
                }
            }
            else
            {
                GUILayout.Label("重さ比較の判断選択 (直接クリックで回答):", GetBoldLabelStyle());
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("【 1 】 第 1 刺激が重い\n(Z キー / クリック)", GetBigChoiceButtonStyle(), GUILayout.Height(62)))
                    {
                        inputHandler?.TriggerResponse("Z");
                    }
                    if (GUILayout.Button("【 2 】 第 2 刺激が重い\n(X キー / クリック)", GetBigChoiceButtonStyle(), GUILayout.Height(62)))
                    {
                        inputHandler?.TriggerResponse("X");
                    }
                }
            }

            GUI.backgroundColor = defaultBg;
            GUI.enabled = true;
        }
    }
}
