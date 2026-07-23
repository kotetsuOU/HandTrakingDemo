using System.IO;
using UnityEngine;

public static class AppPaths
{
    // --- Application Root ---
    public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/");

    // --- Settings & Configs ---
    public static string ConfigDir => Path.Combine(Application.dataPath, "Settings", "Config");
    public static string RealSenseConfigDir => Path.Combine(ConfigDir, "RealSense");
    public static string HapticsConfigDir => Path.Combine(ConfigDir, "Haptics");
    public static string PCVConfigDir => "Assets/Settings/Config/PCV_Profiles";
    
    // --- Data & Logs ---
    public static string DataDir => Path.Combine(Application.dataPath, "Data");
    public static string HandTrackingDataDir => Path.Combine(Application.dataPath, "HandTrackingData");
    
    // --- Persistence & Exports ---
    public static string PersistentNeighborhoodMapsDir => Path.Combine(Application.persistentDataPath, "NeighborhoodMaps");
    public static string PersistentOcclusionMapsDir => Path.Combine(Application.persistentDataPath, "OcclusionMaps");
    public static string PersistentIntegratedDepthMapsDir => Path.Combine(Application.persistentDataPath, "DepthMaps", "Integrated");
}
