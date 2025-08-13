using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraAdjuster : MonoBehaviour
{
    [SerializeField]
    private Transform displayTransform; // �f�B�X�v���C��Transform���w�肷�邽�߂̕ϐ�

    private float calculatedAspectRatio;

    private Vector3 cameraPosition; // �J�����̈ʒu���i�[����ϐ�

    private Vector2 displayHorizontalLeft;   // �f�B�X�v���C�̐��������̍��[�̓_(x,z)
    private Vector2 displayHorizontalRight;  // �f�B�X�v���C�̐��������̉E�[�̓_(x,z)
    private Vector2 displayVerticalTop;      // �f�B�X�v���C�̐��������̏�[�̓_(y,z)
    private Vector2 displayVerticalBottom;   // �f�B�X�v���C�̐��������̉��[�̓_(y,z)

    private float HorizontalViewAngle; // ���������̎���p
    private float VerticalViewAngle; // ���������̎���p

    // Start is called before the first frame update
    void Start()
    {
        if (displayTransform == null)
        {
            UnityEngine.Debug.LogError("displayTransform is not assigned in the Inspector.");
            return;
        }

        cameraPosition = transform.position;
        CalcDisplayPosition();
        // �v�Z���ʂ���U Euler �� float �l�Ƃ��Ď󂯎��
        float horizontalEuler = CalcHorizontalValue();
        float verticalEuler = CalcVerticalValue();
        // Z���͌Œ� (��: 0) �Ƃ��� Quaternion �𐶐�
        transform.rotation = Quaternion.Euler(verticalEuler, horizontalEuler, 0);

        calculatedAspectRatio = displayTransform.localScale.z / displayTransform.localScale.x;

        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            //cam.aspect = calculatedAspectRatio;

            //cam.fieldOfView = VerticalViewAngle;
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    void CalcDisplayPosition()
    {
        displayHorizontalLeft = new Vector2(displayTransform.position.x, displayTransform.position.z)
            + new Vector2(-displayTransform.localScale.x / 2, -displayTransform.localScale.z / 2);
        displayHorizontalRight = new Vector2(displayTransform.position.x, displayTransform.position.z)
            + new Vector2(displayTransform.localScale.x / 2, -displayTransform.localScale.z / 2);
        displayVerticalBottom = new Vector2(displayTransform.position.y, displayTransform.position.z)
            + new Vector2(0, -displayTransform.localScale.z / 2);
        displayVerticalTop = new Vector2(displayTransform.position.y, displayTransform.position.z)
            + new Vector2(0, displayTransform.localScale.z / 2);
    }

    // ���������̌v�Z�B�Ԃ�l�� Y ����]�p
    float CalcHorizontalValue()
    {
        float angleA, angleB;
        float x, z;

        // �E���̊p�x���v�Z
        x = displayHorizontalRight.x - cameraPosition.x;
        z = displayHorizontalRight.y - cameraPosition.z;
        angleA = Mathf.Atan(x / z) * Mathf.Rad2Deg;

        // �����̊p�x���v�Z
        x = displayHorizontalLeft.x - cameraPosition.x;
        z = displayHorizontalLeft.y - cameraPosition.z;
        angleB = Mathf.Atan(x / z) * Mathf.Rad2Deg;

        HorizontalViewAngle = angleA - angleB;
        // ���������̉�]�͍��E�̊p�x�̕���
        return (angleA + angleB) / 2;
    }

    // ���������̌v�Z�B�Ԃ�l�� X ����]�p
    float CalcVerticalValue()
    {
        float angleA, angleB;
        float yDiffTop, yDiffBottom, zTop, zBottom;

        // ��[�̊p�x�v�Z
        yDiffTop = cameraPosition.y - displayVerticalTop.x;
        zTop = displayVerticalTop.y - cameraPosition.z;
        angleA = Mathf.Atan(yDiffTop / zTop) * Mathf.Rad2Deg;

        // ���[�̊p�x�v�Z
        yDiffBottom = cameraPosition.y - displayVerticalBottom.x;
        zBottom = displayVerticalBottom.y - cameraPosition.z;
        angleB = Mathf.Atan(yDiffBottom / zBottom) * Mathf.Rad2Deg;

        VerticalViewAngle = angleA - angleB;
        // ���������̉�]�͏㉺�̊p�x�̕���
        return (angleA + angleB) / 2;
    }
}