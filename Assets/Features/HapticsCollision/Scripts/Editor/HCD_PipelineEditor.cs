#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HCD_Pipeline))]
public class HCD_PipelineEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Assembly Definitionの制約によりReflectionでAnimationControllerを検索
        bool isAutoLinked = false;
        System.Type animCtrlType = System.Type.GetType("AnimationController, Assembly-CSharp");
        if (animCtrlType != null)
        {
            Object animCtrl = Object.FindFirstObjectByType(animCtrlType);
            if (animCtrl != null)
            {
                SerializedObject animCtrlSO = new SerializedObject(animCtrl);
                SerializedProperty autoUpdateProp = animCtrlSO.FindProperty("autoUpdateCollisionTarget");
                if (autoUpdateProp != null && autoUpdateProp.boolValue)
                {
                    isAutoLinked = true;
                }
            }
        }

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "m_Script")
            {
                GUI.enabled = false;
                EditorGUILayout.PropertyField(iterator, true);
                GUI.enabled = true;
                continue;
            }

            if (iterator.name == "distanceProcessor")
            {
                EditorGUILayout.PropertyField(iterator, false); // foldout for the object
                if (iterator.isExpanded)
                {
                    EditorGUI.indentLevel++;

                    SerializedProperty detModeProp = iterator.FindPropertyRelative("detectionMode");
                    SerializedProperty distModeProp = iterator.FindPropertyRelative("distanceMode");

                    int detMode = detModeProp != null ? detModeProp.enumValueIndex : 0;
                    int distMode = distModeProp != null ? distModeProp.enumValueIndex : 0;

                    SerializedProperty dpIterator = iterator.Copy();
                    SerializedProperty endProp = dpIterator.GetEndProperty();
                    bool dpEnterChildren = true;
                    while (dpIterator.NextVisible(dpEnterChildren) && !SerializedProperty.EqualContents(dpIterator, endProp))
                    {
                        dpEnterChildren = false;

                        string propName = dpIterator.name;

                        // DetectionMode に基づくプロパティ表示制限
                        if (propName == "targetObject" && detMode != (int)HCD_DistanceProcessor.DetectionMode.TransformOnly) continue;
                        if (propName == "targetSkinnedMeshes" && detMode != (int)HCD_DistanceProcessor.DetectionMode.SkinnedMeshRenderer) continue;
                        if (propName == "targetMeshFilters" && detMode != (int)HCD_DistanceProcessor.DetectionMode.MeshFilter) continue;

                        // DistanceMode に基づくプロパティ表示制限
                        if (propName == "viewCamera" && distMode != (int)HCD_DistanceProcessor.DistanceMode.ViewDirection) continue;
                        if ((propName == "meshSurfaceDistanceThreshold" || propName == "meshBackfaceDistanceThreshold") && distMode != (int)HCD_DistanceProcessor.DistanceMode.MeshSurface) continue;
                        if ((propName == "viewSurfaceDistanceThreshold" || propName == "viewBackfaceDistanceThreshold") && distMode != (int)HCD_DistanceProcessor.DistanceMode.ViewDirection) continue;

                        // 対象設定に関するプロパティの場合はグレーアウト判定
                        bool isTargetProp = (propName == "detectionMode" || 
                                             propName == "targetObject" || 
                                             propName == "targetSkinnedMeshes" ||
                                             propName == "targetMeshFilters");

                        if (isAutoLinked && isTargetProp)
                        {
                            GUI.enabled = false;
                            EditorGUILayout.PropertyField(dpIterator, true);
                            GUI.enabled = true;
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(dpIterator, true);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        if (isAutoLinked)
        {
            EditorGUILayout.HelpBox("AnimationController の Auto Update が有効なため、DistanceProcessor の対象設定は非プレイ時を含め自動的に上書きされます。", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif