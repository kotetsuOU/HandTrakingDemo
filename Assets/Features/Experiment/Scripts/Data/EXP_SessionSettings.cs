using UnityEngine;
using System;

#nullable enable

/// <summary>
/// 被験者情報、セッション構造、および各種タイミング設定をまとめるシリアライズド設定データ。
/// <see cref="EXP_ExperimentManager"/> の Inspector 上で折りたたんでスッキリ整理できます。
/// </summary>
[Serializable]
public class EXP_SessionSettings
{
    [Header("Participant & Mode")]
    [Tooltip("被験者 ID（保存ファイル名・匿名化識別子に使用）")]
    public string participantId = "P001";

    [Tooltip("被験者 氏名（個人管理用・ログ内のみ保存）")]
    public string participantName = "";

    [Tooltip("グループ / 条件群キー")]
    public string groupLabel = "";

    [Tooltip("デバッグ表示モード (DebugPlay)。trueの時、数値パラメータや正答率を表示します。")]
    public bool isDebugMode = false;

    [Header("Session Structure")]
    [Tooltip("1ブロックあたりの試行数")]
    public int trialsPerBlock = 20;

    [Tooltip("総ブロック数")]
    [Min(1)]
    public int blockCount = 1;

    [Tooltip("本試行前の練習試行数（0 = 練習なし）")]
    [Min(0)]
    public int practiceTrialCount = 0;

    [Header("Timing Settings [sec]")]
    [Tooltip("試行間隔 (ITI) [秒]")]
    public float itiDuration = 1.0f;

    [Tooltip("刺激提示時間 [秒]（0 = 条件のコルーチン制御に従う）")]
    public float stimulusDuration = 0f;

    [Tooltip("応答タイムアウト時間 [秒]（0 = 無制限タイムアウトなし）")]
    public float responseTimeout = 0f;

    [Tooltip("ブロック間休憩時間 [秒]")]
    public float breakDuration = 60.0f;
}
