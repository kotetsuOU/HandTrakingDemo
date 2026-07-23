using UnityEngine;

#nullable enable

/// <summary>
/// 実験全体の設定を保持する ScriptableObject。
/// Project ウィンドウで右クリック → Create → EXP → ExperimentConfig から作成できます。
/// </summary>
[CreateAssetMenu(fileName = "NewExperimentConfig", menuName = "EXP/ExperimentConfig")]
public class EXP_ExperimentConfig : ScriptableObject
{
    // =====================================================
    // Participant Info
    // =====================================================
    [Header("Participant Info")]
    [Tooltip("参加者ID（実験ファイル名に使用）")]
    public string participantId = "P001";

    [Tooltip("グループ / 条件ラベル（任意）")]
    public string groupLabel = "";

    [Tooltip("デバッグ表示モード (DebugPlay)。trueにすると被験者画面やダッシュボードに物理周波数・Offsetの数値が表示されます。本番実験では false に設定してください。")]
    public bool isDebugMode = false;

    // =====================================================
    // Trial Settings
    // =====================================================
    [Header("Trial Settings")]
    [Tooltip("1ブロックあたりの試行数（blockCount=1 の場合は全試行数）")]
    public int trialsPerBlock = 20;

    [Tooltip("ブロック数（1 = ブロック分けなし）")]
    [Min(1)]
    public int blockCount = 1;

    [Tooltip("本試行前に実施する練習試行数（0 = 練習なし）")]
    [Min(0)]
    public int practiceTrialCount = 5;

    // =====================================================
    // Timing (seconds)
    // =====================================================
    [Header("Timing (seconds)")]
    [Tooltip("試行間間隔 [秒] (ITI)")]
    [Min(0f)]
    public float itiDuration = 1.0f;

    [Tooltip("刺激提示固定時間 [秒]（0 = 応答があるまで継続）")]
    [Min(0f)]
    public float stimulusDuration = 0f;

    [Tooltip("応答タイムアウト [秒]（0 = タイムアウトなし）")]
    [Min(0f)]
    public float responseTimeout = 5.0f;

    [Tooltip("フィードバック提示時間 [秒]（0 = フィードバックなし）")]
    [Min(0f)]
    public float feedbackDuration = 0.5f;

    [Tooltip("ブロック間休憩の最大待機時間 [秒]（0 = 時間制限なし、参加者のキー入力で再開）")]
    [Min(0f)]
    public float breakDuration = 30f;

    // =====================================================
    // Randomization
    // =====================================================
    [Header("Randomization")]
    [Tooltip("乱数シード（-1 = 実行ごとに異なるランダムシード）")]
    public int randomSeed = -1;

    // =====================================================
    // Data Recording
    // =====================================================
    [Header("Data Recording")]
    [Tooltip("保存フォーマット")]
    public EXP_DataFormat dataFormat = EXP_DataFormat.Both;

    [Tooltip("保存先ディレクトリの絶対パス（空の場合は Application.persistentDataPath/ExperimentData を使用）")]
    public string outputDirectory = "";

    [Tooltip("ファイル名プレフィックス（例: \"Exp\" → Exp_P001_20250723_143022.csv）")]
    public string filePrefix = "Trial";

    // =====================================================
    // Input
    // =====================================================
    [Header("Input")]
    [Tooltip("使用する入力デバイス")]
    public EXP_InputDevice inputDevice = EXP_InputDevice.Any;

    [Tooltip("キーボード使用時の応答キーリスト")]
    public KeyCode[] responseKeys = new KeyCode[] { KeyCode.Z, KeyCode.X };

    [Tooltip("ゲームパッド使用時のボタン名リスト（InputSystem の GamepadButton 名）\n例: buttonSouth, buttonNorth, buttonEast, buttonWest, leftTrigger, rightTrigger")]
    public string[] gamepadButtons = new string[] { "buttonSouth", "buttonNorth" };

    // =====================================================
    // UI
    // =====================================================
    [Header("UI")]
    [Tooltip("Unity UI を使用する場合 true。false にすると UI 操作をスキップし、イベントのみ発火します（外部表示対応）")]
    public bool useUnityUI = true;

    [Tooltip("フィードバック (◯ / ✕) を表示するか")]
    public bool showFeedback = true;

    [Tooltip("true にすると Debug.Log の出力を抑制します")]
    public bool suppressLogs = false;
}
