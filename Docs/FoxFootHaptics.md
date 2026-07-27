# Fox & Object Haptics (キツネ足先およびオブジェクト追従ハプティクス) 仕様書

> 📂 **親ノード**: [Haptics.md](./Haptics.md) | 🏷️ **種類**: 🏗️ システム設計書  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、キツネ (Fox) の足先・尻尾や動的オブジェクトの特定ボーンアニメーションに合わせて超音波フィードバックを照射する `HAP_FoxFootHapticsController` およびカスタムオブジェクトハプティクスについて解説します。

---

## 1. 概要

`HAP_FoxFootHapticsController` は、基底抽象クラス `HAP_BaseObjectHapticsController` を継承し、4 足歩行キャラクターの足先および尻尾ボーンの位置に連動した焦点照射を行うコンポーネントです。

---

## 2. 設計思想・アーキテクチャ

### 2.1 クラス構造と継承

* **`HAP_BaseObjectHapticsController`**:
  すべての基本設定（STM 設定、手との接触判定、Gizmos 描画設定）と共通ロジックを保持。
* **`HAP_FoxFootHapticsController`**:
  Fox の足・尻尾ボーン (`Fox_F_LLegDigit11` 等) を自動検出し、`TargetInfos` を基底クラスに提供。

### 2.2 照射モードの分類 (STM Mode)

1. **FociSTM (ハードウェア計算・単焦点)**:
   FPGA 側で単焦点を高周波巡回させる超軽量モード。`Track Mode = Sequential` / `Algorithm = Naive` 扱い。
2. **GainSTM (PC計算・複数焦点対応)**:
   PC 側で焦点・位相を計算。`Sequential` (1点ずつ巡回) または `Simultaneous` (同時マルチフォーカス) を選択可能。

---

## 3. セットアップ・使用方法

### 3.1 セットアップ手順

1. Fox モデルの GameObject に `HAP_FoxFootHapticsController` をアタッチします。
2. `HAP_AUTDHapticsController` の `Source Mode` を `ObjectTarget` に、`Holo Algorithm` を `Custom` に設定します。
3. `Object Target Controllers` リストに作成したコントローラーを追加します。

### 3.2 新しいカスタムオブジェクトハプティクスの作成方法

新しい動的オブジェクト（鳥、小道具等）に対してハプティクスを実装する手順です。

1. `HAP_BaseObjectHapticsController` を継承するクラスを作成。
2. 追跡ターゲットのリストを `TargetInfos` プロパティとして返却。
3. 照射座標データ生成処理 `GetHapticsTargets` を実装。

```csharp
public class HAP_CustomPropHapticsController : HAP_BaseObjectHapticsController
{
    public List<Transform> targets = new List<Transform>();
    public bool isEnabled = true;

    public override List<HapticsTargetInfo> TargetInfos
    {
        get
        {
            var list = new List<HapticsTargetInfo>();
            foreach (var target in targets)
            {
                if (target != null)
                {
                    list.Add(new HapticsTargetInfo
                    {
                        Name = target.name,
                        Transform = target,
                        IsEnabled = isEnabled,
                        IsTail = true
                    });
                }
            }
            return list;
        }
    }

    public override List<HAP_FociGenerator.ClusterFociData> GetHapticsTargets(float defaultIntensityPascal, Vector3 offset)
    {
        // ターゲット座標の Foci / STM データ構築ロジックを実装
        return new List<HAP_FociGenerator.ClusterFociData>();
    }
}
```

---

## 4. 仕様・パラメータ詳細

### 4.1 インスペクター設定パラメータ (`HAP_FoxFootHapticsController`)

* **ボーン割り当て & トグル**: `frontLeftFoot`, `frontRightFoot`, `backLeftFoot`, `backRightFoot`, `tailBone`
* **接地判定設定 (Animation State Settings)**:
  * `disableWhenInAir`: 浮遊中に足への照射をオフ。
  * `airborneHeightThreshold`: 接地判定高さ閾値 (m)。
  * `rootTransform`: 基準 Transform。
* **手との接触設定 (Hand Contact Settings)**:
  * `onlyTargetHandContact`: `HCD_Pipeline` で手検出時のみ照射。
  * `handContactThreshold`: 接触判定距離閾値 (m)。
* **カスタムモード設定**: `stmMode`, `sequentialStmFrequency`, `trackMode`, `customInnerAlgorithm`

---

## 5. デバッグ・留意事項

### 5.1 Gizmo 可視化
* 緑色ワイヤー/実線球: 有効かつ照射中のターゲット
* 赤色ワイヤー球: 非アクティブターゲット
* 接地接続線: 接地内は緑色、閾値超えは赤/緑で色分け
* 手接触線: 近接接触時に緑色の線を描画

### 5.2 留意事項
* `IsTargetActive` メソッドにより空中判定・手接触判定が全コンポーネントで自動適用されます。
