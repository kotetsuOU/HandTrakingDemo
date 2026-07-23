using UnityEngine;
using UnityEditor;

#nullable enable

/// <summary>
/// 【実験用コントロールダッシュボード】（EditorWindow）
/// Unity Editor メニュー Tools -> EXP -> 実験コントロールパネル から開けます。
/// <para>
/// 描画ロジックは <see cref="EXP_ControlPanelDrawer"/> にモジュール化・分離されています。
/// </para>
/// </summary>
public class EXP_ExperimentControlWindow : EditorWindow
{
    [MenuItem("Tools/EXP/実験コントロールパネル")]
    [MenuItem("Tools/EXP/Experiment Control Panel")]
    public static void OpenWindow()
    {
        var window = GetWindow<EXP_ExperimentControlWindow>("実験コントロールパネル");
        window.minSize = new Vector2(460, 640);
        window.Show();
    }

    void OnEnable()
    {
        EditorApplication.update += Repaint;
    }

    void OnDisable()
    {
        EditorApplication.update -= Repaint;
    }

    void OnGUI()
    {
        var manager = Object.FindAnyObjectByType<EXP_ExperimentManager>();
        if (manager == null)
        {
            EditorGUILayout.HelpBox("❌ シーン内に EXP_ExperimentManager が見つかりません。ヒエラルキーを確認してください。", UnityEditor.MessageType.Error);
            return;
        }

        EXP_ControlPanelDrawer.DrawDashboard(manager, isEditorWindow: true);
    }
}
