# ハプティクスシステムの使い方ガイド (How to Use Haptics)

> 📂 **親ノード**: [Haptics.md (AUTD制御システム)](./Haptics.md) | 🏷️ **種類**: 📖 How-Toガイド
>
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントは、ハプティクス（空中超音波触覚提示）システムを初めて使う方のためのセットアップガイドです。AUTD3ハードウェアの接続から、衝突判定の設定、ハプティクス出力の確認までの一連の手順を解説します。

---

## 1. 事前準備

### 必要なハードウェア
- AUTD3 デバイス（1台以上）
- TwinCAT 対応 PC または SOEM 対応ネットワーク環境
- Intel RealSense カメラ（点群取得用、ハプティクスのみのテストでは不要）

### SDK の確認と切り替え
本プロジェクトは AUTD3 SDK の旧版 (Legacy) と新版 (v0.3.0) の両方に対応しています。使用する SDK バージョンに合わせて環境を切り替えてください。

```powershell
# 現在のSDK環境を確認
powershell -ExecutionPolicy Bypass -File .\switch-sdk.ps1

# 旧SDK (AUTD3Sharp) 環境に切り替え
powershell -ExecutionPolicy Bypass -File .\switch-sdk.ps1 legacy

# 新SDK (autd3-sdk v0.3) 環境に切り替え
powershell -ExecutionPolicy Bypass -File .\switch-sdk.ps1 new
```

詳細は [AUTD3_SDK_Transition.md](./AUTD3_SDK_Transition.md) を参照してください。

---

## 2. AUTD3 デバイスのセットアップ

### 2.1 シーン上への配置
1. シーン内に空の GameObject を作成し、`AUTD3Device` コンポーネントをアタッチします。
2. 実際の AUTD3 デバイスの物理的な位置・回転に合わせて Transform を設定します。
3. 複数台使用する場合は、各デバイスに対して同様の手順を繰り返します。

### 2.2 コントローラーの設定
1. シーン内の `HAP_AUTDController` オブジェクト（または新規に作成）を選択します。
2. **Link Mode** を環境に合わせて選択します:
   - **TwinCAT**: 直接接続（推奨）
   - **SOEM**: ネットワーク経由
   - **Simulator**: ハードウェアなしでのテスト
3. **Connected Devices** リストに、Step 2.1 で配置した `AUTD3Device` オブジェクトを登録します。

### 2.3 キャリブレーション
`HAP_AUTDCalibration` コンポーネントを使用して、各デバイスの位置を微調整できます。
- **Emit Focus**: 特定のデバイスのみに焦点を出力し、物理的な位置を確認
- **Device On/Off**: 個別デバイスの出力を一時的に無効化

---

## 3. 衝突判定の設定 (HCD_Pipeline)

### 3.1 基本セットアップ
1. シーン内の `HCD_Pipeline` オブジェクトを選択します（または新規作成してアタッチ）。
2. **Detection Target** に、触覚を感じさせたい仮想オブジェクト（例: Fox）を設定します。
3. **Detection Mode** を選択します:
   - `SkinnedMeshRenderer`: アニメーション付きキャラクター（推奨）
   - `MeshFilter`: 静的メッシュ（球体など）
   - `TransformOnly`: メッシュなし（中心座標からの距離判定）

### 3.2 AnimationController との連携
`AnimationController` の `Auto Update Collision Target` を有効にしておくと、表示オブジェクトの切り替えに自動追従します。

> 💡 **動作確認**: Play モードで Scene ビューを確認し、手（点群）がオブジェクトに触れた際に Gizmo（色付きの球体）が表示されれば、衝突判定は正常に動作しています。

---

## 4. ハプティクス出力の設定

### 4.1 動作モード (Operation Mode)

| モード | 用途 | 説明 |
|:---|:---|:---|
| **AutoHCD** | 通常使用（推奨） | 衝突判定の結果に基づいて自動で超音波を出力 |
| **Manual** | カスタム制御 | 外部スクリプトからAPIで明示的に出力を制御 |

### 4.2 ハプティクス生成モード (Generation Mode)

AutoHCD モード使用時、接触データからどのような触覚パターンを生成するかを選択できます:

- **Centroid (重心)**: 接触面の重心に単一焦点を形成（最も基本的）
- **Ellipse (楕円)**: PCA による接触面の楕円近似で面的な触覚を生成
- **Random**: GPU Reservoir Sampling による不規則な触覚（ザラザラ感）

### 4.3 基本パラメータ

| パラメータ | 説明 | 推奨値 |
|:---|:---|:---|
| `Default Intensity (Pa)` | 出力音圧 | 2000〜5000 |
| `Sine Frequency (Hz)` | 変調周波数 | 200 |
| `Contact Force Reduction` | 接触面積に応じた振幅制御 | ON |

---

## 5. オブジェクトハプティクス（足先照射など）

衝突判定ベースではなく、特定のボーン位置に直接焦点を照射したい場合は、オブジェクトハプティクスコントローラーを使用します。

### 5.1 Fox の足先・尻尾ハプティクスを有効にする
1. Fox オブジェクトに `HAP_FoxFootHapticsController` をアタッチします。
2. `HAP_AUTDController` の **Operation Mode** を `AutoHCD` に、**Holo Algorithm** を `Custom` に設定します。
3. `HAP_AUTDController` の **Object Haptics Controller** に、Step 1 のコンポーネントを参照設定します。
4. 各足・尻尾の有効/無効は、インスペクターのトグルで個別に制御できます。

### 5.2 カスタムオブジェクトへの対応
新しいオブジェクトにハプティクスを追加する手順は、[FoxFootHaptics.md のセクション5](./FoxFootHaptics.md#5-新しいオブジェクトハプティクスの作成方法) を参照してください。

---

## 6. デバッグ・トラブルシューティング

### よくある問題と対処法

| 症状 | 原因 | 対処法 |
|:---|:---|:---|
| 超音波が出力されない | デバイス未接続 / Link Mode 不一致 | TwinCAT / SOEM の接続状態を確認 |
| 触覚が弱い・感じない | 焦点位置がデバイスから離れすぎ | `HAP_GizmoVisualizer` で焦点位置を確認 |
| 衝突判定が反応しない | Detection Target 未設定 | `HCD_Pipeline` の設定を確認 |
| Gizmo が表示されない | Scene ビューの Gizmos が OFF | Scene ビュー上部の Gizmos ボタンを ON に |
| 接触 Gizmo が出るが音が出ない | `HAP_AUTDController` の Operation Mode が `Manual` | `AutoHCD` に変更 |

### デバッグ可視化ツール

- **`HAP_GizmoVisualizer`**: デバイス配置・焦点割り当ての可視化
- **`HAP_AUTDDebugDisabler`**: デバイス個別の出力無効化
- **`HCD_Pipeline` の Gizmo**: 衝突クラスタの重心・法線の可視化

---

## 7. 関連ドキュメント

| ドキュメント | 内容 |
|:---|:---|
| [Haptics.md](./Haptics.md) | AUTD制御システムの設計思想・アルゴリズム詳細 |
| [Collision.md](./Collision.md) | 衝突判定パイプライン（HCD）の設計思想 |
| [FoxFootHaptics.md](./FoxFootHaptics.md) | Fox足先ハプティクスの仕様・カスタムオブジェクト作成方法 |
| [HapticsAlgorithmComparison.md](./HapticsAlgorithmComparison.md) | 旧ネイティブ vs 新C#のアルゴリズム比較 |
| [AUTD3_SDK_Transition.md](./AUTD3_SDK_Transition.md) | AUTD3 SDK 新旧仕様比較と切り替え方法 |
