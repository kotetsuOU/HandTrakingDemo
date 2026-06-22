using UnityEngine;
using System.Collections.Generic;

public enum PointCloudColorMode
{
    Skin,
    Black,
    Blue,
    Custom
}

public class RsMaterialController : MonoBehaviour
{
    [Header("Material Settings")]
    [Tooltip("適用するマテリアル")]
    public Material material;

    [Header("Color Settings")]
    [Tooltip("点群の色のモード選択")]
    [HideInInspector]
    public PointCloudColorMode colorMode = PointCloudColorMode.Skin;

    private List<MeshRenderer> _cachedMeshRenderers = new List<MeshRenderer>();

    private Dictionary<RsPointCloudRenderer, Color> _initialColors = new Dictionary<RsPointCloudRenderer, Color>();

    private readonly Color _skinColor = new Color(241f / 255f, 187f / 255f, 147f / 255f, 1f);
    private readonly Color _blackColor = Color.black;
    private readonly Color _blueColor = Color.blue;

    private RsGlobalPointCloudManager _globalManager;

    private RsGlobalPointCloudManager GlobalManager
    {
        get
        {
            if (_globalManager == null)
            {
                _globalManager = RsGlobalPointCloudManager.Instance;
                if (_globalManager == null)
                {
                    _globalManager = GetComponent<RsGlobalPointCloudManager>();
                }
            }
            return _globalManager;
        }
    }

    void Start()
    {
        InitializeRenderers();
        ApplyMaterial();
        ApplyColorMode();
    }

    private void InitializeRenderers()
    {
        _cachedMeshRenderers.Clear();
        _initialColors.Clear();

        if (GlobalManager == null) return;

        foreach (var pcRenderer in GlobalManager.GetChildRenderers())
        {
            if (pcRenderer != null)
            {
                var meshRenderer = pcRenderer.GetComponent<MeshRenderer>();

                if (meshRenderer != null)
                {
                    _cachedMeshRenderers.Add(meshRenderer);
                }
                else
                {
                    Debug.LogWarning($"[RsMaterialController] {pcRenderer.name} に MeshRenderer が見つかりません。", pcRenderer);
                }

                if (!_initialColors.ContainsKey(pcRenderer))
                {
                    _initialColors.Add(pcRenderer, pcRenderer.pointCloudColor);
                }
            }
        }
    }

    public void ApplyMaterial()
    {
        if (_cachedMeshRenderers.Count == 0)
        {
            return;
        }

        foreach (var renderer in _cachedMeshRenderers)
        {
            if (renderer != null && material != null)
            {
                renderer.material = material;
            }
        }
    }

    public void ChangeColorMode(PointCloudColorMode mode)
    {
        this.colorMode = mode;
        ApplyColorMode();
    }

    public void ApplyColorMode()
    {
        if (GlobalManager == null) return;

        foreach (var pRenderer in GlobalManager.GetChildRenderers())
        {
            if (pRenderer == null) continue;

            Color targetColor = Color.white;
            bool applyColor = true;

            switch (colorMode)
            {
                case PointCloudColorMode.Skin:
                    targetColor = _skinColor;
                    break;
                case PointCloudColorMode.Black:
                    targetColor = _blackColor;
                    break;
                case PointCloudColorMode.Blue:
                    targetColor = _blueColor;
                    break;
                case PointCloudColorMode.Custom:
                    if (_initialColors.TryGetValue(pRenderer, out Color originalColor))
                    {
                        targetColor = originalColor;
                    }
                    else
                    {
                        applyColor = false;
                    }
                    break;
                default:
                    applyColor = false;
                    break;
            }

            if (applyColor)
            {
                pRenderer.pointCloudColor = targetColor;
            }
        }
    }

}
