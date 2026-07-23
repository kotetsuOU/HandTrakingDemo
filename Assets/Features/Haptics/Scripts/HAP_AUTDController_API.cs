#if USE_AUTD3_LEGACY
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using AUTD3Sharp;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
using AUTD3Sharp.Gain.Holo;
using AUTD3Sharp.Driver.Datagram;
using static AUTD3Sharp.Units;

#nullable enable

public partial class HAP_AUTDController
{
    // =========================================================================
    // 手動操作用API (MANUAL APIs)
    // =========================================================================

    /// <summary>
    /// 任意のDatagramをAUTDデバイスに直接送信します。
    /// </summary>
    /// <param name="datagram">送信するDatagramオブジェクト</param>
    public void Send(IDatagram datagram)
    {
        if (_autd == null) return;
        lock (_sendLock) { _autd.Send(datagram); }
        _isCurrentlyOff = false;
    }

    /// <summary>
    /// 出力を停止（Nullデータグラムを送信）します。
    /// </summary>
    public void SetNull()
    {
        if (_autd == null) return;
        Debug.LogWarning("SetNull is not supported in v31 yet");
        _isCurrentlyOff = true;
    }

    /// <summary>
    /// デバイスの冷却ファンをON/OFFします。
    /// </summary>
    /// <param name="on">trueでファンON、falseでファンOFF</param>
    public void SetFan(bool on)
    {
        enableFan = on;
        ApplyFan();
    }

    /// <summary>
    /// 単一の焦点に超音波を集中させます。
    /// </summary>
    /// <param name="position">焦点の3D座標(ローカル空間)</param>
    /// <param name="amplitude">出力強度(0.0〜1.0)</param>
    public void SetFocus(Vector3 position, float amplitude = 1f)
    {
        if (_autd == null) return;
        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);
        var p = new AUTD3Sharp.Utils.Point3(position.x + offset.x, position.y + offset.y, position.z + offset.z);
        lock (_sendLock) { _autd.Send(new Focus(p, new FocusOption { Intensity = new Intensity(intensityVal) })); }
        _isCurrentlyOff = false;
    }

    /// <summary>
    /// ホログラフィアルゴリズムを使用して、複数の焦点に同時に超音波を出力します。
    /// </summary>
    /// <param name="positions">各焦点の座標リスト</param>
    /// <param name="amplitudesPa">各焦点の出力強度（Pascal）のリスト</param>
    /// <param name="algorithm">使用するホログラフィアルゴリズム（GSPAT または Naive）</param>
    public void SetHolo(IEnumerable<Vector3> positions, IEnumerable<float> amplitudesPa, HoloAlgorithm algorithm = HoloAlgorithm.GSPAT)
    {
        if (_autd == null) return;
        
        var posArray = positions.ToArray();
        var ampArray = amplitudesPa.ToArray();
        var activeFoci = new (AUTD3Sharp.Utils.Point3, Amplitude)[posArray.Length];
        
        for (int i = 0; i < posArray.Length; i++)
        {
            var p = posArray[i];
            activeFoci[i] = (
                new AUTD3Sharp.Utils.Point3(p.x + offset.x, p.y + offset.y, p.z + offset.z),
                ampArray[i] * Pa
            );
        }

        if (algorithm == HoloAlgorithm.GSPAT)
            lock (_sendLock) { _autd.Send(new GSPAT(activeFoci, new GSPATOption())); }
        else
            lock (_sendLock) { _autd.Send(new Naive(activeFoci, new NaiveOption())); }
            
        _isCurrentlyOff = false;
    }

    // ---------- STM APIs (空間時間変調によるフォーカス移動) ----------

    /// <summary>
    /// STM（Spatio-Temporal Modulation）を使用して、指定した焦点リストを高速で切り替えて移動させます。
    /// 単一焦点が動く軌跡を描くのに使用します。
    /// </summary>
    /// <param name="positions">焦点の移動軌跡（座標リスト）</param>
    /// <param name="frequency">軌跡を1周する周波数(Hz)</param>
    /// <param name="amplitude">出力強度(0.0〜1.0)</param>
    public void SetFocusStm(IEnumerable<Vector3> positions, float frequency, float amplitude = 1f)
    {
        if (_autd == null) return;
        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);
        var intensity = new Intensity(intensityVal);

        var foci = positions.Select(p => 
            new ControlPoints(new[] { new ControlPoint(new AUTD3Sharp.Utils.Point3(p.x + offset.x, p.y + offset.y, p.z + offset.z)) }, intensity)
        ).ToArray();

        lock (_sendLock) { _autd.Send(new FociSTM(foci, frequency * Hz)); }
        _isCurrentlyOff = false;
    }

    /// <summary>
    /// STMを使用して、複数のフォーカスを持つ状態を高速で切り替えます（マルチフォーカスの移動）。
    /// </summary>
    /// <param name="frames">各フレームにおける複数焦点のリスト</param>
    /// <param name="frequency">ループ周波数(Hz)</param>
    /// <param name="amplitude">出力強度(0.0〜1.0)</param>
    public void SetMultiFocusStm(IEnumerable<IEnumerable<Vector3>> frames, float frequency, float amplitude = 1f)
    {
        if (_autd == null) return;
        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);
        var intensity = new Intensity(intensityVal);

        var fociSTM = new List<ControlPoints>();
        foreach(var frame in frames)
        {
            var points = frame.Select(p => new ControlPoint(new AUTD3Sharp.Utils.Point3(p.x + offset.x, p.y + offset.y, p.z + offset.z))).ToArray();
            fociSTM.Add(new ControlPoints(points, intensity));
        }
        
        lock (_sendLock) { _autd.Send(new FociSTM(fociSTM, frequency * Hz)); }
        _isCurrentlyOff = false;
    }

    /// <summary>
    /// Gain（振幅・位相のパターン）自体をSTMで切り替えます。
    /// </summary>
    /// <param name="frames">切り替えるGainのリスト</param>
    /// <param name="frequency">ループ周波数(Hz)</param>
    /// <param name="modeOverride">GainSTMのモード（省略時はInspectorの設定に従う）</param>
    public void SetGainStm(IEnumerable<IGain> frames, float frequency, GainSTMMode? modeOverride = null)
    {
        if (_autd == null) return;
        var mode = modeOverride ?? gainStmMode;
        lock (_sendLock) { _autd.Send(new GainSTM(frames, frequency * Hz, new GainSTMOption { Mode = mode })); }
        _isCurrentlyOff = false;
    }

    // ---------- 拡張Gain APIs ----------

    /// <summary>
    /// カスタムのGain（各素子の位相と振幅を直接指定）を設定します。
    /// </summary>
    public void SetCustomGain(Func<Device, Func<Transducer, Drive>> f)
    {
        if (_autd == null) return;
        lock (_sendLock) { _autd.Send(new AUTD3Sharp.Gain.Custom(f)); }
        _isCurrentlyOff = false;
    }

    /// <summary>
    /// デバイスのトランスデューサをグループ化して別々の制御を行います。
    /// </summary>
    public void SetGainGroup(Func<Device, object?> keyMap, GroupDictionary datagramMap)
    {
        if (_autd == null) return;
        lock (_sendLock) { _autd.Send(new Group(keyMap, datagramMap)); }
        _isCurrentlyOff = false;
    }

    // ---------- 変調(Modulation) APIs ----------

    /// <summary>
    /// 定常的な出力（変調なし）に設定します。
    /// </summary>
    /// <param name="amplitude">出力強度(0.0〜1.0)</param>
    public void SetStaticModulation(float amplitude = 1f)
    {
        if (_autd == null) return;
        modulationMode = ModulationMode.Static;
        staticAmplitude = amplitude;
        ApplyModulation();
    }

    /// <summary>
    /// サイン波による変調（人間の肌で最も感じやすい振動）を設定します。
    /// </summary>
    /// <param name="frequency">サイン波の周波数(Hz)</param>
    public void SetSine(float frequency)
    {
        if (_autd == null) return;
        modulationMode = ModulationMode.Sine;
        sineFrequency = frequency;
        ApplyModulation();
    }

    /// <summary>
    /// 任意の波形データを使用して変調を行います。
    /// </summary>
    /// <param name="buffer">波形データ配列</param>
    /// <param name="frequency">基本周波数</param>
    public void SetCustomModulation(byte[] buffer, uint frequency)
    {
        if (_autd == null) return;
        // 基本周波数 = サンプリング周波数 / バッファ長
        lock (_sendLock) { _autd.Send(new AUTD3Sharp.Modulation.Custom(buffer, (frequency * buffer.Length) * Hz)); }
    }

    // ---------- サイレンサー(Silencer) APIs ----------

    /// <summary>
    /// 更新レート固定のサイレンサーを設定し、動作音を低減します。
    /// </summary>
    /// <param name="stepPhase">位相の変化ステップ（小さいほど滑らか）</param>
    /// <param name="stepAmplitude">振幅の変化ステップ（小さいほど滑らか）</param>
    public void SetSilenceFixedUpdateRate(ushort stepPhase = 500, ushort stepAmplitude = ushort.MaxValue)
    {
        if (_autd == null) return;
        silencerMode = SilencerMode.FixedUpdateRate;
        silencerStepPhase = stepPhase;
        silencerStepAmplitude = stepAmplitude;
        ApplySilencer();
    }

    /// <summary>
    /// 完了時間固定のサイレンサーを設定します（一定時間かけて滑らかに変化）。
    /// </summary>
    public void SetSilenceFixedCompletionTime()
    {
        if (_autd == null) return;
        silencerMode = SilencerMode.FixedCompletionTime;
        ApplySilencer();
    }

    /// <summary>
    /// サイレンサーを無効化し、瞬時に出力を切り替えます（動作音が出やすくなります）。
    /// </summary>
    public void SetSilenceNull()
    {
        if (_autd == null) return;
        silencerMode = SilencerMode.Disabled;
        ApplySilencer();
    }
}

#else
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using AUTD3;
using AUTD3.Holo;
using static AUTD3.Units;

#nullable enable

public partial class HAP_AUTDController
{
    public Client? client => _client;

    // =========================================================================
    // 手動操作用API (MANUAL APIs)
    // =========================================================================

    /// <summary>
    /// 任意のDatagramをAUTDデバイスに直接送信します。
    /// </summary>
    /// <param name="datagram">送信するDatagramオブジェクト</param>
    public void Send(object datagram)
    {
        if (_client == null || geometry == null) return;
        Debug.LogWarning("Manual Send is not supported in v31");
        _isCurrentlyOff = false;
    }

    /// <summary>
    /// 出力を停止（Nullデータグラムを送信）します。
    /// </summary>
    public void SetNull()
    {
        if (_client == null || geometry == null) return;
        Debug.LogWarning("SetNull is not supported in v31 yet");
        _isCurrentlyOff = true;
    }

    /// <summary>
    /// デバイスの冷却ファンをON/OFFします。
    /// </summary>
    /// <param name="on">trueでファンON、falseでファンOFF</param>
    public void SetFan(bool on)
    {
        enableFan = on;
        ApplyFan();
    }

    /// <summary>
    /// 単一の焦点に超音波を集中させます。
    /// </summary>
    /// <param name="position">焦点の3D座標(ローカル空間)</param>
    /// <param name="amplitude">出力強度(0.0〜1.0)</param>
    public async void SetFocus(Vector3 position, float amplitude = 1f)
    {
        if (_client == null || geometry == null) return;
        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);
        var p = new Vector3(position.x + offset.x, position.y + offset.y, position.z + offset.z);
        
        using var builder = _client.DatagramBuilder();
        builder.Push(null /* Focus not implemented in v31 */);
        using var frames = builder.Build();
        foreach(var frame in frames) { await _client.SendCheckedAsync(frame); }
        _isCurrentlyOff = false;
    }

    /// <summary>
    /// ホログラフィアルゴリズムを使用して、複数の焦点に同時に超音波を出力します。
    /// </summary>
    /// <param name="positions">各焦点の座標リスト</param>
    /// <param name="amplitudesPa">各焦点の出力強度（Pascal）のリスト</param>
    /// <param name="algorithm">使用するホログラフィアルゴリズム（GSPAT または Naive）</param>
    public async void SetHolo(IEnumerable<Vector3> positions, IEnumerable<float> amplitudesPa, HoloAlgorithm algorithm = HoloAlgorithm.GSPAT)
    {
        if (_client == null || geometry == null) return;
        
        var posArray = positions.ToArray();
        var ampArray = amplitudesPa.ToArray();
        var activeFoci = new AUTD3.Holo.ControlPoint[posArray.Length];
        
        for (int i = 0; i < posArray.Length; i++)
        {
            var p = posArray[i];
            activeFoci[i] = new AUTD3.Holo.ControlPoint(
                new Vector3(p.x + offset.x, p.y + offset.y, p.z + offset.z),
                Amplitude.FromPascal(ampArray[i])
            );
        }

        using var builder = _client.DatagramBuilder();
        var buffer = geometry.PatternBuffer();
        var wavelength = Pattern.Wavelength(Velocity.FromMS(340f));
        
        if (algorithm == HoloAlgorithm.GSPAT)
            AUTD3.Holo.Holo.Gspat(geometry, activeFoci, wavelength, new GspatOption(), buffer);
        else
            AUTD3.Holo.Holo.Naive(geometry, activeFoci, wavelength, new NaiveOption(), buffer);
            
        builder.Push(new Pattern(PatternBank.B0, buffer));
        using var frames = builder.Build();
        foreach(var frame in frames) { await _client.SendCheckedAsync(frame); }
            
        _isCurrentlyOff = false;
    }

    // ---------- STM APIs (空間時間変調によるフォーカス移動) ----------

    /// <summary>
    /// STM（Spatio-Temporal Modulation）を使用して、指定した焦点リストを高速で切り替えて移動させます。
    /// 単一焦点が動く軌跡を描くのに使用します。
    /// </summary>
    /// <param name="positions">焦点の移動軌跡（座標リスト）</param>
    /// <param name="frequency">軌跡を1周する周波数(Hz)</param>
    /// <param name="amplitude">出力強度(0.0〜1.0)</param>
    public async void SetFocusStm(IEnumerable<Vector3> positions, float frequency, float amplitude = 1f)
    {
        if (_client == null || geometry == null) return;
        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);
        var intensity = new Intensity(intensityVal);

        var points = positions.Select(p => 
            new AUTD3.ControlPoints(new[] { new AUTD3.ControlPoint(new Vector3(p.x + offset.x, p.y + offset.y, p.z + offset.z)) })
        ).ToArray();

        var stm = new FociStm(frequency * Hz, points);

        using var builder = _client.DatagramBuilder();
        builder.Push(stm);
        using var frames = builder.Build();
        foreach(var frame in frames) { await _client.SendCheckedAsync(frame); }
        _isCurrentlyOff = false;
    }

    /// <summary>
    /// STMを使用して、複数のフォーカスを持つ状態を高速で切り替えます（マルチフォーカスの移動）。
    /// </summary>
    /// <param name="frames">各フレームにおける複数焦点のリスト</param>
    /// <param name="frequency">ループ周波数(Hz)</param>
    /// <param name="amplitude">出力強度(0.0〜1.0)</param>
    public void SetMultiFocusStm(IEnumerable<IEnumerable<Vector3>> frames, float frequency, float amplitude = 1f)
    {
        if (_client == null || geometry == null) return;
        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);
        var intensity = new Intensity(intensityVal);

        Debug.LogWarning("SetMultiFocusStm requires GainSTM in v31, not yet implemented here.");
    }

    /// <summary>
    /// Gain（振幅・位相のパターン）自体をSTMで切り替えます。
    /// </summary>
    /// <param name="frames">切り替えるGainのリスト</param>
    /// <param name="frequency">ループ周波数(Hz)</param>
    /// <param name="modeOverride">GainSTMのモード（省略時はInspectorの設定に従う）</param>
    public void SetGainStm(IEnumerable<ICommand> frames, float frequency)
    {
        Debug.LogWarning("SetGainStm requires new v31 API, not yet implemented here.");
    }

    // ---------- 拡張Gain APIs ----------

    /// <summary>
    /// カスタムのGain（各素子の位相と振幅を直接指定）を設定します。
    /// </summary>
    public void SetCustomGain(Func<Device, Func<object, Emission>> f)
    {
        Debug.LogWarning("SetCustomGain requires new v31 API, not yet implemented here.");
    }

    /// <summary>
    /// デバイスのトランスデューサをグループ化して別々の制御を行います。
    /// </summary>
    public void SetGainGroup()
    {
        Debug.LogWarning("SetGainGroup requires new v31 API, not yet implemented here.");
    }

    // ---------- 変調(Modulation) APIs ----------

    /// <summary>
    /// 定常的な出力（変調なし）に設定します。
    /// </summary>
    /// <param name="amplitude">出力強度(0.0〜1.0)</param>
    public void SetStaticModulation(float amplitude = 1f)
    {
        if (_client == null || geometry == null) return;
        modulationMode = ModulationMode.Static;
        staticAmplitude = amplitude;
        ApplyModulation();
    }

    /// <summary>
    /// サイン波による変調（人間の肌で最も感じやすい振動）を設定します。
    /// </summary>
    /// <param name="frequency">サイン波の周波数(Hz)</param>
    public void SetSine(float frequency)
    {
        if (_client == null || geometry == null) return;
        modulationMode = ModulationMode.Sine;
        sineFrequency = frequency;
        ApplyModulation();
    }

    /// <summary>
    /// 任意の波形データを使用して変調を行います。
    /// </summary>
    /// <param name="buffer">波形データ配列</param>
    /// <param name="frequency">基本周波数</param>
    public void SetCustomModulation(byte[] buffer, uint frequency)
    {
        Debug.LogWarning("SetCustomModulation requires new v31 API, not yet implemented here.");
    }

    // ---------- サイレンサー(Silencer) APIs ----------

    /// <summary>
    /// 更新レート固定のサイレンサーを設定し、動作音を低減します。
    /// </summary>
    /// <param name="stepPhase">位相の変化ステップ（小さいほど滑らか）</param>
    /// <param name="stepAmplitude">振幅の変化ステップ（小さいほど滑らか）</param>
    public void SetSilenceFixedUpdateRate(ushort stepPhase = 500, ushort stepAmplitude = ushort.MaxValue)
    {
        if (_client == null || geometry == null) return;
        silencerMode = SilencerMode.FixedUpdateRate;
        silencerStepPhase = stepPhase;
        silencerStepAmplitude = stepAmplitude;
        ApplySilencer();
    }

    /// <summary>
    /// 完了時間固定のサイレンサーを設定します（一定時間かけて滑らかに変化）。
    /// </summary>
    public void SetSilenceFixedCompletionTime()
    {
        if (_client == null || geometry == null) return;
        silencerMode = SilencerMode.FixedCompletionTime;
        ApplySilencer();
    }

    /// <summary>
    /// サイレンサーを無効化し、瞬時に出力を切り替えます（動作音が出やすくなります）。
    /// </summary>
    public void SetSilenceNull()
    {
        if (_client == null || geometry == null) return;
        silencerMode = SilencerMode.Disabled;
        ApplySilencer();
    }
}

#endif
