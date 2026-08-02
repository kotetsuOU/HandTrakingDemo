using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Core.Logging;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class PCV_ConfigIO
{
    private static string DEFAULT_SAVE_FOLDER => AppPaths.PCVConfigDir;

    [Serializable]
        public class PCV_ProfileData
    {
        // File Settings
        public List<FileSettingDTO> fileSettings = new List<FileSettingDTO>();
    }

    [Serializable]
    public class FileSettingDTO
    {
        public string filePath;
        public bool useFile;
    }

    public static void SaveConfig(PCV_Settings settings, string fileName)
    {
        if (settings == null)
        {
            AppLogger.LogError(PCV_LogTriggers.TagConfigIO, "[PCV_ConfigIO] Settings component is null.");
            return;
        }

        PCV_ProfileData data = new PCV_ProfileData();

        if (settings.fileSettings != null)
        {
            foreach (var fs in settings.fileSettings)
            {
                data.fileSettings.Add(new FileSettingDTO
                {
                    filePath = fs.filePath,
                    useFile = fs.useFile
                });
            }
        }



        if (!Directory.Exists(DEFAULT_SAVE_FOLDER))
        {
            Directory.CreateDirectory(DEFAULT_SAVE_FOLDER);
        }

        string safeFileName = fileName.EndsWith(".json") ? fileName : fileName + ".json";
        string fullPath = Path.Combine(DEFAULT_SAVE_FOLDER, safeFileName);

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(fullPath, json);
            AppLogger.Log(settings, PCV_LogTriggers.TagConfigIO, $"[PCV_ConfigIO] Profile saved to: {fullPath}");
#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
        }
        catch (Exception e)
        {
            AppLogger.LogError(settings, PCV_LogTriggers.TagConfigIO, $"[PCV_ConfigIO] Failed to save profile: {e.Message}");
        }
    }

    public static void LoadConfig(PCV_Settings settings, string fileName)
    {
        if (settings == null)
        {
            AppLogger.LogError(PCV_LogTriggers.TagConfigIO, "[PCV_ConfigIO] Settings component is null.");
            return;
        }

        string safeFileName = fileName.EndsWith(".json") ? fileName : fileName + ".json";
        string fullPath = Path.Combine(DEFAULT_SAVE_FOLDER, safeFileName);

        if (!File.Exists(fullPath))
        {
            AppLogger.LogError(settings, PCV_LogTriggers.TagConfigIO, $"[PCV_ConfigIO] File not found: {fullPath}");
            return;
        }

        try
        {
            string json = File.ReadAllText(fullPath);
            PCV_ProfileData data = JsonUtility.FromJson<PCV_ProfileData>(json);

            if (data == null)
            {
                AppLogger.LogError(settings, PCV_LogTriggers.TagConfigIO, "[PCV_ConfigIO] Failed to parse JSON.");
                return;
            }

#if UNITY_EDITOR
            Undo.RecordObject(settings, "Load PCV Profile");
#endif
            if (settings.fileSettings != null)
            {
                for (int i = 0; i < settings.fileSettings.Length; i++)
                {
                    FileSettingDTO matchedDto = null;
                    foreach (var dto in data.fileSettings)
                    {
                        if (dto.filePath == settings.fileSettings[i].filePath)
                        {
                            matchedDto = dto;
                            break;
                        }
                    }

                    if (matchedDto != null)
                    {
                        settings.fileSettings[i].useFile = matchedDto.useFile;
                    }
                }
            }



#if UNITY_EDITOR
            EditorUtility.SetDirty(settings);
#endif
            AppLogger.Log(settings, PCV_LogTriggers.TagConfigIO, $"[PCV_ConfigIO] Profile loaded from: {fullPath}");
        }
        catch (Exception e)
        {
            AppLogger.LogError(settings, PCV_LogTriggers.TagConfigIO, $"[PCV_ConfigIO] Failed to load profile: {e.Message}");
        }
    }
}
