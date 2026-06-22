using UnityEngine;

public enum PointCloudSource
{
    PCV_File_CPU,          // PCVでロードしたファイルデータを使用
    RealSense_GPU_Global   // PointCloudRenderer (GlobalManager) の統合データを使用
}

[System.Serializable]
public struct FileSettings
{
    public bool useFile;
    public string filePath;
    public Color color;

    [Tooltip("ファイル内の色情報(PLY等)を優先して使用するか")]
    public bool useFileColor;

    [Tooltip("位置合わせの結果を反映させる対象のゲームオブジェクト")]
    public GameObject targetObject;

    public bool IsDifferent(FileSettings other)
    {
        return useFile != other.useFile ||
                       filePath != other.filePath ||
                       color != other.color ||
                       useFileColor != other.useFileColor ||
                       targetObject != other.targetObject;
    }
}

public class PCV_Settings : MonoBehaviour
{
    [Header("Rendering Source")]
    [Tooltip("PCDRendererFeatureに送るデータのソースを選択します")]
    public PointCloudSource renderingSource = PointCloudSource.PCV_File_CPU;

    public FileSettings[] fileSettings = new FileSettings[4]
    {
        new FileSettings { useFile = true,  filePath = "Assets/HandTrackingData/PointCloudData/currentGlobalVerticesRight.txt",  color = Color.red, useFileColor = true },
        new FileSettings { useFile = false, filePath = "Assets/HandTrackingData/PointCloudData/currentGlobalVerticesLeft.txt",   color = Color.green, useFileColor = true },
        new FileSettings { useFile = false, filePath = "Assets/HandTrackingData/PointCloudData/currentGlobalVerticesBottom.txt", color = Color.blue, useFileColor = true },
        new FileSettings { useFile = false, filePath = "Assets/HandTrackingData/PointCloudData/currentGlobalVerticesTop.txt",    color = Color.yellow, useFileColor = true }
    };

    public float pointSize = 0.01f;
    public GameObject outline;
    public Color outlineColor = Color.white;


    private PointCloudSource lastRenderingSource;
    private FileSettings[] lastFileSettings;
    private float lastPointSize;
    private GameObject lastOutline;
    private Color lastOutlineColor;


    private void Awake()
    {
        SaveInspectorState();
    }

    public void SaveInspectorState()
    {
        lastRenderingSource = renderingSource;

        lastFileSettings = new FileSettings[fileSettings.Length];
        for (int i = 0; i < fileSettings.Length; i++)
        {
            lastFileSettings[i] = fileSettings[i];
        }
        lastPointSize = pointSize;
        lastOutline = outline;
        lastOutlineColor = outlineColor;
    }

    public bool HasRenderingSourceChanged()
    {
        return renderingSource != lastRenderingSource;
    }

    public bool HasFileSettingsChanged()
    {
        if (lastFileSettings == null || lastFileSettings.Length != fileSettings.Length) return true;

        for (int i = 0; i < fileSettings.Length; i++)
        {
            if (fileSettings[i].IsDifferent(lastFileSettings[i]))
            {
                return true;
            }
        }
        return false;
    }

    public bool HasRenderingSettingsChanged()
    {
        return pointSize != lastPointSize || outlineColor != lastOutlineColor || outline != lastOutline;
    }


}
