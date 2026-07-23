/// <summary>
/// Unityが起動・再コンパイルするたびに、インストールされているSDKに応じて
/// スクリプト定義シンボルを自動設定するエディタスクリプト。
///
/// 判定ロジック:
///   manifest.json に "com.shinolab.autd3" が含まれる → USE_AUTD3_LEGACY を追加
///   manifest.json に "com.shinolab.autd3-sdk" が含まれる → USE_AUTD3_LEGACY を削除
/// </summary>
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using System.IO;
using System.Linq;

[InitializeOnLoad]
public static class AUTD3_DefineSymbolSetup
{
    private const string LegacySymbol = "USE_AUTD3_LEGACY";

    static AUTD3_DefineSymbolSetup()
    {
        Apply();
    }

    [MenuItem("AUTD3/Apply SDK Define Symbols")]
    public static void Apply()
    {
        string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning("[AUTD3_DefineSymbolSetup] manifest.json not found.");
            return;
        }

        string manifest = File.ReadAllText(manifestPath);
        bool isLegacy = manifest.Contains("\"com.shinolab.autd3\"") &&
                        !manifest.Contains("\"com.shinolab.autd3-sdk\"");

        // 現在の対象プラットフォームのシンボルを取得
        var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
        var target = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
        string current = PlayerSettings.GetScriptingDefineSymbols(target);
        var symbols = current.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        bool hasSymbol = symbols.Contains(LegacySymbol);

        if (isLegacy && !hasSymbol)
        {
            symbols.Add(LegacySymbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            Debug.Log($"[AUTD3_DefineSymbolSetup] Added '{LegacySymbol}' for {targetGroup}. Legacy SDK detected.");
        }
        else if (!isLegacy && hasSymbol)
        {
            symbols.Remove(LegacySymbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            Debug.Log($"[AUTD3_DefineSymbolSetup] Removed '{LegacySymbol}' for {targetGroup}. New SDK detected.");
        }
        else
        {
            Debug.Log($"[AUTD3_DefineSymbolSetup] No change needed. isLegacy={isLegacy}, hasSymbol={hasSymbol}");
        }
    }
}
#endif
