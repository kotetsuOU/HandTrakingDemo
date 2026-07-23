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

    public static void DrawProgressBar(float progress, string text)
    {
        Rect rect = GUILayoutUtility.GetRect(18, 30, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "", GUI.skin.box);
        Rect fillRect = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height);

        var prevColor = GUI.color;
        GUI.color = new Color(0.2f, 0.65f, 0.95f, 0.8f);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
        GUI.color = prevColor;

        var textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 12
        };
        GUI.Label(rect, text, textStyle);
    }

    public static void DrawMessageBox(string message)
    {
        using (new GUILayout.VerticalScope(GUI.skin.box))
        {
            GUILayout.Label(message, GetBoldLabelStyle());
        }
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
