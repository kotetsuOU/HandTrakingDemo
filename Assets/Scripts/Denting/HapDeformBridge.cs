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
        if (hapCollision == null) return;
        if (!hapCollision.IsColliding) return;

        _softBodyDeform.Deform(hapCollision.HitPosition, deformForce);
    }
}
