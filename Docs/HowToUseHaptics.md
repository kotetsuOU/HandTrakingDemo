# ハプティクスシステムの使い方ガイド (How to Use Haptics)

> 📂 **親ノード**: [Haptics.md](./Haptics.md) | 🏷️ **種類**: 📖 How-Toガイド  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントは、ハプティクス（空中超音波触覚提示）システムを初めて使う方のためのセットアップガイドです。AUTD3 ハードウェアの接続から、衝突判定の設定、ハプティクス出力の確認までの一連の手順を解説します。

---

## 1. 概要

本システムは、点群センサーデータからの接触判定 (`HCD_Pipeline`) と連動し、AUTD3 ハードウェアを用いた空中超音波触覚フィードバックを提示します。

---

## 2. 設計思想・アーキテクチャ

システム構成および内部アルゴリズムについては以下の設計書を参照してください。

* **全体アーキテクチャ**: [Haptics.md](./Haptics.md)
* **衝突判定パイプライン**: [Collision.md](./Collision.md)
* **SDK 移行仕様**: [AUTD3_SDK_Transition.md](./AUTD3_SDK_Transition.md)

---

## 3. セットアップ・使用方法

### 3.1 事前準備と SDK 切り替え

1. 必要なハードウェア（AUTD3 デバイス、TwinCAT / SOEM 対応 PC、RealSense カメラ）を準備します。
2. ルートディレクトリのスクリプトで SDK バージョンを合わせます。

```powershell
# 現在のSDK環境を確認
powershell -ExecutionPolicy Bypass -File .\switch-sdk.ps1

# 旧SDK (AUTD3Sharp) 環境に切り替え
powershell -ExecutionPolicy Bypass -File .\switch-sdk.ps1 legacy

# 新SDK (autd3-sdk v0.3) 環境に切り替え
powershell -ExecutionPolicy Bypass -File .\switch-sdk.ps1 new
```

### 3.2 AUTD3 デバイスのセットアップ

1. シーン内に `AUTD3Device` コンポーネントをアタッチした GameObject を配置し、物理トランスフォームに合わせます。
2. `HAP_AUTDHardwareController` の **Link Type** (`TwinCAT` / `SOEM` / `Simulator`) を選択します。
3. `HAP_AUTDCalibration` で個別の動作テストおよびアライメント補正を行います。

### 3.3 衝突判定の設定 (`HCD_Pipeline`)

1. シーン内の `HCD_Pipeline` に **Detection Target**（例: Fox）をセットします。
2. `Detection Mode` を選択します (`SkinnedMeshRenderer`, `MeshFilter`, `TransformOnly`)。
3. `AnimationController` の `Auto Update Collision Target` を有効にし自動追従させます。

### 3.4 オブジェクトハプティクス（足先・部位照射）

1. キャラクターに `HAP_FoxFootHapticsController` や `HAP_FoxBodyHapticsController` をアタッチします。
2. `HAP_AUTDHapticsController` の **Source Mode** を `ObjectTarget` に設定します。
3. 詳細な設定手順は [FoxFootHaptics.md](./FoxFootHaptics.md) または [FoxBodyHaptics.md](./FoxBodyHaptics.md) を参照してください。

---

## 4. 仕様・パラメータ詳細

### 4.1 動作モードと主要パラメータ

| モード (`sourceMode`) | 用途 | 説明 |
|:---|:---|:---|
| **`AutoHCD`** | 通常使用 (推奨) | 衝突判定の結果に基づいて自動で超音波を出力 |
| **`ObjectTarget`** | 部位指定 | 登録コントローラーのターゲット座標へ直接照射 |
| **`Manual`** | カスタム制御 | 外部 API で明示的に出力を制御 |

| パラメータ | 説明 | 推奨値 |
|:---|:---|:---|
| `Default Intensity (Pa)` | 出力音圧 | 2000〜5000 |
| `Sine Frequency (Hz)` | 変調周波数 | 200 |
| `Contact Force Reduction` | 接触面積に応じた振幅制御 | ON |

---

## 5. デバッグ・留意事項

### 5.1 トラブルシューティング

| 症状 | 原因 | 対処法 |
|:---|:---|:---|
| 超音波が出力されない | デバイス未接続 / Link Mode 不一致 | TwinCAT / SOEM の接続状態を確認 |
| 触覚が弱い・感じない | 焦点位置がデバイスから離れすぎ | `HAP_GizmoVisualizer` で焦点位置を確認 |
| 衝突判定が反応しない | Detection Target 未設定 | `HCD_Pipeline` の設定を確認 |
| Gizmo が表示されない | Scene ビューの Gizmos が OFF | Scene ビュー上部の Gizmos ボタンを ON に |
| 接触 Gizmo は出るが出力されない | Source Mode 不一致 | `sourceMode` 設定を確認 |

### 5.2 関連ドキュメントリンク
* [Haptics.md](./Haptics.md)
* [Collision.md](./Collision.md)
* [FoxFootHaptics.md](./FoxFootHaptics.md)
* [FoxBodyHaptics.md](./FoxBodyHaptics.md)
* [HapticsIllusion.md](./HapticsIllusion.md)
* [HapticsAlgorithmComparison.md](./HapticsAlgorithmComparison.md)
* [AUTD3_SDK_Transition.md](./AUTD3_SDK_Transition.md)
