using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraAdjuster : MonoBehaviour
{
    [SerializeField]
    private Transform displayTransform; // ディスプレイのTransformを指定するための変数

    private float calculatedAspectRatio;

    private Vector3 cameraPosition; // カメラの位置を格納する変数

    private Vector2 displayHorizontalLeft;   // ディスプレイの水平方向の左端の点(x,z)
    private Vector2 displayHorizontalRight;  // ディスプレイの水平方向の右端の点(x,z)
    private Vector2 displayVerticalTop;      // ディスプレイの垂直方向の上端の点(y,z)
    private Vector2 displayVerticalBottom;   // ディスプレイの垂直方向の下端の点(y,z)

    private float HorizontalViewAngle; // 水平方向の視野角
    private float VerticalViewAngle; // 垂直方向の視野角

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
        // 計算結果を一旦 Euler の float 値として受け取る
        float horizontalEuler = CalcHorizontalValue();
        float verticalEuler = CalcVerticalValue();
        // Z軸は固定 (例: 0) として Quaternion を生成
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

    // 水平方向の計算。返り値は Y 軸回転角
    float CalcHorizontalValue()
    {
        float angleA, angleB;
        float x, z;

        // 右側の角度を計算
        x = displayHorizontalRight.x - cameraPosition.x;
        z = displayHorizontalRight.y - cameraPosition.z;
        angleA = Mathf.Atan(x / z) * Mathf.Rad2Deg;

        // 左側の角度を計算
        x = displayHorizontalLeft.x - cameraPosition.x;
        z = displayHorizontalLeft.y - cameraPosition.z;
        angleB = Mathf.Atan(x / z) * Mathf.Rad2Deg;

        HorizontalViewAngle = angleA - angleB;
        // 水平方向の回転は左右の角度の平均
        return (angleA + angleB) / 2;
    }

    // 垂直方向の計算。返り値は X 軸回転角
    float CalcVerticalValue()
    {
        float angleA, angleB;
        float yDiffTop, yDiffBottom, zTop, zBottom;

        // 上端の角度計算
        yDiffTop = cameraPosition.y - displayVerticalTop.x;
        zTop = displayVerticalTop.y - cameraPosition.z;
        angleA = Mathf.Atan(yDiffTop / zTop) * Mathf.Rad2Deg;

        // 下端の角度計算
        yDiffBottom = cameraPosition.y - displayVerticalBottom.x;
        zBottom = displayVerticalBottom.y - cameraPosition.z;
        angleB = Mathf.Atan(yDiffBottom / zBottom) * Mathf.Rad2Deg;

        VerticalViewAngle = angleA - angleB;
        // 垂直方向の回転は上下の角度の平均
        return (angleA + angleB) / 2;
    }
}