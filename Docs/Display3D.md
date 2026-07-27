# 3D立体視・ハーフミラー制御設計思想 Specification

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、Sony Spatial Reality Display (SRDisplay) 等の視線追跡（アイトラッキング）技術と、ハーフミラーを用いた光学的配置を組み合わせた「3D立体視・ハーフミラー制御モジュール」の設計思想および数理モデルについて解説します。

---

## 1. 概要

本モジュールは、独自のトラッキング計算を廃止し、**「SDK のネイティブなアイトラッキング機能」と「描画空間の X 軸鏡像反転」を組み合わせる**堅牢なアーキテクチャを採用しています。

物理的なアイトラッキングセンサー (`SRDManager`) が取得したユーザーの両目 3D 座標と投影行列（フラスタム）の精度を 100% 活用しつつ、ハーフミラー越しの正しい視差（パララックス）を表現します。

---

## 2. 設計思想・アーキテクチャ

### 2.1 課題と「描画空間反転」アプローチ

1. **投影行列反転の破綻**:
   カメラの投影行列自体を反転させると、視差方向の逆転やポリゴンカリングの表裏逆転 (`GL.invertCulling`) が発生します。
2. **空間反転による正しい鏡面世界**:
   「カメラのトラッキングと投影行列は一切いじらず、描画される世界（空間）の方を反転させる」アプローチにより破綻を回避します。

### 2.2 X軸反転の数理モデル証明

鏡面反射による虚像の目 $E'$ と、ソフトウェアが期待する対面座標 $E_{\mathrm{expected}}$ を比較します。

* **カメラが取得した虚像**: $E' = (x, -y + y_{\mathrm{off\_mirror}}, z)^T$
* **ソフトが期待する対面座標**: $E_{\mathrm{expected}} = (-x + 2x_{\mathrm{off}}, -y + 2y_{\mathrm{off}}, z)^T$

キャリブレーションにより $y_{\mathrm{off\_mirror}} \approx 2y_{\mathrm{off}}$ となるとき、唯一ズレているのは **X 軸の符号** のみです。したがって、ディスプレイ中心 $x_{\mathrm{off}}$ を原点として **X 軸座標を -1 倍（左右反転）** することで幾何学的なギャップが完全解消されます。

---

## 3. セットアップ・使用方法

1. シーン内に `SRDManager`（Sony SRDisplay SDK コンポーネント）を配置します。
2. シーン内のオブジェクトに `CameraAdjuster` をアタッチし、`displayTransform` にディスプレイ面をセットします。
3. `Initialization.md` に従い、`SRDManager` の Gizmo（ディスプレイ枠）を現実のハーフミラー内に映る虚像位置・傾きに正確に位置合わせします。

---

## 4. 仕様・パラメータ詳細

### 4.1 空間鏡像化ロジック

#### Step 1: 点群 (Point Cloud) の X 軸反転
`PCD_RenderPass_BindParams.cs` 内で、`CameraAdjuster` から得たディスプレイ中心 `TRS` 行列を用いて ViewMatrix を合成反転します。

```csharp
Matrix4x4 displayTRS = Matrix4x4.TRS(center, rotation, Vector3.one);
Matrix4x4 flipX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
Matrix4x4 displayInverse = displayTRS.inverse;

// ViewMatrix に反転行列を合成
vMatrix = vMatrix * displayTRS * flipX * displayInverse;
```

#### Step 2: 仮想オブジェクトの X 軸反転
仮想キャラクター（Fox 等）は親 `Transform` の `Scale.x` を `-1` に設定するだけで、Unity 標準機能によりカリングが自動同期されます。

### 4.2 関連ファイル構造

* `CameraAdjuster.cs`: ディスプレイ位置情報の管理と提供
* `PCD_RenderPass_BindParams.cs`: Compute Shader へ渡す ViewMatrix の空間鏡像化処理

---

## 5. デバッグ・留意事項

* `SRDManager` の Gizmo 位置が実際のディスプレイ虚像位置からズレていると、視差パララックスおよびカリングが破綻するため厳密なキャリブレーションが必要です。
* カスタムシェーダーを使用する場合は、`_IsReversedZ` と投影行列の逆行列変換の整合性を確認してください。
