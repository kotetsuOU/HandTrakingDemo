# 空中超音波ハプティクス (AUTD制御) システム 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る  
> 📎 **関連ドキュメント**: [HapticsAlgorithmComparison.md](./HapticsAlgorithmComparison.md) | [FoxFootHaptics.md](./FoxFootHaptics.md) | [FoxBodyHaptics.md](./FoxBodyHaptics.md) | [HapticsIllusion.md](./HapticsIllusion.md) | [HowToUseHaptics.md](./HowToUseHaptics.md) | [AUTD3_SDK_Transition.md](./AUTD3_SDK_Transition.md)

本ドキュメントでは、`Collision.md` から出力された接触重心や法線、および接触強度 (Force) データを受け取り、AUTD3 ハードウェアを駆動して空中超音波による触覚フィードバックを提示するシステムについて解説します。

---

## 1. 概要

システム全体におけるハプティクス処理は、責務分離の観点から「判定」と「出力」の 2 段階に分かれています。

1. **Haptics Collision ([Collision.md](./Collision.md))**: 仮想オブジェクトと点群の衝突判定、クラスタリング、トラッキング、Force 計算を担当。
2. **Haptics AUTD Controller (本ドキュメント)**: トラッキングデータを受け取り、音響ホログラフィ (GSPAT 等) や STM の計算を行い、超音波デバイスを駆動。

---

## 2. 設計思想・アーキテクチャ

### 2.1 コントローラーの役割分離設計

神クラス化を防ぐため、物理接続管理とリアルタイム照射制御が独立したコンポーネントに物理分割されています。

* **`HAP_AUTDHardwareController`**: 物理接続 (TwinCAT / SOEM / Simulator)、ファン・温度設定、Modulation/Silencer 維持を管理。内部処理は `HAP_AUTDLinkService`, `HAP_AUTDModulationService` へカプセル化。
* **`HAP_AUTDHapticsController`**: ターゲットソース (`AutoHCD`, `ObjectTarget`, `Manual`) の切り替え、GSPAT / STM の計算および照射制御オーケストレーション。
* **`HAP_AUTDTransformLoader`**: デバイス群の配置 (Position/Rotation) の JSON 保存・復元。
* **`HAP_HCDFociSettings`**: HCD クラスタからの焦点生成モード (`Simplified` / `Precision`) および各表現ソース設定の管理。
* **ヘルパークラス群**: `HAP_FociGenerator`, `HAP_ObjectFociGenerator`, `HAP_BaseObjectHapticsController`, `HAP_GSPATDeviceAllocator`

### 2.2 3軸独立アーキテクチャ (3-Axis Architecture)

| 軸 | 設定項目 (`Enum`) | 選択肢 | 概要 |
|---|---|---|---|
| **軸 1: ターゲットデータソース** | **`sourceMode`** | `AutoHCD` / `ObjectTarget` / `Manual` | 焦点（出力座標）の生成元を指定 |
| **軸 2: 空間ソルバー** | **`holoAlgorithm`** | `GSPAT` / `Naive` | 複数焦点をどう合成計算するか |
| **軸 3: 時間・STM駆動方式** | **`stmMode`** | `FociSTM` / `GainSTM` | 時間変化をどうデバイスへ送るか |

---

## 3. セットアップ・使用方法

1. シーン内に `AUTD3Device` オブジェクトを配置し、実世界の配置と一致させます。
2. `HAP_AUTDHardwareController` で接続モード (`TwinCAT` / `SOEM` / `Simulator`) を設定します。
3. `HAP_AUTDHapticsController` の `sourceMode` を用途に合わせて選択します (`AutoHCD` / `ObjectTarget` / `Manual`)。
4. [HowToUseHaptics.md](./HowToUseHaptics.md) の手順に従いキャリブレーションと動作確認を行います。

---

## 4. 仕様・パラメータ詳細

### 4.1 音響理論と数式モデル

#### 単一焦点 (Focus / Naive)
波長 $\lambda$、トランスデューサ位置 $\mathbf{r}_i$、目標点 $\mathbf{p}$ に対する位相 $\phi_i$：

$$\phi_i = -\frac{2\pi}{\lambda} \|\mathbf{p} - \mathbf{r}_i\| + \phi_0$$

#### 音響ホログラフィ (GSPAT)
複素音圧 $p_j$ と伝達関数 $H_{ji}$：

$$p_j = \sum_{i=1}^N H_{ji} q_i \quad \left( H_{ji} = \frac{e^{-jk \|\mathbf{p}_j - \mathbf{r}_i\|}}{\|\mathbf{p}_j - \mathbf{r}_i\|} \right)$$

#### 動的振幅スケーリング
接触強度 $F \in [0, 1]$ によるスケーリング：

$$P_{\mathrm{target}} = P_{\mathrm{max}} \cdot F$$

#### 指向性ルーティング (Directional Device Grouping)
デバイス正面ベクトル $\mathbf{d}$ と面の法線 $\mathbf{n}$ のなす角 $\theta$：

$$\theta = \arccos(-\mathbf{d} \cdot \mathbf{n}) \times \frac{180}{\pi} \le \theta_{\text{th}}$$

---

## 5. デバッグ・留意事項

### 5.1 手動制御 API リファレンス (`Manual` モード)
* `SetNull()`: 全出力停止
* `SetFocus(Vector3 position, float amplitude)`: 単一焦点
* `SetHolo(positions, amplitudes, algorithm)`: 多焦点提示
* `SetFocusStm(positions, frequency, amplitude)`: 単焦点 STM
* `SetMultiFocusStm(...)` / `SetGainStm(...)`: 高度パターン STM

### 5.2 デバッグ可視化・プロファイリング & 統制ログ管理

* `HAP_GizmoVisualizer`: デバイスグループと面照射状況の描画
* `HAP_AUTDDebugDisabler`: デバイス ID ベースの個別無効化
* `HAP_AUTDPerformanceProfiler`: GSPAT 計算時間および送信遅延のプロファイリング
* **統制ログ管理 (`AppLogManager` 同期)**: `HAP_LogTriggers` ヘルパーにより、`AppLogManager` の "Haptics" グループ配下に以下の 7 つのサブログトリガー（`[HAP_Controller]`, `[HAP_LinkService]`, `[HAP_ModulationService]`, `[HAP_TransformLoader]`, `[HAP_Calibration]`, `[HAP_PerformanceProfiler]`, `[HAP_SDKSetup]`）が自動登録され、個別にトグル制御が可能です。詳細仕様は [Logging.md](./Logging.md) を参照してください。
