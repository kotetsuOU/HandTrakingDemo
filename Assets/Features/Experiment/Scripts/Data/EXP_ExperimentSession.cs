using System;
using System.Collections.Generic;

#nullable enable

/// <summary>
/// 現在実行中のセッションの実行時情報を保持するクラス。
/// JSON 保存のために [Serializable] を付与しています。
/// </summary>
[Serializable]
public class EXP_ExperimentSession
{
    // =====================================================
    // Session Identity
    // =====================================================

    /// <summary>参加者 ID</summary>
    public string participantId = "";

    /// <summary>グループ / 条件ラベル</summary>
    public string groupLabel = "";

    /// <summary>セッション固有 ID（8桁英数字）</summary>
    public string sessionId = "";

    /// <summary>セッション開始日時（ISO 8601 形式）</summary>
    public string startTimeISO = "";

    /// <summary>セッション終了日時（ISO 8601 形式）</summary>
    public string endTimeISO = "";

    // =====================================================
    // Progress
    // =====================================================

    /// <summary>本試行の総試行数</summary>
    public int totalTrials;

    /// <summary>完了した試行数（中断時の部分保存に利用）</summary>
    public int completedTrials;

    /// <summary>正解試行数</summary>
    public int correctTrials;

    /// <summary>正答率（0〜1）</summary>
    public float accuracy => totalTrials > 0 ? (float)correctTrials / totalTrials : 0f;

    // =====================================================
    // Trial Data
    // =====================================================

    /// <summary>全試行のデータリスト（JSON 保存に使用）</summary>
    public List<EXP_TrialData> trialDataList = new();

    // =====================================================
    // Factory & Lifecycle
    // =====================================================

    /// <summary>
    /// 新規セッションを作成して返します。
    /// </summary>
    public static EXP_ExperimentSession Create(string participantId, string groupLabel)
    {
        return new EXP_ExperimentSession
        {
            participantId  = participantId,
            groupLabel     = groupLabel,
            sessionId      = GenerateSessionId(),
            startTimeISO   = DateTime.Now.ToString("o"),
        };
    }

    /// <summary>
    /// セッション終了時に呼び出し、終了時刻を記録します。
    /// </summary>
    public void FinalizeSession()
    {
        endTimeISO = DateTime.Now.ToString("o");
    }

    // =====================================================
    // Helpers
    // =====================================================

    private static string GenerateSessionId()
    {
        // "N" フォーマット = ハイフンなしの32文字 GUID → 先頭8文字を大文字で返す
        return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }
}
