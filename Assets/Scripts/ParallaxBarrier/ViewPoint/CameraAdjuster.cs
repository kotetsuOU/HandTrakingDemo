using UnityEngine;

/// <summary>
/// ディスプレイ（視面）の位置情報とハーフミラーの有効状態を保持し、
/// コンピュートシェーダー（PCD_RenderPass_BindParams）での点群の空間反転処理に
/// パラメータを提供するデータコンテナクラス。
/// ※カメラ自体のトラッキングや投影行列はSDK標準機能に任せるため、カメラ制御は行いません。
/// </summary>
public class CameraAdjuster : MonoBehaviour
{
    [Header("Mirror & Display Configurations")]
    [Tooltip("基準となるディスプレイを表すTransform（点群の空間反転の基準点として使用）")]
    public Transform displayTransform;

    [Tooltip("ハーフミラー環境用に点群空間を反転（鏡像化）するかどうか")]
    public bool isHalfMirrorEnabled = true;
}