using Intel.RealSense;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RsDevice))]
public class RsDeviceEditor : Editor
{
    private SerializedProperty config;
    private SerializedProperty mode;

    /// <summary>
    /// This function is called when the object becomes enabled and active.
    /// </summary>
    void OnEnable()
    {
        config = serializedObject.FindProperty("DeviceConfiguration");
        mode = config.FindPropertyRelative("mode");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var device = target as RsDevice;
        bool isStreaming = device.isActiveAndEnabled && device.ActiveProfile != null;

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.Space();

        EditorGUI.BeginDisabledGroup(isStreaming);
        mode.enumValueIndex = GUILayout.Toolbar(mode.enumValueIndex, mode.enumDisplayNames);
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("processMode"));
        EditorGUILayout.Space();
        EditorGUI.EndDisabledGroup();

        switch ((RsConfiguration.Mode)mode.enumValueIndex)
        {
            case RsConfiguration.Mode.Live:
                {
                    EditorGUI.BeginDisabledGroup(isStreaming);
                    EditorGUILayout.PropertyField(config.FindPropertyRelative("RequestedSerialNumber"));
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(config.FindPropertyRelative("Profiles"), true);
                    EditorGUILayout.Space();
                    EditorGUI.EndDisabledGroup();
                    break;
                }
            case RsConfiguration.Mode.Playback:
                {
                    EditorGUI.BeginDisabledGroup(isStreaming);
                    EditorGUILayout.PropertyField(config.FindPropertyRelative("RequestedSerialNumber"));

                    var prop = config.FindPropertyRelative("PlaybackFile");
                    if (!string.IsNullOrEmpty(prop.stringValue) && System.IO.Path.IsPathRooted(prop.stringValue))
                    {
                        EditorGUILayout.HelpBox("Warning: The playback file path is an absolute path. For successful distribution, please place the file inside the project (e.g. Assets/StreamingAssets) and click 'Sanitize Paths' below.", MessageType.Warning);
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(prop);
                    if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        var path = EditorUtility.OpenFilePanel("Recorded sequence", "", "bag");
                        if (path.Length != 0)
                        {
                            serializedObject.Update();
                            prop.stringValue = MakeRelativePath(path);
                            serializedObject.ApplyModifiedProperties();
                            GUI.FocusControl(null);
                        }
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();
                    EditorGUI.EndDisabledGroup();

                    if (isStreaming)
                    {
                        using (var playback = PlaybackDevice.FromDevice(device.ActiveProfile.Device))
                        {
                            bool isPlaying = playback.Status == PlaybackStatus.Playing;
                            var playBtnStyle = EditorGUIUtility.IconContent("PlayButton", "|Play");
                            var pauseBtnStyle = EditorGUIUtility.IconContent("PauseButton", "|Pause");
                            var rewindBtnStyle = EditorGUIUtility.IconContent("animation.firstkey.png");
                            EditorGUILayout.BeginHorizontal();
                            if (GUILayout.Button(rewindBtnStyle, "CommandLeft"))
                                playback.Position = 0;
                            if (GUILayout.Button(isPlaying ? pauseBtnStyle : playBtnStyle, "CommandRight"))
                            {
                                if (isPlaying)
                                    playback.Pause();
                                else
                                    playback.Resume();
                            }
                            EditorGUILayout.EndHorizontal();
                            if (!isPlaying)
                            {
                                playback.Position = (ulong)EditorGUILayout.Slider(playback.Position, 0, playback.Duration);
                            }
                            EditorGUI.BeginDisabledGroup(true);
                            EditorGUILayout.Space();
                            EditorGUILayout.PropertyField(config.FindPropertyRelative("Profiles"), true);
                            EditorGUI.EndDisabledGroup();
                        }
                    }
                    break;
                }
            case RsConfiguration.Mode.Record:
                {
                    EditorGUI.BeginDisabledGroup(isStreaming);
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("recordDurationInFrames"));
                    EditorGUILayout.PropertyField(config.FindPropertyRelative("RequestedSerialNumber"));

                    var prop = config.FindPropertyRelative("RecordPath");
                    if (!string.IsNullOrEmpty(prop.stringValue) && System.IO.Path.IsPathRooted(prop.stringValue))
                    {
                        EditorGUILayout.HelpBox("Warning: The record path is an absolute path. For successful distribution, please place it inside the project (e.g. Assets/StreamingAssets) and click 'Sanitize Paths' below.", MessageType.Warning);
                    }

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(prop);
                    if (GUILayout.Button("Choose", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        var path = EditorUtility.SaveFilePanel("Recorded sequence", "", System.DateTime.Now.ToString("yyyyMMdd_hhmmss"), "bag");
                        if (path.Length != 0)
                        {
                            serializedObject.Update();
                            prop.stringValue = MakeRelativePath(path);
                            serializedObject.ApplyModifiedProperties();
                            GUI.FocusControl(null);
                        }
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space();
                    EditorGUILayout.PropertyField(config.FindPropertyRelative("Profiles"), true);
                    EditorGUILayout.Space();
                    EditorGUI.EndDisabledGroup();
                    break;
                }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Distribution Tools", EditorStyles.boldLabel);
        if (GUILayout.Button("Sanitize Paths (Make Relative)"))
        {
            serializedObject.Update();
            var playbackProp = config.FindPropertyRelative("PlaybackFile");
            var recordProp = config.FindPropertyRelative("RecordPath");

            playbackProp.stringValue = MakeRelativePath(playbackProp.stringValue);
            recordProp.stringValue = MakeRelativePath(recordProp.stringValue);

            serializedObject.ApplyModifiedProperties();
            Debug.Log("[RsDeviceEditor] Sanitized paths for this RsDevice component.");
        }

        serializedObject.ApplyModifiedProperties();
        EditorGUI.EndChangeCheck();
    }

    private static string MakeRelativePath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath)) return absolutePath;
        absolutePath = absolutePath.Replace("\\", "/");

        string projectRoot = AppPaths.ProjectRoot;

        if (absolutePath.StartsWith(projectRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            string relativePath = absolutePath.Substring(projectRoot.Length);
            if (relativePath.StartsWith("/"))
            {
                relativePath = relativePath.Substring(1);
            }
            return relativePath;
        }

        return absolutePath;
    }

    [MenuItem("Tools/RealSense/Sanitize All RsDevice Paths in Active Scene")]
    public static void SanitizeAllPathsInActiveScene()
    {
        var devices = Resources.FindObjectsOfTypeAll<RsDevice>();
        int count = 0;
        foreach (var device in devices)
        {
            if (device.gameObject.scene.name == null) continue;

            bool changed = false;
            var playbackFile = device.DeviceConfiguration.PlaybackFile;
            var recordPath = device.DeviceConfiguration.RecordPath;

            string newPlayback = MakeRelativePath(playbackFile);
            if (newPlayback != playbackFile)
            {
                device.DeviceConfiguration.PlaybackFile = newPlayback;
                changed = true;
            }

            string newRecord = MakeRelativePath(recordPath);
            if (newRecord != recordPath)
            {
                device.DeviceConfiguration.RecordPath = newRecord;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(device);
                if (!Application.isPlaying)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(device.gameObject.scene);
                }
                count++;
            }
        }

        Debug.Log($"[RsDeviceEditor] Sanitized paths in {count} RsDevice components in the active scene.");
    }
}
