# Fox & Object Haptics (キツネおよびオブジェクト追従ハプティクス) 仕様書

> 📂 **親ノード**: [Haptics.md (AUTD制御システム)](./Haptics.md) | 🏷️ **種類**: 🏗️ システム設計書
>
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本モジュールは、キツネ（Fox）などの4足歩行キャラクターの足先や尻尾、またはその他の動的オブジェクトの特定ボーン（Bone）アニメーションに合わせて、そのターゲット座標に焦点を合わせた空中超音波フィードバックを照射するシステムです。

---

## 1. 概要
オブジェクトハプティクス制御は、基底抽象クラスである `HAP_BaseObjectHapticsController` と、それを実装するオブジェクト個別（例: `HAP_FoxFootHapticsController`）のコンポーネントに分かれています。

- **`HAP_BaseObjectHapticsController.cs`**:
  ハプティクス制御のすべての基本設定（STM設定、手との接触判定、Gizmos描画設定など）と、ターゲットのアクティブ状態の自動判定、OnDrawGizmos描画コードなどの共通ロジックを保持しています。
- **`HAP_FoxFootHapticsController.cs`**:
  キツネの骨格階層内から指定したパターン名（例: `Fox_F_LLegDigit11` や `Fox_Tail6` 等）に合致する4本の足および尻尾のボーン（Transform）を自動検出し、その追跡情報（`TargetInfos`）を基底クラスに提供します。

これらが `HAP_AUTDController` のハプティクス送信ループ（`UpdateHaptics`）と連携し、リアルタイムにターゲット位置へ超音波の焦点を形成します。

---

## 2. 照射モードの分類 (STM Mode)
`HAP_AUTDController` のホログラフィアルゴリズム (`HoloAlgorithm`) を **`Custom`** に設定した場合、インスペクタの `STM Mode` の設定に応じて、ハードウェアまたはPC側で計算された高速な時分割（STM）照射が行えます。

### ① FociSTM (ハードウェア計算・単焦点)
* **概要**: ハードウェア(FPGA)側で単焦点を計算・巡回させる超高速かつ超軽量なモードです。
* **動作**: 有効かつ照射条件を満たしているターゲット（足・尻尾）に対して、指定した周波数（STM Frequency）で1点ずつ高速に焦点を切り替えます。
* **特徴**:
  * 演算負荷が極めて低く、非常に高い周波数（150Hz等）での切り替えが可能です。
  * このモードでは自動的に `Track Mode = Sequential` (単焦点巡回) かつ `Algorithm = Naive` 扱いになります。

### ② GainSTM (PC計算・複数焦点対応)
* **概要**: PC側で全フレームの焦点や位相を事前計算し、バッファとしてデバイスに送信するモードです。
* **動作**: `Track Mode` や `Custom Inner Algorithm` の組み合わせにより、柔軟な照射が可能です。
  * **Track Mode = Sequential**: アクティブなターゲットを1点ずつ巡回します。GSPATを使用すると、より正確で高品質な単焦点STMを形成できます。
  * **Track Mode = Simultaneous**: アクティブなすべてのターゲットに対して**同時**に焦点を形成します。毎フレームPCでGSPATを解く動作になります。
* **特徴**:
  * 複数焦点の最適化（GSPAT等）をSTMとして利用できますが、PC側の演算負荷が高くなります。

---

## 3. インスペクター設定パラメータ (HAP_FoxFootHapticsController)

### 基本設定 (Foot Bone Transforms & Toggles)
* **`Front Left / Front Right / Back Left / Back Right Foot / Tail Bone`**: 追跡対象となる各Transform（未指定の場合は起動時に自動検出されます）。
* **`Enable Front Left / Front Right / Back Left / Back Right / Enable Tail`**: それぞれの部位への照射を個別で有効化/無効化するトグル。

### 接地判定設定 (Animation State Settings) ※足パーツのみ適用
* **`Disable When In Air`**: 有効にすると、地面から浮いている（キャラクターのルート位置からの高さがしきい値を超えている）部位への照射をスキップします。
* **`Airborne Height Threshold`**: 接地と判定するためのルート位置からの高さのしきい値（メートル）。
* **`Root Transform`**: 接地の基準となるキャラクターのルートTransform（未指定時は本GameObjectを使用します）。
* **`Foot Target Normal`**: 方向グルーピング用のクラスタ法線。どの向きのデバイスから照射するかを決定するヒントとして機能します。

### 手との接触設定 (Hand Contact Settings) ※全パーツ適用
* **`Only Target Hand Contact`**: 有効にすると、HCD_Pipelineで検出された手のクラスタがターゲットの近くにあるときのみ照射します。
* **`Hand Contact Threshold`**: 手との接触と判定する距離のしきい値（メートル）。

### カスタムモード設定 (Custom Mode Settings)
* **`STM Mode`**: 使用するSTMの種類（`FociSTM` または `GainSTM`）を選択します。
* **`STM Frequency (Hz)`**: 高速シーケンシャル照射時の切り替え周波数。
* **`Track Mode`**: ターゲットの追跡・照射方式（`Sequential` または `Simultaneous`）。※`GainSTM` 選択時のみ表示
* **`Custom Inner Algorithm`**: 使用する内部ソルバー（`Naive` または `GSPAT`）。※`GainSTM` 選択時のみ表示

---

## 4. デバッグと視覚化 (Gizmos)
Sceneビュー上でのリアルタイムな動作確認のため、以下のGizmoが自動描画されます（基底クラスで制御）。

* **ターゲット位置の球体（Wireframe Sphere & Solid Sphere）**:
  * 有効かつ照射条件を満たしている（照射中）ターゲット：**緑色のワイヤー球および実線球**で描画されます。
  * 非アクティブなターゲット：**赤色（または非アクティブ色）のワイヤー球**で描画されます。
* **接地しきい値と接続線（足パーツのみ）**:
  * 空中判定が有効な場合、`airborneHeightThreshold` の高さを示す小さな十字マークが表示されます。
  * ターゲットから地面に向けて引かれる接続線：
    * **接地内**：緑色の接続線を描画。
    * **接地外**：しきい値より上の部分を赤線、下の部分を緑線で描き分けます。
* **手との接触線**:
  * 手との近接接触判定（`onlyTargetHandContact`）が有効かつ接触時、手のクラスタ重心からターゲット位置に向けて緑色の線が描画されます。

---

## 5. 新しいオブジェクトハプティクスの作成方法

本プロジェクトでは、ハプティクス（超音波焦点）をキャラクターやオブジェクトの特定部位に照射する処理が抽象化されています。新しい動的オブジェクト（例：鳥、人間の手、インタラクティブな各種小道具など）に対してハプティクスフィードバックを実装する手順を解説します。

### 5.1 作成の流れ

新しいハプティクス制御を実装する手順は、以下の3ステップです。

1. **`HAP_BaseObjectHapticsController` を継承したクラスを作成する**
2. **追跡するターゲット座標（Transform）の一覧を `TargetInfos` プロパティとして提供する**
3. **照射座標データ生成処理 `GetHapticsTargets` を実装する**

### 5.2 実装例 (`HAP_CustomPropHapticsController.cs`)

以下は、複数の指定したノード（Transform）を順次または同時に狙う、最もシンプルなカスタムコントローラーの実装例です。

```csharp
using UnityEngine;
using System.Collections.Generic;

#if !USE_AUTD3_LEGACY
using AUTD3;
using AUTD3.Holo;
#else
using AUTD3Sharp;
using static AUTD3Sharp.Units;
#endif

#nullable enable

public class HAP_CustomPropHapticsController : HAP_BaseObjectHapticsController
{
    [Header("Target Nodes")]
    [Tooltip("ハプティクスを照射したいノードのリスト")]
    public List<Transform> targets = new List<Transform>();
    
    [Tooltip("ターゲット全体を有効にするかどうかのトグル")]
    public bool isEnabled = true;

    /// <summary>
    /// 基底クラスに必要なターゲット情報を返します（Gizmosの描画や状態の判定に自動的に使われます）。
    /// </summary>
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
                        IsTail = true // 足のように高さ接地判定を行わない場合は true にします
                    });
                }
            }
            return list;
        }
    }

    /// <summary>
    /// AUTDControllerが使用する、現在有効なターゲット座標データ（Foci/STMフレーム）のリストを構築します。
    /// </summary>
    public override List<HAP_FociGenerator.ClusterFociData> GetHapticsTargets(float defaultIntensityPascal, Vector3 offset)
    {
        var result = new List<HAP_FociGenerator.ClusterFociData>();
        
        // Custom Mode (時分割STM) の判定
        bool useCustomCycle = autdController != null 
            && autdController.holoAlgorithm == HoloAlgorithm.Custom
            && (stmMode == HapticsSTMMode.FociSTM || (stmMode == HapticsSTMMode.GainSTM && trackMode == HapticsTrackMode.Sequential));

        if (useCustomCycle)
        {
            // --- ① STM (シーケンシャル) 巡回モード ---
            var activeTargets = new List<Transform>();
            foreach (var target in targets)
            {
                if (target != null && IsTargetActive(target, isEnabled, isTail: true))
                {
                    activeTargets.Add(target);
                }
            }

            if (activeTargets.Count > 0)
            {
                // 代表点を用いてダミークラスタを作成し、STMフレームとして全ターゲットの座標を順次追加する
                TrackedCluster dummyCluster = new TrackedCluster
                {
                    Centroid = activeTargets[0].position,
                    Normal = footTargetNormal.normalized,
                    Force = 1.0f,
                    IsAlive = true
                };

                var fociData = new HAP_FociGenerator.ClusterFociData(dummyCluster);
                fociData.UseSTM = true;
                fociData.IsGainSTM = (stmMode == HapticsSTMMode.GainSTM);
                fociData.STMFrequency = sequentialSTMFrequency;

                foreach (var t in activeTargets)
                {
                    Vector3 pos = t.position;
                    fociData.STMFrames.Add(new List<Vector3> { 
                        new Vector3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z) 
                    });
                }
                result.Add(fociData);
            }
        }
        else
        {
            // --- ② GSPAT (同時マルチフォーカス) モード ---
            foreach (var target in targets)
            {
                if (target != null && IsTargetActive(target, isEnabled, isTail: true))
                {
                    Vector3 pos = target.position;
                    TrackedCluster dummyCluster = new TrackedCluster
                    {
                        Centroid = pos,
                        Normal = footTargetNormal.normalized,
                        Force = 1.0f,
                        IsAlive = true
                    };

                    var fociData = new HAP_FociGenerator.ClusterFociData(dummyCluster);
#if !USE_AUTD3_LEGACY
                    fociData.SequentialFoci.Add(new AUTD3.Holo.ControlPoint(
                        new Vector3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z),
                        Amplitude.FromPascal(defaultIntensityPascal)
                    ));
#else
                    fociData.SequentialFoci.Add((
                        new AUTD3Sharp.Utils.Point3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z),
                        defaultIntensityPascal * Pa
                    ));
#endif
                    result.Add(fociData);
                }
            }
        }

        return result;
    }
}
```

### 5.3 実装上のポイント・共通機能の活用

基底クラス `HAP_BaseObjectHapticsController` を継承することにより、以下の機能が自動的に利用可能になります。

#### ① インスペクター設定の継承
基底クラスに定義された設定項目が、インスペクター上に自動的に配置されます。
- **Custom Mode Settings**: `STM Mode`, `STM Frequency`, `Track Mode`, `Custom Inner Algorithm`
- **Hand Contact Settings**: 手との近接判定フラグ `Only Target Hand Contact` や距離しきい値 `Hand Contact Threshold`
- **Debug Visualization**: Gizmo描画フラグや色設定

#### ② 空中判定と手接触判定の自動化 (`IsTargetActive`)
個別のターゲット座標を処理する際、基底クラスで実装されている `IsTargetActive(Transform target, bool isEnabled, bool isTail)` メソッドを呼び出すことで、**「接地判定」や「手との距離しきい値判定」が自動的に適用されます。**
- `isTail = false` の場合：`disableWhenInAir = true` 時の高さチェックが適用されます。
- `isTail = true` の場合：高さチェックをバイパスし、空中にあっても常に有効とみなします。

#### ③ Gizmos 描画の自動描画
基底クラスが `OnDrawGizmos()` を内部で実行するため、**自分で Gizmos 描画コードを書く必要はありません。** 
`TargetInfos` に正しい情報を格納して返すだけで、アクティブ状態（緑色）や非アクティブ状態（赤色）の球体、地面への接地ライン、および手と接触している時の接続線が自動的にSceneビュー上に描画されます。

### 5.4 Unity上でのセットアップ

1. 作成したスクリプトを、ハプティクスを照射したいオブジェクトにアタッチします。
2. シーン内の `AUTDController` ゲームオブジェクトを選択し、インスペクター上で **`Object Haptics Controller`** フィールドに、今回作成したオブジェクトのコンポーネントをアタッチ/参照指定します。
3. シーンを実行し、デバッグ可視化球（Gizmo）が正しく配置・追従することを確認してください。
