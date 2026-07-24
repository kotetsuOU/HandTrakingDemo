# Fox Body Haptics (キツネ全身体・頭・耳・四肢・尻尾ハプティクス) 仕様書

> 📂 **親ノード**: [Haptics.md (AUTD制御システム)](./Haptics.md) | 🏷️ **種類**: 🏗️ システム設計書
>
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本モジュールは、キツネ（Fox）の頭（Head）、両耳（Left Ear / Right Ear）、四肢（4本の足）、および尻尾（Tail）の各ボーンアニメーションに合わせて、そのターゲット座標に焦点を合わせた空中超音波フィードバックを照射するシステムです。

---

## 1. 概要

`HAP_FoxBodyHapticsController.cs` は、基底抽象クラスである `HAP_BaseObjectHapticsController` を継承し、Foxの全身体部位（頭・両耳・四肢・尻尾）を統合制御するコンポーネントです。

- **`HAP_FoxBodyHapticsController.cs`**:
  キツネの骨格階層内から指定したパターン名（例: `Fox_Head`, `Fox_LEar1`, `Fox_REar1`, `Fox_F_LLegDigit11`, `Fox_Tail6` 等）に合致する全8箇所のボーン（Transform）を自動検出し、その追跡情報（`TargetInfos`）および各部位の照射・接触向き（`headTargetTouchDirection`, `footTargetTouchDirection`）を基底クラスに提供します。

- **`HAP_FoxBodyHapticsControllerEditor.cs`**:
  インスペクター上の「Auto Detect Bones」ボタンによる一括自動バインドや、部位ごとの有効化トグル、照射向き設定を簡単に管理できるカスタムエディタ機能を提供します。

これらが `HAP_AUTDHapticsController` のハプティクス送信ループ（`UpdateHaptics`）と連携し、リアルタイムにターゲット位置へ超音波の焦点を形成します。

---

## 2. インスペクター設定パラメータ (HAP_FoxBodyHapticsController)

### ① ボーン割り当て (Body Bone Transforms)
* **`Head Bone`**: 頭部ボーン (`Fox_Head` 等)
* **`Left Ear Bone / Right Ear Bone`**: 左耳・右耳ボーン (`Fox_LEar1`, `Fox_REar1` 等)
* **`Front Left / Front Right / Back Left / Back Right Foot`**: 4本の足ボーン (`Fox_F_LLegDigit11` 等)
* **`Tail Bone`**: 尻尾ボーン (`Fox_Tail6` 等)
* **「Auto Detect Bones」ボタン**: ボタンを押すことで、モデル階層下から各部位のTransformを自動検出してセットします。

### ② 有効化トグル (Body Part Toggles)
* **`Enable Head / Enable Left Ear / Enable Right Ear`**: 頭部・両耳への照射の有効/無効
* **`Enable Front Left / Enable Front Right / Enable Back Left / Enable Back Right / Enable Tail`**: 四肢・尻尾への照射の有効/無効

### ③ 照射向き設定 (Target Touch Directions)
* **`Head/Ear Target Touch Direction`**: 頭部および両耳ターゲットへの超音波照射向きベクトル (デフォルト: `Vector3.down`)
* **`Foot/Tail Target Touch Direction`**: 四肢および尻尾ターゲットへの超音波照射向きベクトル (デフォルト: `Vector3.down`)

### ④ 接地判定設定 (Animation State Settings) ※足パーツのみ適用
* **`Disable When In Air`**: 空中浮遊中（ルート座標からの高さがしきい値を超えている場合）に足の触覚照射をオフにします。
  * ※ 頭・耳・尻尾は `IsTail = true` として登録されており、空中判定の影響を受けずに常にアクティブに維持されます。
* **`Airborne Height Threshold`**: 接地判定の高さしきい値（メートル）
* **`Root Transform`**: 接地判定の基準Transform

### ⑤ 手との接触設定 (Hand Contact Settings) ※全パーツ適用
* **`Only Target Hand Contact`**: HCD_Pipeline で検出された手が近くにある場合のみ照射します。
* **`Hand Contact Threshold`**: 接触判定距離しきい値（メートル）

### ⑥ カスタムモード・STM設定 (Custom Mode Settings)
* **`STM Mode`**: `FociSTM` (ハードウェア超高速単焦点) または `GainSTM` (PC計算複数焦点/GSPAT対応)
* **`Sequential STM Frequency (Hz)`**: シーケンシャル照射周波数

---

## 3. 自動ボーン検出パターン (Auto Detect Rules)

`AutoDetectBones()` 実行時、以下の優先度・検索ルールでモデル階層下からボーンを自動バインドします：

| 部位 | 検出キーワード / 優先パターン |
| :--- | :--- |
| **頭部 (Head)** | `Fox_Head`, `Head`, `Fox_Neck` |
| **左耳 (Left Ear)** | `Fox_LEar1`, `Fox_LEar2`, `Ear1_L`, `Ear_L` |
| **右耳 (Right Ear)** | `Fox_REar1`, `Fox_REar2`, `Ear1_R`, `Ear_R` |
| **左前足 (Front Left)** | `Fox_F_LLegDigit11`, `F_LLegDigit11`, `F_LLegAnkle` |
| **右前足 (Front Right)** | `Fox_F_RLegDigit11`, `F_RLegDigit11`, `F_RLegAnkle` |
| **左後足 (Back Left)** | `Fox_LLegDigit11`, `LLegDigit11`, `LLegAnkle` |
| **右後足 (Back Right)** | `Fox_RLegDigit11`, `RLegDigit11`, `RLegAnkle` |
| **尻尾 (Tail)** | `Fox_Tail6`, `Fox_Tail5`, `Tail` |

---

## 4. デバッグ視覚化 (Gizmos)

Sceneビュー上でのリアルタイム確認のため、以下のGizmoが自動描画されます（基底クラスで制御）。

* **ターゲット位置の球体（Wireframe Sphere & Solid Sphere）**:
  * 有効かつ照射条件を満たしているターゲット：**緑色のワイヤー球および実線球**で描画されます。
  * 非アクティブなターゲット：**赤色のワイヤー球**で描画されます。
* **接地しきい値と接続線（足パーツのみ）**:
  * 空中判定が有効な場合、`airborneHeightThreshold` の高さを示す小さな十字マークおよび高さに応じた線が表示されます。
* **手との接触線**:
  * 手との近接接触判定（`onlyTargetHandContact`）が有効かつ接触時、手のクラスタ重心からターゲット位置に向けて緑色の線が描画されます。
