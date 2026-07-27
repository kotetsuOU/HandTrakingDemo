# Haptics Illusion (触覚錯覚・独立単焦点実験モジュール) ドキュメント

> 📂 **親ノード**: [Haptics.md](./Haptics.md) | 🏷️ **種類**: 🔬 アルゴリズム検証・実験モジュール  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、超音波焦点による「持っている感（保持触覚）」の向上メカニズムを物理的・生理学的に検証するために設計された、**干渉なし独立多重単焦点照射モジュール** (`HapticsIllusion`) について解説します。

---

## 1. 概要

`HapticsIllusion` モジュールは、80Hz の STM（Spatio-Temporal Modulation: 時分割焦点円運動）提示時に手への触感が向上する現象の原因を解明するために開発されました。

### 主な開発目的
* **複数焦点形成時の波の干渉効果**（GSPAT 等）と**単焦点自体の音圧・裏側回り込み圧力**の物理的分離・評価
* 焦点の形成位置（手の表面 / めり込み内側 / 外側）や再生パラメータ（80Hz STM vs 定点）の影響検証
* 独立した AUTD デバイスへのダイレクト焦点アサインによる干渉計算バイパス

---

## 2. 設計思想・アーキテクチャ

### 2.1 クラス・ファイル構造

```
Assets/Features/Haptics/
├── Scripts/
│   └── HapticsIllusion/
│       ├── HAP_HapticsIllusionFoxFootController.cs   # Fox の足検知継承・対向照射モデル
│       └── HAP_HapticsIllusionCustomController.cs    # 任意 Target 用の汎用独立焦点モデル
└── Scripts/
    └── Editor/
        └── HapticsIllusion/
            ├── HAP_HapticsIllusionFoxFootControllerEditor.cs  # FoxFoot 用カスタム Editor
            └── HAP_HapticsIllusionCustomControllerEditor.cs   # Custom 用カスタム Editor
```

関連する共通クラス一覧:

| クラス名 | 役割 |
|---|---|
| `HAP_AUTDHapticsController` | メインコントローラー。`ObjectTarget` モードで Illusion Controller を駆動 |
| `HAP_GSPATDeviceAllocator` | デバイス配分ロジック本体。優先度順に Disabler → Group → DirectionalGrouping を適用 |
| `HAP_AUTDDebugDisabler` | デバッグ用デバイス強制無効化コンポーネント |
| `HAP_AUTDDeviceGroup` | チェックボックス選択で複数デバイスをグループ化するクラス |
| `HAP_GizmoVisualizer` | デバイス配置・Illusion Group 割当の Gizmo 描画ユーティリティ |

### 2.2 デバイス配分の優先順位 (Device Allocation Priority)

`HAP_GSPATDeviceAllocator` における、各焦点へのデバイス割り当て優先順位は以下の通りです。

```
優先度 1: HAP_AUTDDebugDisabler
          └─ IsDisabled(dev.ID) == true のデバイスは候補から除外（最優先）
               ↓ 通過したデバイスのみ
優先度 2: Illusion Group 指定 (assignedDeviceGroup)
          └─ AssignedDeviceIndices に含まれるデバイスのみ candidateDevs として絞り込み
               ↓ 通過した候補デバイスのみ
優先度 3: enableDirectionalGrouping (方向グルーピング)
          └─ 角度閾値 (directionalAngleThreshold) 内のデバイスのみに絞り込み
             角度 NG の場合、候補の中で最も角度が小さいデバイスに強制割り当て
               ↓
          実際に照射するデバイスが確定
```

> **補足**: `enableDirectionalGrouping = false` の場合、優先度 3 はスキップされ、Disabler で有効かつ Group 指定に含まれる全デバイスに同一焦点が割り当てられます。

---

## 3. セットアップ・使用方法

### 3.1 基本セットアップ手順

1. **コンポーネント追加**: 実験用オブジェクトに `HAP_HapticsIllusionCustomController` をアタッチします。
2. **焦点を追加**: Inspector の `Illusion Focus Configurations` リストで `Add New Target Configuration` ボタンを押し、焦点を追加します。
3. **焦点パラメータ設定**:
   * `Focus Name`: デバッグ用の識別名を入力（例: `Contact Point Focus`）。
   * `Target Transform`: 照射先の `Transform` を設定。
   * `Assigned AUTD Devices`: チェックボックスで担当 AUTD デバイス（AUTD #0, #1 等）を選択。
   * `STM 設定`: `Use STM` を有効にし、`STM Frequency`・`STM Radius` を設定（推奨: 80Hz / 5mm）。
4. **メインコントローラー登録**:
   * `HAP_AUTDHapticsController` の `Source Mode` を `ObjectTarget` に設定。
   * `Object Target Controllers` リストに作成したコントローラーを追加。
   * `Active Controller Target` ドロップダウンでターゲットを選択。

### 3.2 実験・比較検証の推奨プロトコル

* **実験 A: 手の裏回り込み（回折・反射圧力）の遮断テスト**:
  * くり抜いた厚紙を設置し、反対側焦点の `isEnabled` を切り替えて保持感の変化を測定。
* **実験 B: 焦点形成位置のオフセット比較**:
  * `offsetPosition` を表面 `(0,0,0)`、めり込み `(0,0,-0.003)`、外側 `(0,0,+0.003)` で比較評価。
* **実験 C: STM (80Hz) vs 定点照射の比較**:
  * `useSTM` の有効/無効、周波数 (`stmFrequency`)、回転半径 (`stmRadius`) の影響を検証。

---

## 4. 仕様・パラメータ詳細

### 4.1 `HAP_HapticsIllusionFoxFootController`

`HAP_FoxFootHapticsController` を継承した触覚錯覚実験専用コントローラーです。

| パラメータ名 | 型 | 説明 |
| :--- | :--- | :--- |
| `contactDeviceIndex` | `int` | 接点（足の表面）へ照射を担当する AUTD デバイスのインデックス (例: `0`) |
| `oppositeDeviceIndex` | `int` | 接点の反対側（裏側）へ照射を担当する AUTD のインデックス (例: `1`) |
| `enableOppositeFocus` | `bool` | 反対側への焦点照射を有効にするかどうか |
| `contactOffset` | `Vector3` | 接点側焦点のローカル位置微調整オフセット |
| `oppositeOffset` | `Vector3` | 反対側焦点の位置オフセット（法線と逆方向・上方向への距離） |
| `useSTM` | `bool` | 時分割焦点回転 (80Hz 等) を使用するかどうか |
| `stmFrequency` | `float` | STM 回転の再生周波数 (Hz)。既定値 `80` |
| `stmRadius` | `float` | STM 回転軌跡の半径 (m)。既定値 `0.005`（5mm） |
| `stmPoints` | `int` | 1周期あたりの分割サンプル点数（例: `16`） |

### 4.2 `HAP_HapticsIllusionCustomController`

任意の `Transform` に対して、担当デバイスグループ・焦点パラメータを直接定義できる汎用コントローラーです。

#### `HapticsIllusionTargetConfig` 構造体

| フィールド | 型 | 説明 |
|---|---|---|
| `focusName` | `string` | 焦点の識別名（デバッグ / Inspector 表示用） |
| `targetTransform` | `Transform` | 照射対象の `Transform` |
| `assignedDeviceGroup` | `HAP_AUTDDeviceGroup` | 担当 AUTD デバイスを複数選択できるグループ |
| `offsetPosition` | `Vector3` | 表面 / めり込み内側 / 外側のローカルオフセット (m) |
| `isEnabled` | `bool` | この焦点の有効 / 無効 |
| `useSTM` | `bool` | STM（時分割回転）を使用するか |
| `stmFrequency` | `float` | STM 再生周波数 (Hz) |
| `stmRadius` | `float` | STM 回転半径 (m) |
| `stmPoints` | `int` | 1周期あたりの STM 分割点数（4〜64） |
| `focusIntensityPascal` | `float` | 音圧強度 (Pa)。`0` で全系の既定値を使用 |

---

## 5. デバッグ・留意事項

### 5.1 Gizmo 可視化

* **焦点 Gizmo (`HAP_HapticsIllusionCustomController` の `OnDrawGizmos`)**:
  * グレー: 無効 (`isEnabled = false`)
  * 赤: 照射不可（`HAP_AUTDDebugDisabler` で無効化）
  * 橙: 照射不可（`enableDirectionalGrouping` の角度閾値超過）
  * シアン / マゼンタ / 黄 / 緑: 正常照射可能
* **デバイス Gizmo (`HAP_GizmoVisualizer`)**:
  * 白い内側二重枠: Illusion Group 指定に含まれているデバイス

### 5.2 トラブルシューティング

| 症状 | 原因 | 対処 |
|---|---|---|
| Gizmo 焦点が**赤**になっている | Disabler で担当デバイスが全滅 | `HAP_AUTDDebugDisabler` の設定を確認 |
| Gizmo 焦点が**橙**になっている | DirectionalGrouping で角度 NG | `directionalAngleThreshold` を広げるか配置を調整 |
| Gizmo 焦点に**ラベルが出ない** | `drawGizmos = false` または非アクティブ | Inspector で `Draw Gizmos` を有効化 |
| デバイスに**白内枠が出ない** | `sourceMode != ObjectTarget` | `Source Mode = ObjectTarget` でコントローラーを確認 |
| 照射が全デバイスに広がる | Group 指定なし | `assignedDeviceGroup` で担当デバイスを明示選択 |
