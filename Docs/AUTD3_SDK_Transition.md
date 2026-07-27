# AUTD3 SDK 新旧仕様比較ドキュメント (v3.x/v38 ➔ v31/v0.3.0)

> 📂 **親ノード**: [Haptics.md](./Haptics.md) | 🏷️ **種類**: 🔧 SDK移行ガイド  
> [RealTimeOcclusion Wiki (ポータル)](./Wiki.md) に戻る

本ドキュメントでは、本システムでサポートしている AUTD3 SDK の**旧仕様 (Legacy SDK: AUTD3Sharp v38/v3.x系)**と**新仕様 (New SDK: v31/v0.3.0系)**における API、設計思想、名前空間、実装方法の違い、およびプロジェクトでの切り替え方法について解説します。

---

## 1. 概要

本プロジェクトは、旧 SDK 環境と新 SDK 環境の両方で同じコードベースを検証・動作させられるよう、コンパイルシンボル `USE_AUTD3_LEGACY` による条件付きコンパイル (`#if USE_AUTD3_LEGACY`) を全面的に導入しています。

---

## 2. 設計思想・アーキテクチャ

旧 SDK (v38) と新 SDK (v31) の設計思想における主要なアーキテクチャ変更点は以下の通りです。

1. **同期 vs 非同期の非ブロッキング通信**:
   旧仕様の同期送信 API (`Controller.Send`) から、新仕様では `async/await` ベースの非同期送信 API (`Client.SendCheckedAsync`) へと移行しました。
2. **ジオメトリと通信機能の分離**:
   旧仕様では `Controller` がデバイス配置（`Geometry`）と通信を両方管理していましたが、新仕様では `Geometry` と `Client` に完全に物理分離されました。
3. **ビルダーパターンによる送信構築**:
   `DatagramBuilder` を用いてコマンド列を並列に構築し、安全なメモリマネージドフレームとして一括送信する設計に変更されました。

---

## 3. セットアップ・使用方法

### 3.1 SDK環境の自動切り替えスクリプト (`switch-sdk.ps1`)

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
* `Packages/manifest.json` を指定された SDK 用のテンプレート (`manifest.legacy.json` または `manifest.new-sdk.json`) で上書きします。
* `ProjectSettings/ProjectSettings.asset` 内の Standalone ターゲットグループに対する `scriptingDefineSymbols` に **`USE_AUTD3_LEGACY`** を自動的に追加、または削除します。
* `Packages/packages-lock.json` を削除し、次回 Unity 起動時にクリーンな依存解決を強制します。

---

## 4. 仕様・パラメータ詳細

### 4.1 API クイック比較表

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
| **音速・温度補正** | `ConfigureSoundSpeed` (音速) / `ApplyTemperature` | パターン生成時に直接波長 (`Wavelength`) を指定 |

### 4.2 主な変更点とコード比較

#### 4.2.1 名前空間の整理

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

#### 4.2.2 接続初期化 (Open) と寿命管理

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

#### 4.2.3 データグラム送信フロー

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

#### 4.2.4 複数デバイスへの個別コマンド割り当て (Group vs PushEach)

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
        return null; 
    }
    else
    {
        return GenerateDeviceCommand(geometry, deviceIndex, ...);
    }
});
```

#### 4.2.5 変調 (Modulation) 設定

```csharp
// 旧仕様 (Legacy)
var sineMod = new Sine(sineFrequency);
_autd.Send(sineMod);

// 新仕様 (New v31)
using var modulationBuffer = Modulation.ModulationBuffer();
Modulation.Sine(sineFrequency * Hz, new SineOption(), modulationBuffer);
builder.Push(new Modulation(SamplingConfig.Freq4k, modulationBuffer));
```

#### 4.2.6 サイレンサー (Silencer) 設定

```csharp
// 旧仕様 (Legacy)
_autd.Send(new Silencer(stepAmplitude, stepPhase));
_autd.Send(Silencer.Disable());

// 新仕様 (New v31)
builder.Push(new SetSilencer(new FixedUpdateRate(intensity: stepAmplitude, phase: stepPhase)));
builder.Push(SetSilencer.Disable());
```

#### 4.2.7 音速補正と波長指定

```csharp
// 旧仕様 (Legacy)
_autd.Send(new ConfigureSoundSpeed(temp));

// 新仕様 (New v31)
var wavelength = Pattern.Wavelength(Velocity.FromMS(340f));
AUTD3.Holo.Holo.Gspat(geometry, fociArray, wavelength, option, buffer);
```

---

## 5. デバッグ・留意事項

### 5.1 本プロジェクトにおける両対応設計の詳細

コア機能を担うスクリプト群において、新旧両 SDK に対応する設計が行われています。

* **Foci 生成 (`HAP_FociGenerator.cs`)**:
  * 旧仕様 (`#if USE_AUTD3_LEGACY`): 焦点座標を `List<(AUTD3Sharp.Utils.Point3, Amplitude)>` として保持。
  * 新仕様 (`#if !USE_AUTD3_LEGACY`): 焦点座標を `List<AUTD3.Holo.ControlPoint>` として保持。
* **方向性グループ分け制御 (`HAP_GSPATDeviceAllocator.cs`)**:
  * 旧仕様: `Controller.Group` と `GroupDictionary` を使用。
  * 新仕様: `DatagramBuilder.PushEach` を使用。型衝突を避けるためエイリアス `HoloCP` を使用。
* **キャリブレーション (`HAP_AUTDCalibration.cs`)**:
  * 旧仕様: `Controller.Group` による同期送信。
  * 新仕様: `PushEach` を用いた非同期タスク (`EmitCalibrationFocusAsync`) と通信中ロック制御。
