# ダミー点群生成・ノイズ付加システム (Dummy Point Cloud System) 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、Unity 上の 3D オブジェクト（`MeshFilter` または `SkinnedMeshRenderer`）から物理密度および色を指定して実測点群をリアルタイム生成し、さらにメッシュ法線方向のノイズや外れ値（Outliers）を付与する**ダミー点群生成・ノイズ付加システム**（`DPC`: Dummy Point Cloud System）の仕様・構造・使用方法について解説します。

---

## 1. 概要

本システムは、実機の RealSense カメラが接続されていない開発環境やテスト環境において、RealSense 互換の点群ストリーム (`RsFrameProvider` / `SoftwareDevice`) を提供し、オクルージョン描画 (`PCD`) や触覚衝突判定 (`HCD`) などの後続パイプラインの動作検証を可能にします。

---

## 2. 設計思想・アーキテクチャ

本モジュールは、保守性およびパフォーマンスを高めるため、各機能の責務を厳密に分離設計しています。

```
[Unity 3D Objects] 
       │
       ▼
[RsMeshPointCloudSampler] ──(メッシュ面/頂点サンプリング & ワールド法線抽出)
       │
       ▼
[RsPointCloudNoiseProcessor] ──(法線方向ノイズ / 外れ値付与: Pure C#)
       │
       ├──► [RsDummySoftwareDevice] ──► RealSense SDK DepthFrame / Pipeline
       │
       └──► [RsDummyPointCloudRenderer] ──► Dirty-based GPU Single-Buffer 描画
```

### 主要コンポーネントと責務

* **`RsDummyPointCloudProvider`**:
  * 外部への点群ストリーム供給元 (`RsFrameProvider`) となるコンポーネントです。
  * サンプリング・ノイズ処理・ソフトウェアデバイス発行の全体フローを統括します。
* **`RsMeshPointCloudSampler`**:
  * メッシュの三角ポリゴン表面積に応じて点をランダムサンプリングし、ワールド座標 `Positions` およびワールド法線 `Normals` を抽出します。
  * オブジェクトのトランスフォームに変更がないフレームでは CPU サンプリング計算をスキップ (0ms) します。
* **`RsPointCloudNoiseProcessor`**:
  * サンプリングされた点群に対して、メッシュ法線方向へのオフセット移動ノイズ（ガウス分布 / 一様分布）および飛び値となる外れ値（Outliers）を付加する Pure C# クラスです。
  * 毎フレームの GC Alloc 発生を防ぐためバッファ再利用設計となっています。
* **`RsDummySoftwareDevice`**:
  * RealSense SDK の `SoftwareDevice` を用いて、サンプリング座標を RealSense 互換の `DepthFrame` ストリームへ変換・発行します。
* **`RsDummyPointCloudRenderer`**:
  * 生成された点群を Procedural Instancing により GPU 上で高速描画する専用レンダラーです。
* **`DPC_LogTriggers`**:
  * 統一ログ管理システム (`AppLogManager`) と連動し、本モジュールの動作ログを一元制御するトリガー定義コンポーネントです。

---

## 3. セットアップ・使用方法

### 3.1 ダミー点群生成コンポーネントの配置

1. Scene 上の任意の GameObject に `RsDummyPointCloudProvider` をアタッチします。
2. `Target 3D Objects` リストに、点群化させたい Unity 3D オブジェクト（例: 手のメッシュ、3D モデルなど）を登録します。
3. 必要に応じて、描画用の `RsDummyPointCloudRenderer` を同オブジェクトまたは別オブジェクトにアタッチし、`dummyProvider` に参照を設定します。

### 3.2 ノイズおよび外れ値のパラメータ調整

1. `RsDummyPointCloudProvider` の Inspector 上にある **[Noise & Outliers Settings]** セクションを開きます。
2. 法線方向ノイズを付与する場合は `Enable Noise` を True に設定し、`Noise Amount Mm` (ノイズ振幅, mm単位) や `Noise Type` (ガウス分布 / 一様分布) を調整します。
3. 孤立した外れ値を付与する場合は `Enable Outliers` を True に設定し、`Outlier Ratio` (発生割合) および `Outlier Distance Mm` (離脱距離, mm単位) を調整します。

---

## 4. 仕様・パラメータ詳細

### 4.1 `RsDummyPointCloudProvider` パラメータ仕様

| パラメータ名 | 型 | 既定値 | 説明 |
| :--- | :--- | :--- | :--- |
| `targetObjects` | `List<GameObject>` | `[]` | 点群化の対象となる 3D オブジェクトのリスト |
| `includeChildren` | `bool` | `true` | 子要素の全ての Renderer を含めるかどうか |
| `densityUnit` | `PointDensityUnit` | `PointsPerCm2` | 点群密度の指定単位 (`PointsPerCm2`, `PointsPerMm2`, `PointSpacingMm`, `TotalPointCount`) |
| `densityValue` | `float` | `1.0` | 密度の数値設定 |
| `maxPointLimit` | `int` | `100000` | 生成する点群数の最大上限キャップ |
| `colorMode` | `PointColorMode` | `SolidColor` | 点群のカラー指定モード (`SolidColor`, `MaterialColor`, `VertexColor`) |
| `solidColor` | `Color` | `RGB(241,187,147)` | 単色指定時のカラー |
| `noiseSettings` | `RsPointCloudNoiseSettings` | `-` | ノイズおよび外れ値の設定構造体 |
| `useCameraPerspective` | `bool` | `true` | カメラ視点・画角・遮蔽を適用するか、全方向出力するか |
| `simulatedCameraTransform` | `Transform` | `null` | 仮想 RealSense カメラの Transform（指定なし時は本オブジェクト） |
| `updateFPS` | `int` | `30` | 更新フレームレート (FPS) |

### 4.2 `RsPointCloudNoiseSettings` パラメータ仕様

| パラメータ名 | 型 | 既定値 | 説明 |
| :--- | :--- | :--- | :--- |
| `enableNoise` | `bool` | `false` | メッシュ法線方向へのノイズ移動を有効化 |
| `noiseAmountMm` | `float` | `2.0` | 法線方向への移動ノイズ量 (mm) |
| `noiseType` | `NoiseDistributionType` | `Gaussian` | ノイズの確率分布 (`Gaussian`: 正規分布 / `Uniform`: 一様分布) |
| `enableOutliers` | `bool` | `false` | 外れ値（飛び値）の生成を有効化 |
| `outlierRatio` | `float` | `0.02` | 全点群に対する外れ値の発生割合 (0.01 = 1%) |
| `outlierDistanceMm` | `float` | `50.0` | 外れ値の移動離脱距離 (mm) |
| `outlierUseRandomDirection` | `bool` | `false` | True: 全方向ランダム / False: メッシュ法線方向に離脱 |

---

## 5. デバッグ・留意事項

### 5.1 統一ログ管理システム (`AppLogManager`) との連動

本モジュールの動作ログは、`AppLogManager` の **`[DummyPointCloud]`** グループで一元制御されます。

* **`DPC_Provider`**: 点群ストリームの開始・停止およびデータ更新ログを出力します。
* **`DPC_Renderer`**: GPU ComputeBuffer への転送および描画実行ログを出力します。
* **`DPC_NoiseProcessor`**: ノイズおよび外れ値の適用結果（処理点数、パラメータ状態）を出力します。

### 5.2 パフォーマンスおよび GC に関する留意事項

* **アロケーションフリー設計**:  
  `RsPointCloudNoiseProcessor` は内部配列バッファを再利用するため、ノイズ処理実行時の GC Alloc は原則発生しません。
* **静止時描画最適化**:  
  `RsDummyPointCloudRenderer` は `DataVersion` を参照し、オブジェクトおよびノイズに変化がない場合は GPU への `SetData` を回避して 0ms でレンダリングします。ノイズが有効な場合は毎フレーム動的に位置が微変動するため、`DataVersion` が更新されて動的描画が行われます。
