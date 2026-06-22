using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RsMaterialController))]
public class RsMaterialControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();

        RsMaterialController controller = (RsMaterialController)target;

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(controller, "Change Material Settings");
            controller.ApplyMaterial();
            EditorUtility.SetDirty(controller);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Color Selection", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        PointCloudColorMode selectedMode =
            (PointCloudColorMode)EditorGUILayout.EnumPopup("Color Mode", controller.colorMode);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(controller, "Change PointCloud Color");
            controller.ChangeColorMode(selectedMode);
            EditorUtility.SetDirty(controller);
        }
    }
}
