# 物理応答パラメータ制御 (PhysicalResponse)

> 📂 **親ノード**: [Wiki.md (ポータル)](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書
>
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る
>
> 📎 **関連ドキュメント**: [PhysicalResponseLiftController.md](./PhysicalResponseLiftController.md)

`PhysicalResponse` は、Midair Haptics Unity Core における各種物理応答（Physics Response）コンポーネントのパラメータを、実行時に一括で調整・管理するためのコントローラースクリプトです。

本スクリプトは `Assets\Features\Animation\Scripts` 内に配置されており、既存の `AnimationController` などから対象オブジェクトを動的に切り替えて連動させることが可能です。

## 主な機能

1. **リアルタイムパラメータ調整**
   - インスペクター上に整理されたスライダー等のUIを通じて、実行時に即座に物理パラメータ（Stiffness, Damping, Forceなど）を変更できます。
2. **外部スクリプトからのAPI制御**
   - パブリックメソッドを利用し、特定のキー入力やアニメーションイベントのタイミングで、プログラムから直接オブジェクトの柔らかさや反発力を操作できます。
3. **SkinnedMesh 階層の自動検出**
   - 元の SkinnedMesh オブジェクト（例: `Fox`）がターゲットとして渡された場合、Midair Haptics の `Contact Physics Setup` が生成した物理階層（例: `FoxBonePhysics` や `FoxSoftBody`）を兄弟階層やシーン内から自動的に検索し、適切にリンクします。

## 使い方

1. 管理用オブジェクト（`AnimationController` と同じオブジェクトなど）に `PR_Controller` をアタッチします。
2. `AnimationController` 側のインスペクターにある `PR_Controller` フィールドに、アタッチした `PR_Controller` をセットします。
3. Play モードに入ると、`AnimationController` で選択された描画オブジェクトに合わせて、自動的に関連する物理コンポーネントが同期され、インスペクターから値を調整できるようになります。

---

## 公式ドキュメント (`physicsResponse.md`) との実装の差分

`PR_Controller` は公式ドキュメントに記載されている物理パラメータのほぼ全てを網羅していますが、設計上の理由により**意図的に実装（連携）から除外している要素**が存在します。

### 実装から除外した要素とその理由

#### 1. 各ボーンごとの個別設定 (`BonePhysicsInfo`)
- **除外理由**: `BonePhysicsInfo` (`pointForceScale`, `positionSpringScale`, `rotationSpringScale`, `isFixed` など) は、キャラクターの指先や尻尾など**ボーン単位**で細かく設定するためのコンポーネントです。
- 今回のコントローラーは「オブジェクト全体の物理パラメータを一括で操作する」ことを目的としているため、ボーンごとに異なる設定値を一つのスクリプトでオーバーライドしてしまうと、細やかな調整が破壊されてしまうため除外しています。個別のボーンのパラメータは各ボーンの Inspector で直接調整してください。

#### 2. アセットおよび Renderer への静的参照
- **除外要素**: `poseTargetBoneRenderer`, `drivenBoneRenderer`, `modelAsset`, `shapeTargetRenderer`, `displayRenderer`, `clusterBoneRoot`, `preset` (PhysicsProfile / Softbody)
- **除外理由**: これらはすべて、物理シミュレーションの対象となるメッシュやアセットデータなどの「初期セットアップ時に固定されるべき静的な参照データ」です。実行時にスライダーで調整するような「物理パラメータ」ではないため、コントローラーから操作・上書きする対象から外しています。

#### 3. `PhysicsSolver` への登録処理など
- **除外要素**: `PhysicsSolver.profiles` などへの登録処理。
- **除外理由**: Midair Haptics のシステム側 (`InteractionOrchestrator`) がライフサイクルを管理しているため、コントローラー側ではあくまで「パラメータの変更」に専念し、フレームワークのコアな実行ループの管理には介入しない設計としています。

---
*上記以外のチューニングパラメータ（GPU計算パスの切り替えや、Softbodyのシミュレーションステップ数、MoveToStartPosApplierのノイズフォース等）は全て `PR_Controller` のインスペクターから制御可能です。*
