# 衝突判定・クラスタリングシステム (HCD Pipeline) 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る  
> 📎 **関連ドキュメント**: [CollisionAlgorithmComparison.md](./CollisionAlgorithmComparison.md)

本ドキュメントでは、10万点に及ぶ点群データとアニメーションメッシュとの接触判定を、CPU を一切圧迫することなく完全 GPU 完結アーキテクチャで高速処理する `HCD_Pipeline` システムについて解説します。

---

## 1. 概要

本システムは、点群バッファと仮想 3D メッシュとの接触判定・クラスタリング・トラッキングをリアルタイムに実行します。

旧仕様 (`HapCollisionDetectors`) から大幅なリアーキテクチャが行われ、プロセッサをパイプライン状に連結する `HCD_Pipeline` 構造へ進化しました。また、God Class（肥大化クラス）化を防ぐため **`partial` クラスを一切使わず**、単一責任原則に基づき **`Core` / `Processors` / `Debug` / `Editor`** の 4 階層へ完全に役割分離・軽量化されています。

---

## 2. 設計思想・アーキテクチャ

### 2.1 データフローとパイプライン構成

```text
[リアルタイム統合点群バッファ] (RsGlobalPointCloudManager)
               │ (GPU ComputeBuffer 直接参照)
               ▼
[HCD_Pipeline] (コアマネージャー)
               │
               ├─▶ 1. [HCD_DistanceProcessor] 
               │      ├── [HCD_MeshBaker] (SkinnedMesh/MeshFilter の Bake & Combine & Bounds算出)
               │      └── [HCD_SpatialGridBuilder] (GPU 8x8x8 Voxel Grid構築 & Dispatch)
               │
               ├─▶ 2. [HCD_SpatialClusteringProcessor]
               │      (接触した点群を空間ハッシュでクラスタリングし、接触重心・共分散・16点ランダムサンプルを計算)
               │
               ├─▶ 3. [HCD_ReadbackHandler] (GPU AsyncReadback キュー監視)
               │      └── [HCD_ClusterDecoder] (GPU固定小数点数バイナリ構造体の Vector3 デコード)
               │
               ├─▶ 4. [HCD_ClusterTracker] (CPU)
               │      (前フレームのクラスタと照合し、安定したIDと生存期間(Age)・Forceを付与)
               │
               └─▶ 5. [HCD_DebugVisualizer] & [AppLogManager]
                      (Scene ビュー描画 & サブログ別トグル制御)
```

### 2.2 ディレクトリ・ファイル構成

```
Assets/Features/HapticsCollision/Scripts/
├── Core/                               # 【コア機能】
│   ├── HCD_Pipeline.cs                 # プロセッサディスパッチ・パイプラインコア（約160行）
│   ├── HCD_ReadbackHandler.cs          # 非同期 AsyncGPUReadback のキュー管理と完了判定
│   ├── HCD_ClusterDecoder.cs           # GPUバイナリ構造体 (60B/216B) の Fixed-point デコード
│   └── HCD_Processor.cs                # IHCD_Processor インターフェース
├── Processors/                         # 【プロセッサ群】
│   ├── HCD_DistanceProcessor.cs         # 距離判定オーケストレーター
│   ├── HCD_MeshBaker.cs                 # メッシュ結合・Bake・頂点データ抽出・Bounds算出
│   ├── HCD_SpatialGridBuilder.cs       # GPU 8x8x8 空間グリッドの構築・バッファ管理
│   ├── HCD_SpatialClusteringProcessor.cs# 空間ハッシュクラスタリング計算
│   └── HCD_ClusterTracker.cs            # フレーム間クラスタ追跡・Force計算
├── Debug/                              # 【デバッグ・ログ専用機能】
│   ├── HCD_DebugVisualizer.cs          # Scene ビュー Gizmos & Handles ラベル描画コンポーネント
│   └── HCD_LogTriggers.cs              # AppLogManager 連動サブトリガー登録ヘルパー
└── Editor/                             # 【エディタ機能】
    └── HCD_PipelineEditor.cs           # カスタム Inspector エディタ
```

---

## 3. セットアップ・使用方法

1. シーン上の管理オブジェクトに `HCD_Pipeline` をアタッチします。
2. `Awake` 時に `HCD_DebugVisualizer` と `HCD_LogTriggers` が自動アタッチされ、シーンビュー描画とログ制御が準備されます。
3. **`Detection Target`** に接触判定を行わせたい仮想オブジェクトをセットします。
4. **`Detection Mode`** を選択します (`SkinnedMeshRenderer`, `MeshFilter`, `TransformOnly`)。

---

## 4. ログシステム (`AppLogManager`) との連携

HCD モジュールは `AppLogManager` の統合ログ管理に対応しています。`AppLogManager` の Inspector 上で以下の 4 つのサブトリガーを個別 ON/OFF 制御できます：

| サブトリガー表示名 | タグ (`subTag`) | 主な出力内容 |
| :--- | :--- | :--- |
| **`[HCD_Pipeline] Summary & Readback`** | `HCD_Pipeline` | アグリゲーションモード、検出クラスタ数、追跡数 |
| **`[HCD_DistanceProcessor] Mesh & Bounds Debug`** | `HCD_DistanceProcessor` | ターゲットTransform・頂点数・World Bounds・グリッドセルサイズ |
| **`[HCD_SpatialClusteringProcessor] Clustering Debug`** | `HCD_SpatialClusteringProcessor` | ボクセルCellSize・集約モード・距離減衰冪数・Precisionモード |
| **`[HCD_ClusterTracker] Cluster Tracking Info`** | `HCD_ClusterTracker` | 生存・追跡中クラスタ数 |

---

## 5. デバッグ・留意事項

### 5.1 `HCD_DebugVisualizer` による Scene ビュー可視化
`HCD_DebugVisualizer` コンポーネントにより、GPU で検出されたアクティブクラスタ (`GetActiveClusterInfos`) が Scene ビュー上にリアルタイム可視化されます：
* **クラスタ重心 (WireSphere)**: 生存期間 (Age) に応じて黄色（新生）からマゼンタ（安定）へグラデーション。
* **実測座標 vs メッシュ表面座標 (黄色/緑色球 + 直線)**: 点群の実測空間位置 (`RawPointPosition`) とメッシュ表面投影位置 (`MeshSurfacePosition`) の乖離を線描画。
* **法線ベクトル (Cyan Ray)**: 接触パッチの平均表面法線方向。
* **情報ラベル**: `ID`, 生存フレーム数 `Age`, 接触強度 `F`, クラスタ内最小表面距離 `MinD: X.Xmm` を表示。

### 5.2 留意事項
* `AsyncGPUReadback` により CPU との同期待ちを非同期化しており、1〜2 フレームの読込ラグが存在します。
* メッシュのポリゴン数が極めて多い場合は、簡易メッシュを判定用ターゲットとしてアサインすることを推奨します。
