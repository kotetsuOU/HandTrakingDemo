using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class DetectionTest : MonoBehaviour
{
    public struct DetectTrial
    {
        public bool hasStimulus;
        public float freq;
    }

    [SerializeField] private MultiAUTD3Controller autd;

    [Header("Subject")]
    [SerializeField] private string subjectID = "S01";
    [SerializeField] private int seed = 1;

    [Header("Test Settings")]
    [Tooltip("どちらのアレイを測るか")]
    public MultiAUTD3Controller.OutputSide testSide = MultiAUTD3Controller.OutputSide.DownOnly;

    [Tooltip("テストする変調周波数のリスト")]
    public float[] testFreqs = new float[] { 150f, 200f, 300f };

    [Tooltip("1周波数あたりの試行数（刺激あり・なし それぞれこの回数）")]
    [SerializeField] private int trialsPerCondition = 10;

    [Header("Timing")]
    [SerializeField] private float stimulusDuration = 1.0f;
    [SerializeField] private float itiDuration = 1.0f;
    [SerializeField] private float responseDeadline = 3.0f;

    private List<DetectTrial> trials = new List<DetectTrial>();
    private StreamWriter writer;

    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 120;

        OpenCsv();
        GenerateSequence();
        StartCoroutine(RunTest());
    }

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
        string fileName = $"detect_{subjectID}_{testSide}_{stamp}.csv";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        writer = new StreamWriter(path, false);
        writer.WriteLine("subjectID,testSide,seed,trialIndex,modFreq,hasStimulus,response,correct");
        writer.Flush();

        Debug.Log($"CSV: {path}");
    }

    private void GenerateSequence()
    {
        trials.Clear();

        foreach (float f in testFreqs)
        {
            for (int i = 0; i < trialsPerCondition; i++)
            {
                trials.Add(new DetectTrial { hasStimulus = true,  freq = f });
                trials.Add(new DetectTrial { hasStimulus = false, freq = f });
            }
        }

        System.Random rng = new System.Random(seed);
        for (int i = trials.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            DetectTrial tmp = trials[i];
            trials[i] = trials[j];
            trials[j] = tmp;
        }

        Debug.Log($"総試行数: {trials.Count}");
    }

    private IEnumerator RunTest()
    {
        Debug.Log("スペースキーで開始");

        yield return new WaitUntil(() => Keyboard.current != null
                                      && Keyboard.current.spaceKey.wasPressedThisFrame);

        for (int i = 0; i < trials.Count; i++)
        {
            yield return StartCoroutine(RunTrial(i));
        }

        Debug.Log("=== 終了。CSVを確認してください ===");
    }

    private IEnumerator RunTrial(int index)
    {
        DetectTrial trial = trials[index];

        yield return new WaitForSecondsRealtime(itiDuration);

        // --- 刺激フェーズ ---
        if (autd != null)
        {
            autd.modFreq = trial.freq;
            autd.ApplyModulation();

            if (trial.hasStimulus) autd.SetMode(testSide);
            else                   autd.StopOutput();
        }

        yield return new WaitForSecondsRealtime(stimulusDuration);

        if (autd != null) autd.StopOutput();

        // --- 応答フェーズ ---
        Debug.Log($"[{index + 1}/{trials.Count}] ← 感じた / → 感じない");

        bool? response = null;
        double t0 = Time.realtimeSinceStartupAsDouble;

        while (Time.realtimeSinceStartupAsDouble - t0 < responseDeadline)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame)  { response = true;  break; }
                if (Keyboard.current.rightArrowKey.wasPressedThisFrame) { response = false; break; }
            }
            yield return null;
        }

        WriteTrial(index, trial, response);
    }

    private void WriteTrial(int index, DetectTrial trial, bool? response)
    {
        string resp    = (response == null) ? "NA" : (response.Value ? "felt" : "notfelt");
        string correct = (response == null) ? "NA" : (response.Value == trial.hasStimulus).ToString();

        writer.WriteLine($"{subjectID},{testSide},{seed},{index + 1},{trial.freq},{trial.hasStimulus},{resp},{correct}");
        writer.Flush();
    }
}