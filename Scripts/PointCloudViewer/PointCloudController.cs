using System.Diagnostics;
using UnityEngine;

public class PointCloudController : MonoBehaviour
{
    public Transform file1Transform;
    public Transform file2Transform;
    public Transform file3Transform;
    public Transform file4Transform;

    private Transform currentTarget;

    void Update()
    {
        HandleSelectionInput();
        HandleMovement();
    }

    void HandleSelectionInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SetTarget(file1Transform);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SetTarget(file2Transform);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SetTarget(file3Transform);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SetTarget(file4Transform);
    }

    void SetTarget(Transform target)
    {
        currentTarget = target;
        UnityEngine.Debug.Log($"åªç›ÇÃëIëëŒè€: {target.name}");
    }

    void HandleMovement()
    {
        if (currentTarget == null) return;

        float moveSpeed = 1f;
        float rotateSpeed = 30f;

        // à⁄ìÆ
        float moveX = Input.GetKey(KeyCode.LeftArrow) ? -1 : Input.GetKey(KeyCode.RightArrow) ? 1 : 0;
        float moveY = Input.GetKey(KeyCode.PageUp) ? 1 : Input.GetKey(KeyCode.PageDown) ? -1 : 0;
        float moveZ = Input.GetKey(KeyCode.UpArrow) ? 1 : Input.GetKey(KeyCode.DownArrow) ? -1 : 0;
        Vector3 movement = new Vector3(moveX, moveY, moveZ) * moveSpeed * Time.deltaTime;
        currentTarget.Translate(movement, Space.Self);

        // âÒì]ÅiQ/E/Y/UÅj
        float rotY = Input.GetKey(KeyCode.Q) ? -1 : Input.GetKey(KeyCode.E) ? 1 : 0;
        float rotX = Input.GetKey(KeyCode.Y) ? -1 : Input.GetKey(KeyCode.U) ? 1 : 0;
        currentTarget.Rotate(Vector3.up, rotY * rotateSpeed * Time.deltaTime, Space.Self);
        currentTarget.Rotate(Vector3.right, rotX * rotateSpeed * Time.deltaTime, Space.Self);
    }
}
