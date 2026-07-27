# キャラクターリフト・追従コントローラー (PhysicalResponseLiftController) 仕様書

> 📂 **親ノード**: [PhysicalResponse.md](./PhysicalResponse.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、`HCD_Pipeline` によって検出された現実世界の手（点群クラスタ）がキャラクターの足元に近づいた際に、キャラクター全体が手に乗って追従・持ち上げインタラクションを提供する `PR_LiftController` モジュールについて解説します。

---

## 1. 概要

`PR_LiftController` は、キャラクター（Fox 等）の 4 つの足の位置から基準平面（足元の面）を計算し、その平面に対して実空間の手（点群の重心）が近接した際に追従持ち上げおよび自由落下復帰を行うシステムです。

---

## 2. 設計思想・アーキテクチャ

### 2.1 コアロジックと状態遷移

```text
[4つの足の座標] ──> [足元基準平面の算出 (法線 Vector)]
                           │
                           ▼
 [HCD_Pipeline の手の重心] ──> [距離が Contact Threshold 以下の判定]
                           │
             ┌─────────────┴─────────────┐
             ▼                           ▼
    【接触中 (Contacting)】     【非接触/離脱 (Release)】
  手の変位(delta)に追従移動     Fallback Point へ自由落下
```

---

## 3. セットアップ・使用方法

### 3.1 セットアップと自動検出 (Auto Detect)

1. キャラクター管理オブジェクトに `PR_LiftController` をアタッチします。
2. アタッチ時およびゲーム開始時に、`AnimationController` からアクティブな描画対象 `targetTransform` と足のボーン Transform を自動検索・割り当てます。
3. Inspector の **「Auto Detect Target & Bones」** ボタンをクリックして手動再取得も可能です。

---

## 4. 仕様・パラメータ詳細

### 4.1 インスペクター設定パラメータ

* **Target Settings**:
  * `targetTransform`: 実際に移動させる対象の Root オブジェクト。
* **Foot Bone Transforms & Toggles**:
  * `frontLeftFoot`, `frontRightFoot`, `backLeftFoot`, `backRightFoot`: 基準平面構成ボーン。
  * `enableFrontLeftFoot` 等: 各足を平面計算に含めるかのトグル。
* **Lift Settings**:
  * `contactThreshold`: 接触判定しきい値 (m)。
  * `liftSensitivity`: 追従移動感度倍率（通常 `1.0`）。
* **Fall Settings**:
  * `fallbackPoint`: 非接触時に落下・復帰する目標 Transform。未指定時は初期位置。
  * `fallSpeed`: 復帰移動速度 (m/s)。

---

## 5. デバッグ・留意事項

### 5.1 Gizmo 可視化
* **平面輪郭線**:
  * 黄色 (Yellow): 非接触状態（待機・落下中）
  * 緑色 (Green): 接触状態（手の点群に追従中）
* **平面法線**:
  * 中心から上向きに伸びるシアン色 (Cyan) の短い線で追従基準軸を表示。

### 5.2 留意事項
* クラスタ重心が突如大きくジャンプ（0.2m 以上）した場合は、トラッキング飛びとして自動的に接触解除され落下復帰が作動します。
