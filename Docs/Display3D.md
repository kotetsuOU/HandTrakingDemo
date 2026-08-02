# 3D立体視・ハーフミラー制御設計思想 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、Sony Spatial Reality Display (SRDisplay) 等の視線追跡（アイトラッキング）技術と、ハーフミラーを用いた光学的配置を組み合わせた「3D立体視・ハーフミラー制御モジュール」の設計思想、数理モデル、各モジュールの役割および使用手順について解説します。

---

## 1. 概要

本モジュールは、独自のトラッキング計算を廃止し、**「SDK のネイティブなアイトラッキング機能」と「描画空間の X 軸鏡像反転」を組み合わせる**堅牢なアーキテクチャを採用しています。

物理的なアイトラッキングセンサー (`SRDManager`) が取得したユーザーの両目 3D 座標と投影行列（フラスタム）の精度を 100% 活用しつつ、ハーフミラー越しの正しい視差（パララックス）をリアルタイム表現します。

### 主な特徴

* **投影行列非破壊アプローチ**: カメラの投影行列自体を反転させる従来手法の破綻（視差逆転やポリゴンカリング崩れ）を避け、「カメラ画像ではなく空間の方を X 軸反転させる」アプローチを採用しています。
* **幾何学的完全適合**: 鏡面反射による視点座標ギャップを幾何学的に証明し、ディスプレイ中心を軸とした左右反転スケール合成のみで完全補正します。
* **URP レンダリングパス透過統合**: `CameraAdjuster` から与えられたディスプレイ行列を `PCD_RenderPass_BindParams` 内で自動合成し、Compute Shader 投影パラメータへ透過バインドします。

---

## 2. 設計思想・アーキテクチャ

### 2.1 生成ファイル・ディレクトリ構成

```text
Assets/Features/Display3D/
├── Prefabs/                           # 3Dディスプレイ調整枠プレハブ
└── Scripts/
    ├── CameraAdjuster.cs              # ディスプレイ位置・反転行列の統括管理
    └── PCD_RenderPass_BindParams.cs   # Compute Shader 用 ViewMatrix 反転合成処理
```

### 2.2 クラス相関図

```mermaid
graph TD
    SRD["SRDManager (Sony SRDisplay SDK)"] --> |Eye Tracking Data| Cam["Camera.main"]
    Adj["CameraAdjuster"] --> |displayTRS / Flip Matrix| Bind["PCD_RenderPass_BindParams"]
    Bind --> |vMatrix (X-Flipped ViewMatrix)| CS["PCD_Occlusion.compute"]
    Cam --> |Standard Frustum| CS

    style SRD fill:#4a90d9,color:#fff
    style Adj fill:#f5a623,color:#fff
    style CS fill:#50e3c2,color:#000
```

### 2.3 空間反転データフロー

```text
[ユーザー両目 (アイトラッキング)] ──► [SRDManager] (標準カメラ視差計算)
                                             │
[CameraAdjuster] (ディスプレイ面 TRS) ────────┤ (X軸反転合成: scaleX = -1)
                                             ▼
                             [PCD_RenderPass_BindParams]
                                             │
                                             ▼
                             [PCD_Occlusion.compute]
```

---

## 3. セットアップ・使用方法

### 3.1 クイックスタート手順

#### Step 1: SDK およびコンポーネント配置

1. シーン内に `SRDManager`（Sony SRDisplay SDK コンポーネント）を配置します。
2. シーン内の描画制御用 GameObject に `CameraAdjuster` をアタッチし、`displayTransform` にディスプレイ物理平面枠をアサインします。

#### Step 2: キャリブレーションと反転設定

1. `Initialization.md` に従い、`SRDManager` の Gizmo（ディスプレイ枠）を現実のハーフミラー内に映る虚像位置・傾きに正確に位置合わせします。
2. 仮想キャラクター（例: Fox 等）は、親 `Transform` の `Scale.x` を `-1` に設定することで、カリング順を崩さずに左右反転表示を同期させます。

---

## 4. 仕様・パラメータ詳細

### 4.1 パラメータ・変換処理仕様

* `CameraAdjuster` パラメータ仕様:

| パラメータ名 | 型 | 既定値 | 説明 |
|---|---|---|---|
| `displayTransform` | `Transform` | `null` | ディスプレイ物理面の Transform 参照 |
| `enableXFlip` | `bool` | `true` | X 軸鏡像反転の有効化 |

* 空間鏡像化処理 (`PCD_RenderPass_BindParams.cs`):

```csharp
Matrix4x4 displayTRS = Matrix4x4.TRS(center, rotation, Vector3.one);
Matrix4x4 flipX = Matrix4x4.Scale(new Vector3(-1, 1, 1));
Matrix4x4 displayInverse = displayTRS.inverse;

// ViewMatrix に空間反転行列を合成
vMatrix = vMatrix * displayTRS * flipX * displayInverse;
```

### 4.2 数式モデル・理論的背景

<details>
<summary><b>📐 ハーフミラー視差ギャップ補正と X 軸反転の理論的証明（クリックで展開）</b></summary>

#### A. 鏡面反射による視点座標と代数補正

鏡面反射による虚像の目 $E'$ と、ソフトウェアが期待する対面座標 $E_{\text{expected}}$ を比較検証します。

* **カメラが取得した虚像位置**:

$$
E' = \begin{pmatrix} x \\ -y + y_{\text{off\_mirror}} \\ z \end{pmatrix}
$$

* **ソフトウェアが期待する対面座標**:

$$
E_{\text{expected}} = \begin{pmatrix} -x + 2x_{\text{off}} \\ -y + 2y_{\text{off}} \\ z \end{pmatrix}
$$

キャリブレーションにより $y_{\text{off\_mirror}} \approx 2y_{\text{off}}$ と調整されたとき、唯一残される不一致は **X 軸の符号** のみです。したがって、ディスプレイ中心 $x_{\text{off}}$ を原点として **X 軸座標を $-1$ 倍（左右反転）** させる変換行列 $\mathbf{S}_x$ を合成することで、幾何学的視差ギャップが完全解消されます。

$$
\mathbf{S}_x = \begin{pmatrix} -1 & 0 & 0 & 0 \\ 0 & 1 & 0 & 0 \\ 0 & 0 & 1 & 0 \\ 0 & 0 & 0 & 1 \end{pmatrix}
$$

</details>

---

## 5. デバッグ・留意事項

### 5.1 留意事項

* `SRDManager` の Gizmo 位置が実際のディスプレイ虚像位置からズレていると、視差パララックスおよびカリングが破綻するため、初期位置調整スクリプトに従った調整が必須です。
* カスタムシェーダーを使用する場合は、`_IsReversedZ` と投影行列の逆行列変換の整合性を確認してください。

### 5.2 統制ログシステム (AppLogManager) との同期

本モジュールの動作ログには、プレフィックス `[Display3D]` が付加されます。

* `[Display3D] CameraAdjuster: X軸鏡像反転行列を更新しました。`

詳細な共通ログルールについては [Logging.md](./Logging.md) を参照してください。
