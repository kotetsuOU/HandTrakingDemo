# Haptics Illusion (触覚錯覚・独立単焦点実験モジュール) ドキュメント

## 1. 概要 (Overview)

`HapticsIllusion` は、超音波焦点による「持っている感（保持触覚）」の向上メカニズムを物理的・生理学的に検証するために設計された、**干渉なし独立多重単焦点照射モジュール**です。

### 開発背景と実験目的
80Hz の STM（Spatio-Temporal Modulation: 時分割焦点円運動）を提示した際、手への触感が著しく向上する現象が確認されています。この原因として：
- **GSPAT 等による複数焦点形成時の波の干渉効果か？**
- **単焦点自体の音圧や、手の接点・裏側への回り込み（反射・回折圧力）による物理的増幅か？**
- **焦点の形成位置（手の表面 / めり込み内側 / 外側）や再生パラメータ（80Hz STM vs 定点）の影響か？**

をクリアに切り分けて実験・比較検証するために、本モジュールが開発されました。

### 主な特徴
1. **干渉計算 (GSPAT) のバイパス**:
   複数の AUTD デバイス（例: AUTD #0 と AUTD #1）が存在する場合、合成ホログラム計算を行わず、**各 AUTD に専用の単焦点（Focus / FocusSTM）をダイレクトに割り当てて出力**します。
2. **接点側 ＆ 反対側（裏側）の対向照射**:
   1台の AUTD (AUTD #0) から接点表面へ、もう1台の AUTD (AUTD #1) から接点の反対側（裏側）へ同時に単焦点 / STM を照射可能。
3. **チェックボックスによる柔軟なグループ選択**:
   `HapticsIllusionTargetConfig.assignedDeviceGroup`（`HAP_AUTDDeviceGroup`）により、各焦点が担当する AUTD デバイスを Inspector 上のチェックボックスで複数選択可能。
4. **クリーンな排他切替（GameObject 名ドロップダウン）**:
   `HAP_AUTDHapticsController` の Inspector 上で、アタッチされている GameObject の名前を選択するだけで、照射対象となるコントローラーを即座かつ安全に排他切り替え可能。

---

## 2. ファイル構成 (File Structure)

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

関連する共通ファイル:
| ファイル | 役割 |
|---|---|
| [HAP_AUTDHapticsController.cs](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/Assets/Features/Haptics/Scripts/HAP_AUTDHapticsController.cs) | メインコントローラー。Disabler / DirectionalGrouping を保持し、ObjectTarget モードで Illusion Controller を駆動する |
| [HAP_GSPATDeviceAllocator.cs](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/Assets/Features/Haptics/Scripts/HAP_GSPATDeviceAllocator.cs) | デバイス配分ロジック本体。優先度順に Disabler → Group → DirectionalGrouping を適用 |
| [HAP_AUTDDebugDisabler.cs](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/Assets/Features/Haptics/Scripts/HAP_AUTDDebugDisabler.cs) | デバッグ用デバイス強制無効化コンポーネント |
| [HAP_AUTDDeviceGroup.cs](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/Assets/Features/Haptics/Scripts/HAP_AUTDDeviceGroup.cs) | チェックボックス選択で複数デバイスをグループ化するクラス |
| [HAP_GizmoVisualizer.cs](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/Assets/Features/Haptics/Scripts/HAP_GizmoVisualizer.cs) | デバイス配置・Illusion Group割当の Gizmo 描画ユーティリティ |

---

## 3. デバイス配分の優先順位 (Device Allocation Priority)

`HAP_GSPATDeviceAllocator` における、各焦点へのデバイス割り当て優先順位は以下の通りです：

```
優先度1  HAP_AUTDDebugDisabler
         └─ IsDisabled(dev.ID) == true のデバイスは候補から除外（最優先）
              ↓ 通過したデバイスのみ
優先度2  Illusion Group 指定（assignedDeviceGroup）
         └─ AssignedDeviceIndices に含まれるデバイスのみ candidateDevs として絞り込む
              ↓ 通過した候補デバイスのみ
優先度3  enableDirectionalGrouping（方向グルーピング）
         └─ 角度閾値（directionalAngleThreshold）内のデバイスのみに絞り込む
            角度 NG の場合、候補の中で最も角度が小さいデバイスに強制割り当て
              ↓
         実際に照射するデバイスが確定
```

> **補足**: `enableDirectionalGrouping = false` の場合、優先度3はスキップされ、Disabler で有効かつ Group 指定に含まれる全デバイスに同一焦点が割り当てられます。

---

## 4. コンポーネント詳細 (Component Details)

### 4.1 `HAP_HapticsIllusionFoxFootController`
`HAP_FoxFootHapticsController` を継承した触覚錯覚実験専用コントローラーです。

#### 主要パラメータ
| パラメータ名 | 型 | 説明 |
| :--- | :--- | :--- |
| **`contactDeviceIndex`** | `int` | 接点（足の表面）へ照射を担当する AUTD デバイスのインデックス (例: `0`) |
| **`oppositeDeviceIndex`** | `int` | 接点の反対側（裏側）へ照射を担当する AUTD のインデックス (例: `1`) |
| **`enableOppositeFocus`** | `bool` | 反対側への焦点照射を有効にするかどうか |
| **`contactOffset`** | `Vector3` | 接点側焦点のローカル位置微調整オフセット |
| **`oppositeOffset`** | `Vector3` | 反対側焦点の位置オフセット（法線と逆方向・上方向への距離） |
| **`useSTM`** | `bool` | 時分割焦点回転 (80Hz 等) を使用するかどうか |
| **`stmFrequency`** | `float` | STM 回転の再生周波数 (Hz)。デフォルト `80` |
| **`stmRadius`** | `float` | STM 回転軌跡の半径 (m)。デフォルト `0.005`（5mm） |
| **`stmPoints`** | `int` | 1周期あたりの分割サンプル点数（例: `16`） |

---

### 4.2 `HAP_HapticsIllusionCustomController`
任意の `Transform`（接点、または接点以外の自由位置）に対して、担当デバイスグループ・焦点パラメータを直接定義できる汎用コントローラーです。

#### `HapticsIllusionTargetConfig` 構造体

| フィールド | 型 | 説明 |
|---|---|---|
| `focusName` | `string` | 焦点の識別名（デバッグ / Inspector 表示用） |
| `targetTransform` | `Transform?` | 照射対象の Transform |
| `assignedDeviceGroup` | `HAP_AUTDDeviceGroup` | 担当 AUTD デバイスをチェックボックスで複数選択できるグループ |
| `offsetPosition` | `Vector3` | 表面 / めり込み内側 / 外側のローカルオフセット（m） |
| `isEnabled` | `bool` | この焦点の有効 / 無効 |
| `useSTM` | `bool` | STM（時分割回転）を使用するか |
| `stmFrequency` | `float` | STM 再生周波数 (Hz) |
| `stmRadius` | `float` | STM 回転半径 (m) |
| `stmPoints` | `int` | 1周期あたりの STM 分割点数（4〜64） |
| `focusIntensityPascal` | `float` | 音圧強度 (Pa)。`0` で全系の `defaultIntensityPascal` を使用 |

> **`assignedDeviceGroup` について**: 旧来の `assignedDeviceIndex`（`int`）は下位互換プロパティとして残っています。新規設定では Inspector 上のチェックボックスで複数デバイスをグループ選択してください。

---

## 5. Gizmo 可視化 (Gizmo Visualization)

### 5.1 焦点 Gizmo（`HAP_HapticsIllusionCustomController` の `OnDrawGizmos`）

各 focusConfig について、**デバイス配分の優先順位に従った「実照射可能か」の状態**を色で示します。

| 色 | 状態 | 条件 |
|---|---|---|
| **グレー** | 無効 | `isEnabled = false` |
| **赤** | 照射不可（Disabler） | 担当デバイスが全て `HAP_AUTDDebugDisabler` で無効化されている |
| **橙** | 照射不可（角度NG） | `enableDirectionalGrouping = true` で、担当デバイスが全て角度閾値を超えている |
| **シアン / マゼンタ / 黄 / 緑** | 照射可能 | 上記に該当しない（焦点番号ごとに色が変わる） |

焦点ごとに `Handles.Label` で `AUTD [0,1] [all devices disabled]` のようなラベルも表示されます。

### 5.2 デバイス Gizmo（`HAP_GizmoVisualizer`）

`HAP_AUTDHapticsController.visualizeDevices = true` の状態で Sceneビューに描画されます。

| 表示 | 意味 |
|---|---|
| デバイス外枠（グレー） | `HAP_AUTDDebugDisabler` で無効化されているデバイス |
| デバイス外枠（グループ色） | `enableDirectionalGrouping` のグループ色。グループごとに HSV 色分け |
| **白い内側二重枠** | **Illusion Group 指定に含まれているデバイス（優先度2の可視化）** |
| 上部ラベル `AUTD#0: Contact Point Focus` | Illusion Group 割当の焦点名。`[DISABLED]` サフィックスが付く場合は Disabler で無効 |

> **`ObjectTarget` モード時のみ**: `sourceMode = ObjectTarget` かつアクティブなコントローラーが `HAP_HapticsIllusionCustomController` の場合に、デバイス Gizmo の Illusion Group 表示が有効になります。

---

## 6. セットアップと操作手順 (Usage Guide)

### 手順1: GameObject へのコンポーネント追加
1. 実験用オブジェクトに `HAP_HapticsIllusionCustomController` をアタッチします。
2. Inspector の `Illusion Focus Configurations` リストで `Add New Target Configuration` ボタンを押し、焦点を追加します。

### 手順2: 各焦点の設定
1. **Focus Name**: デバッグ用の識別名を入力します（例: `Contact Point Focus`）。
2. **Target Transform**: 照射先の Transform をドラッグ＆ドロップで設定します。
3. **Assigned AUTD Devices**: チェックボックスで担当 AUTD デバイス（AUTD #0, #1, ...）を選択します。
4. **STM 設定**: `Use STM` を有効にし、`STM Frequency`・`STM Radius` を設定します（推奨: 80Hz / 5mm）。

### 手順3: メインコントローラーでの登録と切り替え
1. `HAP_AUTDHapticsController` の **`Source Mode`** を **`ObjectTarget`** に設定。
2. `Object Target Controllers` リストに作成したコントローラーをドラッグ＆ドロップで追加。
3. **`Active Controller Target`** ドロップダウンで、動作させたい GameObject の名前を選択します。選択されたコントローラーのみ `enabled = true` に同期され、他は非アクティブになります。

### 手順4: デバイス動作の確認（Gizmo）
1. Scene ビューで `HAP_AUTDHapticsController` を選択した状態にします（`visualizeDevices = true`）。
2. 各 Illusion 焦点の Gizmo 色と、デバイスの白内枠ラベルで、**実際に照射されるかどうか**をリアルタイムに確認できます。

---

## 7. 実験・比較検証の推奨プロトコル (Experimental Protocols)

### 実験 A: 手の裏回り込み（回折・反射圧力）の遮断テスト
1. 手の接点サイズ（例: 1辺 5cm の正方形）だけをくり抜いた厚紙を設置。
2. AUTD #1 担当焦点の `isEnabled` を `true` / `false` で切り替え、持っている感の変化を測定。
3. 回り込みを厚紙で阻止した状態で感度が変化するかどうかを検証。

### 実験 B: 焦点形成位置のオフセット比較
1. `offsetPosition` の Z / Y オフセット値を変更：
   - **表面**: `(0, 0, 0)`
   - **わずかに内側（めり込み）**: `(0, 0, -0.003)` 〜 `(0, 0, -0.005)`
   - **外側**: `(0, 0, +0.003)` 〜 `(0, 0, +0.005)`
2. 保持触感の強さとリアリティを被験者評価。

### 実験 C: STM (80Hz) vs 定点照射の比較
1. `useSTM` を `true`（80Hz STM）↔ `false`（同一定点照射）で切り替え。
2. 周波数（`stmFrequency`: 40Hz / 80Hz / 150Hz）や回転半径（`stmRadius`: 3mm / 5mm / 10mm）の影響を検証。

### 実験 D: DirectionalGrouping との組み合わせ検証
1. `enableDirectionalGrouping = true` に設定し、`directionalAngleThreshold` を変更（例: 30° / 45° / 60°）。
2. Gizmo の橙色表示で「角度NG となる焦点」を確認しながら、照射範囲を調整。
3. Group 指定によるデバイス限定と DirectionalGrouping の組み合わせで、最適な照射角度条件を探索。

---

## 8. トラブルシューティング (Troubleshooting)

| 症状 | 原因 | 対処 |
|---|---|---|
| Gizmo 焦点が**赤**になっている | Disabler で担当デバイスが全滅 | `HAP_AUTDDebugDisabler` の設定を確認 |
| Gizmo 焦点が**橙**になっている | DirectionalGrouping で角度NG | `directionalAngleThreshold` を広げるか、デバイス配置を調整 |
| Gizmo 焦点に**ラベルが出ない** | `drawGizmos = false` または GameObjectが非アクティブ | Inspector で `Draw Gizmos` を有効化 |
| デバイスに**白内枠が出ない** | `sourceMode != ObjectTarget` または IllusionController が未選択 | `Source Mode = ObjectTarget` でアクティブなコントローラーを確認 |
| 照射が全デバイスに広がる | Group 指定なし（全デバイスに割り当てされる仕様） | `assignedDeviceGroup` で担当デバイスを明示的に選択 |
