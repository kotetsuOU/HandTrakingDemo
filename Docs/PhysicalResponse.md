# 物理応答パラメータ制御 (PhysicalResponse) 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る  
> 📎 **関連ドキュメント**: [PhysicalResponseLiftController.md](./PhysicalResponseLiftController.md)

本ドキュメントでは、Midair Haptics Unity Core における各種物理応答（Physics Response）コンポーネントのパラメータを、実行時に一括調整・管理する `PR_Controller` スクリプトについて解説します。

---

## 1. 概要

`PR_Controller` は、インスペクターやスクリプト経由で対象オブジェクト（Fox 等）の物理パラメータ（Stiffness, Damping, Force 等）を一括操作するコントローラーです。

---

## 2. 設計思想・アーキテクチャ

`PR_Controller` は、既存の `AnimationController` などから対象オブジェクトを動的に切り替えて連動させることが可能です。

### 主な機能
1. **リアルタイムパラメータ調整**: インスペクターの UI を通じて実行時に物理パラメータを即座に変更。
2. **外部スクリプトからの API 制御**: イベントのタイミングでプログラムから直接オブジェクトの柔らかさや反発力を操作。
3. **SkinnedMesh 階層の自動検出**: ターゲット（例: `Fox`）が渡された際、物理階層（例: `FoxBonePhysics` や `FoxSoftBody`）を兄弟階層やシーン内から自動検索してリンク。

---

## 3. セットアップ・使用方法

1. 管理用オブジェクト（`AnimationController` と同じオブジェクト等）に `PR_Controller` をアタッチします。
2. `AnimationController` 側のインスペクターにある `PR_Controller` フィールドにアタッチしたコンポーネントをセットします。
3. Play モードに入ると、選択された描画オブジェクトに合わせて物理コンポーネントが自動同期され、パラメータ調整が可能になります。

---

## 4. 仕様・パラメータ詳細

### 4.1 公式ドキュメント (`physicsResponse.md`) との実装差分

`PR_Controller` では、「オブジェクト全体の物理一括制御」を目的とする設計上の理由から、以下の要素を意図的に連携対象から除外しています。

#### 1. ボーンごとの個別設定 (`BonePhysicsInfo`)
* **除外理由**: ボーン単位 (`pointForceScale`, `positionSpringScale` 等) の細かな調整を壊さないため。

#### 2. アセットおよび Renderer への静的参照
* **除外要素**: `poseTargetBoneRenderer`, `drivenBoneRenderer`, `modelAsset`, `shapeTargetRenderer`, `displayRenderer`, `clusterBoneRoot`, `preset`
* **除外理由**: 初期セットアップ時に固定される静的データであり、動的パラメータではないため。

#### 3. `PhysicsSolver` への登録処理
* **除外理由**: Midair Haptics システム側 (`InteractionOrchestrator`) がライフサイクルを管理するため。

---

## 5. デバッグ・留意事項

* 上記以外のチューニングパラメータ（GPU 計算パスの切り替え、Softbody シミュレーションステップ数、`MoveToStartPosApplier` のノイズフォース等）はすべて `PR_Controller` のインスペクターから制御可能です。
