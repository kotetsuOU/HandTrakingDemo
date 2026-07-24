using UnityEngine;

#nullable enable

/// <summary>
/// コントロールパネルの共通 UI 要素（バッジ、プログレスバー、スタイル）の描画ユーティリティ。
/// </summary>
public static class EXP_PanelElementDrawers
{
    private static GUIStyle? _titleStyle;
    private static GUIStyle? _sectionStyle;
    private static GUIStyle? _boldLabelStyle;
    private static GUIStyle? _centerBoldStyle;
    private static GUIStyle? _miniLabelStyle;
    private static GUIStyle? _bigButtonStyle;
    private static GUIStyle? _bigChoiceButtonStyle;

    // UI System Height Constants
    public const float PROGRESS_BAR_HEIGHT = 36f;
    public const float BIG_BUTTON_HEIGHT = 56f;
    public const float CONTROL_BUTTON_HEIGHT = 44f;

    public static void DrawSectionHeader(string title)
    {
        GUILayout.Label(title, GetSectionStyle());
    }

    public static void DrawBadge(string text, Color color, int fontSize = 13, float height = 28)
    {
        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            normal = { textColor = Color.white }
        };

        var prevColor = GUI.color;
        GUI.color = color;
        GUILayout.Box(text, style, GUILayout.Height(height), GUILayout.ExpandWidth(true));
        GUI.color = prevColor;
    }

    public static void DrawProgressBar(float progress, string text) => DrawSegmentedProgressBar(progress, text, null);

    public static void DrawSegmentedProgressBar(float progress, string text, float[]? dividerRatios)
    {
        Rect rect = GUILayoutUtility.GetRect(18, PROGRESS_BAR_HEIGHT, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "", GUI.skin.box);

        Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height);

        var prevColor = GUI.color;
        GUI.color = new Color(0.2f, 0.65f, 0.95f, 0.85f);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        if (dividerRatios != null)
        {
            GUI.color = new Color(1.0f, 1.0f, 1.0f, 0.9f);
            float notchHeight = 6f; // 文字エリアとの被りを避ける上下ノッチ目盛りの高さ
            foreach (float ratio in dividerRatios)
            {
                if (ratio > 0.01f && ratio < 0.99f)
                {
                    float lineX = rect.x + rect.width * ratio;
                    // 上端目盛り (Top Notch)
                    GUI.DrawTexture(new Rect(lineX - 1f, rect.y, 2f, notchHeight), Texture2D.whiteTexture);
                    // 下端目盛り (Bottom Notch)
                    GUI.DrawTexture(new Rect(lineX - 1f, rect.y + rect.height - notchHeight, 2f, notchHeight), Texture2D.whiteTexture);
                }
            }
        }

        GUI.color = prevColor;

        var textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };
        GUI.Label(rect, text, textStyle);
    }

    public const float MESSAGE_BOX_HEIGHT = 60f;

    public static void DrawMessageBox(string message) => DrawFixedMessageBox(message, MESSAGE_BOX_HEIGHT);

    public static void DrawFixedMessageBox(string message, float height = 60f)
    {
        Rect rect = GUILayoutUtility.GetRect(18, height, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "", GUI.skin.box);

        var textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12,
            wordWrap = true,
            normal = { textColor = GUI.skin.label.normal.textColor }
        };

        GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, rect.height - 8f), message ?? "", textStyle);
    }

    // Styles (Lazy Initialized)
    public static GUIStyle GetTitleStyle() => _titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
    public static GUIStyle GetSectionStyle() => _sectionStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
    public static GUIStyle GetBoldLabelStyle() => _boldLabelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
    public static GUIStyle GetCenterBoldStyle() => _centerBoldStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    public static GUIStyle GetMiniLabelStyle() => _miniLabelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
    public static GUIStyle GetBigButtonStyle() => _bigButtonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
    public static GUIStyle GetBigChoiceButtonStyle() => _bigChoiceButtonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
}
