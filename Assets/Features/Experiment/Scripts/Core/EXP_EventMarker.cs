using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#nullable enable

/// <summary>
/// タイムスタンプ付きイベントログコンポーネント。
/// 試行開始・終了・刺激提示・応答などの実験イベントを記録します。
/// <para>
/// イベントは <see cref="OnEventMarked"/> に登録することで、
/// LSL（Lab Streaming Layer）マーカーの送信など外部システムとの連携が可能です。
/// </para>
/// <para>
/// ファイルには TSV（タブ区切り）で逐次保存されます:
/// <c>time[s]  label</c>
/// </para>
/// </summary>
public class EXP_EventMarker : MonoBehaviour
{
    // =====================================================
    // Inspector Settings
    // =====================================================

    [Header("Event Log Settings")]
    [Tooltip("イベントログを TSV ファイルに保存するか")]
    public bool saveToFile = true;

    [Tooltip("Debug.Log にも出力するか")]
    public bool logToConsole = true;

    // =====================================================
    // Events
    // =====================================================

    /// <summary>
    /// イベントが記録されたときに発火します。
    /// 第1引数: ラベル, 第2引数: タイムスタンプ [秒]（Application.realtimeSinceStartup）。
    /// LSL マーカー送信などに利用してください。
    /// </summary>
    public event Action<string, double>? OnEventMarked;

    // =====================================================
    // State (Read-Only)
    // =====================================================

    /// <summary>TSV ファイルのパス</summary>
    public string FilePath { get; private set; } = "";

    // =====================================================
    // Private Fields
    // =====================================================

    private readonly List<(double time, string label)> _events = new();
    private bool _headerWritten = false;

    // =====================================================
    // Public API
    // =====================================================

    /// <summary>
    /// 初期化します。セッション開始時に EXP_ExperimentManager から呼ばれます。
    /// </summary>
    /// <param name="outputDirectory">保存先ディレクトリ</param>
    /// <param name="sessionId">セッション ID（ファイル名に使用）</param>
    public void Initialize(string outputDirectory, string sessionId)
    {
        _events.Clear();
        _headerWritten = false;

        if (saveToFile)
        {
            Directory.CreateDirectory(outputDirectory);
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            FilePath = Path.Combine(outputDirectory, $"Events_{sessionId}_{timestamp}.tsv");
        }
    }

    /// <summary>
    /// イベントを現在のタイムスタンプで記録します。
    /// </summary>
    /// <param name="label">
    /// イベントラベル（推奨命名例: TrialStart, StimulusOn, StimulusOff, Response_Z, Timeout, BlockBreak）
    /// </param>
    public void Mark(string label)
    {
        double t = (double)Time.realtimeSinceStartup;
        _events.Add((t, label));
        OnEventMarked?.Invoke(label, t);

        if (logToConsole)
            Debug.Log($"[EXP_EventMarker] {t:F4}s  {label}");

        if (saveToFile && !string.IsNullOrEmpty(FilePath))
            AppendToFile(t, label);
    }

    /// <summary>
    /// タイムスタンプを指定してイベントを記録します（外部クロック使用時など）。
    /// </summary>
    public void Mark(string label, double timestamp)
    {
        _events.Add((timestamp, label));
        OnEventMarked?.Invoke(label, timestamp);

        if (logToConsole)
            Debug.Log($"[EXP_EventMarker] {timestamp:F4}s  {label}");

        if (saveToFile && !string.IsNullOrEmpty(FilePath))
            AppendToFile(timestamp, label);
    }

    /// <summary>記録済み全イベントを読み取り専用で返します。</summary>
    public IReadOnlyList<(double time, string label)> GetEvents() => _events.AsReadOnly();

    // =====================================================
    // Private Helpers
    // =====================================================

    private void AppendToFile(double time, string label)
    {
        try
        {
            using var writer = new StreamWriter(FilePath, append: true, Encoding.UTF8);

            if (!_headerWritten)
            {
                writer.WriteLine("time_s\tlabel");
                _headerWritten = true;
            }

            writer.WriteLine($"{time:F6}\t{label}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[EXP_EventMarker] ファイル書き込みエラー: {e.Message}");
        }
    }
}
