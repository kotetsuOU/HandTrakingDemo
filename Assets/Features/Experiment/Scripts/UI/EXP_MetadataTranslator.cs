using System.Collections.Generic;

#nullable enable

/// <summary>
/// 実験メタデータキーの日本語表示名変換ユーティリティ。
/// </summary>
public static class EXP_MetadataTranslator
{
    private static readonly Dictionary<string, string> KeyTranslations = new()
    {
        { "afcMode", "2AFC ペア構成モード" },
        { "referenceFrequency", "基準 STM 周波数 [Hz]" },
        { "comparisonFrequency", "比較 STM 周波数 [Hz]" },
        { "interval1Frequency", "第1刺激の STM 周波数 [Hz]" },
        { "interval2Frequency", "第2刺激の STM 周波数 [Hz]" },
        { "frequencyDelta", "周波数の差分 |ΔHz|" },
        { "referenceOffsetY", "基準 Y オフセット [m]" },
        { "comparisonOffsetY", "比較 Y オフセット [m]" },
        { "interval1Y", "第1刺激の Y オフセット [m]" },
        { "interval2Y", "第2刺激の Y オフセット [m]" },
        { "offsetDelta", "Y オフセットの差分 |Δm|" },
        { "referencePosition", "基準刺激の提示位置" },
        { "refFirst", "第1刺激が基準刺激か" },
        { "currentInterval", "現在の刺激提示フェーズ" }
    };

    /// <summary>
    /// メタデータキーに対応する日本語表示名を取得します。
    /// 未登録のキーの場合はそのままキー名を返します。
    /// </summary>
    public static string TranslateKey(string key)
    {
        return KeyTranslations.TryGetValue(key, out var translation) ? translation : key;
    }
}
