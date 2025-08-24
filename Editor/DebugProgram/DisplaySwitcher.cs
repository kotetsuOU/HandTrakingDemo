using UnityEngine;

public class DisplaySwitcher : MonoBehaviour
{
    void Start()
    {
        // �ŏ��̃f�B�X�v���C�ȊO��L��������
        if (Display.displays.Length > 1)
        {
            Display.displays[1].Activate();
        }
    }

    void Update()
    {
        // Space�L�[�ŃJ�����̕\�����؂�ւ���
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // ���݂̃J�������擾
            Camera mainCamera = Camera.main;

            // ���݂̃^�[�Q�b�g�f�B�X�v���C���擾
            int currentTargetDisplay = mainCamera.targetDisplay;

            // ���̃^�[�Q�b�g�f�B�X�v���C���v�Z
            int nextTargetDisplay = (currentTargetDisplay + 1) % Display.displays.Length;

            // �J�����̃^�[�Q�b�g�f�B�X�v���C��؂�ւ���
            mainCamera.targetDisplay = nextTargetDisplay;

            Debug.Log("�J�����̕\������f�B�X�v���C " + nextTargetDisplay + " �ɐ؂�ւ��܂����B");
        }
    }
}