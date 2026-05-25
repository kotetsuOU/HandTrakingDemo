using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class SoftBodyDeform : MonoBehaviour
{
    [Header("変形設定")]
    public float deformRadius = 0.3f;    // 影響範囲
    public float deformStrength = 0.5f;  // へこみの強さ
    public float restoreSpeed = 2f;      // 元に戻る速さ

    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] currentVertices;

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        originalVertices = mesh.vertices;
        currentVertices = mesh.vertices.Clone() as Vector3[];
    }

    void Update()
    {
        // 毎フレーム元の形に戻していく
        for (int i = 0; i < currentVertices.Length; i++)
        {
            currentVertices[i] = Vector3.Lerp(currentVertices[i], originalVertices[i], Time.deltaTime * restoreSpeed);
        }

        mesh.vertices = currentVertices;
        mesh.RecalculateNormals();
    }

    // 外から呼ぶ変形関数
    public void Deform(Vector3 worldPoint, float force)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);

        for (int i = 0; i < currentVertices.Length; i++)
        {
            float dist = Vector3.Distance(originalVertices[i], localPoint);
            if (dist < deformRadius)
            {
                float falloff = 1f - (dist / deformRadius);
                Vector3 inwardNormal = -originalVertices[i].normalized;
                currentVertices[i] += inwardNormal * force * falloff * deformStrength;

                // 元の位置からの最大変位をdeformRadiusの半分でクランプ（蓄積防止）
                Vector3 displacement = currentVertices[i] - originalVertices[i];
                float maxDepth = deformRadius * 0.5f;
                if (displacement.magnitude > maxDepth)
                    currentVertices[i] = originalVertices[i] + displacement.normalized * maxDepth;
            }
        }

        mesh.vertices = currentVertices;
        mesh.RecalculateNormals();
    }
}