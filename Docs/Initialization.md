# 初期化とアライメント・キャリブレーションシステム 仕様書

> 📂 **親ノード**: [Wiki.md](./Wiki.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、RealSense カメラから取得した点群データの初期化処理、および複数カメラ間のアライメント（位置合わせ）を行うキャリブレーションシステムについて解説します。

---

## 1. 概要

本システムは、複数台の RealSense カメラから取得される点群データの統合、描画用マテリアルの適用、およびキャリブレーション情報の保存・復元を一貫して管理します。

---

## 2. 設計思想・アーキテクチャ

### 2.1 構成コンポーネントとデータフロー

```mermaid
graph TD
    classDef manager fill:#27AE60,stroke:#1E8449,stroke-width:2px,color:#FFFFFF;
    classDef controller fill:#2980B9,stroke:#1A5276,stroke-width:2px,color:#FFFFFF;
    classDef file fill:#E67E22,stroke:#D35400,stroke-width:2px,color:#FFFFFF;

    Manager["📦 RsGlobalPointCloudManager<br/>(点群データの管理・統合ハブ)"]:::manager
    MaterialCtrl["🎨 RsMaterialController<br/>(マテリアル・カラー制御)"]:::controller
    TransformCtrl["⚙️ RsTransformController<br/>(アライメント・JSONセーブロード)"]:::controller
    JsonFile["📄 ChildTransforms.json<br/>(位置姿勢設定ファイル)"]:::file

    Manager -->|"GetChildRenderers()"| MaterialCtrl
    Manager -->|"GetChildRenderers()"| TransformCtrl
    TransformCtrl -->|"Save/Load"| JsonFile
```

```mermaid
sequenceDiagram
    actor Developer as 開発者/エディター
    participant TC as RsTransformController
    participant GM as RsGlobalPointCloudManager
    participant Disk as Config/RealSense/*.json

    rect rgb(230, 245, 230)
    note right of Developer: 保存フロー (Save)
    Developer->>TC: Save Transforms to JSON ボタン押下
    TC->>GM: GetChildRenderers() で全子カメラ取得
    GM-->>TC: レンダラー(Transform)リスト返却
    TC->>TC: 各Transform情報を ChildTransformData に変換
    TC->>Disk: JSONシリアライズしてファイル書き込み
    end

    rect rgb(230, 240, 255)
    note right of Developer: 読み込みフロー (Load)
    Developer->>TC: Load Transforms from JSON ボタン押下
    TC->>Disk: JSONファイルの存在確認・デシリアライズ
    Disk-->>TC: 座標データリスト返却
    TC->>GM: GetChildRenderers() で現在のシーン上のカメラ取得
    GM-->>TC: 現在のカメラリスト返却
    TC->>TC: 名前の一致するオブジェクトに Transform を適用
    TC->>Developer: Undo登録 & SceneView再描画通知
    end
```

---

## 3. セットアップ・使用方法

### 3.1 アライメント・キャリブレーション手順

1. **キャリブレーション・ガイドの配置**:
   `RsTransformController` の Inspector で **Show Calibration Guide** にチェックを入れ、緑色のワイヤーフレームボックスをアライメント基準物に合わせます。
2. **各カメラの位置合わせ**:
   各 `RsPointCloudRenderer` の `Transform` を微調整し、点群がガイドボックスと一致するように調整します。
3. **設定の保存 (Save)**:
   `RsTransformController` の **「Save Transforms to JSON」** ボタンをクリックし、`Assets/Settings/Config/RealSense/ChildTransforms.json` に設定をエクスポートします。
4. **設定の復元 (Load)**:
   **「Load Transforms from JSON」** ボタンで保存設定をロード復元します（Unity の Ctrl + Z Undo に対応）。

---

## 4. 仕様・パラメータ詳細

### 4.1 主要コンポーネント

* `RsGlobalPointCloudManager`: 管理対象となる全 `RsPointCloudRenderer` の参照リストを一元管理し、イテレータ (`GetChildRenderers()`) 経由で提供。
* `RsMaterialController`: 動的にレンダラーリストを取得し、カラーモード (`Skin`, `Black`, `Blue`, `Custom`) を制御。
* `RsTransformController`: アライメントガイドの表示、スロット別保存・復元、JSON ファイル管理。

### 4.2 JSON データ構造 (`ChildTransforms.json`)

```json
{
  "transforms": [
    {
      "name": "RealSense_Camera_1",
      "localPosition": { "x": 0.123, "y": -0.456, "z": 1.789 },
      "localRotation": { "x": 0.0, "y": 0.7071068, "z": 0.0, "w": 0.7071068 },
      "localScale": { "x": 1.0, "y": 1.0, "z": 1.0 }
    }
  ]
}
```

---

## 5. デバッグ・留意事項

### 5.1 3Dディスプレイ (`SRDManager`) の位置調整
ハーフミラーを利用した立体視を行う場合、RealSense カメラのアライメントに加えて `SRDManager` の Gizmo（ディスプレイ面枠）を現実のハーフミラー内に反射して見える虚像位置・傾きに正しく一致させる必要があります。詳細は [Display3D.md](./Display3D.md) を参照してください。
