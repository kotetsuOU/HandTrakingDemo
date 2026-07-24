using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【実験フローコントローラー】
/// 実験全体のマクロ進行フロー（同意確認 ➔ 教示 ➔ 練習試行 ➔ 本試行 ➔ 休憩 ➔ 終了）を担当します。
/// </summary>
public static class EXP_ExperimentFlowController
{
    public static IEnumerator RunMainLoop(EXP_ExperimentManager manager)
    {
        var sequencer = manager.sequencer;
        var dataRecorder = manager.dataRecorder;
        var eventMarker = manager.eventMarker;
        var inputHandler = manager.inputHandler;
        var txt = manager.instructionText;

        manager.TransitionTo(EXP_ExperimentState.Instruction);

        var session = new EXP_ExperimentSession
        {
            participantId    = manager.participantId,
            participantName  = manager.participantName,
            groupLabel       = manager.groupLabel,
            sessionStartTime = (double)Time.realtimeSinceStartup,
        };
        manager.SetCurrentSession(session);

        int plannedTotal = manager.blockCount * manager.trialsPerBlock;
        if (sequencer != null)
        {
            sequencer.GenerateSequence(manager.blockCount, manager.trialsPerBlock);
        }
        session.totalTrials = sequencer != null && sequencer.TotalTrials > 0 ? sequencer.TotalTrials : plannedTotal;

        dataRecorder?.InitializeSession(session);
        eventMarker?.Mark($"ExperimentStart_{manager.participantId}");

        // --- 1. 同意確認フェーズ (Informed Consent) ---
        manager.SetFixation(false);
        string consentTitle = txt != null ? txt.consentTitle : "【実験協力・同意のお願い】";
        string consentBody  = txt != null ? txt.consentBody  : "本実験は触覚知覚の測定を目的としています。データは完全匿名化されます。\n同意される場合はボタンを押してください。";

        manager.SetMessage($"{consentTitle}\n\n{consentBody}\n\n【▶ 次へ進む】(Space キー / クリック)");

        manager.ResetResponseReceived();
        inputHandler?.StartListening();
        yield return manager.WaitForResponse(0f);
        inputHandler?.StopListening();
        eventMarker?.Mark("ConsentGiven");

        manager.ClearAll();

        // --- 2. 実験説明・教示フェーズ (Instruction) ---
        string mainTitle = txt != null ? txt.mainInstructionTitle : "【実験の説明】";
        string mainBody  = txt != null ? txt.mainInstructionBody  : "これより触覚刺激の比較実験を開始します。\n準備ができたら「次へ進む」を押してください。";

        manager.SetMessage($"{mainTitle}\n\n{mainBody}");

        manager.ResetResponseReceived();
        inputHandler?.StartListening();
        yield return manager.WaitForResponse(0f);
        inputHandler?.StopListening();

        manager.ClearAll();

        // --- 3. 練習試行 ---
        if (manager.practiceTrialCount > 0)
        {
            manager.TransitionTo(EXP_ExperimentState.Practice);
            eventMarker?.Mark("PracticeStart");

            yield return RunPracticeTrials(manager);

            manager.TransitionTo(EXP_ExperimentState.Instruction);
            manager.SetCurrentTrial(null);
            manager.SetPhase(EXP_TrialPhase.ITI);

            manager.SetMessage("【練習完了】\n次から本試行を開始します。準備ができたらボタンを押してください。");
            manager.ResetResponseReceived();
            inputHandler?.StartListening();
            yield return manager.WaitForResponse(0f);
            inputHandler?.StopListening();

            manager.ClearAll();
        }

        // --- 4. 本試行 ---
        manager.TransitionTo(EXP_ExperimentState.Trial);
        eventMarker?.Mark("MainTrialsStart");

        int totalBlocks = manager.blockCount;
        int trialsPerBlock = manager.trialsPerBlock;

        for (int block = 0; block < totalBlocks; block++)
        {
            for (int t = 0; t < trialsPerBlock; t++)
            {
                var condition = sequencer?.NextCondition();
                if (condition == null) break;

                int globalTrialIndex = block * trialsPerBlock + t;
                yield return EXP_TrialRunner.RunTrial(manager, globalTrialIndex, blockIndex: block, isPractice: false, condition);

                if (manager.CurrentState == EXP_ExperimentState.Idle) yield break;
            }

            if (block < totalBlocks - 1 && manager.breakDuration > 0f)
            {
                yield return RunBreak(manager, block + 1, totalBlocks);
            }
        }

        // --- 5. 終了フェーズ ---
        manager.TransitionTo(EXP_ExperimentState.Finished);
        session.isFinished = true;
        session.sessionEndTime = (double)Time.realtimeSinceStartup;

        dataRecorder?.FinalizeSession(session);
        eventMarker?.Mark("ExperimentFinished");

        string compText = txt != null ? txt.completionText
                                      : "【全試行完了】\n\n実験が終了しました。ご協力ありがとうございました。\nデータは正常に保存されました。";

        manager.SetMessage(compText);
        manager.SuppressCustomHaptics(false);  // custom 背景信号を復元
        manager.InvokeExperimentFinished(session);
        Debug.Log($"[EXP_ExperimentManager] 全試行完了 (総試行数: {session.completedTrials})");
    }

    private static IEnumerator RunPracticeTrials(EXP_ExperimentManager manager)
    {
        var seqReadOnly = manager.sequencer!.GetSequence();
        int condCount = seqReadOnly.Count;
        for (int i = 0; i < manager.practiceTrialCount; i++)
        {
            if (condCount == 0) break;
            var cond = seqReadOnly[i % condCount];
            yield return EXP_TrialRunner.RunTrial(manager, i, blockIndex: -1, isPractice: true, cond);
        }
        manager.eventMarker?.Mark("PracticeEnd");
    }

    private static IEnumerator RunBreak(EXP_ExperimentManager manager, int blockIndex, int totalBlocks)
    {
        manager.TransitionTo(EXP_ExperimentState.Break);
        manager.eventMarker?.Mark($"BlockBreak_{blockIndex}");

        manager.SetFixation(false);
        manager.SetMessage($"休憩してください（ブロック {blockIndex} / {totalBlocks} 完了）\n準備ができたらボタンを押して再開してください。");

        manager.ResetResponseReceived();
        manager.inputHandler?.StartListening();
        yield return manager.WaitForResponse(manager.breakDuration);
        manager.inputHandler?.StopListening();

        manager.ClearAll();
        manager.TransitionTo(EXP_ExperimentState.Trial);
    }
}
