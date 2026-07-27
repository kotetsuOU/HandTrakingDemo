# Fox Body Haptics (キツネ全身体・頭・耳・四肢・尻尾ハプティクス) 仕様書

> 📂 **親ノード**: [Haptics.md](./Haptics.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、キツネ（Fox）の頭（Head）、両耳（Left Ear / Right Ear）、四肢（4本の足）、および尻尾（Tail）の各ボーンアニメーションに合わせて、そのターゲット座標に焦点を合わせた空中超音波フィードバックを照射する `HAP_FoxBodyHapticsController` について解説します。

---

## 1. 概要

`HAP_FoxBodyHapticsController` は、基底抽象クラスである `HAP_BaseObjectHapticsController` を継承し、Fox の全身体部位（頭・両耳・四肢・尻尾の計8箇所）を統合制御するコンポーネントです。

キツネの骨格階層内から指定したパターン名（例: `Fox_Head`, `Fox_LEar1`, `Fox_REar1`, `Fox_F_LLegDigit11`, `Fox_Tail6` 等）に合致する全ボーンを自動検出し、その追跡情報 (`TargetInfos`) および各部位の照射・接触向き (`headTargetTouchDirection`, `footTargetTouchDirection`) を基底クラスに提供します。

---

## 2. 設計思想・アーキテクチャ

### 2.1 クラス構造と継承

本モジュールは以下のクラスで構成されています。

* **`HAP_FoxBodyHapticsController`**:
  `HAP_BaseObjectHapticsController` を継承するメインコンポーネントです。`HAP_AUTDHapticsController` のハプティクス送信ループ (`UpdateHaptics`) と連携し、リアルタイムにターゲット位置へ超音波の焦点を形成します。
* **`HAP_FoxBodyHapticsControllerEditor`**:
  インスペクター上の「Auto Detect Bones」ボタンによる一括自動バインドや、部位ごとの有効化トグル、照射向き設定を簡単に管理できるカスタムエディタ機能を提供します。

### 2.2 自動ボーン検出ルール

`AutoDetectBones()` 実行時、以下の優先度・検索ルールでモデル階層下からボーンを自動バインドします。

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

## 3. セットアップ・使用方法

### 3.1 セットアップ手順

1. ターゲットとなる Fox モデルの GameObject に `HAP_FoxBodyHapticsController` コンポーネントをアタッチします。
2. Inspector 上の **「Auto Detect Bones」** ボタンをクリックし、モデル階層から各ボーン Transform を自動検出して割り当てます。
3. 必要に応じて部位ごとの有効化トグル (`enableHead`, `enableFrontLeftFoot` 等) や照射向きベクトルを調整します。
4. `HAP_AUTDHapticsController` の `Source Mode` を `ObjectTarget` に設定し、`Object Target Controllers` に本コンポーネントを追加します。

---

## 4. 仕様・パラメータ詳細

`HAP_FoxBodyHapticsController` のインスペクター設定パラメータ一覧です。

### 4.1 ボーン割り当て (Body Bone Transforms)
* `headBone`: 頭部ボーン (`Fox_Head` 等)
* `leftEarBone` / `rightEarBone`: 左耳・右耳ボーン (`Fox_LEar1`, `Fox_REar1` 等)
* `frontLeftFoot` / `frontRightFoot` / `backLeftFoot` / `backRightFoot`: 4本の足ボーン (`Fox_F_LLegDigit11` 等)
* `tailBone`: 尻尾ボーン (`Fox_Tail6` 等)

### 4.2 有効化トグル (Body Part Toggles)
* `enableHead` / `enableLeftEar` / `enableRightEar`: 頭部・両耳への照射の有効/無効
* `enableFrontLeftFoot` / `enableFrontRightFoot` / `enableBackLeftFoot` / `enableBackRightFoot` / `enableTail`: 四肢・尻尾への照射の有効/無効

### 4.3 照射向き設定 (Target Touch Directions)
* `headTargetTouchDirection`: 頭部および両耳ターゲットへの超音波照射向きベクトル (既定値: `Vector3.down`)
* `footTargetTouchDirection`: 四肢および尻尾ターゲットへの超音波照射向きベクトル (既定値: `Vector3.down`)

### 4.4 接地判定設定 (Animation State Settings) ※足パーツのみ適用
* `disableWhenInAir`: 空中浮遊中（ルート座標からの高さが閾値を超えている場合）に足の触覚照射をオフにします。
  * ※ 頭・耳・尻尾は `IsTail = true` として登録されており、空中判定の影響を受けずに常にアクティブに維持されます。
* `airborneHeightThreshold`: 接地判定の高さ閾値（メートル）
* `rootTransform`: 接地判定の基準 Transform

### 4.5 手との接触設定 (Hand Contact Settings) ※全パーツ適用
* `onlyTargetHandContact`: `HCD_Pipeline` で検出された手が近くにある場合のみ照射します。
* `handContactThreshold`: 接触判定距離閾値（メートル）

### 4.6 カスタムモード・STM設定 (Custom Mode Settings)
* `stmMode`: `FociSTM` (ハードウェア超高速単焦点) または `GainSTM` (PC計算複数焦点/GSPAT対応)
* `sequentialStmFrequency`: シーケンシャル照射周波数 (Hz)

---

## 5. デバッグ・留意事項

### 5.1 Gizmo 可視化

Scene ビュー上でのリアルタイム確認のため、以下の Gizmo が自動描画されます（基底クラスで制御）。

* **ターゲット位置の球体 (`Wireframe Sphere` & `Solid Sphere`)**:
  * 有効かつ照射条件を満たしているターゲット: **緑色のワイヤー球および実線球**で描画されます。
  * 非アクティブなターゲット: **赤色のワイヤー球**で描画されます。
* **接地閾値と接続線（足パーツのみ）**:
  * 空中判定が有効な場合、`airborneHeightThreshold` の高さを示す小さな十字マークおよび高さに応じた線が表示されます。
* **手との接触線**:
  * 手との近接接触判定 (`onlyTargetHandContact`) が有効かつ接触時、手のクラスタ重心からターゲット位置に向けて緑色の線が描画されます。

### 5.2 留意事項

* 自動検出で一部のボーンが見つからない場合は、手動で Transform をドラッグ＆ドロップしてアサインしてください。
* 空中判定 (`disableWhenInAir`) は足パーツのみに適用され、頭部・耳・尻尾パーツには適用されません。
