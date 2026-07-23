/// <summary>
/// EXP_ フレームワーク全体で共有する列挙型定義。
/// </summary>

#nullable enable

/// <summary>実験全体のステートマシン状態</summary>
public enum EXP_ExperimentState
{
    /// <summary>未開始・待機中</summary>
    Idle,

    /// <summary>教示表示中</summary>
    Instruction,

    /// <summary>練習試行中</summary>
    Practice,

    /// <summary>本試行中</summary>
    Trial,

    /// <summary>ブロック間休憩</summary>
    Break,

    /// <summary>実験終了</summary>
    Finished
}

/// <summary>1試行内のフェーズ</summary>
public enum EXP_TrialPhase
{
    /// <summary>試行間間隔 (Inter-Trial Interval)</summary>
    ITI,

    /// <summary>刺激提示中</summary>
    Stimulus,

    /// <summary>参加者応答待機中</summary>
    Response,

    /// <summary>フィードバック提示中</summary>
    Feedback
}

/// <summary>参加者の応答結果</summary>
public enum EXP_ResponseType
{
    /// <summary>未応答（初期値）</summary>
    None,

    /// <summary>正解</summary>
    Correct,

    /// <summary>不正解</summary>
    Incorrect,

    /// <summary>タイムアウト（応答なし）</summary>
    Timeout,

    /// <summary>スキップされた試行</summary>
    Skipped
}

/// <summary>データ保存フォーマット</summary>
public enum EXP_DataFormat
{
    /// <summary>CSV のみ</summary>
    CSV,

    /// <summary>JSON のみ</summary>
    JSON,

    /// <summary>CSV と JSON の両方</summary>
    Both
}

/// <summary>入力デバイス種別</summary>
public enum EXP_InputDevice
{
    /// <summary>キーボードのみ</summary>
    Keyboard,

    /// <summary>ゲームパッドのみ</summary>
    Gamepad,

    /// <summary>キーボードとゲームパッドの両方を受け付ける</summary>
    Any
}
