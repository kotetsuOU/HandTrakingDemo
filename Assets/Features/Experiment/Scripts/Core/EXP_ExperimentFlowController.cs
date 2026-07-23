using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【実験フローコントローラー】
/// 実験全体のマクロ進行フロー（教示 ➔ 練習試行ループ ➔ 本試行ブロックループ ➔ ブロック休憩 ➔ 終了）のコルーチン制御を担当します。
/// </summary>
public static class EXP_ExperimentFlowController
{
    public static IEnumerator RunMainLoop(EXP_ExperimentManager manager)
    {
        var config = manager.config;
        var sequencer = manager.sequencer;
        var dataRecorder = manager.dataRecorder;
        var eventMarker = manager.eventMarker;
        var inputHandler = manager.inputHandler;

        manager.TransitionTo(EXP_ExperimentState.Instruction);

        var session = new EXP_ExperimentSession
        {
            participantId    = config.participantId,
            groupLabel       = config.groupLabel,
            sessionStartTime = (double)Time.realtimeSinceStartup,
        };
        manager.SetCurrentSession(session);

        if (sequencer != null)
        {
            sequencer.GenerateSequence();
            session.totalTrials = sequencer.TotalTrials;
        }

        dataRecorder?.InitializeSession(session);
        eventMarker?.Mark($"ExperimentStart_{config.participantId}");

        // --- 1. 教示フェーズ ---
        manager.SetFixation(false);
        manager.SetMessage(
            "【実験の説明】\n\n"
          + "これより触覚刺激の比較実験を開始します。\n"
          + "準備ができたらボタン（または Space キー）を押してください。");

        manager.ResetResponseReceived();
        inputHandler?.StartListening();
        yield return manager.WaitForResponse(0f);
        inputHandler?.StopListening();

        manager.ClearAll();

        // --- 2. 練習試行 ---
        if (config.practiceTrialCount > 0)
        {
            manager.TransitionTo(EXP_ExperimentState.Practice);
            eventMarker?.Mark("PracticeStart");

            yield return RunPracticeTrials(manager);

            manager.SetMessage("【練習完了】\n次から本試行を開始します。準備ができたらボタンを押してください。");
            manager.ResetResponseReceived();
            inputHandler?.StartListening();
            yield return manager.WaitForResponse(0f);
            inputHandler?.StopListening();

            manager.ClearAll();
        }

        // --- 3. 本試行 ---
        manager.TransitionTo(EXP_ExperimentState.Trial);
        eventMarker?.Mark("MainTrialsStart");

        int totalBlocks = config.blockCount;
        int trialsPerBlock = config.trialsPerBlock;

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

            if (block < totalBlocks - 1 && config.breakDuration > 0f)
            {
                yield return RunBreak(manager, block + 1, totalBlocks);
            }
        }

        // --- 4. 終了フェーズ ---
        manager.TransitionTo(EXP_ExperimentState.Finished);
        session.isFinished = true;
        session.sessionEndTime = (double)Time.realtimeSinceStartup;

        dataRecorder?.FinalizeSession(session);
        eventMarker?.Mark("ExperimentFinished");

        manager.SetMessage(
            "【全試行完了】\n\n"
          + "実験が終了しました。ご協力ありがとうございました。\n"
          + "データは正常に保存されました。");

        manager.InvokeExperimentFinished(session);
        Debug.Log($"[EXP_ExperimentManager] 全試行完了 (総試行数: {session.completedTrials})");
    }

    private static IEnumerator RunPracticeTrials(EXP_ExperimentManager manager)
    {
        var seqReadOnly = manager.sequencer!.GetSequence();
        int condCount = seqReadOnly.Count;
        for (int i = 0; i < manager.config.practiceTrialCount; i++)
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
        manager.SetMessage(
            $"休憩してください（ブロック {blockIndex} / {totalBlocks} 完了）\n"
          + "準備ができたらボタンを押して再開してください。");

        manager.ResetResponseReceived();
        manager.inputHandler?.StartListening();
        yield return manager.WaitForResponse(manager.config.breakDuration);
        manager.inputHandler?.StopListening();

        manager.ClearAll();
        manager.TransitionTo(EXP_ExperimentState.Trial);
    }
}
