# キーボード操作対応表 (Keyboard Controls) 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 📖 リファレンスガイド  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、デモや実験撮影時の操作を効率化するために `AnimationController` に実装されているキーボードショートカットおよびオブジェクト操作仕様について解説します。

---

## 1. 概要

`AnimationController` は、キーボード入力に応じてターゲットオブジェクトの表示切り替え、アニメーション停止、撮影、オクルージョンパラメータやカラースイッチなどの主要操作をインゲームで即座に実行するデバッグ・実験制御スクリプトです。

---

## 2. 設計思想・アーキテクチャ

本スクリプトは、ヒエラルキー上の管理オブジェクト（`Main Camera` や `GameManager` 等）にアタッチし、インスペクターから操作対象の `Transform` や `Animator` をバインドして使用します。

### HCD パイプライン連携アーキテクチャ
インスペクター上の `Auto Update Collision Target` を有効（既定値: `true`）にしておくと、`AnimationController` 側で表示オブジェクトを切り替えた際、自動的に `HCD_Pipeline` の接触判定対象が追従・更新されます。

* **`SkinnedMeshRenderer` を持つオブジェクト**: アニメーション用のメッシュ表面で判定 (`DetectionMode.SkinnedMeshRenderer`)
* **`MeshFilter` を持つオブジェクト**: 通常の静的メッシュ表面で判定 (`DetectionMode.MeshFilter`)
* **どちらも持たないオブジェクト**: 中心座標からの距離で判定 (`DetectionMode.TransformOnly`)

---

## 3. セットアップ・使用方法

### 3.1 セットアップ手順

1. シーン内のオブジェクトに `AnimationController` をアタッチします。
2. `Toggle Objects` リストに切り替えたいオブジェクト群を登録します。
3. 必要に応じて `Target Animator` や `PR_Controller` の参照をインスペクターでアサインします。
4. Play モードに入り、下記の操作キーで動作確認を行います。

---

## 4. 仕様・パラメータ詳細

### 4.1 キーボードショートカット一覧

| アクション | キー (Key) | 詳細 |
|:---|:---|:---|
| **撮影 (Screenshot)** | `Enter` / `Return` | オクルージョン DebugMap、ピクセル Tagマップ、統合 DepthMap、近傍探索範囲マップ等の多種マップを同時間保存 |
| **表示オブジェクト切り替え** | `Tab` | `toggleObjects` 配列のオブジェクトを順番にトグル切り替え |
| **アニメーション再生/停止** | `Space` | Animator の `speed` を `0` と `1` でトグル切り替え |
| **手法の一括切り替え** | `M` | 全ての提案手法（①～④）をまとめて ON/OFF 切替 |
| **① タグによるスキップ** | `1` | `Enable Tag Based Optimization` を切り替え |
| **② 密度計算の補正** | `2` | `Enable Type Aware Density` を切り替え |
| **③ ソフトフェード** | `3` | `Enable Soft Occlusion Fade` を切り替え |
| **④ 穴埋め補完** | `4` | `Enable Joint Bilateral Hole Filling` を切り替え |
| **PixelTag Map** | `P` | `Enable Pixel Tag Map` を切り替え |
| **Occlusion Map** | `O` | `Enable Occlusion Map` を切り替え |
| **滑らかさ幅の強制設定** | `T` | `Occlusion Fade Width` の実数値を `0.2` と `0.0` で切り替え |
| **カーネル関数の切り替え** | `L` | カーネル関数 (`Bouchiba`, `Exponential`, `Linear`) を順次切り替え |
| **ビニング手法の切り替え** | `K` | 重み計算手法 (`Soft`, `Hard`) を切り替え |
| **空間分割数の切り替え** | `J` | 空間の分割方向数 (`Single`, `Bins3`, `Bins6`, `Bins8`) を順次切り替え |
| **カラーモードの切り替え** | `C` | 点群のカラーモード (`Skin`, `Black`, `Blue`, `Custom`) を順次切り替え |
| **ゲーム終了** | `Esc` | エディタ再生、またはビルド後のアプリを終了 |
| **視点追従の切り替え** | `F` | キャラクターがカメラ（視点）方向を自動追従する機能の ON/OFF 切替 |

### 4.2 オブジェクトの Transform 移動操作

対象 Transform がセットされている場合、以下のキーで 3D 空間内を自由に移動させることができます（速度は `moveSpeed` で調整）。

* `W` / `↑`: 奥へ移動 (Forward)
* `S` / `↓`: 手前へ移動 (Backward)
* `A` / `←`: 左へ移動 (Left)
* `D` / `→`: 右へ移動 (Right)
* `E`: 上へ移動 (Up)
* `Q`: 下へ移動 (Down)

---

## 5. デバッグ・留意事項

* `Auto Update Collision Target` が有効な間は、競合を防ぐため `HCD_Pipeline` 側の対象設定 UI がグレーアウトされます。
* 手動で特定ターゲットを固定検証したい場合は、`Auto Update Collision Target` のチェックを解除してください。
