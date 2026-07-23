using UnityEngine;
using System.Collections;

#nullable enable

/// <summary>
/// 【1試行実行エンジン】
/// 単一試行 (Trial) の実行サイクル（ITI ➔ 刺激提示 ➔ 応答受付 ➔ 正誤評価 ➔ データ記録）のコルーチン処理のみを担当します。
/// </summary>
public static class EXP_TrialRunner
{
    public static IEnumerator RunTrial(
        EXP_ExperimentManager manager,
        int trialIndex,
        int blockIndex,
        bool isPractice,
        EXP_BaseCondition condition)
    {
        manager.ResetResponseReceived();

        var trial = new EXP_TrialData
        {
            trialIndex      = trialIndex,
            blockIndex      = blockIndex,
            isPractice      = isPractice,
            conditionName   = condition.conditionName,
            trialStartTime  = (double)Time.realtimeSinceStartup,
        };

        manager.SetCurrentTrial(trial);
        manager.eventMarker?.Mark($"TrialStart_{trialIndex}_{condition.conditionName}");
        manager.InvokeTrialStarted(trial);

        // --- 1. ITI (試行間隔) ---
        manager.SetPhase(EXP_TrialPhase.ITI);
        manager.SetFixation(true);

        if (manager.itiDuration > 0f)
            yield return new WaitForSeconds(manager.itiDuration);

        // --- 2. Stimulus (刺激提示) ---
        manager.SetPhase(EXP_TrialPhase.Stimulus);
        trial.stimulusOnsetTime = (double)Time.realtimeSinceStartup;
        manager.eventMarker?.Mark("StimulusOn");

        var stimCoro = condition.StimulusCoroutine(trial, manager);
        if (stimCoro != null)
        {
            yield return stimCoro;
        }
        else
        {
            condition.Apply(trial);
        }

        // --- 3. Response (応答受付) ---
        manager.SetPhase(EXP_TrialPhase.Response);
        manager.ResetResponseReceived();
        manager.inputHandler?.StartListening();

        if (manager.stimulusDuration > 0f)
            yield return new WaitForSeconds(manager.stimulusDuration);

        yield return manager.WaitForResponse(manager.responseTimeout);
        manager.inputHandler?.StopListening();
        manager.eventMarker?.Mark("StimulusOff");

        // 応答結果・タイムアウト評価
        if (!manager.HasReceivedResponse())
        {
            trial.responseType = EXP_ResponseType.Timeout;
            trial.responseTime = (double)Time.realtimeSinceStartup;
            trial.isCorrect    = false;
            manager.eventMarker?.Mark("Timeout");
        }
        else
        {
            trial.isCorrect = condition.EvaluateResponse(trial);
            if (trial.isCorrect == true) trial.responseType = EXP_ResponseType.Correct;
            else if (trial.isCorrect == false) trial.responseType = EXP_ResponseType.Incorrect;
        }

        manager.SetFixation(false);
        condition.OnTrialEnd(trial);

        // 記録 & 通知
        manager.dataRecorder?.RecordTrial(trial);
        manager.CurrentSession?.trialDataList.Add(trial);

        manager.InvokeTrialCompleted(trial);
        manager.eventMarker?.Mark($"TrialEnd_{trialIndex}");
        manager.SetCurrentTrial(null);
    }
}
