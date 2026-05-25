using UnityEngine;

/// <summary>
/// テスト用。このスクリプトをSoftBodyDeformと同じSphereにアタッチする。
/// IsTrigger付きColliderを持つオブジェクトが触れると Deform() が呼ばれる。
/// </summary>
[RequireComponent(typeof(SoftBodyDeform))]
public class DeformTester : MonoBehaviour
{
    public float deformForce = 0.05f;

    private SoftBodyDeform _softBodyDeform;

    private void Start()
    {
        _softBodyDeform = GetComponent<SoftBodyDeform>();
    }

    private void OnTriggerStay(Collider other)
    {
        Vector3 contactPoint = other.ClosestPoint(transform.position);
        _softBodyDeform.Deform(contactPoint, deformForce);
    }
}
