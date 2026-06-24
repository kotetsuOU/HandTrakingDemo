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

public enum HoloAlgorithm
{
    GSPAT,
    Naive
}

public enum ModulationMode
{
    Sine,
    Static
}

public enum SilencerMode
{
    Disabled,
    FixedUpdateRate,
    FixedCompletionTime
}

public enum OperationMode
{
    AutoHCD,  // Automatically read from HCD_Pipeline
    Manual    // Wait for manual API calls (SetFocus, SetFocusStm, etc.)
}

/// <summary>
/// HCD_Pipeline によって計算された接触重心を受け取り、
/// AUTD3デバイス群に GSPAT (Acoustic Holography) 等を用いてマルチフォーカス出力を行うコントローラー。
/// 公式AUTD3SharpのC#ネイティブラッパーとして機能します。
/// </summary>
public class HAP_AUTDController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("重心座標を提供する HCD_Pipeline")]
    public HCD_Pipeline hcdPipeline = null!;

    [Header("Operation Settings")]
    [Tooltip("自動モード(AutoHCD)か、スクリプトからの手動制御(Manual)か")]
    public OperationMode operationMode = OperationMode.AutoHCD;

    [Header("Acoustic Settings")]
    public HoloAlgorithm holoAlgorithm = HoloAlgorithm.GSPAT;
    
    [Tooltip("超音波の出力強度 (Pascal)")]
    public float focusIntensityPascal = 10000f;

    [Header("Modulation Settings")]
    public ModulationMode modulationMode = ModulationMode.Sine;
    [Tooltip("サイン波の変調周波数 (Hz)")]
    public float sineFrequency = 150f;
    [Tooltip("定常波の振幅 (0.0〜1.0)")]
    public float staticAmplitude = 1.0f;

    [Header("Silencer Settings")]
    public SilencerMode silencerMode = SilencerMode.FixedUpdateRate;
    public ushort silencerStepPhase = 500;
    public ushort silencerStepAmplitude = 65535;

    [Header("Hardware Settings")]
    [Tooltip("環境温度（音速計算に使用）")]
    public float temperature = 25f;
    [Tooltip("デバイス冷却ファンのON/OFF")]
    public bool enableFan = false;

    [Header("Coordinate Settings")]
    [Tooltip("焦点位置の全体オフセット")]
    public Vector3 offset = Vector3.zero;

    [Header("STM Settings (for future extension)")]
    [Tooltip("GainSTM時のモード")]
    public GainSTMMode gainStmMode = GainSTMMode.PhaseIntensityFull;

    [Header("Debug")]
    public bool visualizeDevices = true;

    private Controller? _autd = null;
    private bool _isCurrentlyOff = true;

    // 前回の設定を記憶して変更を検知するためのフィールド
    private ModulationMode _prevModMode;
    private float _prevSineFreq;
    private float _prevStaticAmp;
    
    private SilencerMode _prevSilencerMode;
    private ushort _prevSilStepPhase;
    private ushort _prevSilStepAmp;
    
    private bool _prevFanState;
    private float _prevTemperature;

    void Awake()
    {
        if (hcdPipeline == null)
        {
            hcdPipeline = FindAnyObjectByType<HCD_Pipeline>();
            if (hcdPipeline == null)
            {
                Debug.LogWarning("[HAP_AUTDController] HCD_Pipeline is not assigned and could not be found in the scene.");
            }
        }

        // シーン内のすべての AUTD3Device コンポーネントを収集し、ID順にソートしてデバイス配置情報を生成
        var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None)
            .OrderBy(obj => obj.ID)
            .Select(obj => new AUTD3(pos: obj.transform.position, rot: obj.transform.rotation));

        try
        {
            // TwinCAT リンクで接続
            _autd = Controller.Open(devices, new AUTD3Sharp.Link.TwinCAT());
            
            // 初期設定の送信
            ApplyTemperature();
            ApplyModulation();
            ApplySilencer();
            ApplyFan();
            
            // 初期状態はオフ (Null出力)
            _autd.Send(new Null());
            _isCurrentlyOff = true;
            
            Debug.Log("[HAP_AUTDController] Successfully connected to AUTD3 devices via TwinCAT.");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            Debug.LogError("[HAP_AUTDController] Failed to connect to AUTD3 via TwinCAT. Ensure TwinCAT is running in Run Mode.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
            Application.Quit();
#endif
        }
    }

    void Update()
    {
        if (_autd == null) return;

        CheckForConfigChanges();

        // 手動モードの場合はここで終了し、Updateからの自動送信を行わない
        if (operationMode != OperationMode.AutoHCD) return;
        if (hcdPipeline == null) return;

        // トラッカーから安定化・追跡済みのクラスタリストを取得
        var trackedClusters = hcdPipeline.GetTrackedClusters();

        // 生存しており、かつ Force が有効なクラスタを抽出
        var activeFoci = trackedClusters
            .Where(c => c.IsAlive && c.Force > 0.01f)
            .Select(c => (
                new AUTD3Sharp.Utils.Point3(c.Centroid.x + offset.x, c.Centroid.y + offset.y, c.Centroid.z + offset.z), 
                (focusIntensityPascal * c.Force) * Pa
            )).ToArray();

        if (activeFoci.Length > 0)
        {
            // ホログラフィアルゴリズムの分岐
            if (holoAlgorithm == HoloAlgorithm.GSPAT)
            {
                var gspat = new GSPAT(activeFoci, new GSPATOption());
                _autd.Send(gspat);
            }
            else if (holoAlgorithm == HoloAlgorithm.Naive)
            {
                var naive = new Naive(activeFoci, new NaiveOption());
                _autd.Send(naive);
            }
            
            _isCurrentlyOff = false;
        }
        else
        {
            // 接触がなくなった場合、出力を停止 (Null)
            if (!_isCurrentlyOff)
            {
                _autd.Send(new Null());
                _isCurrentlyOff = true;
            }
        }
    }

    // =========================================================================
    // MANUAL APIs (Drop-in replacements for original AutdController)
    // =========================================================================

    public void Send(IDatagram datagram)
    {
        if (_autd == null) return;
        _autd.Send(datagram);
        _isCurrentlyOff = false;
    }

    public void SetNull()
    {
        if (_autd == null) return;
        _autd.Send(new Null());
        _isCurrentlyOff = true;
    }

    public void SetFan(bool on)
    {
        enableFan = on;
        ApplyFan();
    }

    public void SetFocus(Vector3 position, float amplitude = 1f)
    {
        if (_autd == null) return;
        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);
        var p = new AUTD3Sharp.Utils.Point3(position.x + offset.x, position.y + offset.y, position.z + offset.z);
        _autd.Send(new Focus(p, new FocusOption { Intensity = new Intensity(intensityVal) }));
        _isCurrentlyOff = false;
    }

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
            _autd.Send(new GSPAT(activeFoci, new GSPATOption()));
        else
            _autd.Send(new Naive(activeFoci, new NaiveOption()));
            
        _isCurrentlyOff = false;
    }

    // ---------- STM APIs ----------

    public void SetFocusStm(IEnumerable<Vector3> positions, float frequency, float amplitude = 1f)
    {
        if (_autd == null) return;
        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);
        var intensity = new Intensity(intensityVal);

        var foci = positions.Select(p => 
            new ControlPoints(new[] { new ControlPoint(new AUTD3Sharp.Utils.Point3(p.x + offset.x, p.y + offset.y, p.z + offset.z)) }, intensity)
        ).ToArray();

        _autd.Send(new FociSTM(foci, frequency * Hz));
        _isCurrentlyOff = false;
    }

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
        
        _autd.Send(new FociSTM(fociSTM, frequency * Hz));
        _isCurrentlyOff = false;
    }

    public void SetGainStm(IEnumerable<IGain> frames, float frequency, GainSTMMode? modeOverride = null)
    {
        if (_autd == null) return;
        var mode = modeOverride ?? gainStmMode;
        _autd.Send(new GainSTM(frames, frequency * Hz, new GainSTMOption { Mode = mode }));
        _isCurrentlyOff = false;
    }

    // ---------- Extended Gain APIs ----------

    public void SetCustomGain(Func<Device, Func<Transducer, Drive>> f)
    {
        if (_autd == null) return;
        _autd.Send(new AUTD3Sharp.Gain.Custom(f));
        _isCurrentlyOff = false;
    }

    public void SetGainGroup(Func<Device, object?> keyMap, GroupDictionary datagramMap)
    {
        if (_autd == null) return;
        _autd.Send(new Group(keyMap, datagramMap));
        _isCurrentlyOff = false;
    }

    // ---------- Modulation APIs ----------

    public void SetStaticModulation(float amplitude = 1f)
    {
        if (_autd == null) return;
        modulationMode = ModulationMode.Static;
        staticAmplitude = amplitude;
        ApplyModulation();
    }

    public void SetSine(float frequency)
    {
        if (_autd == null) return;
        modulationMode = ModulationMode.Sine;
        sineFrequency = frequency;
        ApplyModulation();
    }

    public void SetCustomModulation(byte[] buffer, uint frequency)
    {
        if (_autd == null) return;
        // Frequency f = sampling freq / length. So SamplingFreq = frequency * length.
        _autd.Send(new AUTD3Sharp.Modulation.Custom(buffer, (frequency * buffer.Length) * Hz));
    }

    // ---------- Silencer APIs ----------

    public void SetSilenceFixedUpdateRate(ushort stepPhase = 500, ushort stepAmplitude = ushort.MaxValue)
    {
        if (_autd == null) return;
        silencerMode = SilencerMode.FixedUpdateRate;
        silencerStepPhase = stepPhase;
        silencerStepAmplitude = stepAmplitude;
        ApplySilencer();
    }

    public void SetSilenceFixedCompletionTime()
    {
        if (_autd == null) return;
        silencerMode = SilencerMode.FixedCompletionTime;
        ApplySilencer();
    }

    public void SetSilenceNull()
    {
        if (_autd == null) return;
        silencerMode = SilencerMode.Disabled;
        ApplySilencer();
    }

    // =========================================================================

    private void CheckForConfigChanges()
    {
        bool modulationChanged = (_prevModMode != modulationMode) ||
                                 (_prevSineFreq != sineFrequency) ||
                                 (_prevStaticAmp != staticAmplitude);
        if (modulationChanged) ApplyModulation();

        bool silencerChanged = (_prevSilencerMode != silencerMode) ||
                               (_prevSilStepPhase != silencerStepPhase) ||
                               (_prevSilStepAmp != silencerStepAmplitude);
        if (silencerChanged) ApplySilencer();

        if (_prevFanState != enableFan) ApplyFan();
        if (_prevTemperature != temperature) ApplyTemperature();
    }

    private void ApplyModulation()
    {
        if (_autd == null) return;
        
        try
        {
            switch (modulationMode)
            {
                case ModulationMode.Sine:
                    _autd.Send(new Sine(freq: sineFrequency * Hz, option: new SineOption()));
                    break;
                case ModulationMode.Static:
                    _autd.Send(new Static());
                    break;
            }
            
            _prevModMode = modulationMode;
            _prevSineFreq = sineFrequency;
            _prevStaticAmp = staticAmplitude;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDController] Failed to apply modulation: {ex.Message}");
        }
    }

    private void ApplySilencer()
    {
        if (_autd == null) return;

        try
        {
            switch (silencerMode)
            {
                case SilencerMode.Disabled:
                    _autd.Send(Silencer.Disable());
                    break;
                case SilencerMode.FixedUpdateRate:
                    _autd.Send(new Silencer(new FixedUpdateRate { Intensity = silencerStepAmplitude, Phase = silencerStepPhase }));
                    break;
                case SilencerMode.FixedCompletionTime:
                    _autd.Send(new Silencer(new FixedCompletionTime()));
                    break;
            }
            
            _prevSilencerMode = silencerMode;
            _prevSilStepPhase = silencerStepPhase;
            _prevSilStepAmp = silencerStepAmplitude;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDController] Failed to apply silencer: {ex.Message}");
        }
    }

    private void ApplyFan()
    {
        if (_autd == null) return;
        
        try
        {
            _autd.Send(new ForceFan(dev => enableFan));
            _prevFanState = enableFan;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDController] Failed to apply fan state: {ex.Message}");
        }
    }

    private void ApplyTemperature()
    {
        if (_autd == null) return;
        
        try
        {
            _autd.Environment.SetSoundSpeedFromTemp(temperature);
            _prevTemperature = temperature;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDController] Failed to set temperature: {ex.Message}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!visualizeDevices) return;

        var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None);
        foreach (var device in devices)
        {
            Gizmos.matrix = Matrix4x4.TRS(device.transform.position, device.transform.rotation, Vector3.one);
            Gizmos.color = new Color(0.2f, 0.2f, 0.8f, 0.5f);
            
            // AUTD3デバイスの簡易描画 (目安として 192mm x 151mm)
            Gizmos.DrawWireCube(new Vector3(0.096f, 0.075f, 0), new Vector3(0.192f, 0.151f, 0.01f));
        }
    }

    private void OnDestroy()
    {
        if (_autd != null)
        {
            _autd.Send(new Null());
            _autd.Close();
            _autd.Dispose();
            _autd = null;
            Debug.Log("[HAP_AUTDController] AUTD3 connection closed.");
        }
    }
}
