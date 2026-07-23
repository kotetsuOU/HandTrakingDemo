using System;
using System.Collections.Generic;
using System.Text;

#nullable enable

/// <summary>
/// 1試行分のデータを格納するクラス。
/// 実験固有のデータを追加したい場合はこのクラスを継承してください。
/// <para>
/// 使用例:
/// <code>
/// public class MyTrialData : EXP_TrialData
/// {
///     public float hapticIntensity;
///     public override string ToCSVRow() => base.ToCSVRow() + $",{hapticIntensity}";
///     public static new string GetCSVHeader() => EXP_TrialData.GetCSVHeader() + ",hapticIntensity";
/// }
/// </code>
/// </para>
/// </summary>
[Serializable]
public class EXP_TrialData
{
    // =====================================================
    // Trial Identity
    // =====================================================

    /// <summary>試行インデックス（0始まり）</summary>
    public int trialIndex;

    /// <summary>ブロックインデックス（0始まり）</summary>
    public int blockIndex;

    /// <summary>練習試行か本試行か</summary>
    public bool isPractice;

    /// <summary>割り当てられた実験条件名（EXP_BaseCondition.conditionName）</summary>
    public string conditionName = "";

    // =====================================================
    // Timestamps (Application.realtimeSinceStartup [秒])
    // =====================================================

    /// <summary>試行開始タイムスタンプ [秒]</summary>
    public double trialStartTime;

    /// <summary>刺激提示開始タイムスタンプ [秒]</summary>
    public double stimulusOnsetTime;

    /// <summary>参加者が応答した時刻 [秒]</summary>
    public double responseTime;

    /// <summary>
    /// 反応時間 [秒]（responseTime - stimulusOnsetTime）。
    /// 刺激がまだ提示されていない場合は -1 を返します。
    /// </summary>
    public double reactionTime => stimulusOnsetTime > 0 && responseTime > 0
        ? responseTime - stimulusOnsetTime
        : -1.0;

    // =====================================================
    // Response
    // =====================================================

    /// <summary>参加者の応答結果</summary>
    public EXP_ResponseType responseType = EXP_ResponseType.None;

    /// <summary>参加者の応答値（キーコード名やゲームパッドボタン名など）</summary>
    public string responseValue = "";

    /// <summary>
    /// 正誤結果（null = 正誤判定なし）。
    /// EXP_BaseCondition.EvaluateResponse() または EXP_ExperimentManager で設定されます。
    /// </summary>
    public bool? isCorrect;

    // =====================================================
    // Extension
    // =====================================================

    /// <summary>
    /// 任意の追加メタデータ（キー = 値形式）。
    /// EXP_BaseCondition.Apply() 内で条件固有のパラメータを記録するのに使用してください。
    /// CSV 出力時は metadata_key=value 形式で末尾列に追加されます。
    /// </summary>
    public Dictionary<string, string> metadata = new();

    // =====================================================
    // CSV Serialization
    // =====================================================

    /// <summary>CSVヘッダー行を返します。</summary>
    public static string GetCSVHeader()
        => "trialIndex,blockIndex,isPractice,conditionName,"
         + "trialStartTime,stimulusOnsetTime,responseTime,reactionTime,"
         + "responseType,responseValue,isCorrect,metadata";

    /// <summary>このデータをCSV 1行に変換します。</summary>
    public virtual string ToCSVRow()
    {
        string metaStr = "";
        if (metadata.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var kv in metadata)
                sb.Append($"{kv.Key}={kv.Value};");
            metaStr = sb.ToString().TrimEnd(';');
        }

        return string.Join(",",
            trialIndex,
            blockIndex,
            isPractice,
            EscapeCSV(conditionName),
            trialStartTime.ToString("F6"),
            stimulusOnsetTime.ToString("F6"),
            responseTime.ToString("F6"),
            reactionTime.ToString("F6"),
            responseType,
            EscapeCSV(responseValue),
            isCorrect?.ToString() ?? "N/A",
            EscapeCSV(metaStr));
    }

    /// <summary>CSV セル内のカンマ・ダブルクォートをエスケープします。</summary>
    protected static string EscapeCSV(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
