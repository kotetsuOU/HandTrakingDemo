using UnityEngine;
using static EXP_PanelElementDrawers;

#nullable enable

/// <summary>
/// コントロールパネルの条件表示・操作ボタン・応答入力パネル描画コンポーネント（全4パラダイムUI対応）。
/// </summary>
public static class EXP_ControlInputPanelDrawer
{
    public static void DrawTrialConditionSection(EXP_ExperimentManager manager)
    {
        DrawSectionHeader("3. 実行中の試行条件パラメータ");
        using (new GUILayout.VerticalScope(GUI.skin.box))
        {
            if (manager.CurrentTrial == null)
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("現在の条件名: 未設定 (試行開始前)", GetBoldLabelStyle());
                    GUILayout.FlexibleSpace();
                    DrawBadge("待機中 (IDLE)", new Color(0.5f, 0.5f, 0.5f), 12, 24);
                }
                return;
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label($"現在の条件名: {manager.CurrentTrial.conditionName}", GetBoldLabelStyle());
                GUILayout.FlexibleSpace();
                DrawBadge(manager.CurrentTrial.isPractice ? "練習試行" : "本試行",
                          manager.CurrentTrial.isPractice ? new Color(0.9f, 0.55f, 0.1f) : new Color(0.2f, 0.6f, 0.9f), 12, 24);
            }

            if (manager.isDebugMode && manager.CurrentTrial.metadata.Count > 0)
            {
                GUILayout.Space(6);
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("🐞 詳細パラメータ (デバッグ表示):", GetBoldLabelStyle());
                    foreach (var kv in manager.CurrentTrial.metadata)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            GUILayout.Label($"  • {EXP_MetadataTranslator.TranslateKey(kv.Key)}", GUILayout.Width(230));
                            GUILayout.Label(kv.Value, GetBoldLabelStyle());
                        }
                    }
                }
            }
        }
    }

    public static void DrawControlButtonsSection(EXP_ExperimentManager manager)
    {
        DrawSectionHeader("4. 実験コントロール操作");
        var keys = (manager.inputHandler ?? manager.GetComponent<EXP_InputHandler>())?.keyBindings ?? new EXP_KeyBindings();

        using (new GUILayout.HorizontalScope())
        {
            GUI.enabled = (manager.CurrentState == EXP_ExperimentState.Idle);
            var prevBg = GUI.backgroundColor;

            if (GUI.enabled) GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);
            if (GUILayout.Button($"▶ 実験を開始する ({keys.startKey})", GetBigButtonStyle(), GUILayout.Height(CONTROL_BUTTON_HEIGHT)))
                manager.StartExperiment();

            GUI.backgroundColor = prevBg;
            GUI.enabled = (manager.CurrentState != EXP_ExperimentState.Idle && manager.CurrentState != EXP_ExperimentState.Finished);

            if (GUI.enabled) GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);
            if (GUILayout.Button($"■ 実験を中断する ({keys.abortKey})", GetBigButtonStyle(), GUILayout.Height(CONTROL_BUTTON_HEIGHT)))
                manager.AbortExperiment();

            GUI.backgroundColor = prevBg;

            if (manager.CurrentState == EXP_ExperimentState.Finished)
            {
                GUI.backgroundColor = new Color(0.35f, 0.75f, 1.0f);
                if (GUILayout.Button("🔄 実験をリセットして初期状態に戻る", GetBigButtonStyle(), GUILayout.Height(CONTROL_BUTTON_HEIGHT)))
                    manager.ResetToIdle();
                GUI.backgroundColor = prevBg;
            }

            GUI.enabled = true;
        }
    }

    public static void DrawResponsePanelSection(EXP_ExperimentManager manager)
    {
        DrawSectionHeader("5. コンテキスト操作 & 参加者応答パネル");
        var inputHandler = manager.inputHandler ?? manager.GetComponent<EXP_InputHandler>();
        var keys = inputHandler?.keyBindings ?? new EXP_KeyBindings();

        using (new GUILayout.VerticalScope(GUI.skin.box))
        {
            switch (manager.CurrentState)
            {
                case EXP_ExperimentState.Idle:
                    DrawIdlePanel(manager, keys);
                    break;

                case EXP_ExperimentState.Instruction:
                case EXP_ExperimentState.Break:
                    DrawInstructionPanel(manager, inputHandler, keys);
                    break;

                case EXP_ExperimentState.Practice:
                case EXP_ExperimentState.Trial:
                    DrawTrialResponsePanel(manager, inputHandler, keys);
                    break;

                case EXP_ExperimentState.Finished:
                    DrawFinishedPanel(manager, keys);
                    break;
            }
        }
    }

    // =====================================================
    // Private Panel Drawers
    // =====================================================

    private static void DrawPanelHeaderSection(string badgeText, Color badgeColor, System.Action bodyDrawer, float minBodyHeight = 72f)
    {
        DrawBadge(badgeText, badgeColor, 13, 28);
        GUILayout.Space(6);

        using (new GUILayout.VerticalScope(GUI.skin.box, GUILayout.MinHeight(minBodyHeight)))
        {
            bodyDrawer();
        }

        GUILayout.Space(8);
    }

    private static void DrawIdlePanel(EXP_ExperimentManager manager, EXP_KeyBindings keys)
    {
        DrawPanelHeaderSection("📝 被験者情報の入力（実験開始前）", new Color(0.2f, 0.6f, 0.9f), () =>
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("被験者 ID (匿名化識別子):", GetBoldLabelStyle(), GUILayout.Width(170));
                manager.participantId = GUILayout.TextField(manager.participantId);
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("被験者 氏名 (管理保存用):", GetBoldLabelStyle(), GUILayout.Width(170));
                manager.participantName = GUILayout.TextField(manager.participantName);
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("グループ / 条件名:", GetBoldLabelStyle(), GUILayout.Width(170));
                manager.groupLabel = GUILayout.TextField(manager.groupLabel);
            }
        });

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);

        if (GUILayout.Button($"▶ 実験を開始する ({keys.startKey} キー / クリック)", GetBigChoiceButtonStyle(), GUILayout.Height(BIG_BUTTON_HEIGHT)))
        {
            manager.StartExperiment();
        }
        GUI.backgroundColor = prevBg;
    }

    private static void DrawInstructionPanel(EXP_ExperimentManager manager, EXP_InputHandler? inputHandler, EXP_KeyBindings keys)
    {
        DrawPanelHeaderSection("● 教示表示中: 内容を確認後「次へ進む」を押してください", new Color(0.1f, 0.78f, 0.3f), () =>
        {
            GUILayout.Label("画面の中央に教示・案内メッセージが表示されています。", GetBoldLabelStyle());
            GUILayout.Label("被験者が内容を確認したことを確認し、下記のボタンを押して次に進んでください。", GetMiniLabelStyle());
        });

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.35f, 0.75f, 1.0f);

        if (GUILayout.Button($"▶ 内容を確認して次へ進む ({keys.nextKey} キー / クリック)", GetBigChoiceButtonStyle(), GUILayout.Height(BIG_BUTTON_HEIGHT)))
        {
            inputHandler?.TriggerResponse("Space");
        }
        GUI.backgroundColor = prevBg;
    }

    private static void DrawFinishedPanel(EXP_ExperimentManager manager, EXP_KeyBindings keys)
    {
        DrawPanelHeaderSection("🎉 実験セッション完了: 全試行が終了しました", new Color(0.6f, 0.3f, 0.85f), () =>
        {
            GUILayout.Label("全試行が終了し、データは正常に保存・確定されました。", GetBoldLabelStyle());
            GUILayout.Label("別の被験者で実施する場合、またはもう一度実験を行う場合は以下のボタンを押してください。", GetMiniLabelStyle());
        });

        var prevBg = GUI.backgroundColor;
        using (new GUILayout.HorizontalScope())
        {
            GUI.backgroundColor = new Color(0.35f, 0.75f, 1.0f);
            if (GUILayout.Button("🔄 初期画面 (Idle) に戻ってもう一度実験を行う", GetBigChoiceButtonStyle(), GUILayout.Height(BIG_BUTTON_HEIGHT)))
            {
                manager.ResetToIdle();
            }

            GUI.backgroundColor = new Color(0.35f, 0.85f, 0.35f);
            if (GUILayout.Button($"▶ 同じ設定・被験者で再スタートする ({keys.startKey})", GetBigChoiceButtonStyle(), GUILayout.Height(BIG_BUTTON_HEIGHT)))
            {
                manager.ResetToIdle();
                manager.StartExperiment();
            }
        }
        GUI.backgroundColor = prevBg;
    }

    private static void DrawTrialResponsePanel(EXP_ExperimentManager manager, EXP_InputHandler? inputHandler, EXP_KeyBindings keys)
    {
        bool isResponse = (manager.CurrentPhase == EXP_TrialPhase.Response) ||
            (manager.CurrentTrial?.metadata.TryGetValue("currentInterval", out var ci) == true && (ci.Contains("応答") || ci.Contains("調整")));

        if (!isResponse)
        {
            DrawPanelHeaderSection("🔒 刺激照射中 / 待機中 (入力受付オフ)", new Color(0.5f, 0.5f, 0.5f), () =>
            {
                GUILayout.Label("触覚刺激を照射中、または試行間待機中です。", GetBoldLabelStyle());
                GUILayout.Label("提示終了までお待ちください...", GetMiniLabelStyle());
            });
            return;
        }

        string interval = manager.CurrentTrial?.metadata.TryGetValue("currentInterval", out var val) == true ? val : "";
        bool isAdj = interval.Contains("調整") || (manager.CurrentTrial?.metadata.ContainsKey("currentValue") == true);

        DrawPanelHeaderSection("● 応答受付中: 判別・評定・調整を選択してください", new Color(0.1f, 0.78f, 0.3f), () =>
        {
            string condName = manager.CurrentTrial?.conditionName ?? "触覚比較条件";
            string statusText = string.IsNullOrEmpty(interval) ? condName : $"{condName} ({interval})";
            GUILayout.Label($"判別条件: {statusText}", GetBoldLabelStyle());
            GUILayout.Label("下記またはキーボード/ゲームパッドの対応ボタンを押して応答を選択してください。", GetMiniLabelStyle());
        });

        if (isAdj)
        {
            DrawAdjustmentButtons(inputHandler, keys);
        }
        else
        {
            DrawChoiceButtons(manager, inputHandler, keys);
        }
    }

    private static void DrawAdjustmentButtons(EXP_InputHandler? inputHandler, EXP_KeyBindings keys)
    {
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1.0f, 0.85f, 0.4f);

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button($"【 ▲ 値を上げる 】 ({keys.upKey} / W)", GetBigChoiceButtonStyle(), GUILayout.Height(BIG_BUTTON_HEIGHT))) inputHandler?.TriggerResponse("Up");
            if (GUILayout.Button($"【 ▼ 値を下げる 】 ({keys.downKey} / S)", GetBigChoiceButtonStyle(), GUILayout.Height(BIG_BUTTON_HEIGHT))) inputHandler?.TriggerResponse("Down");
        }

        GUI.backgroundColor = new Color(0.35f, 0.85f, 0.4f);
        if (GUILayout.Button($"【 🟢 調整値を確定する 】 ({keys.confirmKey} / Enter)", GetBigChoiceButtonStyle(), GUILayout.Height(BIG_BUTTON_HEIGHT)))
        {
            inputHandler?.TriggerResponse("Space");
        }
        GUI.backgroundColor = prevBg;
    }

    private static void DrawChoiceButtons(EXP_ExperimentManager manager, EXP_InputHandler? inputHandler, EXP_KeyBindings keys)
    {
        bool isABX = manager.CurrentTrial?.metadata.ContainsKey("valueA") == true;
        bool isSingle = manager.CurrentTrial?.metadata.ContainsKey("stimulusValue") == true;

        string label1 = isABX    ? $"【 1 】 刺激 A と同じ\n({keys.choice1Key})"
                      : isSingle ? $"【 1 】 はい (感知あり)\n({keys.yesKey})"
                                 : $"【 1 】 第 1 刺激が重い\n({keys.choice1Key})";

        string label2 = isABX    ? $"【 2 】 刺激 B と同じ\n({keys.choice2Key})"
                      : isSingle ? $"【 2 】 いいえ (感知なし)\n({keys.noKey})"
                                 : $"【 2 】 第 2 刺激が重い\n({keys.choice2Key})";

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.55f, 0.88f, 1.0f);

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button(label1, GetBigChoiceButtonStyle(), GUILayout.Height(BIG_BUTTON_HEIGHT))) inputHandler?.TriggerResponse("Z");
            if (GUILayout.Button(label2, GetBigChoiceButtonStyle(), GUILayout.Height(BIG_BUTTON_HEIGHT))) inputHandler?.TriggerResponse("X");
        }
        GUI.backgroundColor = prevBg;
    }
}
