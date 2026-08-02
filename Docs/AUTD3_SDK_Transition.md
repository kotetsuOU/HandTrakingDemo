# AUTD3 SDK 新旧仕様比較ドキュメント (v3.x/v38 ➔ v31/v0.3.0) 仕様書

> 📂 **親ノード**: [Haptics.md](./Haptics.md) | 🏷️ **種類**: 🔧 SDK移行ガイド  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、本システムでサポートしている AUTD3 SDK の**旧仕様 (Legacy SDK: AUTD3Sharp v38/v3.x系)**と**新仕様 (New SDK: v31/v0.3.0系)**における API、設計思想、名前空間、実装方法の違い、およびプロジェクトでの切り替え手順について解説します。

---

## 1. 概要

本プロジェクトは、旧 SDK 環境と新 SDK 環境の両方で同じコードベースを検証・動作させられるよう、コンパイルシンボル `USE_AUTD3_LEGACY` による条件付きコンパイル (`#if USE_AUTD3_LEGACY`) を全面的に導入しています。

### 主な特徴

* **双方向 SDK 互換アーキテクチャ**: `#if USE_AUTD3_LEGACY` 条件分岐により、旧 SDK (v38) と新 SDK (v31) の両環境でそのままビルド・動作可能です。
* **ワンタップ自動切り替えスクリプト**: PowerShell スクリプト [switch-sdk.ps1](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/switch-sdk.ps1) を用意し、Package Manager 依存関係と Player Settings シンボルを一括トグル切り替えできます。
* **非同期 Client 通信への進化**: 新 SDK では非同期 `async/await` 通信モデルを採用し、ネットワーク送信によるメインスレッドのブロッキングを排除しています。

---

## 2. 設計思想・アーキテクチャの比較

### 2.1 関連構成スクリプト

```text
RealTimeOcclusion/
├── switch-sdk.ps1                     # SDK一括切り替え PowerShell スクリプト
└── Assets/Features/Haptics/Scripts/Core/
    ├── HAP_DeviceController.cs        # 条件付きコンパイル(#if USE_AUTD3_LEGACY)実装
    └── HAP_GeometryBuilder.cs         # ジオメトリ構築の抽象化レイヤー
```

### 2.2 アーキテクチャ比較図

```mermaid
graph TD
    subgraph "旧 SDK (v38 / USE_AUTD3_LEGACY)"
        Ctrl1["Controller (ジオメトリ+通信の同居)"] --> |Send (ブロッキング同期送信)| HW1["AUTD3 Hardware"]
    end

    subgraph "新 SDK (v31 / v0.3.0)"
        Geom2["Geometry (純粋な物理配置データ)"]
        Client2["Client (非同期通信用)"] --> |SendCheckedAsync (非ブロッキング)| HW2["AUTD3 Hardware"]
    end

    style Ctrl1 fill:#f5a623,color:#fff
    style Client2 fill:#50e3c2,color:#000
```

---

## 3. セットアップ・使用方法

### 3.1 SDK環境の自動切り替え手順 (`switch-sdk.ps1`)

ルートディレクトリスクリプト [switch-sdk.ps1](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/switch-sdk.ps1) を使用した切り替え手順です。

#### Step 1: PowerShell 起動と引数指定

Unity エディタを閉じた状態で、PowerShell から以下のコマンドを実行します。

```powershell
# 新 SDK (v31 / v0.3.0) へ切り替える場合
.\switch-sdk.ps1 -Target new

# 旧 SDK (v38 / v3.x) へ切り替える場合
.\switch-sdk.ps1 -Target legacy
```

#### Step 2: 変更の確認と Unity 起動

スクリプト実行により、`Packages/manifest.json` のパッケージ参照および `ProjectSettings/ProjectSettings.asset` の `scriptingDefineSymbols` (`USE_AUTD3_LEGACY`) が自動更新されます。Unity を再起動して完了します。

---

## 4. 仕様・パラメータ詳細

### 4.1 新旧 API 仕様比較表

| 項目 | 旧仕様 (v38 / `USE_AUTD3_LEGACY`) | 新仕様 (v31 / v0.3.0) |
|---|---|---|
| **名前空間** | `AUTD3Sharp` | `AUTD3Sharp` / `AUTD3Sharp.Utils` |
| **接続管理** | `Controller` クラス | `Client` クラス |
| **送信 API** | `controller.Send(group)` (同期ブロッキング) | `await client.SendCheckedAsync(data)` (非同期) |
| `Focus` 生成 | `new Point(x, y, z)` | `ControlPoints.FromPoint(Vector3)` |
| `Modulation` | `new Static()` / `new Sine()` | `Modulation.Static()` / `Modulation.Sine()` |

---

## 5. デバッグ・留意事項

### 5.1 トラブルシューティング

* **コンパイルエラーが発生する場合**: `USE_AUTD3_LEGACY` シンボルの追加・削除と `manifest.json` 内の AUTD3Sharp バージョンが一致しているか `switch-sdk.ps1` を再実行して確認してください。

### 5.2 統制ログシステム (AppLogManager) との同期

動作ログには `[Haptics]` プレフィックスが付与されます。詳細については [Logging.md](./Logging.md) を参照してください。
