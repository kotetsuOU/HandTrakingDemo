# キーボード操作対応表 (Keyboard Controls)

研究・実験時の撮影やデモを効率化するために、`AnimationController.cs`に以下のショートカットキーがアサインされています。
このスクリプトはヒエラルキー上のどこか（例えば `Main Camera` や `GameManager`）にアタッチし、インスペクターから操作したい動的オブジェクト（キツネのTransformやAnimator）をセットして使用します。

| アクション | キー (Key) | 詳細 |
|:---|:---|:---|
| **撮影 (Screenshot)** | `Enter` / `Return` | 「オクルージョンDebugMap」「ピクセルTagマップ」「統合DepthMap」「近傍探索範囲マップ」「近傍点カウント数」「現在のカメラビュー」を同時保存します（保存先: `Assets/HandTrackingData/OcclusionMaps` / `Assets/HandTrackingData/PixelTagMaps` / `Assets/HandTrackingData/DepthMaps/Integrated`） |
| **表示オブジェクトの順番切り替え** | `Tab` | `toggleObjects` の配列に設定されたオブジェクトを順番に一つずつアクティブにして切り替えます |
| **アニメーション再生/停止** | `Space` | 対象のAnimatorの `speed` を 0 と 1 でトグルし、一時停止させます（被写体を止めて撮影したい時に便利です） |
| **手法の一括切り替え (Method)** | `M` | すべての提案手法（①～④）をまとめてON/OFFし、従来と提案の設定を瞬時比較します |
| **① タグによるスキップ (Tag)** | `1` | `Enable Tag Based Optimization` を切り替えます (冗長な自己遮蔽計算をスキップし効率化)/SICE FES 2026発表内容 |
| **② 密度計算の補正 (Density)** | `2` | `Enable Type Aware Density` を切り替えます (従来手法のカウント漏れ・過剰を修正) |
| **③ ソフトフェード (SoftFade)** | `3` | `Enable Soft Occlusion Fade` を切り替えます (エッジのグラデーションスムージング) |
| **④ 穴埋め補完 (HoleFilling)** | `4` | `Enable Joint Bilateral Hole Filling` を切り替えます (透過ノイズの修復) |
| **PixelTag Map (Material ID Debug)** | `P` | `Enable Pixel Tag Map` を切り替えます。ON のときデバッグ配色ルールで画面上に常時可視化します |
| **Occlusion Map** | `O` | `Enable Occlusion Map` を切り替えます。内積計算で得た `occlusionAverage(0~1)` を `Record Occlusion Debug Map` と同じ配色ルールで常時可視化します |
| **滑らかさ幅の強制設定** | `T` | `Occlusion Fade Width` の実数値を `0.2` (滑らか) と `0.0` (くっきり) で設定します |
| **カーネル関数の切り替え (Kernel)** | `L` | オクルージョン計算に用いるカーネル関数 (`Bouchiba`, `Exponential`, `Linear`) を順に切り替えます |
| **ビニング手法の切り替え (Binning)** | `K` | 空間分割時の重みの計算手法 (`Soft`, `Hard`) を切り替えます |
| **空間分割数の切り替え (Direction)** | `J` | 空間の分割方向数 (`Single`, `Bins3`, `Bins6`, `Bins8`) を順に切り替えます |
| **カラーモードの切り替え (Color)** | `C` | 点群のカラーモード (`Skin`, `Black`, `Blue`, `Custom`) を順に切り替えます (`RsMaterialController` 内の `ChangeColorMode` を使用) |
| **ゲーム終了 (Quit)** | `Esc` | エディタ再生、またはビルド後のアプリを終了させます (`QuitGame.cs`の統合) |
| **視点追従の切り替え (LookAt)** | `F` | キャラクターがカメラ（視点）の方を自動で向く機能のON/OFFを切り替えます |

### オブジェクトの移動 (Transform Movement)
対象オブジェクトのTransformがセットされている場合、以下のキーで3D空間内を自由に移動させることができます（移動速度は `moveSpeed` で調整可能）。

* `W` or `↑`: 奥へ移動 (Forward)
* `S` or `↓`: 手前へ移動 (Backward)
* `A` or `←`: 左へ移動 (Left)
* `D` or `→`: 右へ移動 (Right)
* `E` : 上へ移動 (Up)
* `Q` : 下へ移動 (Down)

### HCD パイプラインとの連携 (Collision Target Auto-Update)
インスペクター上の `Auto Update Collision Target` を有効（デフォルト: `true`）にしておくと、`AnimationController` 側で表示中のオブジェクトを切り替えた際（またはエディタ上で非プレイ時に設定を変更した際）、自動的に `HCD_Pipeline` の接触判定対象が追従・更新されます。

- **`SkinnedMeshRenderer` を持つオブジェクト**: アニメーション用のメッシュ表面で判定 (`DetectionMode.SkinnedMeshRenderer`)
- **`MeshFilter` を持つオブジェクト**: 通常の球などの静的メッシュ表面で判定 (`DetectionMode.MeshFilter`)
- **どちらも持たないオブジェクト**: 中心座標からの距離で判定 (`DetectionMode.TransformOnly`)

※ Auto Updateが有効な間は、設定の競合を防ぐため `HCD_Pipeline` 側の対象設定UIはグレーアウトされ編集不可となります。手動で特定の対象を検証したい場合は、`AnimationController` の `Auto Update Collision Target` のチェックを外してください。
