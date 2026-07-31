# 衝突判定・クラスタリングシステム (HCD Pipeline) 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る  
> 📎 **関連ドキュメント**: [CollisionAlgorithmComparison.md](./CollisionAlgorithmComparison.md)

本ドキュメントでは、10万点に及ぶ点群データとアニメーションメッシュとの接触判定を、CPU を一切圧迫することなく完全 GPU 完結アーキテクチャで高速処理する `HCD_Pipeline` システムについて解説します。

---

## 1. 概要

本システムは、点群バッファと仮想 3D メッシュとの接触判定・クラスタリング・トラッキングをリアルタイムに実行します。

旧仕様 (`HapCollisionDetectors`) から大幅なリアーキテクチャが行われ、プロセッサをパイプライン状に連結する `HCD_Pipeline` 構造へ進化しました。詳細な数理モデル比較については [CollisionAlgorithmComparison.md](./CollisionAlgorithmComparison.md) を参照してください。

---

## 2. 設計思想・アーキテクチャ

### 2.1 データフローとパイプライン構成

```text
[リアルタイム統合点群バッファ] (RsGlobalPointCloudManager)
               │ (GPU ComputeBuffer 直接参照)
               ▼
[HCD_Pipeline] (パイプライン・オーケストレーター)
               │
               ├─▶ 1. [HCD_DistanceProcessor] 
               │      (GPU Voxel Grid構築 & Point-to-Triangle 距離判定 + Möller-Trumbore InsideMesh判定)
               │
               ├─▶ 2. [HCD_SpatialClusteringProcessor]
               │      (接触した点群を空間ハッシュでクラスタリングし、接触重心・16点ランダムサンプルを計算)
               │
               ├─▶ 3. [HCD_ClusterTracker] (CPU)
               │      (前フレームのクラスタと照合し、安定したIDと生存期間(Age)を付与)
               │
               └─▶ 4. [HCD_Pipeline (Gizmo)] または [AUTD3連携クラス]
                      (安定したクラスタ情報を元に描画や焦点生成を実行)
```

### 2.2 ファイル・システム構成

* **`HCD_Pipeline.cs`**: 各プロセッサの実行順序やバッファを管理・仲介するオーケストレーター
* **`IHCD_Processor.cs`**: 各プロセッサが実装すべき共通インターフェース
* **`HCD_DistanceProcessor.cs`**: [GPU] ボクセル構築と Point-to-Triangle 最短距離・めり込み判定
* **`HCD_SpatialClusteringProcessor.cs`**: [GPU] 空間ハッシュによる接触点のグループ化、重心・共分散・16ランダム点の算出
* **`HCD_ClusterTracker.cs`**: [CPU] フレーム間のクラスタ追跡と ID・寿命管理
* **`HCD_Distance.compute` / `HCD_SpatialClustering.compute`**: GPU 並列計算コンピュートシェーダー

---

## 3. セットアップ・使用方法

1. シーン上の管理オブジェクトに `HCD_Pipeline` をアタッチします。
2. **`Detection Target`** に接触判定を行わせたい仮想オブジェクトをセットします。
3. **`Detection Mode`** を選択します (`SkinnedMeshRenderer`, `MeshFilter`, `TransformOnly`)。
4. `AnimationController` と連携している場合、ターゲット選択切り替えに連動して自動的に追従・更新されます。

---

## 4. 仕様・パラメータ詳細

### 4.1 モジュール別アルゴリズム詳細

#### A. `HCD_DistanceProcessor` (距離・接触判定)
* **GPU Voxel Grid 構築**: メッシュの全三角形を並列処理し、ボクセルへ `InterlockedAdd` で登録。
* **Point-to-Triangle 距離計算**: AABB フィルタ通過後に最短距離を算出。
* **Möller-Trumbore レイキャスト**: 点から X+ 方向へレイを飛ばし交差回数をカウント（奇数回＝内部、偶数回＝外部）。

#### B. `HCD_SpatialClusteringProcessor` (空間クラスタリング・表面推定)
* **TactileClustering**: 座標と表面法線を量子化し、ハイブリッドハッシュで空間グループ化。
* **表面推定設定 (Surface Estimation Settings)**:
  * **`AggregationMode`**: クラスタ重心の集約アルゴリズムを選択 (`precisionMode` の有無にかかわらず Pass 1 で適用)。
    * `UnweightedCentroid`: 従来の単純平均（平滑さ優先）。
    * `DistanceWeightedCentroid`: メッシュ表面 0mm に近い点ほど重みを大きく評価し、ノイズに強い真の接触面を推定 ($w = (1 - d/d_{\mathrm{thresh}})^p$)。
    * `NearestPoint`: 最小距離点の重みを極大化し、最も浅い接触点をシャープに推定。
  * **`PositionSource`**: 照射先・基準座標として出力する座標ソースを選択 (`MeshSurface`: メッシュ投影座標 `hitPoint` / `PointCloudRaw`: 点群実測空間座標 `pointPos`)。
  * **`DistanceWeightPower`**: `DistanceWeightedCentroid` モード時の距離減衰累乗数（デフォルト `2.0`）。
* **Precision Mode**: 共分散行列（Covariance Matrix）の計算および GPU Reservoir Sampling による 16 点のランダムサンプリング (Pass 2)。

#### C. `HCD_ClusterTracker` (フレーム間トラッキング)
* **最近傍マッチング**: 前フレームの重心との距離比較による ID 固定。
* **拡張データ保持**: クラスタごとの `RawPointPosition` (実測位置)、`MeshSurfacePosition` (メッシュ表面位置)、`MinDistance` (最小表面距離) を追跡・提供。
* **ContactForceReduction**: 接触点数からベース振幅 $F_{\mathrm{raw}}$ を計算し、指数移動平均（EMA）でフェード処理。

---

## 5. デバッグ・留意事項

### 5.1 Gizmo 可視化
`HCD_Pipeline` のインスペクターから Gizmo 描画を有効にすることで、シーンビュー上に以下の情報がリアルタイム可視化されます：
* **クラスタ重心 (WireSphere)**: 生存期間 (Age) に応じて黄色（新生）からマゼンタ（安定）へグラデーション。
* **実測座標 vs メッシュ表面座標 (黄色/緑色球 + 直線)**: 点群の実測空間位置 (`RawPointPosition`) とメッシュ表面投影位置 (`MeshSurfacePosition`) の乖離を線描画。
* **法線ベクトル (Cyan Ray)**: 接触パッチの平均表面法線方向。
* **情報ラベル**: `ID`, 生存フレーム数 `Age`, 接触強度 `F`, クラスタ内最小表面距離 `MinD: X.Xmm` を表示。

### 5.2 留意事項
* `AsyncGPUReadback` により CPU との同期待ちを非同期化しており、1〜2 フレームの読込ラグが存在します。
* メッシュのポリゴン数が極めて多い場合は、簡易メッシュを判定用ターゲットとしてアサインすることを推奨します。
