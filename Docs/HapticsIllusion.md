# Haptics Illusion (触覚錯覚・独立単焦点実験モジュール) ドキュメント

## 1. 概要 (Overview)

`HapticsIllusion` は、超音波焦点による「持っている感（保持触覚）」の向上メカニズムを物理的・生理学的に検証するために設計された、**干渉なし独立多重単焦点照射モジュール**です。

### 開発背景と実験目的
80HzのSTM（Spatio-Temporal Modulation: 時分割焦点円運動）を提示した際、手への触感が著しく向上する現象が確認されています。この原因として：
- **GSPAT等による複数焦点形成時の波の干渉効果か？**
- **単焦点自体の音圧や、手の接点・裏側への回り込み（反射・回折圧力）による物理的増幅か？**
- **焦点の形成位置（手の表面 / めり込み内側 / 外側）や再生パラメータ（80Hz STM vs 定点）の影響か？**

をクリアに切り分けて実験・比較検証するために、本モジュールが開発されました。

### 主な特徴
1. **干渉計算 (GSPAT) のバイパス**:
   複数のAUTDデバイス（例: AUTD #0 と AUTD #1）が存在する場合、合成ホログラム計算を行わず、**各AUTDに専用の単焦点（Focus / FocusSTM）をダイレクトに割り当てて出力**します。
2. **接点側 ＆ 反対側（裏側）の対向照射**:
   1台のAUTD (AUTD #0) から接点表面へ、もう1台のAUTD (AUTD #1) から接点の反対側（裏側）へ同時に単焦点/STMを照射可能。
3. **既存の足検知・判定ロジックの100%継承**:
   `HAP_FoxFootHapticsController` のボーン自動検出、手との接触距離判定、空中判定などのロジックをそのまま活用。
4. **クリーンな排他切替（GameObject名ドロップダウン）**:
   `HAP_AUTDHapticsController` の Inspector 上で、アタッチされている GameObject の名前を選択するだけで、照射対象となるコントローラーを即座かつ安全に排他切り替え可能。

---

## 2. ディレクトリ・ファイル構成 (File Structure)

```
Assets/Features/Haptics/
├── Scripts/
│   └── HapticsIllusion/
│       ├── HAP_HapticsIllusionFoxFootController.cs   # Foxの足検知継承・対向照射モデル
│       └── HAP_HapticsIllusionCustomController.cs    # 任意Target用の汎用独立焦点モデル
└── Scripts/
    └── Editor/
        └── HapticsIllusion/
            ├── HAP_HapticsIllusionFoxFootControllerEditor.cs # FoxFoot用カスタムEditor
            └── HAP_HapticsIllusionCustomControllerEditor.cs  # Custom用カスタムEditor
```

---

## 3. コンポーネント詳細 (Component Details)

### 3.1 `HAP_HapticsIllusionFoxFootController`
`HAP_FoxFootHapticsController` を継承した触覚錯覚実験専用コントローラーです。

#### 主要パラメータ
| パラメータ名 | 型 | 説明 |
| :--- | :--- | :--- |
| **`contactDeviceIndex`** | `int` | 接点（足の表面）へ照射を担当する AUTD デバイスのインデックス (例: `0` = AUTD #0) |
| **`oppositeDeviceIndex`** | `int` | 接点の反対側（裏側/回り込み位置）へ照射を担当する AUTD のインデックス (例: `1` = AUTD #1) |
| **`enableOppositeFocus`** | `bool` | 反対側（裏側）への焦点照射を有効にするかどうか (`true` / `false`) |
| **`contactOffset`** | `Vector3` | 接点側焦点のローカル位置微調整オフセット |
| **`oppositeOffset`** | `Vector3` | 反対側焦点の位置オフセット（法線と逆方向・上方向への距離） |
| **`useSTM`** | `bool` | 時分割焦点回転 (80Hz等) を使用するかどうか (`false` で定点照射) |
| **`stmFrequency`** | `float` | STM回転の再生周波数 (Hz)。デフォルト `80` (Hz) |
| **`stmRadius`** | `float` | STM回転軌跡の半径 (メートル)。デフォルト `0.005` (5mm) |
| **`stmPoints`** | `int` | 1周期あたりの分割サンプル点数 (例: `16`) |

---

### 3.2 `HAP_HapticsIllusionCustomController`
任意の `Transform`（接点、または接点以外の自由位置）に対して、デバイスインデックス・焦点パラメータを直接定義できる汎用コントローラーです。

#### `HapticsIllusionTargetConfig` 構造体
- `targetTransform`: 照射対象の Transform
- `assignedDeviceIndex`: 担当する AUTD のインデックス (`0`, `1`, `2`...)
- `offsetPosition`: 表面 / めり込み内側 / 外側のオフセット
- `useSTM` / `stmFrequency` / `stmRadius`: STM回転パラメータ

---

## 4. デバイス配分ロジック (Device Allocation Logic)

### 焦点データの拡張 (`ClusterFociData`)
[HAP_FociGenerator.cs](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/Assets/Features/Haptics/Scripts/HAP_FociGenerator.cs) 内の `ClusterFociData` に明示的デバイス割り当てプロパティ `public int AssignedDeviceIndex = -1;` が用意されています。
- `-1`: 自動割り当て（従来通り / 方向グルーピングまたは全デバイス共有）
- `0, 1, 2...`: 指定したインデックスの AUTD デバイスに限定割り当て

### 干渉考慮なしダイレクト送信 (`HAP_GSPATDeviceAllocator`)
[HAP_GSPATDeviceAllocator.cs](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/Assets/Features/Haptics/Scripts/HAP_GSPATDeviceAllocator.cs) では、`AssignedDeviceIndex >= 0` の焦点データが存在する場合、他の焦点との GSPAT 多焦点合成を行わず、対象デバイス専用の `Focus` / `FocusSTM` コマンド（Datagram）を構築して独立送信します。

---

## 5. セットアップと操作手順 (Usage Guide)

### 1. GameObject へのコンポーネント追加
1. Fox キャラクター、または実験用オブジェクトに `HAP_HapticsIllusionFoxFootController` をアタッチします。
2. Inspector の `Auto Detect Bones` ボタンを押して、足ボーンを自動バインドします。

### 2. 照射パラメータの設定
- **Contact AUTD Index**: `0` （1台目の超音波デバイス）
- **Opposite AUTD Index**: `1` （2台目の超音波デバイス）
- **Enable Opposite Focus**: `true`
- **Opposite Offset**: `(0, 0.03, 0)` （反対側へ3cmずらす）
- **Use STM**: `true` / **STM Frequency**: `80` (Hz)

### 3. メインコントローラーでの登録と切り替え
1. `HAP_AUTDHapticsController` の **`Source Mode`** を **`ObjectTarget`** に設定。
2. `Object Target Controllers` リストに作成したコントローラーをドラッグ＆ドロップで追加。
3. **`Active Controller Target`** ドロップダウンで、動作させたい GameObject の名前を選択します。選択されたオブジェクトのコンポーネントのみが自動的に `enabled = true` に同期され、他は非アクティブになります。

---

## 6. 実験・比較検証の推奨プロトコル (Experimental Protocols)

### 実験 A: 手の裏回り込み（回折・反射圧力）の遮断テスト
1. 手の接点サイズ（例: 1辺5cmの正方形）だけをくり抜いた厚紙を設置。
2. `enableOppositeFocus = true` (AUTD #1 から反対側照射) と `false` を切り替え、持っている感の変化を測定。
3. 回り込みを厚紙で阻止した状態で感度が変化するかどうかを検証。

### 実験 B: 焦点形成位置のオフセット比較
1. `oppositeOffset`（または `contactOffset`）の Z/Y オフセット値を変更。
   - **表面**: `0mm`
   - **わずかに内側（めり込み）**: `-3mm` 〜 `-5mm`
   - **外側**: `+3mm` 〜 `+5mm`
2. 保持触感の強さとリアリティを被験者評価。

### 実験 C: STM (80Hz) vs 定点照射の比較
1. `useSTM` を `true` (80Hz STM) ↔ `false` (同一定点照射) で切り替え。
2. 周波数 (`stmFrequency`: 40Hz / 80Hz / 150Hz) や回転半径 (`stmRadius`: 3mm / 5mm / 10mm) の影響を検証。
