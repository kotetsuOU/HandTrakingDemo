# 衝突判定アルゴリズム比較: Native C++ vs GPU Compute Shader

> 📂 **親ノード**: [Collision.md](./Collision.md) | 🏷️ **種類**: 🔬 アルゴリズム比較  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、元の `com.shinolab.midair-haptics-unity-core` パッケージ（ネイティブ C++ / CPU ベース）に実装されていた衝突判定・クラスタリングアルゴリズムと、本プロジェクト向けに再設計された `HCD_Pipeline` (完全 GPU ベース) のアルゴリズムの違い、およびその設計思想と数理モデルについて解説します。

---

## 1. 概要と全体アーキテクチャの変更点

### 元パッケージ (Native C++)
* **動作**: 毎フレーム、点群バッファを CPU に Readback（読み戻し）し、C++ DLL 側で LBVH（Linear Bounding Volume Hierarchy）を用いた距離計算や、CPU 上での空間ハッシュ（SpatialHash）によるクラスタリングを実行。
* **課題**: 10万点という大規模な点群を扱う場合、GPU → CPU への巨大なメモリ転送と CPU 側での直列的な計算がボトルネックとなり、メインスレッドを数ミリ秒〜数十ミリ秒ブロックしてしまう問題がありました。

### 本システム (GPU Compute Shader)
* **動作**: 点群は GPU の `ComputeBuffer` に留めたまま、`Compute Shader` を直接起動し、固定長の空間ハッシュ（Voxel Grid）を用いて完全並列で計算。最終的な「数十個の重心や共分散」といった軽量な結果のみを CPU に回収します。
* **設計思想**: 「厳密な幾何学トポロジー解析」よりも「GPUの強力な並列計算力（`InterlockedAdd`）を最大化し、Unityのメインスレッドを1ミリ秒たりとも止めないこと」を最優先にリアーキテクチャを行いました。これにより処理速度が約60倍 (`3.0ms` → `0.05ms`) に向上しました。

---

## 2. 設計思想・アルゴリズムの比較

### 🔴 割愛・削除した点 (Omitted)
1. **CPUでの SpatialHash および Pairwise クラスタリング**: C++ 側のロジックを全廃。
2. **クラスタの強制分割 (ExtentSplit)**: 楕円 STM で十分表現できるため不要と判断。
3. **LBVHベースの空間探索**: 固定長の空間ハッシュ配列を用いた探索に変更。
4. **物理応答 (PhysicsSolver, Softbody) への出力連携**: `PR_Controller` 等で独立して管理するハイブリッド構成をとっています。
5. **動的なモジュール・リクエストシステム (`PcaObjectData`)**: 固定の Compute Shader パイプラインへ一本化。

### 🟡 変更した点 (Modified)
1. **内外（貫通）判定アルゴリズム**: **Möller-Trumbore レイキャスト（奇偶判定）**を採用。
2. **空間クラスタリング**: **ハイブリッド空間ハッシュ (TactileClustering)** に変更。
3. **トラッキング**: 軽量な **最近傍マッチング (Nearest-Neighbor)** へ変更。

### 🟢 追加した点 (Added)
1. **GPU上での共分散 (Covariance) 並列蓄積**: 楕円形状 (Ellipse) 用の主成分分析 (PCA) 用の分散を GPU で並列計算。
2. **GPU Reservoir Sampling による16点のランダム抽出**: ザラザラ感 (Random STM) のためのランダムサンプリングを GPU で実装。

---

## 3. 数理モデル比較

### 3.1 空間クラスタリング (Spatial Clustering)

接触点群 $\mathcal{P} = \{\mathbf{p}_1, \mathbf{p}_2, \dots, \mathbf{p}_N\}$ に対し：

* **ネイティブ実装 (CPU Pairwise 連結)**:

  $$\|\mathbf{p}_i - \mathbf{p}_j\|^2 \leq d_{\mathrm{thresh}}^2 \implies \mathrm{Union}(i, j)$$

* **C# / GPU 実装 (TactileClustering)**:
  座標 $\mathbf{p}_i$ をボクセルサイズ $S_{\mathrm{cell}}$ で量子化 $\mathbf{q}_i = \lfloor \mathbf{p}_i / S_{\mathrm{cell}} \rfloor$ し、法線方向 $b_i$ と組み合わせたキー $h = \mathrm{Hash}(\mathbf{q}_i, b_i)$ で並列集約：

  $$\mathbf{C}_h = \frac{1}{N_h} \sum_{k=1}^{N_h} \mathbf{p}_k$$

### 3.2 共分散 (Covariance) 行列

GPU 側で重心 $\mu$ に対し差分を並列蓄積：

$$\Sigma = \sum (\mathbf{p}_i - \mu)(\mathbf{p}_i - \mu)^T$$

### 3.3 フレーム間追跡 (Cluster Tracking)

最近傍探索 (Nearest-Neighbor) により前フレームの重心と照合し、接触強度 $F_{\mathrm{raw}}$ を EMA で平滑化：

$$F^{(t)} = (1 - \alpha) F^{(t-1)} + \alpha F_{\mathrm{raw}}$$

---

## 4. 仕様・パラメータ詳細

具体的なパラメータ構成およびコンピュートシェーダーの実装箇所については [Collision.md](./Collision.md) を参照してください。

---

## 5. デバッグ・留意事項

* Möller-Trumbore レイキャスト判定は閉じた 3D メッシュ（Manifold Mesh）を前提としています。メッシュに巨大な穴がある場合は内外判定が反転することがあります。
