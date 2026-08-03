using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Core.Logging;

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
    private Coroutine _registerCoroutine;

    private void OnEnable()
    {
        RegisterAllMeshes();
    }

    /// <summary>
    /// 設定に基づき、対象メッシュを収集して PCDRendererFeature に一括登録します。
    /// </summary>
    public void RegisterAllMeshes()
    {
        if (_registerCoroutine != null)
        {
            StopCoroutine(_registerCoroutine);
            _registerCoroutine = null;
        }

        if (PCDRendererFeature.Instance == null)
        {
            _registerCoroutine = StartCoroutine(RegisterWhenReady());
        }
        else
        {
            CollectAndRegisterMeshes();
        }
    }

    private IEnumerator RegisterWhenReady()
    {
        while (PCDRendererFeature.Instance == null)
        {
            yield return null;
        }

        _registerCoroutine = null;
        CollectAndRegisterMeshes();
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
                    if (mf != null && MatchesLayer(mf.gameObject, root))
                    {
                        ProcessAndAddMesh(mf.gameObject, mf, null);
                    }
                }

                var smrArray = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var smr in smrArray)
                {
                    if (smr != null && MatchesLayer(smr.gameObject, root))
                    {
                        ProcessAndAddMesh(smr.gameObject, null, smr);
                    }
                }
            }
            else
            {
                if (MatchesLayer(root, root))
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
        if (PCDRendererFeature.Instance != null)
        {
            SyncActiveStatesToFeature(forceMarkDirty: true);
            _isRegistered = true;
        }
        else
        {
            _isRegistered = false;
        }
    }

    private bool MatchesLayer(GameObject go, GameObject root = null)
    {
        if (go == null) return false;

        Transform current = go.transform;
        while (current != null)
        {
            int layer = current.gameObject.layer;
            int pcdLayerVal = pcdLayerMask.value;
            int uiLayerVal = uiLayerMask.value;

            // LayerMask 判定
            bool isPCD = (pcdLayerVal & (1 << layer)) != 0;
            bool isUI = (uiLayerVal & (1 << layer)) != 0;

            // レイヤー名判定（レイヤー名が "PCD" または "UI" である場合のサポート）
            string layerName = LayerMask.LayerToName(layer);
            if (layerName == "PCD") isPCD = true;
            if (layerName == "UI") isUI = true;

            bool isMatch = (layerSelectionMode == LayerSelectionMode.PCDOnly) ? (isPCD && !isUI) : (isPCD || isUI);
            if (isMatch) return true;

            if (root != null && current == root.transform) break;
            current = current.parent;
        }

        return false;
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
        Mesh bakedMesh = null;
        Renderer rend = null;

        if (smr != null)
        {
            bakedMesh = new Mesh();
            bakedMesh.name = smr.name + "_Baked";
            smr.BakeMesh(bakedMesh);
            targetMesh = bakedMesh;
            rend = smr;
        }
        else if (mf != null)
        {
            if (go.GetComponent<SkinnedMeshRenderer>() != null) return;
            targetMesh = mf.sharedMesh;
            rend = mf.GetComponent<Renderer>();
        }

        if (targetMesh == null) return;

        // 重複チェック
        if (_trackedItems.Exists(x => x.ownerObject == go && (x.targetMesh == targetMesh || (smr != null && x.skinnedMeshRenderer == smr)))) return;

        _trackedItems.Add(new TrackedMeshData
        {
            ownerObject = go,
            renderer = rend,
            meshFilter = mf,
            skinnedMeshRenderer = smr,
            targetMesh = targetMesh,
            bakedMesh = bakedMesh,
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
            AppLogger.Log(PCD_LogTriggers.TagBuffer, $"Active state synced. Registered active meshes: {registeredCount} / {_trackedItems.Count}");
        }
    }

    private void OnDisable()
    {
        if (_registerCoroutine != null)
        {
            StopCoroutine(_registerCoroutine);
            _registerCoroutine = null;
        }
        UnregisterAllMeshes();
    }

    /// <summary>
    /// 登録済みの全メッシュの登録を解除します。
    /// </summary>
    public void UnregisterAllMeshes()
    {
        if (PCDRendererFeature.Instance != null)
        {
            foreach (var item in _trackedItems)
            {
                if (item.isCurrentlyRegistered && item.targetMesh != null)
                {
                    Transform t = item.ownerObject != null ? item.ownerObject.transform : null;
                    PCDRendererFeature.Instance.RemoveStaticMesh(item.targetMesh, t);
                }
                if (item.bakedMesh != null)
                {
                    Destroy(item.bakedMesh);
                    item.bakedMesh = null;
                }
                item.isCurrentlyRegistered = false;
            }
            AppLogger.Log(PCD_LogTriggers.TagBuffer, $"Unregistered {_trackedItems.Count} meshes.");
        }
        _trackedItems.Clear();
        _isRegistered = false;
    }

    private void Update()
    {
        if (!_isRegistered || PCDRendererFeature.Instance == null) return;

        // 1. アクティブ/非アクティブ状態の変動をチェックしてリアルタイム同期
        SyncActiveStatesToFeature();

        // 2. SkinnedMeshRenderer や Dynamic モード時の位置更新を反映
        bool isTransformDirty = false;

        foreach (var item in _trackedItems)
        {
            if (!item.isCurrentlyRegistered || item.ownerObject == null) continue;

            if (item.skinnedMeshRenderer != null && item.bakedMesh != null)
            {
                item.skinnedMeshRenderer.BakeMesh(item.bakedMesh);
                isTransformDirty = true;
            }

            if (isDynamic)
            {
                Transform t = item.ownerObject.transform;
                if (t.position != item.lastPosition || t.rotation != item.lastRotation || t.localScale != item.lastScale)
                {
                    isTransformDirty = true;
                    item.lastPosition = t.position;
                    item.lastRotation = t.rotation;
                    item.lastScale = t.localScale;
                }
            }
        }

        if (isTransformDirty)
        {
            PCDRendererFeature.Instance.MarkPointCloudDataDirty();
        }
    }
}
