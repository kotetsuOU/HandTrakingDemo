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
        // 毎フレーム元の形に戻していく（メッシュ更新は LateUpdate でまとめて行う）
        for (int i = 0; i < currentVertices.Length; i++)
            currentVertices[i] = Vector3.Lerp(currentVertices[i], originalVertices[i], Time.deltaTime * restoreSpeed);
    }

    void LateUpdate()
    {
        // Update と Deform が両方終わった後にまとめて 1 回だけメッシュを更新する
        // これにより同一フレームに複数の Deform() を呼んでも正しく蓄積される
        mesh.vertices = currentVertices;
        mesh.RecalculateNormals();
    }

    // 外から呼ぶ変形関数（複数方向から呼ばれることを想定。メッシュ更新は LateUpdate に委ねる）
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

                // 元の位置からの最大変位をクランプ（蓄積防止）
                Vector3 displacement = currentVertices[i] - originalVertices[i];
                float maxDepth = deformRadius * 0.5f;
                if (displacement.magnitude > maxDepth)
                    currentVertices[i] = originalVertices[i] + displacement.normalized * maxDepth;
            }
        }
        // メッシュ更新は LateUpdate で行うためここでは呼ばない
    }
}
