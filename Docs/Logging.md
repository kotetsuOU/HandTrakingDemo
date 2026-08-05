# 統制ログ管理システム (AppLogManager & AppLogger) 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、アプリケーション全体のデバッグログ出力を一元管理し、パフォーマンス低下を防ぎつつモジュール別・機能別のトグル制御を可能にする**統制ログ管理システム (`AppLogManager` & `AppLogger`)** の仕様および実装・利用手順について解説します。

---

## 1. 概要

リアルタイム処理（点群生成・オクルージョン描画・触覚衝突判定等）においては、`Update` や hot path での頻繁なログ出力や GC（Garbage Collection）の発生がパフォーマンス悪化に直結します。

本システムは、各コンポーネントの Inspector を汚すことなく、シーン内の主要モジュール（`HCD`, `RealSense`, `PCD`, `Experiment`, `Haptics`, `PCV` 等）のログ出力を `AppLogManager` インスペクター上で一元かつ階層的に ON/OFF 制御する仕組みを提供します。

* **自動コンテキスト識別（複数アタッチ対応）**: 同一スクリプトが複数 GameObject にアタッチされている場合（例: 複数台の RealSense カメラ）、`AppLogger.Log(this, ...)` 呼び出しによってログプレフィックスが **`[型名: GameObject名]`**（例: `[RsDevice: RealSense_Front]`）へ自動拡張され、出力元オブジェクトを即座に識別可能です。
* **中央集中トグル管理**: モジュールカテゴリ（例: `HCD (Haptic Collision)`, `Experiment`, `PCV (PointCloudViewer)`, `PCD (Occlusion)`, `RealSense`）および個別サブトリガー（例: `[EXP_Manager]`, `[PCV_Controller]`, `[RsDevice] RealSense_Front`）単位でログ有効状態を切り替え可能です。
* **モジュール・トリガー個別での 3 段階トグル制御**: 各コンポーネントおよびサブトリガーエントリーごとに、`Log` (通常情報Log)、`Warn` (警告Log)、`Err` (エラーLog) の 3 段階の有効/無効トグルを個別かつ直感的にコントロール可能です。
* **Inspector の非汚染化**: 個別の `MonoBehaviour` に `public bool enableDebugLog` や `public bool EnableLog` などのトグル変数を定義せず、全制御を `AppLogManager` に統一します。
* **自動スキャン・登録機能**: シーン内の `[AppLoggable]` 属性または `IAppLoggable` インターフェースを持つアクティブコンポーネントを全自動で検出・グループ化します。
* **サブトリガーによる詳細分類**: `IAppLoggable` インターフェースを介して、単一コンポーネントから複数の機能別サブログトリガーを `AppLogManager` へ登録できます。

---

## 2. 設計思想・アーキテクチャ

### 2.1 コンポーネント構成と役割分離

```text
Assets/Core/Scripts/Logging/
├── AppLogger.cs                       # モジュール非依存の静的ログ制御 API
├── AppLogManager.cs                   # シーン内の全ログトリガーを一元管理する MonoBehaviour
└── (各 Feature 配下)
    ├── HCD_LogTriggers.cs             # HCD モジュール用 AppLogManager 連動トリガー
    ├── EXP_LogTriggers.cs             # Experiment モジュール用 AppLogManager 連動トリガー
    ├── HAP_LogTriggers.cs             # Haptics モジュール用 AppLogManager 連動トリガー
    ├── DPC_LogTriggers.cs             # DPC (Dummy Point Cloud) モジュール用連動トリガー
    ├── PCV_LogTriggers.cs             # PointCloudViewer モジュール用 AppLogManager 連動トリガー
    ├── (RealSense / センサーデバイス モジュール)
    │   ├── RsDevice.cs                # [AppLoggable("RealSense (Device)")] カメラデバイス統括
    │   ├── RsDeviceController.cs      # [AppLoggable("RealSense (Device)")] デバイス設定コントローラー
    │   ├── RsGlobalPointCloudManager.cs # [AppLoggable("RealSense (Pipeline)")] 全カメラ統合バッファマネージャー
    │   ├── RsProcessingPipe.cs        # [AppLoggable("RealSense (Pipeline)")] フレームパイプライン統括
    │   └── RsIntegratedPointCloud.cs  # [AppLoggable("RealSense (Pipeline)")] 統合点群生成プロセッサ
    ├── (DPC / ダミー実測点群 モジュール)
    │   ├── RsDummyPointCloudProvider.cs # [AppLoggable("DPC (Dummy Point Cloud)")] ダミー点群供給プロバイダー
    │   ├── RsDummyPointCloudRenderer.cs # [AppLoggable("DPC (Dummy Point Cloud)")] GPU Dirty描画レンダラー
    │   └── RsDummyProcessingPipe.cs     # [AppLoggable("DPC (Dummy Point Cloud)")] ダミーフレームパイプライン
    └── (PCD / 3DDisplay モジュール)
        ├── PCD_LogTriggers.cs                 # PCD モジュール用 AppLogManager 連動トリガー (通常ログ/Recordログ分離)
        ├── PCDOcclusionPipelineController.cs # [AppLoggable / IAppLoggable] オクルージョン統括コントローラー
        ├── PCDContextBuilder.cs               # 事前計算コンテキスト・URP入力・検出ピクセル数ログ (PCD_ContextBuilder)
        ├── PCDMeshRegistrarController.cs     # メッシュ一括登録・同期コントローラー
        ├── PCDPointBufferManager.cs          # 点群・メッシュバッファ管理クラス (PCD_BufferManager)
        ├── PCDDebugReadbackManager.cs        # AsyncReadback キャプチャマネージャー (PCD_RecordDebug)
        ├── PCDOcclusionDebugExporter.cs      # オクルージョンマップ保存クラス (PCD_Exporter)
        └── PCDIntegratedDepthMapExporter.cs  # 統合Depthマップ保存クラス (PCD_Exporter)
```

### 2.2 クラス相関図

```mermaid
graph TD
    AppLogManager["AppLogManager<br/>(MonoBehaviour)"] <--> AppLogger["AppLogger<br/>(Static API Class)"]

    LogTriggersHCD["HCD_LogTriggers<br/>[AppLoggable / IAppLoggable]"] --> |RegisterLogTriggers| AppLogManager
    LogTriggersEXP["EXP_LogTriggers<br/>[AppLoggable / IAppLoggable]"] --> |RegisterLogTriggers| AppLogManager
    LogTriggersHAP["HAP_LogTriggers<br/>[AppLoggable / IAppLoggable]"] --> |RegisterLogTriggers| AppLogManager
    LogTriggersPCV["PCV_LogTriggers<br/>[AppLoggable / IAppLoggable]"] --> |RegisterLogTriggers| AppLogManager
    LogTriggersPCD["PCD_LogTriggers<br/>[AppLoggable / IAppLoggable]"] --> |RegisterLogTriggers| AppLogManager

    Processors["HCD Processors / Core"] --> |AppLogger.Log| AppLogger
    EXPModules["Experiment Modules / Core"] --> |AppLogger.Log| AppLogger
    HAPModules["Haptics Modules / Core"] --> |AppLogger.Log| AppLogger
    PCVModules["PCV Modules / Core"] --> |AppLogger.Log| AppLogger
    PCDModules["PCD Modules / Core / Passes / Debug"] --> |AppLogger.Log| AppLogger

    style AppLogManager fill:#4a90d9,color:#fff
    style AppLogger fill:#f5a623,color:#fff
```

### 2.3 `[AppLoggable]` 属性と `IAppLoggable` の適用ルール

* **エントリポイント / ヘルパーコンポーネントのみに適用**:
  `[AppLoggable("カテゴリー名")]` 属性および `IAppLoggable` インターフェースは、モジュールの窓口コンポーネント（例: `HCD_Pipeline`, `EXP_ExperimentManager`）および専用のトリガー登録ヘルパー（例: `HCD_LogTriggers`, `EXP_LogTriggers`）にのみ付与します。
* **サブコンポーネントへの属性直接付与の禁止**:
  内部コンポーネント（例: `EXP_InputHandler`, `EXP_DataRecorder` 等）には `[AppLoggable]` 属性を付与せず、親ターゲットと定義済みサブタグを指定して `AppLogger.Log` を呼び出します。

---

## 3. セットアップ・使用方法

### 3.1 既存モジュールでのログ出力方法

ログを出力したい C# スクリプトでは、`Core.Logging` 名前空間を `using` し、`Debug.Log` の代わりに `AppLogger` の静的メソッドを呼び出します。

```csharp
using UnityEngine;
using Core.Logging;
using Features.Experiment.Debug;

public class EXP_InputHandler : MonoBehaviour
{
    private void Respond(string responseValue)
    {
        // ターゲット (this または manager) と定義済みサブタグを指定してログ出力
        AppLogger.Log(this, EXP_LogTriggers.TagInputHandler, $"Respond('{responseValue}') 発火");
    }
}
```

### 3.2 新規モジュールでの `IAppLoggable` トリガー登録手順

#### Step 1: ログトリガーヘルパーの作成

`Assets/Features/<FeatureName>/Scripts/Debug/` 配下に `LogTriggers` スクリプトを作成し、`[AppLoggable("グループ名")]` 属性と `IAppLoggable` を実装します。

```csharp
using System.Collections.Generic;
using UnityEngine;
using Core.Logging;

namespace Features.MyFeature.Debug
{
    [AppLoggable("My Feature Group")]
    [DisallowMultipleComponent]
    public class MyFeature_LogTriggers : MonoBehaviour, IAppLoggable
    {
        public const string TagCore = "MyFeature_Core";
        public const string TagProcessor = "MyFeature_Processor";

        public void RegisterLogTriggers(LogCategoryGroup group, HashSet<string> existingLabels)
        {
            var manager = GetComponent<MyFeatureManager>() ?? FindFirstObjectByType<MyFeatureManager>();
            Object targetObj = manager != null ? (Object)manager : this;

            AddSubTriggerIfNotExists(group, targetObj, "[MyFeature] Core Manager", TagCore, existingLabels);
            AddSubTriggerIfNotExists(group, targetObj, "[MyFeature] Data Processor", TagProcessor, existingLabels);
        }

        private void AddSubTriggerIfNotExists(LogCategoryGroup group, Object targetObj, string label, string tag, HashSet<string> existingLabels)
        {
            if (!existingLabels.Contains(label))
            {
                group.entries.Add(new LogInstanceEntry
                {
                    label = label,
                    tag = tag,
                    target = targetObj,
                    enabled = true
                });
                existingLabels.Add(label);
            }
        }
    }
}
```

#### Step 2: メインマネージャーでの自動アタッチ

メインの `MonoBehaviour` クラスの `Awake()` にて、ヘルパーコンポーネントを自動アタッチします。

```csharp
void Awake()
{
    if (GetComponent<MyFeature_LogTriggers>() == null)
    {
        gameObject.AddComponent<MyFeature_LogTriggers>();
    }
}
```

---

## 4. 仕様・パラメータ詳細

### 4.1 `AppLogger` API リファレンス

| 型 / メソッド名 | 引数 / 構造 | 説明 |
|---|---|---|
| `AppLogLevel` (enum) | `Info` (0), `Warning` (1), `Error` (2) | ログメッセージの重要度レベルを表す列挙型 |
| `IsEnabled` | `(Object context, string subTag = null)` | `Info` レベルで指定コンポーネント/サブタグのログが有効か判定 |
| `IsEnabled` | `(Object context, AppLogLevel level, string subTag = null)` | 指定ログレベルおよびコンポーネント/サブタグのログが有効か判定 |
| `IsEnabled` | `(string nameTag, AppLogLevel level = AppLogLevel.Info)` | 指定ログレベルおよび識別タグ名でログが有効か判定 |
| `Log` | `(Object context, string message)` / `(Object context, string subTag, string message)` | `Info` レベルの通常情報ログを出力 |
| `Log` | `(string nameTag, string message, Object context = null)` | `Info` レベルの名前タグ指定情報ログを出力 |
| `LogWarning` | `(Object context, string message)` / `(Object context, string subTag, string message)` | `Warning` レベルの警告ログを出力 |
| `LogWarning` | `(string nameTag, string message, Object context = null)` | `Warning` レベルの名前タグ指定警告ログを出力 |
| `LogError` | `(Object context, string message)` / `(Object context, string subTag, string message)` | `Error` レベルのエラーログを出力 |
| `LogError` | `(string nameTag, string message, Object context = null)` | `Error` レベルの名前タグ指定エラーログを出力 |

### 4.2 `AppLogManager` パラメータ・仕様

| パラメータ名 | 型 | 既定値 | 説明 |
|---|---|---|---|
| `globalEnableLogging` | `bool` | `true` | アプリケーション全体のログ出力を統括するマスター切替トグル |
| `categoryGroups` | `List<LogCategoryGroup>` | `-` | モジュールカテゴリー別にグループ化された各ログエントリーのリスト |

#### 4.2.1 `LogInstanceEntry` (個別ログエントリー仕様)

モジュールカテゴリ内の個別のコンポーネント/サブトリガーエントリーごとに、3段階のログレベル制御トグルを保有します。

| パラメータ名 | UIヘッダー表記 | 型 | 既定値 | 説明 |
|---|---|---|---|---|
| `enableInfo` | **Log** | `bool` | `true` | 当該エントリーの通常ログ (`AppLogger.Log`) の個別有効/無効 |
| `enableWarning` | **Warn** | `bool` | `true` | 当該エントリーの警告ログ (`AppLogger.LogWarning`) の個別有効/無効 |
| `enableError` | **Err** | `bool` | `true` | 当該エントリーのエラーログ (`AppLogger.LogError`) の個別有効/無効 |

#### 4.2.2 ログレベル別一括操作機能 (Global & Category Level)

* **アプリ全体の一括制御**: Inspector 上部のボタン (`Log ON/OFF`, `Warn ON/OFF`, `Err ON/OFF`) で、全モジュールの特定ログレベルのみをワンクリックで一括 ON / 全OFF 切り替え可能です（例: `Log OFF` で通常のデバッグログを一括非表示にし、`Warn` と `Err` を保持）。
* **カテゴリ単位の一括制御**: 各カテゴリグループ内の列見出しボタン (`Log`, `Warn`, `Err`) をクリックすることで、そのカテゴリ内の特定ログレベルのみを一括 ON / 全OFF トグル切り替え可能です。

---

## 5. デバッグ・留意事項

### 5.1 実行時パフォーマンスと条件判定

`Update` などの hot path 内で文字列結合を含むログを出力する場合は、無駄な文字列アロケーションを防ぐため、事前に `AppLogger.IsEnabled` でチェックを行うかフレーム数条件を併用することを推奨します。

```csharp
if (AppLogger.IsEnabled(this, HCD_Pipeline.TagDistanceProcessor) && Time.frameCount % 120 == 0)
{
    AppLogger.Log(this, HCD_Pipeline.TagDistanceProcessor, $"Process Time: {elapsedTime:F2} ms");
}
```

### 5.2 留意事項

* **Inspector トグル変数の個別追加禁止**: 個別の `MonoBehaviour` や C# クラスに `public bool enableDebugLog` や `public bool EnableLog` などを定義することは禁止されています。必ず `AppLogManager` および `AppLogger` を経由してください（PCD モジュールもこれに従い、`PCDPointBufferManager` の `EnableLog` 変数は廃止・一元管理化されました）。
* **直接 `Debug.Log` の使用禁止**: 各 Feature 内のプロダクションコードで直接 `Debug.Log` を呼び出すことは避け、必ず `AppLogger` を使用してください。
* **`IAppLoggable` インターフェース実装の徹底**: `[AppLoggable]` 属性を持つコンポーネントは必ず `IAppLoggable` インターフェースを実装し、各種トリガー定義クラス（例: `PCD_LogTriggers`, `DPC_LogTriggers`）へ委譲・登録を行ってください。未実装の場合、`AppLogManager` のコンポーネント自動スキャン時に `[型名] GameObject名` 形式の不揃いなデフォルトエントリーが生成され、重複表示やサブトリガー不完全認識の原因となります。
* **統一ログプレフィックスの指定**: コンソールログ出力時にクラス名がプレフィックスとして冗長表示されるのを防ぐため、サブトリガーを持つコンポーネントでは `AppLogger.Log(PCD_LogTriggers.TagPipeline, message)` や `AppLogger.Log(DPC_LogTriggers.TagPipe, message, this)` のように識別タグ（`nameTag`）を第1引数に指定する形式を標準として使用してください。
* **PCD モジュールにおける通常ログと Record ログの個別分離**:
  `PCD_LogTriggers` により、以下の 4 つのサブトリガーが `AppLogManager` の `PCD (Occlusion)` カテゴリ下に自動配置され、通常パイプラインログとデータ記録（Record / Readback）ログを個別にトグル切替可能です：
  - `[PCD_Pipeline]`: パイプライン制御、`PCDRenderPass`, `PCDKernelRegistry`
  - `[PCD_BufferManager]`: 点群バッファ更新 (`PCDPointBufferManager`), メッシュ一括同期 (`PCDMeshRegistrarController`)
  - `[PCD_RecordDebug]`: GPU テクスチャデータの AsyncReadback・キャプチャ完了通知 (`PCDDebugReadbackManager`)
  - `[PCD_Exporter]`: PNG 画像 / CSV データファイル書き出し通知 (`PCDOcclusionDebugExporter`, `PCDIntegratedDepthMapExporter`)

