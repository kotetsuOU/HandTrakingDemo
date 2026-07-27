using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PCD（Point Cloud Display）オクルージョンパイプラインに、
/// 複数のGameObject配下のメッシュを一括で登録・管理・更新するコントローラー。
/// GameObjectやRendererのアアクティブ/非アクティブ状態の変更をリアルタイムに反映します。
/// </summary>
public class PCDMeshRegistrarController : MonoBehaviour
{
    public enum LayerSelectionMode
    {
        PCDOnly,   // PCDレイヤーに所属するオブジェクトのみ登録
        PCDAndUI   // PCDレイヤーとUIレイヤーに所属するオブジェクトの両方を登録
    }

    [Header("Target Settings")]
    [Tooltip("登録対象のGameObjectリスト（各オブジェクトおよび配下の MeshFilter / SkinnedMeshRenderer が登録されます）")]
    public List<GameObject> targetObjects = new List<GameObject>();

    [Tooltip("このコンポーネントがアタッチされている自身も含めるか")]
    public bool includeSelf = true;

    [Tooltip("指定されたGameObjectの子オブジェクト（階層下）のメッシュも収集するか")]
    public bool includeChildren = true;

    [Header("Layer Selection")]
    [Tooltip("PCDのみ（PCDOnly）、または PCD+UI（PCDAndUI）で登録対象を選択")]
    public LayerSelectionMode layerSelectionMode = LayerSelectionMode.PCDAndUI;

    [Tooltip("PCD用オブジェクトとして扱うLayerMask")]
    public LayerMask pcdLayerMask = ~0;

    [Tooltip("UI用オブジェクトとして扱うLayerMask")]
    public LayerMask uiLayerMask = ~0;

    [Header("Dynamic Options")]
    [Tooltip("有効にすると、毎フレームTransformの更新を検知して点群データを再構築します")]
    public bool isDynamic = false;

    // 内部で追跡する登録単位
    private class TrackedMeshData
    {
        public GameObject ownerObject;
        public Renderer renderer;
        public MeshFilter meshFilter;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public Mesh targetMesh;
        public Mesh bakedMesh;

        public bool isCurrentlyRegistered = false;

        public Vector3 lastPosition;
        public Quaternion lastRotation;
        public Vector3 lastScale;
    }

    private readonly List<TrackedMeshData> _trackedItems = new List<TrackedMeshData>();
    private bool _isRegistered = false;

    private void OnEnable()
    {
        RegisterAllMeshes();
    }

    /// <summary>
    /// 設定に基づき、対象メッシュを収集して PCDRendererFeature に一括登録します。
    /// </summary>
    public void RegisterAllMeshes()
    {
        CollectAndRegisterMeshes();

        if (PCDRendererFeature.Instance == null)
        {
            StartCoroutine(RegisterWhenReady());
        }
    }

    private IEnumerator RegisterWhenReady()
    {
        while (PCDRendererFeature.Instance == null)
        {
            yield return null;
        }

        if (!_isRegistered)
        {
            CollectAndRegisterMeshes();
        }
    }

    private void CollectAndRegisterMeshes()
    {
        // 既存登録を一旦解除
        UnregisterAllMeshes();

        // 収集対象の Root GameObject リストを生成
        List<GameObject> rootList = new List<GameObject>();
        if (includeSelf && gameObject != null)
        {
            rootList.Add(gameObject);
        }
        if (targetObjects != null)
        {
            foreach (var go in targetObjects)
            {
                if (go != null && !rootList.Contains(go))
                {
                    rootList.Add(go);
                }
            }
        }

        // 各 Root から MeshFilter および SkinnedMeshRenderer を収集 (状態変化を追跡できるよう登録候補を保持)
        foreach (var root in rootList)
        {
            if (root == null) continue;

            if (includeChildren)
            {
                var mfArray = root.GetComponentsInChildren<MeshFilter>(true);
                foreach (var mf in mfArray)
                {
                    if (mf != null && MatchesLayer(mf.gameObject))
                    {
                        ProcessAndAddMesh(mf.gameObject, mf, null);
                    }
                }

                var smrArray = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in smrArray)
                {
                    if (smr != null && MatchesLayer(smr.gameObject))
                    {
                        ProcessAndAddMesh(smr.gameObject, null, smr);
                    }
                }
            }
            else
            {
                if (MatchesLayer(root))
                {
                    var mf = root.GetComponent<MeshFilter>();
                    var smr = root.GetComponent<SkinnedMeshRenderer>();
                    if (mf != null || smr != null)
                    {
                        ProcessAndAddMesh(root, mf, smr);
                    }
                }
            }
        }

        // アクティブなメッシュのみ PCDRendererFeature に登録
        SyncActiveStatesToFeature(forceMarkDirty: true);
        _isRegistered = true;
    }

    private bool MatchesLayer(GameObject go)
    {
        if (go == null) return false;

        int layer = go.layer;
        int pcdLayerVal = pcdLayerMask.value;
        int uiLayerVal = uiLayerMask.value;

        // LayerMask 判定
        bool isPCD = (pcdLayerVal & (1 << layer)) != 0;
        bool isUI = (uiLayerVal & (1 << layer)) != 0;

        // レイヤー名判定（レイヤー名が "PCD" または "UI" である場合のサポート）
        string layerName = LayerMask.LayerToName(layer);
        if (layerName == "PCD") isPCD = true;
        if (layerName == "UI") isUI = true;

        if (layerSelectionMode == LayerSelectionMode.PCDOnly)
        {
            return isPCD && !isUI;
        }
        else // PCDAndUI
        {
            return isPCD || isUI;
        }
    }

    private bool IsActiveAndEnabled(TrackedMeshData item)
    {
        if (item.ownerObject == null || !item.ownerObject.activeInHierarchy) return false;
        if (item.renderer != null && !item.renderer.enabled) return false;
        return true;
    }

    private void ProcessAndAddMesh(GameObject go, MeshFilter mf, SkinnedMeshRenderer smr)
    {
        Mesh targetMesh = null;
        Renderer rend = null;

        if (mf != null)
        {
            targetMesh = mf.sharedMesh;
            rend = mf.GetComponent<Renderer>();
        }
        else if (smr != null)
        {
            targetMesh = smr.sharedMesh;
            rend = smr;
        }

        if (targetMesh == null) return;

        // 重複チェック
        if (_trackedItems.Exists(x => x.ownerObject == go && x.targetMesh == targetMesh)) return;

        _trackedItems.Add(new TrackedMeshData
        {
            ownerObject = go,
            renderer = rend,
            meshFilter = mf,
            skinnedMeshRenderer = smr,
            targetMesh = targetMesh,
            bakedMesh = null,
            isCurrentlyRegistered = false
        });
    }

    private void SyncActiveStatesToFeature(bool forceMarkDirty = false)
    {
        if (PCDRendererFeature.Instance == null) return;

        bool stateChanged = false;
        int registeredCount = 0;

        foreach (var item in _trackedItems)
        {
            if (item.targetMesh == null || item.ownerObject == null) continue;

            bool shouldBeActive = IsActiveAndEnabled(item);

            if (shouldBeActive && !item.isCurrentlyRegistered)
            {
                PCDRendererFeature.Instance.AddStaticMesh(item.targetMesh, item.ownerObject.transform);
                item.isCurrentlyRegistered = true;
                item.lastPosition = item.ownerObject.transform.position;
                item.lastRotation = item.ownerObject.transform.rotation;
                item.lastScale = item.ownerObject.transform.localScale;
                stateChanged = true;
            }
            else if (!shouldBeActive && item.isCurrentlyRegistered)
            {
                PCDRendererFeature.Instance.RemoveStaticMesh(item.targetMesh, item.ownerObject.transform);
                item.isCurrentlyRegistered = false;
                stateChanged = true;
            }

            if (item.isCurrentlyRegistered)
            {
                registeredCount++;
            }
        }

        if ((stateChanged || forceMarkDirty) && PCDRendererFeature.Instance != null)
        {
            PCDRendererFeature.Instance.MarkPointCloudDataDirty();
            Debug.Log($"[PCDMeshRegistrarController] Active state synced. Registered active meshes: {registeredCount} / {_trackedItems.Count}");
        }
    }

    private void OnDisable()
    {
        UnregisterAllMeshes();
    }

    /// <summary>
    /// 登録済みの全メッシュの登録を解除します。
    /// </summary>
    public void UnregisterAllMeshes()
    {
        if (_isRegistered && PCDRendererFeature.Instance != null)
        {
            foreach (var item in _trackedItems)
            {
                if (item.isCurrentlyRegistered && item.targetMesh != null && item.ownerObject != null)
                {
                    PCDRendererFeature.Instance.RemoveStaticMesh(item.targetMesh, item.ownerObject.transform);
                }
                if (item.bakedMesh != null)
                {
                    Destroy(item.bakedMesh);
                    item.bakedMesh = null;
                }
                item.isCurrentlyRegistered = false;
            }
            Debug.Log($"[PCDMeshRegistrarController] Unregistered {_trackedItems.Count} meshes.");
        }
        _trackedItems.Clear();
        _isRegistered = false;
    }

    private void Update()
    {
        if (!_isRegistered || PCDRendererFeature.Instance == null) return;

        // 1. アクティブ/非アクティブ状態の変動をチェックしてリアルタイム同期
        SyncActiveStatesToFeature();

        // 2. Dynamic モードが有効な場合、Transform / SkinMesh の変更を検知して更新
        if (!isDynamic) return;

        bool isTransformDirty = false;

        foreach (var item in _trackedItems)
        {
            if (!item.isCurrentlyRegistered || item.ownerObject == null) continue;

            Transform t = item.ownerObject.transform;
            if (t.position != item.lastPosition || t.rotation != item.lastRotation || t.localScale != item.lastScale)
            {
                isTransformDirty = true;
                item.lastPosition = t.position;
                item.lastRotation = t.rotation;
                item.lastScale = t.localScale;
            }

            if (item.skinnedMeshRenderer != null && item.bakedMesh != null)
            {
                item.skinnedMeshRenderer.BakeMesh(item.bakedMesh);
                isTransformDirty = true;
            }
        }

        if (isTransformDirty)
        {
            PCDRendererFeature.Instance.MarkPointCloudDataDirty();
        }
    }
}
