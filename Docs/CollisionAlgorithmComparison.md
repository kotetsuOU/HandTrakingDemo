# 衝突判定アルゴリズム比較: Native C++ vs GPU Compute Shader

> 📂 **親ノード**: [Collision.md (衝突判定システム)](./Collision.md) | 🏷️ **種類**: 🔬 アルゴリズム比較
>
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、元の `com.shinolab.midair-haptics-unity-core` パッケージ（ネイティブ C++ / CPUベース）に実装されていた衝突判定・クラスタリングアルゴリズムと、本プロジェクト向けに再設計された `HCD_Pipeline` (完全 GPU ベース) のアルゴリズムの違い、およびその設計思想と数理モデルについて解説します。

---

## 1. 全体的なアーキテクチャの変更点と設計思想

### 元パッケージ (Native C++)
* **動作**: 毎フレーム、点群バッファを CPU に Readback（読み戻し）し、C++ DLL 側で LBVH（Linear Bounding Volume Hierarchy）を用いた距離計算や、CPU上での空間ハッシュ（SpatialHash）によるクラスタリングを実行。
* **課題**: 10万点という大規模な点群を扱う場合、GPU → CPU への巨大なメモリ転送と CPU 側での直列的な計算がボトルネックとなり、メインスレッドを数ミリ秒〜数十ミリ秒ブロックしてしまう問題がありました。

### 本システム (GPU Compute Shader)
* **動作**: 点群は GPU の `ComputeBuffer` に留めたまま、`Compute Shader` を直接起動し、固定長の空間ハッシュ（Voxel Grid）を用いて完全並列で計算。最終的な「数十個の重心や共分散」といった軽量な結果のみを CPU に回収します。
* **設計思想**: 「厳密な幾何学トポロジー解析」よりも「GPUの強力な並列計算力（InterlockedAdd）を最大化し、Unityのメインスレッドを1ミリ秒たりとも止めないこと」を最優先にリアーキテクチャを行いました。これにより処理速度が約60倍（`3.0ms` → `0.05ms`）に向上しました。

---

## 2. アルゴリズムの変更点まとめ

### 🔴 割愛・削除した点 (Omitted)
1. **CPUでの SpatialHash および Pairwise クラスタリング**: 元の実装では CPU 上で空間ハッシュ（または全探索）を用いて点群をグループ化していましたが、GPUとの往復によるボトルネックを解消するため、C++側のロジックを全廃しました。
2. **クラスタの強制分割 (ExtentSplit)**: 大きくなりすぎた面を長軸で強制分割する処理。楕円STMで十分表現できるため不要と判断。
3. **LBVHベースの空間探索**: LBVHのトラバースはGPU効率を落とすため、固定長の空間ハッシュ配列を用いた探索に変更。
4. **物理応答 (PhysicsSolver, Softbody) への出力連携**: 元のアルゴリズムでは接触判定の結果を独自の PhysicsSolver へ渡していましたが、本システム（HCDパイプライン）は「超音波ハプティクス用の高速抽出」に特化しているため、ハプティクス計算系から物理シミュレーションへの直接的なデータ連携（フォースの受け渡し）は行っていません。（※視覚的な物理変形・Softbody等については、別途 Midair Haptics 公式のコンポーネントを並行稼働させ、新たに設けた `PR_Controller` 等で独立して管理するハイブリッド構成をとっています）
5. **動的なモジュール・リクエストシステム (`PcaObjectData`)**: 元パッケージでは `insideMesh`, `meshDistance`, `pointClustering` などの各機能をモジュール化し、必要な Output（HostCopy 等）を動的に要求する複雑なアーキテクチャを持っていました。本システムではCPUのオーバーヘッドを無くすため、これらの動的モジュール構成を廃止し、**「距離計算からクラスタリングまでを一直線に実行する固定の Compute Shader パイプライン」** へと一本化（ハードコード化）しています。

### 🟡 変更した点 (Modified)
1. **内外（貫通）判定アルゴリズム**: 表面法線との内積近似を廃止し、**Möller-Trumbore レイキャスト（奇偶判定）**を採用。指の間の凹形状での誤検出を根絶。
2. **空間クラスタリング**: 距離によるグラフ結合から、**ハイブリッド空間ハッシュ (TactileClustering)** に変更。
3. **トラッキング**: 優先度キューによるダイクストラ的探索から、超軽量な **最近傍マッチング（Nearest-Neighbor）** へ変更。

### 🟢 追加した点 (Added)
1. **GPU上での共分散（Covariance）並列蓄積**: 楕円形状（Ellipse）のための主成分分析(PCA)用の分散をGPUで並列計算。
2. **GPU Reservoir Sampling による16点のランダム抽出**: ザラザラ感（Random STM）のためのランダムサンプリングをGPUの乱数シードを用いて実装し、CPUの負荷を完全にゼロ化。

---

## 3. アルゴリズムの数理モデル比較

アルゴリズムの詳細な数理モデルがどのように変化したのか、主要な3つの処理について比較します。

### 3.1 空間クラスタリング（Spatial Clustering）
接触点群 $\mathcal{P}$ を複数の指ごとにグループ化する処理です。点群は以下の集合として表されます。

```math
\mathcal{P} = \{\mathbf{p}_1, \mathbf{p}_2, \dots, \mathbf{p}_N\}
```

#### ネイティブ実装：CPUベースの SpatialHash / Pairwise 連結
任意の2点 $\mathbf{p}_i$ および $\mathbf{p}_j$ について、距離が閾値 $d_{\mathrm{thresh}}$ 以下の場合にエッジを張り、クラスタを結合します（空間ハッシュで探索を最適化）。

```math
\|\mathbf{p}_i - \mathbf{p}_j\|^2 \leq d_{\mathrm{thresh}}^2 \implies \mathrm{Union}(i, j)
```

物理的な繋がりは厳密に解析できますが、CPU上のメモリ配列を再帰的または直列に走査するため、GPU並列化の恩恵を受けられませんでした。

#### C#実装：TactileClustering（法線ハイブリッドハッシュ）
位置と表面法線 $\mathbf{n}$ の両方を量子化し、GPUの `InterlockedAdd` で $O(1)$ で並列加算します。
1. **座標の量子化**: ボクセルサイズ $S_{\mathrm{cell}}$ で離散化。

   ```math
   \mathbf{q}_i = \left\lfloor \frac{\mathbf{p}_i}{S_{\mathrm{cell}}} \right\rfloor
   ```

2. **法線のビン化**: 法線を空間の6方向（ $\pm X, \pm Y, \pm Z$ ）に分類。

   ```math
   b_i = \arg\max_{k \in \{0,\dots,5\}} (\mathbf{n}_i \cdot \mathbf{v}_k)
   ```

3. **並列集約**: キー $h = \mathrm{Hash}(\mathbf{q}_i, b_i)$ のバケツに対して並列加算し、重心を算出。

   ```math
   \mathbf{C}_h = \frac{1}{N_h} \sum_{k=1}^{N_h} \mathbf{p}_k
   ```

これにより「表」と「裏」の混線を防ぎつつ、GPUで一瞬で重心を抽出します。

---

### 3.2 共分散（Covariance）とランダム抽出

#### ネイティブ実装
クラスタに属する全ての点群の配列を CPU 上で展開し、配列をループして重心からの差分から共分散行列を求め、さらに `std::rand()` などで配列からランダムにインデックスを抽出していました。

#### C#実装：GPU 並列分散 ＋ Reservoir Sampling
1. **共分散行列の並列計算**: GPU側で第1パスで求まった重心 $\mu$ に対し、第2パスで各スレッドが差分を蓄積。

   ```math
   \Sigma = \sum (\mathbf{p}_i - \mu)(\mathbf{p}_i - \mu)^T
   ```
   
2. **ランダム抽出**: ハッシュ値とシードを組み合わせた乱数 $R$ を用い、ランダムバッファに対して `InterlockedExchange` を確率的に実行することで、限られた16個のスロット（バッファ）に偏りなくランダムな点を残します。

---

### 3.3 フレーム間追跡（Cluster Tracking）
重心 $\mathbf{C}^{(t)}$ を、前のフレームの重心 $\mathbf{C}^{(t-1)}$ と紐付ける処理です。

#### ネイティブ実装：グラフマッチング
前フレームとの距離コスト $E = \|\mathbf{C}^{(t-1)} - \mathbf{p}_{curr}\|^2$ を計算し、優先度付きキューを用いてコスト最小化ルートを探索し、状態遷移を管理していました。複雑な物理解析が可能ですが、計算量が多くなります。

#### C#実装：最近傍マッチングと ContactForceReduction
1. **最近傍探索 (Nearest-Neighbor)**
   前フレームの重心に最も近い現フレームの重心を総当たりで探し、閾値 $R_{\mathrm{match}}$ 以内なら同一IDとして紐付けます。

   ```math
   j^* = \arg\min_j \|\mathbf{C}^{(t-1)}_k - \mathbf{C}^{(t)}_j\|^2 \quad (\mathrm{if} \leq R_{\mathrm{match}}^2)
   ```

2. **ContactForceReduction**
   クラスタ内の点群数 $N$ から提示強度 $F_{\mathrm{raw}} \in [0, 1]$ を算出し、指数移動平均（EMA）で平滑化します。

   ```math
   F_{\mathrm{raw}} = \max\left(0, \min\left(1, \frac{N - N_{\min}}{N_{\max} - N_{\min}}\right)\right)
   ```

   ```math
   F^{(t)} = (1 - \alpha) F^{(t-1)} + \alpha F_{\mathrm{raw}}
   ```

これによりハードウェア制御に必須の「提示強度のフェード」を極小計算量 $O(N \times M)$ で実現しています。
