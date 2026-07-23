using System;

#nullable enable

/// <summary>
/// AUTD3デバイスのホログラフィ（マルチフォーカス）計算アルゴリズム
/// </summary>
public enum HoloAlgorithm
{
    /// <summary>
    /// GSPATアルゴリズム（精度と計算速度のバランスが良い標準的な手法）
    /// </summary>
    GSPAT,
    
    /// <summary>
    /// Naiveアルゴリズム（計算は単純だが、フォーカス数が増えると精度が下がる場合がある）
    /// </summary>
    Naive
}

/// <summary>
/// 超音波の変調モード（振動のパターン）
/// </summary>
public enum ModulationMode
{
    /// <summary>
    /// サイン波による変調（触覚として最も感じやすい）
    /// </summary>
    Sine,
    
    /// <summary>
    /// 変調なしの定常出力（連続的に同じ強さで出力する）
    /// </summary>
    Static
}

/// <summary>
/// 触覚生成モード（単純1点 or 形状に沿った精密生成）
/// </summary>
public enum HapticsGenerationMode
{
    Simplified,
    Precision
}

/// <summary>
/// AUTD3デバイスとの通信接続タイプ
/// </summary>
public enum AUTDLinkType
{
    TwinCAT,
    SOEM,
    Simulator
}

/// <summary>
/// サイレンサーモード（超音波出力の急激な変化を和らげ、騒音を減らす設定）
/// </summary>
public enum SilencerMode
{
    /// <summary>
    /// サイレンサーを無効化（即時変化、騒音が出やすい）
    /// </summary>
    Disabled,
    
    /// <summary>
    /// 更新レート固定のサイレンサー（強度と位相の変化ステップを指定）
    /// </summary>
    FixedUpdateRate,
    
    /// <summary>
    /// 完了時間固定のサイレンサー（一定時間で滑らかに変化させる）
    /// </summary>
    FixedCompletionTime
}
