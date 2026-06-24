using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[Serializable]
public struct HAP_AUTDTransformSnapshot
{
    public int id;
    public Vector3 localPosition;
    public Quaternion localRotation;
}

[CreateAssetMenu(fileName = "AUTDTransformCatalog", menuName = "Haptics/AUTD Transform Catalog")]
public class HAP_AUTDTransformCatalog : ScriptableObject
{
    public List<HAP_AUTDTransformSnapshot> snapshots = new List<HAP_AUTDTransformSnapshot>();
}

[DisallowMultipleComponent]
public class HAP_AUTDTransformLoader : MonoBehaviour
{
    [Header("Configuration")]
    public HAP_AUTDTransformCatalog catalog;
    public GameObject devicePrefab;
    public Transform deviceRoot;
    public int prefabCount = 20;

    public void Save()
    {
        if (catalog == null)
        {
            Debug.LogWarning("[HAP_AUTDTransformLoader] Catalog ScriptableObject is missing!");
            return;
        }

        var devices = ResolveDeviceRoot().GetComponentsInChildren<AUTD3Device>(false);
        var deviceById = new Dictionary<int, AUTD3Device>();

        foreach (var dev in devices)
        {
            if (deviceById.ContainsKey(dev.ID))
            {
                Debug.LogWarning($"[HAP_AUTDTransformLoader] Duplicate AUTD3Device ID {dev.ID} found. Skipping duplicate.");
                continue;
            }
            deviceById[dev.ID] = dev;
        }

        catalog.snapshots.Clear();
        foreach (var dev in deviceById.Values.OrderBy(d => d.ID))
        {
            catalog.snapshots.Add(new HAP_AUTDTransformSnapshot
            {
                id = dev.ID,
                localPosition = dev.transform.localPosition,
                localRotation = dev.transform.localRotation
            });
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
#endif
        Debug.Log($"[HAP_AUTDTransformLoader] Saved {catalog.snapshots.Count} AUTD transform snapshots.");
    }

    public void Load()
    {
        if (catalog == null)
        {
            Debug.LogWarning("[HAP_AUTDTransformLoader] Catalog ScriptableObject is missing!");
            return;
        }

        var devices = ResolveDeviceRoot().GetComponentsInChildren<AUTD3Device>(false);
        var deviceById = new Dictionary<int, AUTD3Device>();

        foreach (var dev in devices)
        {
            if (!deviceById.ContainsKey(dev.ID))
                deviceById.Add(dev.ID, dev);
        }

        var snapshotById = catalog.snapshots.ToDictionary(s => s.id, s => s);

        // Generate missing devices based on catalog
        foreach (var id in snapshotById.Keys.OrderBy(id => id))
        {
            if (!deviceById.ContainsKey(id))
            {
                var newDevice = CreateDevice(id, true);
                if (newDevice != null) deviceById.Add(id, newDevice);
            }
        }

        // Apply transformations
        foreach (var snapshot in snapshotById.Values.OrderBy(s => s.id))
        {
            if (deviceById.TryGetValue(snapshot.id, out var dev))
            {
                dev.transform.localPosition = snapshot.localPosition;
                dev.transform.localRotation = snapshot.localRotation;
            }
        }

        Debug.Log($"[HAP_AUTDTransformLoader] Loaded {snapshotById.Count} AUTD transform snapshots.");
    }

    public void GeneratePrefabs()
    {
        if (prefabCount < 0) return;
        if (devicePrefab == null)
        {
            Debug.LogWarning("[HAP_AUTDTransformLoader] Device Prefab is missing!");
            return;
        }

        var devices = ResolveDeviceRoot().GetComponentsInChildren<AUTD3Device>(false);
        var existingIds = new HashSet<int>(devices.Select(d => d.ID));

        int generatedCount = 0;
        for (int i = 0; i < prefabCount; i++)
        {
            if (!existingIds.Contains(i))
            {
                CreateDevice(i, true);
                generatedCount++;
            }
        }

        Debug.Log($"[HAP_AUTDTransformLoader] Generated {generatedCount} AUTD prefab(s).");
    }

    private AUTD3Device CreateDevice(int id, bool recordUndo)
    {
        if (devicePrefab == null) return null;

        GameObject instance = null;

#if UNITY_EDITOR
        if (!Application.isPlaying && PrefabUtility.IsPartOfPrefabAsset(devicePrefab))
        {
            var prefabObj = PrefabUtility.InstantiatePrefab(devicePrefab) as GameObject;
            if (prefabObj != null)
            {
                if (recordUndo) Undo.RegisterCreatedObjectUndo(prefabObj, "Generate AUTD Prefabs");
                instance = prefabObj;
            }
        }
#endif

        if (instance == null)
        {
            instance = Instantiate(devicePrefab);
#if UNITY_EDITOR
            if (recordUndo && !Application.isPlaying) Undo.RegisterCreatedObjectUndo(instance, "Generate AUTD Prefabs");
#endif
        }

        instance.transform.SetParent(ResolveDeviceRoot(), false);
        instance.name = $"Autd{id}";

        var device = instance.GetComponent<AUTD3Device>();
        if (device == null)
        {
#if UNITY_EDITOR
            if (recordUndo && !Application.isPlaying) device = Undo.AddComponent<AUTD3Device>(instance);
            else
#endif
                device = instance.AddComponent<AUTD3Device>();
        }

        device.ID = id;

#if UNITY_EDITOR
        EditorUtility.SetDirty(device);
        EditorUtility.SetDirty(instance);
#endif

        return device;
    }

    private Transform ResolveDeviceRoot()
    {
        return deviceRoot != null ? deviceRoot : transform;
    }
}
