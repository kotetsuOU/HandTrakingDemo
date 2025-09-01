using UnityEngine;

public class CameraAdjuster : MonoBehaviour
{
    public Transform displayTransform;

    private void LateUpdate()
    {
        if (displayTransform == null)
        {
            return;
        }

        Camera cam = GetComponent<Camera>();
        if (cam == null)
        {
            return;
        }

        // 1. ディスプレイの四隅をワールド座標で取得
        Vector3 displayCenter = displayTransform.position;
        Vector3 displayRight = Vector3.right * displayTransform.localScale.x / 2;
        Vector3 displayUp = Vector3.forward * displayTransform.localScale.z / 2;

        Vector3 bl = displayCenter - displayRight - displayUp; // Bottom-Left
        Vector3 br = displayCenter + displayRight - displayUp; // Bottom-Right
        Vector3 tl = displayCenter - displayRight + displayUp; // Top-Left

        // 2. ディスプレイの四隅をカメラのローカル座標系に変換
        // カメラのローカル座標は、カメラからディスプレイを見た相対的な位置を定義
        Matrix4x4 cameraTransform = cam.worldToCameraMatrix;
        bl = cameraTransform.MultiplyPoint(bl);
        br = cameraTransform.MultiplyPoint(br);
        tl = cameraTransform.MultiplyPoint(tl);

        // 3. 投影行列のパラメータを計算
        //float nearPlane = Mathf.Abs(bl.z);
        float nearPlane = 0.1f;
        float right = br.x * (nearPlane / -br.z);
        float left = bl.x * (nearPlane / -bl.z);
        float top = tl.y * (nearPlane / -tl.z);
        float bottom = bl.y * (nearPlane / -bl.z);

        // 4. オフアクシス投影行列を構築
        Matrix4x4 p = Matrix4x4.Frustum(left, right, bottom, top, nearPlane, 1);

        // 5. カメラに投影行列を適用
        cam.projectionMatrix = p;
    }
}