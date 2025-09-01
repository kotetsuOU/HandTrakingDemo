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

        // 1. �f�B�X�v���C�̎l�������[���h���W�Ŏ擾
        Vector3 displayCenter = displayTransform.position;
        Vector3 displayRight = Vector3.right * displayTransform.localScale.x / 2;
        Vector3 displayUp = Vector3.forward * displayTransform.localScale.z / 2;

        Vector3 bl = displayCenter - displayRight - displayUp; // Bottom-Left
        Vector3 br = displayCenter + displayRight - displayUp; // Bottom-Right
        Vector3 tl = displayCenter - displayRight + displayUp; // Top-Left

        // 2. �f�B�X�v���C�̎l�����J�����̃��[�J�����W�n�ɕϊ�
        // �J�����̃��[�J�����W�́A�J��������f�B�X�v���C���������ΓI�Ȉʒu���`
        Matrix4x4 cameraTransform = cam.worldToCameraMatrix;
        bl = cameraTransform.MultiplyPoint(bl);
        br = cameraTransform.MultiplyPoint(br);
        tl = cameraTransform.MultiplyPoint(tl);

        // 3. ���e�s��̃p�����[�^���v�Z
        //float nearPlane = Mathf.Abs(bl.z);
        float nearPlane = 0.1f;
        float right = br.x * (nearPlane / -br.z);
        float left = bl.x * (nearPlane / -bl.z);
        float top = tl.y * (nearPlane / -tl.z);
        float bottom = bl.y * (nearPlane / -bl.z);

        // 4. �I�t�A�N�V�X���e�s����\�z
        Matrix4x4 p = Matrix4x4.Frustum(left, right, bottom, top, nearPlane, 1);

        // 5. �J�����ɓ��e�s���K�p
        cam.projectionMatrix = p;
    }
}