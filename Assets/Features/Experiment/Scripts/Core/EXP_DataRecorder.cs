using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#nullable enable

/// <summary>
/// 試行データを CSV / JSON 形式でファイルに保存するレコーダー。
/// <para>
/// - CSV はクラッシュ対策のために1試行ごとに逐次追記します（<see cref="appendCSVOnEachTrial"/> = true の場合）。
/// - JSON はセッション終了時に全データを一括保存します。
/// </para>
/// <para>
/// 使用フロー:
/// <code>
/// dataRecorder.Initialize(session);
/// dataRecorder.RecordTrial(trialData);   // 各試行後
/// dataRecorder.SaveAll(session);         // セッション終了時
/// </code>
/// </para>
/// </summary>
public class EXP_DataRecorder : MonoBehaviour
{
    // =====================================================
    // Inspector Settings
    // =====================================================

    [Header("Save Settings")]
    [Tooltip("保存フォーマット")]
    public EXP_DataFormat dataFormat = EXP_DataFormat.Both;

    [Tooltip("保存先ディレクトリの絶対パス（空の場合は Application.persistentDataPath/ExperimentData）")]
    public string outputDirectory = "";

    [Tooltip("ファイル名プレフィックス（例: \"Trial\" → Trial_P001_20250723_143022.csv）")]
    public string filePrefix = "Trial";

    [Tooltip("試行ごとに CSV へ逐次追記する（true 推奨: クラッシュ時のデータロスト対策）")]
    public bool appendCSVOnEachTrial = true;

    // =====================================================
    // State (Read-Only)
    // =====================================================

    /// <summary>現在の保存先ディレクトリパス</summary>
    public string ResolvedDirectory { get; private set; } = "";

    /// <summary>CSV ファイルパス</summary>
    public string CSVFilePath { get; private set; } = "";

    /// <summary>JSON ファイルパス</summary>
    public string JSONFilePath { get; private set; } = "";

    /// <summary>記録済み試行のリスト（読み取り専用）</summary>
    public IReadOnlyList<EXP_TrialData> RecordedTrials => _recordedTrials.AsReadOnly();

    // =====================================================
    // Private Fields
    // =====================================================

    private readonly List<EXP_TrialData> _recordedTrials = new();
    private bool _csvHeaderWritten = false;

    // =====================================================
    // Public API
    // =====================================================

    /// <summary>
    /// セッション開始時に呼び出します。出力先ディレクトリとファイルパスを初期化します。
    /// </summary>
    public void Initialize(EXP_ExperimentSession session)
    {
        _recordedTrials.Clear();
        _csvHeaderWritten = false;

        ResolvedDirectory = string.IsNullOrEmpty(outputDirectory)
            ? Path.Combine(Application.persistentDataPath, "ExperimentData")
            : outputDirectory;

        Directory.CreateDirectory(ResolvedDirectory);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseName  = $"{filePrefix}_{session.participantId}_{timestamp}";

        CSVFilePath  = Path.Combine(ResolvedDirectory, baseName + ".csv");
        JSONFilePath = Path.Combine(ResolvedDirectory, baseName + ".json");

        Debug.Log($"[EXP_DataRecorder] 初期化完了。保存先: {ResolvedDirectory}");
    }

    /// <summary>
    /// 1試行分のデータを記録します。
    /// <see cref="appendCSVOnEachTrial"/> が true の場合は CSV に即時追記します。
    /// </summary>
    public void RecordTrial(EXP_TrialData trial)
    {
        _recordedTrials.Add(trial);

        if ((dataFormat == EXP_DataFormat.CSV || dataFormat == EXP_DataFormat.Both)
            && appendCSVOnEachTrial)
        {
            AppendCSVRow(trial);
        }
    }

    /// <summary>
    /// セッション終了時に全データを保存します。
    /// JSON は常に全データを書き出します。CSV の一括書き出しは <see cref="appendCSVOnEachTrial"/> が false の場合のみ行われます。
    /// </summary>
    public void SaveAll(EXP_ExperimentSession session)
    {
        if (dataFormat == EXP_DataFormat.CSV || dataFormat == EXP_DataFormat.Both)
        {
            if (!appendCSVOnEachTrial) WriteAllCSV();
        }

        if (dataFormat == EXP_DataFormat.JSON || dataFormat == EXP_DataFormat.Both)
        {
            WriteJSON(session);
        }

        Debug.Log($"[EXP_DataRecorder] 保存完了: {_recordedTrials.Count} 試行");
    }

    /// <summary>
    /// セッション開始時に呼び出します（Initialize のエイリアス）。
    /// </summary>
    public void InitializeSession(EXP_ExperimentSession session) => Initialize(session);

    /// <summary>
    /// セッション終了時に全データを保存します（SaveAll のエイリアス）。
    /// </summary>
    public void FinalizeSession(EXP_ExperimentSession session) => SaveAll(session);

    // =====================================================
    // CSV
    // =====================================================

    private void AppendCSVRow(EXP_TrialData trial)
    {
        try
        {
            using var writer = new StreamWriter(CSVFilePath, append: true, Encoding.UTF8);

            if (!_csvHeaderWritten)
            {
                writer.WriteLine(EXP_TrialData.GetCSVHeader());
                _csvHeaderWritten = true;
            }

            writer.WriteLine(trial.ToCSVRow());
        }
        catch (Exception e)
        {
            Debug.LogError($"[EXP_DataRecorder] CSV 書き込みエラー: {e.Message}");
        }
    }

    private void WriteAllCSV()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(EXP_TrialData.GetCSVHeader());

            foreach (var t in _recordedTrials)
                sb.AppendLine(t.ToCSVRow());

            File.WriteAllText(CSVFilePath, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EXP_DataRecorder] CSV 一括書き込みエラー: {e.Message}");
        }
    }

    // =====================================================
    // JSON
    // =====================================================

    private void WriteJSON(EXP_ExperimentSession session)
    {
        try
        {
            // JsonUtility は Dictionary や継承クラスに対応しないため、
            // 軽量な手書き JSON ビルダーを使用します。
            string json = BuildSessionJSON(session);
            File.WriteAllText(JSONFilePath, json, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogError($"[EXP_DataRecorder] JSON 書き込みエラー: {e.Message}");
        }
    }

    /// <summary>
    /// セッション全体を JSON 文字列にシリアライズします。
    /// Newtonsoft.Json を使わずに手書きで構築しているため、外部ライブラリ不要です。
    /// </summary>
    private static string BuildSessionJSON(EXP_ExperimentSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"participantId\": \"{Esc(session.participantId)}\",");
        sb.AppendLine($"  \"groupLabel\": \"{Esc(session.groupLabel)}\",");
        sb.AppendLine($"  \"sessionId\": \"{Esc(session.sessionId)}\",");
        sb.AppendLine($"  \"startTimeISO\": \"{Esc(session.startTimeISO)}\",");
        sb.AppendLine($"  \"endTimeISO\": \"{Esc(session.endTimeISO)}\",");
        sb.AppendLine($"  \"totalTrials\": {session.totalTrials},");
        sb.AppendLine($"  \"completedTrials\": {session.completedTrials},");
        sb.AppendLine($"  \"correctTrials\": {session.correctTrials},");
        sb.AppendLine($"  \"accuracy\": {session.accuracy:F4},");
        sb.AppendLine($"  \"trials\": [");

        for (int i = 0; i < session.trialDataList.Count; i++)
        {
            var t = session.trialDataList[i];
            bool isLast = (i == session.trialDataList.Count - 1);

            sb.AppendLine("    {");
            sb.AppendLine($"      \"blockIndex\": {t.blockIndex},");
            sb.AppendLine($"      \"trialIndex\": {t.trialIndex},");
            sb.AppendLine($"      \"isPractice\": {t.isPractice.ToString().ToLower()},");
            sb.AppendLine($"      \"paradigmType\": \"{Esc(t.paradigmType)}\",");
            sb.AppendLine($"      \"responseValue\": \"{Esc(t.responseValue)}\",");
            sb.AppendLine($"      \"stimulusVal1\": {t.stimulusVal1:F4},");
            sb.AppendLine($"      \"stimulusVal2\": {t.stimulusVal2:F4},");
            sb.AppendLine($"      \"isCorrect\": {(t.isCorrect.HasValue ? t.isCorrect.Value.ToString().ToLower() : "null")},");
            sb.AppendLine($"      \"conditionName\": \"{Esc(t.conditionName)}\",");
            sb.AppendLine($"      \"trialStartTime\": {t.trialStartTime:F6},");
            sb.AppendLine($"      \"stimulusOnsetTime\": {t.stimulusOnsetTime:F6},");
            sb.AppendLine($"      \"responseTime\": {t.responseTime:F6},");
            sb.AppendLine($"      \"reactionTime\": {t.reactionTime:F6},");
            sb.AppendLine($"      \"responseType\": \"{t.responseType}\",");
            sb.AppendLine($"      \"comparisonDetail\": \"{Esc(t.comparisonDetail)}\",");

            // metadata
            sb.Append("      \"metadata\": {");
            if (t.metadata.Count > 0)
            {
                var metaEntries = new List<string>();
                foreach (var kv in t.metadata)
                    metaEntries.Add($"\"{Esc(kv.Key)}\": \"{Esc(kv.Value)}\"");
                sb.Append(" " + string.Join(", ", metaEntries) + " ");
            }
            sb.AppendLine("}");

            sb.Append(isLast ? "    }" : "    },");
            sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>JSON 文字列内の特殊文字をエスケープします。</summary>
    private static string Esc(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
}
