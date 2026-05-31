using UnityEngine;

[RequireComponent(typeof(SoftBodyDeform))]
public class HapDeformBridge : MonoBehaviour
{
    [Tooltip("Sphereにアタッチされた衝突判定コンポーネント")]
    public HapCollisionDetectors hapCollision;

    [Tooltip("へこみの力（HapCollisionDetectorsのforceとして渡す値）")]
    public float deformForce = 1f;

    private SoftBodyDeform _softBodyDeform;

    private void Start()
    {
        _softBodyDeform = GetComponent<SoftBodyDeform>();
    }

    private void Update()
    {
        if (hapCollision == null || !hapCollision.IsColliding) return;

        // 検出された全接触点（最大8方向）それぞれに対してへこみを適用する
        // メッシュ更新は SoftBodyDeform.LateUpdate でまとめて1回行われる
        foreach (var pos in hapCollision.HitPositions)
            _softBodyDeform.Deform(pos, deformForce);
    }
}
