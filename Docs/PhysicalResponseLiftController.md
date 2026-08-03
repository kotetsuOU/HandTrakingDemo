# キャラクターリフト・追従コントローラー (PhysicalResponseLiftController) 仕様書

> 📂 **親ノード**: [PhysicalResponse.md](./PhysicalResponse.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、`HCD_Pipeline` によって検出された現実世界の手（点群クラスタ）がキャラクターの足元に近づいた際に、キャラクター全体が手に乗って追従・持ち上げインタラクションを提供する `PR_LiftController` モジュールについて解説します。

---

## 1. 概要

`PR_LiftController` は、キャラクター（Fox 等）の 4 つの足の位置から基準平面（足元の面）を計算し、その平面に対して実空間の手（点群の重心）が近接した際に追従持ち上げおよび自由落下復帰を行うシステムです。

### 主な特徴

* **足元基準平面のリアルタイム算出**: 4 足ボーン位置から接地平面とその法線ベクトルを算出し、近接判定を行います。
* **スムーズな追従 & 自由落下復帰**: 手の移動変位 ($\Delta \mathbf{p}$) にリアルタイム追従し、手が離れた場合は指定 `fallbackPoint` へスムーズに落下移動します。
* **`AnimationController` 自動ターゲット連動**: 表示オブジェクト切り替え (`Tab` キー) 時にターゲット `Transform` と 4 足ボーン参照を自動的に再アサインします。

---

## 2. 設計思想・アーキテクチャ

### 2.1 生成ファイル・関連構造

```text
Assets/Features/Animation/Scripts/
├── PR_LiftController.cs               # 持ち上げ・落下追従統括
└── AnimationController.cs            # オブジェクト切り替え時の参照自動更新
```

### 2.2 状態遷移および処理フロー

```mermaid
graph TD
    Feet["4足ボーン位置"] --> Planar["基準平面 & 法線算出"]
    Hand["HCD 手の点群重心"] --> Check{"距離 <= contactThreshold?"}
    Planar --> Check

    Check --> |Yes (接触)| Contact["【接触中】 手の変位に追従移動"]
    Check --> |No (離脱)| Fall["【非接触】 Fallback Point へ自由落下"]

    style Check fill:#f5a623,color:#fff
    style Contact fill:#50e3c2,color:#000
    style Fall fill:#4a90d9,color:#fff
```

---

## 3. セットアップ・使用方法

### 3.1 クイックスタート手順

#### Step 1: コンポーネントのアタッチ

キャラクター管理 GameObject に `PR_LiftController` をアタッチします。

#### Step 2: インスペクターパラメータ設定

| 設定項目 | 型 | 既定値 | 説明 |
|---|---|---|---|
| `targetTransform` | `Transform` | `null` | 追従・移動させる対象オブジェクトの Root |
| `contactThreshold` | `float` | `0.05f` | 接触判定とみなす距離閾値 (m) |
| `liftSensitivity` | `float` | `1.0f` | 持ち上げ追従移動感度倍率 |
| `fallbackPoint` | `Transform` | `null` | 手が離れた際の復帰目標 Transform (`null` 時は初期位置) |
| `fallSpeed` | `float` | `2.0f` | 復帰移動速度 (m/s) |

#### Step 3: ボーンの自動検出

Inspector の **「Auto Detect Target & Bones」** ボタンをクリックして足ボーン参照を手動再取得できます。

---

## 4. 仕様・パラメータ詳細

### 4.1 パラメータ・判定仕様

* **トラッキング飛び防御**: クラスタ重心が前フレームから突如大きくジャンプ（0.2m 以上）した場合は、ノイズとして判定し自動的に接触解除・自由落下へ移行します。

---

## 5. デバッグ・留意事項

### 5.1 Gizmo 可視化仕様

* **平面輪郭線 (WireQuad)**:
  * 黄色 (Yellow): 非接触状態（待機・落下中）
  * 緑色 (Green): 接触状態（手の点群に追従持ち上げ中）
* **平面法線 (Cyan Ray)**: 平面中心から垂直上向きに伸ばして視認性を確保。

### 5.2 統制ログシステム (AppLogManager) との同期

動作ログには `[PhysicalResponse]` プレフィックスが付与されます。詳細については [Logging.md](./Logging.md) を参照してください。
