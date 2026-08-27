using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class CrossmodalTask : MonoBehaviour
{
    public enum Side { Upper, Down }

    public struct Trial
    {
        public Side tactile;
        public Side visual;
    }

    [SerializeField] private string subjectID = "S01";
    [SerializeField] private int trialsPerCondition = 20;
    [SerializeField] private int seed = 1;
    [SerializeField] private float stimulusDuration = 0.5f;
    [SerializeField] private float itiDuration = 1.0f;
    [SerializeField] private MultiAUTD3Controller autd;
    [SerializeField] private float responseDeadline = 3.0f;

    private List<Trial> trials = new List<Trial>();
    private double onsetTime;
    private StreamWriter writer;

    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;

        OpenCsv();
        GenerateTrials();
        Debug.Log($"総数: {trials.Count}");

        StartCoroutine(RunSession());
    }

    void Update() { }

    void OnDestroy()
    {
        if (writer != null)
        {
            writer.Close();
            writer = null;
        }
    }

    private void OpenCsv()
    {
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{subjectID}_{stamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        writer = new StreamWriter(path, false);
        writer.WriteLine("subjectID,seed,trialIndex,tactileSide,visualSide,congruency,response,correct,RT_ms,onsetTime,responseTime");
        writer.Flush();

        Debug.Log($"CSV: {path}");
    }

    private void GenerateTrials()
    {
        trials.Clear();

        for (int i = 0; i < trialsPerCondition; i++)
        {
            trials.Add(new Trial { tactile = Side.Upper, visual = Side.Upper });
            trials.Add(new Trial { tactile = Side.Upper, visual = Side.Down  });
            trials.Add(new Trial { tactile = Side.Down,  visual = Side.Upper });
            trials.Add(new Trial { tactile = Side.Down,  visual = Side.Down  });
        }

        Shuffle();
        Debug.Log($"生成直後: {trials.Count} / 設定値: {trialsPerCondition}");
    }

    private void Shuffle()
    {
        System.Random rng = new System.Random(seed);

        for (int i = trials.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            Trial tmp = trials[i];
            trials[i] = trials[j];
            trials[j] = tmp;
        }
    }

    private Side? GetResponse()
    {
        if (Keyboard.current == null) return null;

        bool up   = Keyboard.current.upArrowKey.wasPressedThisFrame;
        bool down = Keyboard.current.downArrowKey.wasPressedThisFrame;

        if (up)   return Side.Upper;
        if (down) return Side.Down;

        return null;
    }

    private IEnumerator RunSession()
    {
        Debug.Log("スペースキーで開始");

        yield return new WaitUntil(() => Keyboard.current != null 
        && Keyboard.current.spaceKey.wasPressedThisFrame);

        for (int i = 0; i < trials.Count; i++)
        {
            yield return StartCoroutine(RunTrial(i));
        }
        Debug.Log("=== 全試行終了 ===");
    }

    private void SetTactile(Side side)
    {
        if(autd == null) return;

        if(side == Side.Upper)
            autd.SetMode(MultiAUTD3Controller.OutputSide.UpperOnly);
        else
            autd.SetMode(MultiAUTD3Controller.OutputSide.DownOnly);
    }

private IEnumerator RunTrial(int index)
{
    Trial trial = trials[index];

    // --- 試行間インターバル ---
    yield return new WaitForSecondsRealtime(itiDuration);

    // --- 刺激フェーズ ---
    SetTactile(trial.tactile);
    onsetTime = Time.realtimeSinceStartupAsDouble;

    yield return new WaitForSecondsRealtime(stimulusDuration);

    if (autd != null) autd.StopOutput();

    // --- 応答フェーズ ---
    Debug.Log($"[{index}] 回答してください");

    Side? response = null;
    double responseTime = 0;
    double respStart = Time.realtimeSinceStartupAsDouble;

    while (Time.realtimeSinceStartupAsDouble - respStart < responseDeadline)
    {
        Side? r = GetResponse();
        if (r != null)
        {
            response = r;
            responseTime = Time.realtimeSinceStartupAsDouble;
            break;
        }
        yield return null;
    }

    WriteTrial(index, trial, response, responseTime);
}

    private void WriteTrial(int index, Trial trial, Side? response, double responseTime)
    {
        string congruency  = (trial.tactile == trial.visual) ? "congruent" : "incongruent";
        string responseStr = (response == null) ? "timeout" : response.ToString();
        string correctStr  = (response == null) ? "NA" : (response == trial.tactile).ToString();
        string rtStr       = (response == null) ? "NA" : ((responseTime - onsetTime) * 1000.0).ToString("F1");
        string respTimeStr = (response == null) ? "NA" : responseTime.ToString("F4");

        writer.WriteLine($"{subjectID},{seed},{index},{trial.tactile},{trial.visual},{congruency},{responseStr},{correctStr},{rtStr},{onsetTime:F4},{respTimeStr}");
        writer.Flush();

        Debug.Log($"[{index}] 触覚:{trial.tactile} 視覚:{trial.visual} → {responseStr} {correctStr} RT:{rtStr}");
    }
}