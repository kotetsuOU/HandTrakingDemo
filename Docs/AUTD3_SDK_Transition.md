# AUTD3 SDK 新旧仕様比較ドキュメント (v3.x/v38 ➔ v31/v0.3.0)

> 📂 **親ノード**: [Haptics.md (AUTD制御システム)](./Haptics.md) | 🏷️ **種類**: 🔧 SDK移行ガイド
>
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、本システムでサポートしている AUTD3 SDK の**旧仕様 (Legacy SDK: AUTD3Sharp v38/v3.x系)**と**新仕様 (New SDK: v31/v0.3.0系)**におけるAPI of 設計思想、名前空間、実装方法の違い、およびプロジェクトでの切り替え方法について解説します。

---

## 1. 概要と切り替え方法

本プロジェクトは、旧SDK環境と新SDK環境の両方で同じコードベースを検証・動作させられるよう、コンパイルシンボル `USE_AUTD3_LEGACY` による条件付きコンパイル (`#if USE_AUTD3_LEGACY`) を全面的に導入しています。

### SDK環境の自動切り替えスクリプト (`switch-sdk.ps1`)
手動での `manifest.json` の書き換えや Unity の `Player Settings` でのシンボル追加の手間を省くため、ルートディレクトリに自動切り替えスクリプト [switch-sdk.ps1](file:///c:/Users/hongo/Documents/tsutsumi/RealTimeOcclusion/switch-sdk.ps1) を用意しています。

#### 実行手順 (Unity エディタを閉じた状態で実行推奨)
1. **PowerShell** を起動します。
2. 以下のコマンドを実行して切り替えます。

```powershell
# 旧SDK (AUTD3Sharp) 環境に切り替える場合
powershell -ExecutionPolicy Bypass -File .\switch-sdk.ps1 legacy

# 新SDK (autd3-sdk v0.3) 環境に切り替える場合
powershell -ExecutionPolicy Bypass -File .\switch-sdk.ps1 new

# 現在の有効なSDK環境を確認する場合
powershell -ExecutionPolicy Bypass -File .\switch-sdk.ps1
```

#### スクリプトの自動処理内容
*   `Packages/manifest.json` を指定されたSDK用のテンプレート (`manifest.legacy.json` または `manifest.new-sdk.json`) で上書きします。
*   `ProjectSettings/ProjectSettings.asset` 内の Standalone ターゲットグループに対する `scriptingDefineSymbols` に **`USE_AUTD3_LEGACY`** を自動的に追加、または削除します。
*   `Packages/packages-lock.json` を削除し、次回 Unity 起動時にクリーンな依存解決を強制します。

---

## 2. API クイック比較表

| 項目 | 旧仕様 (Legacy / v38) | 新仕様 (New / v31) |
| :--- | :--- | :--- |
| **主要名前空間** | `using AUTD3Sharp;` | `using AUTD3;` |
| **接続クラス** | `Controller` (同期型) | `Client` (非同期型・IDisposable) |
| **ジオメトリ管理** | `Controller` に内包 | `Geometry` クラスが分離管理 (IDisposable) |
| **接続メソッド** | `Controller.OpenWithOption(...)` | `await Client.OpenAsync(...)` |
| **データ送信** | `_autd.Send(datagram)` (同期) | `client.SendCheckedAsync(frame)` (非同期) |
| **データ構築** | 各種 Datagram インスタンスを直接生成 | `client.DatagramBuilder()` によるビルドパターン |
| **複数デバイス個別出力** | `Group` データグラムによる振り分け | `builder.PushEach(deviceIndex => ...)` による振り分け |
| **変調 (Modulation)** | `new Sine(...)` 等のクラス生成 | `Modulation.Sine(...)` 等のバッファ書込＋送信 |
| **サイレンサー** | `new Silencer(...)` または `Silencer.Disable()` | `builder.Push(new SetSilencer(...))` |
| **冷却ファン** | `new ConfigureForceFan(bool)` | `new ForceFan(bool)` |
| **音速・温度補正** | `ConfigureSoundSpeed` (音速) / `ApplyTemperature` | パターン生成時に直接波長（`Wavelength`）を指定 |

---

## 3. 主な変更点とコード比較

### 3.1 名前空間の整理
*   **旧仕様**: `AUTD3Sharp` というプレフィックスがすべての名前空間についていました。
*   **新仕様**: `AUTD3` に統一され、よりシンプルになりました。

```csharp
// 旧仕様 (Legacy)
using AUTD3Sharp;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
using static AUTD3Sharp.Units;

// 新仕様 (New v31)
using AUTD3;
using AUTD3.Holo;
using static AUTD3.Units;
```

---

### 3.2 接続初期化 (Open) と寿命管理
*   **旧仕様**: `Controller` オブジェクトが同期的に接続を確立し、デバイス情報（Geometry）も内包していました。
*   **新仕様**: 接続は非同期 (`await Client.OpenAsync`) で行われ、デバイス位置などを司る `Geometry` と通信を司る `Client` が分離しました。両者ともに `IDisposable` を実装しており、明示的な破棄処理 (`Dispose`) が必要です。

```csharp
// 旧仕様 (Legacy) - 同期接続
var option = new AUTD3Sharp.SenderOption { Timeout = AUTD3Sharp.Duration.FromMillis(5000) };
var devices = connectedDevices.Select(obj => new AUTD3Sharp.AUTD3(pos: obj.transform.position, rot: obj.transform.rotation)).ToList();
_autd = Controller.OpenWithOption(devices, new AUTD3Sharp.Link.TwinCAT(), option);

// 新仕様 (New v31) - 非同期接続、Geometryの分離
var devices = connectedDevices.Select(obj => new Autd3(obj.transform.position, obj.transform.rotation)).ToList();
geometry = new Geometry(devices);
_client = await Client.OpenAsync(geometry, AUTD3.Link.TwinCATLinkOption.Local(), new ClientConfig());
```

---

### 3.3 データグラム送信フロー
*   **旧仕様**: `Controller.Send` を用いて、データグラム（`Null` や `Gain` 等）を直接送信していました。
*   **新仕様**: ビルダーパターンが導入されました。`_client.DatagramBuilder()` からビルダーを生成して送信コマンド（`Push` / `PushEach`）を追加し、`Build()` されたフレーム群をループで非同期送信します。

```csharp
// 旧仕様 (Legacy) - 直接送信
_autd.Send(new Null());

// 新仕様 (New v31) - ビルダーによる構築と非同期送信
using (var builder = _client.DatagramBuilder())
{
    var buffer = geometry.PatternBuffer();
    Pattern.Null(buffer);
    builder.Push(new Pattern(PatternBank.B0, buffer));
    
    using var frames = builder.Build();
    foreach (var frame in frames)
    {
        await _client.SendCheckedAsync(frame);
    }
}
```

---

### 3.4 複数デバイスへの個別コマンド割り当て (Group vs PushEach)
複数台 of AUTD を接続し、各デバイスに対して異なる触覚パターン（または Null）を割り当てる手法が大きく変わりました。

*   **旧仕様 (`Group`)**:
    各デバイスに対応するキー（文字列等）を返すマップ関数と、キーごとの `IDatagram` を保持する `GroupDictionary` を使って `Group` データグラムを構築・送信していました。
*   **新仕様 (`PushEach`)**:
    `DatagramBuilder` に備わっている `PushEach` API を使用します。接続されているデバイスインデックス (`deviceIndex`) ごとに、そのデバイスに送信したいコマンド（`ICommand?`）を返すラムダ式を指定します。

```csharp
// 旧仕様 (Legacy) - Group を使用
var groupDict = new GroupDictionary();
groupDict.Add("device_0", new Null());
groupDict.Add("device_1", customGain);

var groupDatagram = new Group(dev => 
{
    int idx = dev.Idx();
    return idx == 0 ? "device_0" : "device_1";
}, groupDict);

_autd.Send(groupDatagram);

// 新仕様 (New v31) - PushEach を使用
builder.PushEach(deviceIndex =>
{
    if (deviceIndex == 0)
    {
        // デバイス0にはNull（出力なし）を設定
        return null; 
    }
    else
    {
        // デバイス1には特定のGainパターン（ICommand）を生成して返す
        return GenerateDeviceCommand(geometry, deviceIndex, ...);
    }
});
```

---

### 3.5 変調 (Modulation) 設定
*   **旧仕様**: `Modulation` 自体をインスタンス化して直接送信していました。
*   **新仕様**: `ModulationBuffer` に静的メソッド（`Modulation.Sine` 等）を介してデータを書き込み、それをラップした `Modulation` コマンドをビルダーに `Push` します。

```csharp
// 旧仕様 (Legacy)
var sineMod = new Sine(sineFrequency);
_autd.Send(sineMod);

// 新仕様 (New v31)
using var modulationBuffer = Modulation.ModulationBuffer();
Modulation.Sine(sineFrequency * Hz, new SineOption(), modulationBuffer);
builder.Push(new Modulation(SamplingConfig.Freq4k, modulationBuffer));
```

---

## 3.6 サイレンサー (Silencer) 設定
*   **旧仕様**: `new Silencer(...)` クラスを送信していました。
*   **新仕様**: `SetSilencer` コマンドを用います。また、設定モードが `FixedUpdateRate` や `FixedCompletionTime` としてクラス構造化されています。

```csharp
// 旧仕様 (Legacy)
_autd.Send(new Silencer(stepAmplitude, stepPhase));
// もしくは無効化
_autd.Send(Silencer.Disable());

// 新仕様 (New v31)
builder.Push(new SetSilencer(new FixedUpdateRate(intensity: stepAmplitude, phase: stepPhase)));
// もしくは無効化
builder.Push(SetSilencer.Disable());
```

---

### 3.7 音速補正と波長指定
*   **旧仕様**: 気温等による音速変化を反映するために `ConfigureSoundSpeed` をデバイスに送信して内部設定を書き換えていました。
*   **新仕様**: ホログラム等のパターンを計算する際に、環境音速から求めた波長（`Wavelength`）を引数として陽に指定する形になりました。

```csharp
// 旧仕様 (Legacy)
_autd.Send(new ConfigureSoundSpeed(temp));

// 新仕様 (New v31)
var wavelength = Pattern.Wavelength(Velocity.FromMS(340f)); // 音速340m/sでの波長を算出
// パターン生成時（例: Gspat）に wavelength を直接渡す
AUTD3.Holo.Holo.Gspat(geometry, fociArray, wavelength, option, buffer);
```

---

## 4. 本プロジェクトにおける両対応設計の詳細

本プロジェクトでは、コア機能を担うスクリプト群において、新旧両SDKに対応する設計が行われています。主要スクリプトの条件付き分岐の概要は以下の通りです。

### 4.1 Foci 生成 (`HAP_FociGenerator.cs`)
干渉計算のもととなる焦点データ構造 `ClusterFociData` において、保持する座標の型が新旧で異なります。
*   **旧仕様 (`#if USE_AUTD3_LEGACY`)**: 焦点座標を `AUTD3Sharp.Utils.Point3` と `Amplitude` (float) のタプルリスト `List<(AUTD3Sharp.Utils.Point3, Amplitude)>` として保持します。
*   **新仕様 (`#if !USE_AUTD3_LEGACY`)**: 焦点座標を v31 独自の `AUTD3.Holo.ControlPoint` のオブジェクトリスト `List<AUTD3.Holo.ControlPoint>` として保持します。

### 4.2 方向性グループ分け制御 (`HAP_GSPATDeviceAllocator.cs`)
手の向きなどの幾何情報からデバイス別に出力をグループ分けし送信する処理において、送信のコアロジックを切り替えています。
*   **旧仕様**: 旧 `Controller.Group` の仕組みを用いて、`GroupDictionary` を構築して送信します。
*   **新仕様**: `DatagramBuilder.PushEach` の仕組みを用います。GSPAT使用時は各デバイスインデックスに対応する `TransducerMask` を適用してCPU計算させた `PatternStm` を非同期送信し、Naive使用時は `FociStm` を構築して送信します。また、新SDKの `AUTD3.ControlPoint` と `AUTD3.Holo.ControlPoint` の型衝突を避けるため、エイリアス `HoloCP` を用いて曖昧性を回避しています。

### 4.3 キャリブレーション (`HAP_AUTDCalibration.cs`)
各デバイスに順番に出力（または `Null`）を送って幾何位置のキャリブレーションを行う機能です。
*   **旧仕様**: 旧 `Controller.Group` を用いた同期送信を行います。
*   **新仕様**: `PushEach` を用いた非同期送信タスク (`EmitCalibrationFocusAsync`) を行い、通信中フラグを利用して重複送信による送信詰まりを防ぐ非同期ライフサイクル管理が組み込まれています。
