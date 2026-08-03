# 衝突判定アルゴリズム比較: Native C++ vs GPU Compute Shader 仕様書

> 📂 **親ノード**: [Collision.md](./Collision.md) | 🏷️ **種類**: 🔬 アルゴリズム比較  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、元の `com.shinolab.midair-haptics-unity-core` パッケージ（ネイティブ C++ / CPU ベース）に実装されていた衝突判定・クラスタリングアルゴリズムと、本プロジェクト向けに再設計された `HCD_Pipeline` (完全 GPU ベース) のアルゴリズムの違い、およびその設計思想と数理モデルについて解説します。

---

## 1. 概要

本比較ドキュメントは、従来手法の CPU ベース LBVH / SpatialHash パイプラインと、新規開発された GPU Compute Shader 完全並列化パイプラインのアーキテクチャ・計算性能・数理モデルの違いを明確にするための比較仕様書です。

### 主な特徴

* **約 60 倍の高速化**: 10 万点の大規模点群処理において、CPU への全点群リードバックを撤廃し、GPU Compute Shader 完全並列化によって処理時間を `3.0ms` から `0.05ms` へ大幅削減しました。
* **メモリ転送ボトルネックの解消**: GPU `ComputeBuffer` 上で直接衝突判定・ボクセル集約を行うことで、メインスレッドの同期待ちフリーズを排除しました。
* **固定小数点並列アキュムレーション**: GPU `InterlockedAdd` を活用した固定小数点数アキュムレーションにより、並列化に伴う桁落ち・丸め誤差を防ぎます。

---

## 2. 設計思想・アーキテクチャの比較

### 2.1 生成ファイル・関連モジュール構造

```text
Assets/Features/HapticsCollision/
├── Scripts/
│   ├── Processors/
│   │   ├── HCD_DistanceProcessor.cs        # GPU 距離判定
│   │   ├── HCD_SpatialClusteringProcessor.cs # GPU ボクセルハッシュクラスタリング
│   │   └── HCD_ClusterTracker.cs           # CPU 最近傍マッチング追跡
│   └── Core/
│       └── HCD_ClusterDecoder.cs          # 固定小数点バイナリデコード
```

### 2.2 アーキテクチャ比較図

```mermaid
graph TD
    subgraph "従来手法 (Native C++ CPU)"
        GPU1["点群バッファ (GPU)"] --> |全点群 Readback (遅い)| CPU1["CPU LBVH 構築"]
        CPU1 --> CPU2["CPU Pairwise クラスタリング"]
        CPU2 --> Main1["メインスレッドブロック (3.0ms)"]
    end

    subgraph "提案手法 (HCD GPU Compute Shader)"
        GPU2["点群バッファ (GPU)"] --> CS1["HCD Compute Shader (並列判定)"]
        CS1 --> CS2["GPU Spatial Hash & Accumulate"]
        CS2 --> |軽量結果のみ AsyncReadback| CPU3["HCD_ClusterTracker (0.05ms)"]
    end

    style Main1 fill:#f56c6c,color:#fff
    style CPU3 fill:#67c23a,color:#fff
```

### 2.3 アーキテクチャ変更点一覧

* **割愛・削除した機能**: CPU 側 Pairwise 連結クラスタリング、LBVH 空間木構築、およびExtentSplit クラスタ強制分割を全廃。
* **変更・改良した機能**: 内外（貫通）判定に Möller-Trumbore レイキャストを導入し、ハイブリッド空間ハッシュと EMA 最近傍追跡を採用。
* **新規追加した機能**: GPU 上での共分散 (Covariance) 行列並列蓄積と、ザラザラ感 (Random STM) 用の Reservoir Sampling を新規追加。

---

## 3. セットアップ・使用方法

1. `HCD_Pipeline` をシーンオブジェクトにアタッチして実行します。
2. ロジック比較の詳細は [Collision.md](./Collision.md) のセットアップ手順を参照してください。

---

## 4. 仕様・パラメータ詳細

### 4.1 数式モデル・理論的背景

<details>
<summary><b>📐 衝突判定・空間ハッシュ・EMA 平滑化の数理モデル比較（クリックで展開）</b></summary>

#### A. 空間クラスタリング (Spatial Clustering) 数式

接触点群 $\mathcal{P} = \{\mathbf{p}_1, \mathbf{p}_2, \dots, \mathbf{p}_N\}$ に対し、ボクセルサイズ $S_{\text{cell}}$ で量子化 $\mathbf{q}_i = \lfloor \mathbf{p}_i / S_{\text{cell}} \rfloor$ を適用し、法線方向 $\mathbf{n}_i$ と組み合わせたハッシュキー $h = \text{Hash}(\mathbf{q}_i, \mathbf{n}_i)$ で並列集約します。

重み係数 $w_i$ およびクラスタ重心 $\mathbf{C}_h$ は次式で導出されます。

$$
w_i = \text{clamp}\left( \left( \text{saturate}\left( 1 - \frac{d_i}{d_{\text{thresh}}} \right) \right)^p, \, 0.05, \, 1.0 \right)
$$

$$
\mathbf{C}_h = \frac{\sum_{k=1}^{N_h} w_k \mathbf{x}_k}{\sum_{k=1}^{N_h} w_k}
$$

| 記号 | 説明 | 単位 / 型 |
|---|---|---|
| $\mathbf{p}_i$ | 点群の実測 3D 空間座標 | `Vector3` |
| $\mathbf{x}_k$ | 判定対象位置（実測点群 $\mathbf{p}_k$ またはメッシュ表面点 $\mathbf{h}_k$） | `Vector3` |
| $d_i$ | 点群とメッシュ表面の最短距離 | $\mathrm{m}$ (`float`) |
| $d_{\text{thresh}}$ | 接触距離閾値 (`distanceThreshold`) | $\mathrm{m}$ (`float`) |
| $p$ | 距離減衰指数 | `float` |

#### B. 共分散 (Covariance) 行列の GPU 並列蓄積

クラスタ重心 $\mathbf{C}_h$ に対する共分散行列 $\mathbf{\Sigma}_h$ は、GPU `InterlockedAdd` を用いて差分積を並列蓄積します。

$$
\mathbf{\Sigma}_h = \sum_{k=1}^{N_h} (\mathbf{p}_k - \mathbf{C}_h)(\mathbf{p}_k - \mathbf{C}_h)^T
$$

#### C. フレーム間追跡と EMA 平滑化

前フレームの重心との最近傍照合に基づき、接触強度 $F^{(t)}$ は指数移動平均 (EMA) で平滑化されます。

$$
F^{(t)} = (1 - \alpha) F^{(t-1)} + \alpha F_{\text{raw}}
$$

| 記号 | 説明 | 単位 / 型 |
|---|---|---|
| $\alpha$ | EMA 平滑化係数 ($0 < \alpha \le 1$) | `float` |
| $F_{\text{raw}}$ | 当該フレームで算出された生の接触強度 | `float` |

</details>

---

## 5. デバッグ・留意事項

### 5.1 留意事項

* Möller-Trumbore レイキャスト判定は閉じた 3D メッシュ（Manifold Mesh）を前提としています。ポリゴンに巨大な穴がある形状では内外判定が反転することがあります。

### 5.2 統制ログシステム (AppLogManager) との同期

比較・デバッグログには `[Collision]` グループのタグが適用されます。詳細については [Logging.md](./Logging.md) を参照してください。
