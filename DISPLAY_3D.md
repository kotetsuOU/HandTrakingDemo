# 3D立体視・ハーフミラー制御設計思想 (DISPLAY_3D.md)

本ドキュメントは、Sony Spatial Reality Display (SRDisplay) 等の視線追跡（アイトラッキング）技術と、ハーフミラーを用いた光学的配置を組み合わせた「3D立体視・ハーフミラー制御モジュール」の設計思想および数理モデルについて解説します。

---

## 1. モジュール概要

本モジュールは、物理的なアイトラッキングセンサー（`SRDManager`）が取得したユーザーの「両目の3D座標」を、ハーフミラーの反射による「仮想空間（鏡像空間）」の視点として正しく変換し、左右のカメラ（`camera3DLeft`, `camera3DRight`）に割り当てる役割を担います。

**主要コンポーネント:**
*   `StereoCameraController.cs`

---

## 2. 解決すべき課題と「メタ認知」に基づく数理モデル

ハーフミラー越しに3D空間を正しく（裏返しにならずに、かつ現実と1ミリの狂いもなく）表示・インタラクションするためには、以下の2つの重大な乖離（オフセット）を解決する必要がありました。

### A. 空間オフセットの不整合（Z軸のズレ）
物理的なセンサー（`SRDManager`）の位置と、ハーフミラーの反射によって生じる「虚像のディスプレイ面」との間には、数cm（約10cm）の物理的な奥行きの隙間が存在します。
単純にセンサーのワールド座標をそのまま使用すると、カメラが常に虚像の手前側を追従する設定になってしまい、現実とのインタラクション（触覚やオクルージョン）が成立しません。

### B. 反転概念の誤謬（X軸のズレ）
ハーフミラーによる鏡像効果を相殺するため、空間全体をただ反転（`Vector3(-1, 1, 1)`）させてしまうと、顔の中心座標ごと横に移動してしまいます。
（例：物理的に顔が右に4.5cm移動すると、ハーフミラー越しの鏡の世界では顔が左に4.5cm移動していなければなりませんが、単なる空間反転ではIPDが入れ替わるだけで絶対位置が追従しません）。

---

## 3. 実装されたアルゴリズム（鏡像化＆スワップ統合ロジック）

これらの課題を解決するため、`StereoCameraController.cs` では以下の3ステップの数理変換プロセスを毎フレーム実行します。

### Step 1: SDKからのネイティブローカル座標の抽出
`SRDManager` の `TransformPoint`（ワールド化）をバイパスし、`InverseTransformPoint` を用いて、SDKが認識している「生の顔トラッキング相対座標（`nativeLocalL`, `nativeLocalR`）」を純粋なベクトルとして抽出します。

### Step 2: 光学的な鏡像化（X軸マイナス化）と視点のスワップ
1. **鏡像化（Flip Axes）**: 
   取得したローカル座標のX軸に対して `-1` を乗算し、絶対的な顔の移動位置を鏡像化（鏡の中の世界のルールに適合）させます。
2. **クロススワップ（Eye Swapping）**: 
   X軸を反転させると、物理的な「右目（Xがプラス）」は仮想空間の「左側（Xがマイナス）」に移動します。Unityのカメラ空間ルール（Xが小さい方が左）を維持し、かつ正しい視差（IPD）の映像を出力するため、反転後の右目データを `camera3DLeft` に、左目データを `camera3DRight` に交差して代入します。

### Step 3: 仮想空間マトリックス合成（位置の基点の差し替え）
物理センサーの回転（例えば45度の傾き）を維持したまま、位置の基点だけを「虚像ディスプレイ（`displayTransform`）」に置き換えた新しい `Matrix4x4.TRS`（仮想空間行列）を合成します。
この合成行列に、Step 2でスワップ済みのローカル座標を流し込むことで、**Z軸の物理ギャップを完全に吸収**しつつ、ハーフミラー越しの正しい視点座標（`worldL`, `worldR`）が確定します。

```csharp
// 1. 指定された軸（通常はX軸）の座標を反転し、空間全体を鏡像化する
Vector3 flippedLocalL = new Vector3(nativeLocalL.x * flipAxes.x, nativeLocalL.y * flipAxes.y, nativeLocalL.z * flipAxes.z);
Vector3 flippedLocalR = new Vector3(nativeLocalR.x * flipAxes.x, nativeLocalR.y * flipAxes.y, nativeLocalR.z * flipAxes.z);

// 2. X軸が反転された場合、物理的な右目が仮想空間の左側に移動するため、スワップして割り当てる
Vector3 virtualLocalL = (flipAxes.x < 0) ? flippedLocalR : flippedLocalL;
Vector3 virtualLocalR = (flipAxes.x < 0) ? flippedLocalL : flippedLocalR;

// 3. SRDManagerの傾きを維持した仮想空間行列を合成
Matrix4x4 virtualSpaceMatrix = Matrix4x4.TRS(
    displayTransform.position, 
    srdManager.transform.rotation, 
    Vector3.one
);
```

---

## 4. 結び
この「仮想空間マトリックス合成」と「左右アンカーのクロススワップ」の導入により、ハーフミラーシステムにおいて物理空間と仮想空間の 1:1 の完全な座標同期（キャリブレーション）が達成され、オクルージョンやハプティクス（超音波触覚）等の現実干渉モジュールが極めて高い精度で動作する強固な基盤が完成しました。
