using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
#if !USE_AUTD3_LEGACY
using System.Threading.Tasks;
using AUTD3;
using AUTD3.Holo;
using static AUTD3.Units;
#else
using AUTD3Sharp;
using AUTD3Sharp.Link;
using AUTD3Sharp.Driver.Datagram;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
using static AUTD3Sharp.Units;
#endif

#nullable enable

/// <summary>
/// AUTD3デバイス群との物理接続（TwinCAT / SOEM / Simulator）、
/// およびハードウェア設定（Modulation, Silencer, Fan, Temperature）の管理を行う専用マネージャー。
/// 
/// HAP_AUTDController などの上位コンポーネントから参照され、
/// デバイス接続のライフサイクル管理と底層のデータ送信インターフェースを提供します。
/// </summary>
public class HAP_AUTDHardwareManager : MonoBehaviour
{
    [Header("Link Settings")]
    [Tooltip("AUTDデバイスとの接続方法を選択します")]
    public AUTDLinkType linkType = AUTDLinkType.TwinCAT;

    [Tooltip("SOEM使用時のネットワークアダプタ名（必要であれば指定）")]
    public string soemAdapterName = "";

    [Header("Hardware Settings")]
    [Tooltip("環境温度（摂氏）。音速計算に使用され、焦点の正確さに影響します。室温に合わせてください。")]
    public float temperature = 25f;

    [Tooltip("デバイス冷却ファンのON/OFF。高出力で長時間使用する場合は ON にしてください。")]
    public bool enableFan = false;

    [Header("Modulation Settings")]
    [Tooltip("変調モード。\nSine: 指定周波数で明滅（ブーンという感触）。\nStatic: 連続出力（押される感触）。")]
    public ModulationMode modulationMode = ModulationMode.Sine;

    [Tooltip("サイン波の変調周波数 (Hz)。一般的に人間の皮膚は 150〜200Hz で最も感度が高くなります。")]
    public float sineFrequency = 150f;

    [Tooltip("定常波(Static)の振幅 (0.0〜1.0)。通常は1.0を使用します。")]
    public float staticAmplitude = 1.0f;

    [Header("Silencer Settings")]
    [Tooltip("サイレンサーのモード。可聴ノイズ（ジージー音）を減らします。\nFixedUpdateRate: 強度と位相のステップで指定。\nFixedCompletionTime: 完了時間で指定。")]
    public SilencerMode silencerMode = SilencerMode.FixedUpdateRate;

    [Tooltip("位相の変化ステップ。小さいほど静かになりますが、応答が遅れます。")]
    public ushort silencerStepPhase = 500;

    [Tooltip("振幅の変化ステップ。小さいほど静かになりますが、応答が遅れます。")]
    public ushort silencerStepAmplitude = 65535;

    [HideInInspector]
    public List<AUTD3Device> connectedDevices = new List<AUTD3Device>();

#if !USE_AUTD3_LEGACY
    private Client? _client = null;
    public Client? Client => _client;

    private Geometry? _geometry = null;
    public Geometry? Geometry => _geometry;
#else
    private Controller? _autd = null;
    public Controller? Autd => _autd;
#endif

    public bool IsConnected =>
#if !USE_AUTD3_LEGACY
        _client != null && _geometry != null;
#else
        _autd != null;
#endif

    // スレッドセーフかつ非同期に送信を行うためのロック
    private readonly object _sendLock = new object();
    public object SendLock => _sendLock;

    // 前回の設定を記憶して変更を検知するためのフィールド
    private ModulationMode _prevModMode;
    private float _prevSineFreq;
    private float _prevStaticAmp;

    private SilencerMode _prevSilencerMode;
    private ushort _prevSilStepPhase;
    private ushort _prevSilStepAmp;

    private bool _prevFanState;
#if USE_AUTD3_LEGACY
    private float _prevTemperature;
#endif

#if !USE_AUTD3_LEGACY
    async void Awake()
#else
    void Awake()
#endif
    {
        // シーン内のすべての AUTD3Device コンポーネントを収集し、ID順にソートしてデバイス配置情報を生成
        connectedDevices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None)
            .OrderBy(obj => obj.ID)
            .ToList();

#if !USE_AUTD3_LEGACY
        var devices = connectedDevices.Select(obj => new Autd3(obj.transform.position, obj.transform.rotation)).ToList();
        _geometry = new Geometry(devices);
#else
        var devices = connectedDevices.Select(obj => new AUTD3Sharp.AUTD3(pos: obj.transform.position, rot: obj.transform.rotation)).ToList();
#endif

        Debug.Log($"[HAP_AUTDHardwareManager] Attempting to connect to AUTD3. Found {devices.Count} AUTD3Device components in the scene.");

        try
        {
#if USE_AUTD3_LEGACY
            var option = new AUTD3Sharp.SenderOption { Timeout = AUTD3Sharp.Duration.FromMillis(5000) };
#endif
            switch (linkType)
            {
                case AUTDLinkType.TwinCAT:
#if !USE_AUTD3_LEGACY
                    _client = await Client.OpenAsync(_geometry, AUTD3.Link.TwinCATLinkOption.Local(), new ClientConfig());
#else
                    _autd = Controller.OpenWithOption(devices, new AUTD3Sharp.Link.TwinCAT(), option);
#endif
                    Debug.Log("[HAP_AUTDHardwareManager] Successfully connected to AUTD3 via TwinCAT.");
                    break;

#if USE_AUTD3_LEGACY
                case AUTDLinkType.SOEM:
                    Debug.LogWarning("[HAP_AUTDHardwareManager] SOEMを使用するには Unity Package Manager から SOEMリンクパッケージ のインストールが必要です。");
                    break;
#endif

                case AUTDLinkType.Simulator:
#if !USE_AUTD3_LEGACY
                    Debug.LogWarning("[HAP_AUTDHardwareManager] Simulator (Remote link) is not available in the current v31 installation. Link initialization skipped.");
#else
                    Debug.LogWarning("[HAP_AUTDHardwareManager] Simulatorを使用するには autd3-server を起動しておく必要があります。");
                    var simLink = new AUTD3Sharp.Link.Remote(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), 8080), new AUTD3Sharp.Link.RemoteOption());
                    _autd = Controller.OpenWithOption(devices, simLink, option);
                    Debug.Log("[HAP_AUTDHardwareManager] Successfully connected to AUTD3 via Simulator (Remote).");
#endif
                    break;
            }

            if (!IsConnected)
            {
                Debug.LogWarning("[HAP_AUTDHardwareManager] Link initialization was skipped or failed.");
                return;
            }

            // 初期設定の送信
#if USE_AUTD3_LEGACY
            ApplyTemperature();
#endif
            ApplyModulation();
            ApplySilencer();
            ApplyFan();

            // 初期状態は Null 出力
            SendNull();

            Debug.Log("[HAP_AUTDHardwareManager] Successfully connected and initialized AUTD3 devices.");
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            Debug.LogError("[HAP_AUTDHardwareManager] Failed to connect to AUTD3. Ensure the target link is running.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
            Application.Quit();
#endif
        }
    }

    void Update()
    {
        if (!IsConnected) return;

        // インスペクターの設定変更を監視して適用
        CheckForConfigChanges();
    }

    /// <summary>
    /// インスペクターで変更されたハードウェア設定を検知し、デバイスに適用します。
    /// </summary>
    public void CheckForConfigChanges()
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
#if USE_AUTD3_LEGACY
        if (_prevTemperature != temperature) ApplyTemperature();
#endif
    }

    /// <summary>
    /// 出力を停止 (Null 送信) します。
    /// </summary>
#if !USE_AUTD3_LEGACY
    public async void SendNull()
    {
        if (_client == null || _geometry == null) return;
        try
        {
            using var builder = _client.DatagramBuilder();
            var buffer = _geometry.PatternBuffer();
            Pattern.Null(buffer);
            builder.Push(new Pattern(PatternBank.B0, buffer));

            using var frames = builder.Build();
            foreach (var frame in frames)
            {
                await _client.SendCheckedAsync(frame);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDHardwareManager] Failed to send Null: {ex.Message}");
        }
    }
#else
    public void SendNull()
    {
        if (_autd == null) return;
        try
        {
            lock (_sendLock)
            {
                _autd.Send(new Null());
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDHardwareManager] Failed to send Null: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// 変調（Modulation）設定をデバイスに送信します。
    /// </summary>
#if !USE_AUTD3_LEGACY
    public async void ApplyModulation()
    {
        if (_client == null || _geometry == null) return;

        try
        {
            using var builder = _client.DatagramBuilder();
            using var modulationBuffer = Modulation.ModulationBuffer();

            switch (modulationMode)
            {
                case ModulationMode.Sine:
                    Modulation.Sine(sineFrequency * Hz, new SineOption(), modulationBuffer);
                    builder.Push(new Modulation(SamplingConfig.Freq4k, modulationBuffer));
                    break;
                case ModulationMode.Static:
                    byte intensity = (byte)Mathf.Clamp(staticAmplitude * 255f, 0, 255);
                    Modulation.Constant(intensity, modulationBuffer);
                    builder.Push(new Modulation(SamplingConfig.Freq4k, modulationBuffer));
                    break;
            }

            using var frames = builder.Build();
            foreach (var frame in frames) { await _client.SendCheckedAsync(frame); }

            _prevModMode = modulationMode;
            _prevSineFreq = sineFrequency;
            _prevStaticAmp = staticAmplitude;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDHardwareManager] Failed to apply modulation: {ex.Message}");
        }
    }
#else
    public void ApplyModulation()
    {
        if (_autd == null) return;

        try
        {
            switch (modulationMode)
            {
                case ModulationMode.Sine:
                    lock (_sendLock) { _autd.Send(new Sine(freq: sineFrequency * Hz, option: new SineOption())); }
                    break;
                case ModulationMode.Static:
                    lock (_sendLock) { _autd.Send(new Static()); }
                    break;
            }

            _prevModMode = modulationMode;
            _prevSineFreq = sineFrequency;
            _prevStaticAmp = staticAmplitude;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDHardwareManager] Failed to apply modulation: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// サイレンサー（Silencer）設定をデバイスに送信します。
    /// </summary>
#if !USE_AUTD3_LEGACY
    public async void ApplySilencer()
    {
        if (_client == null || _geometry == null) return;

        try
        {
            using var builder = _client.DatagramBuilder();

            switch (silencerMode)
            {
                case SilencerMode.Disabled:
                    builder.Push(SetSilencer.Disable());
                    break;
                case SilencerMode.FixedUpdateRate:
                    builder.Push(new SetSilencer(new FixedUpdateRate(intensity: silencerStepAmplitude, phase: silencerStepPhase)));
                    break;
                case SilencerMode.FixedCompletionTime:
                    builder.Push(new SetSilencer(new FixedCompletionTime()));
                    break;
            }

            using var frames = builder.Build();
            foreach (var frame in frames) { await _client.SendCheckedAsync(frame); }

            _prevSilencerMode = silencerMode;
            _prevSilStepPhase = silencerStepPhase;
            _prevSilStepAmp = silencerStepAmplitude;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDHardwareManager] Failed to apply silencer: {ex.Message}");
        }
    }
#else
    public void ApplySilencer()
    {
        if (_autd == null) return;

        try
        {
            switch (silencerMode)
            {
                case SilencerMode.Disabled:
                    lock (_sendLock) { _autd.Send(Silencer.Disable()); }
                    break;
                case SilencerMode.FixedUpdateRate:
                    lock (_sendLock) { _autd.Send(new Silencer(new FixedUpdateRate { Intensity = silencerStepAmplitude, Phase = silencerStepPhase })); }
                    break;
                case SilencerMode.FixedCompletionTime:
                    lock (_sendLock) { _autd.Send(new Silencer(new FixedCompletionTime())); }
                    break;
            }

            _prevSilencerMode = silencerMode;
            _prevSilStepPhase = silencerStepPhase;
            _prevSilStepAmp = silencerStepAmplitude;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDHardwareManager] Failed to apply silencer: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// 冷却ファンの状態（ON/OFF）を適用します。
    /// </summary>
#if !USE_AUTD3_LEGACY
    public async void ApplyFan()
    {
        if (_client == null || _geometry == null) return;

        try
        {
            using var builder = _client.DatagramBuilder();
            builder.Push(new ForceFan(enableFan));

            using var frames = builder.Build();
            foreach (var frame in frames) { await _client.SendCheckedAsync(frame); }

            _prevFanState = enableFan;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDHardwareManager] Failed to apply fan state: {ex.Message}");
        }
    }
#else
    public void ApplyFan()
    {
        if (_autd == null) return;

        try
        {
            lock (_sendLock) { _autd.Send(new ForceFan(dev => enableFan)); }
            _prevFanState = enableFan;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDHardwareManager] Failed to apply fan state: {ex.Message}");
        }
    }

    /// <summary>
    /// 環境温度（摂氏）を適用します。
    /// </summary>
    public void ApplyTemperature()
    {
        if (_autd == null) return;

        try
        {
            _autd.Environment.SetSoundSpeedFromTemp(temperature);
            _prevTemperature = temperature;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[HAP_AUTDHardwareManager] Failed to set temperature: {ex.Message}");
        }
    }
#endif

#if !USE_AUTD3_LEGACY
    private async void OnDestroy()
    {
        if (_client != null)
        {
            using (var builder = _client.DatagramBuilder())
            {
                var buffer = _geometry!.PatternBuffer();
                Pattern.Null(buffer);
                builder.Push(new Pattern(PatternBank.B0, buffer));
                using var frames = builder.Build();
                foreach (var frame in frames)
                {
                    await _client.SendCheckedAsync(frame);
                }
            }

            await _client.CloseAsync();
            _client.Dispose();
            _client = null;
            Debug.Log("[HAP_AUTDHardwareManager] AUTD3 connection closed.");
        }
        if (_geometry != null)
        {
            _geometry.Dispose();
            _geometry = null;
        }
    }
#else
    private void OnDestroy()
    {
        if (_autd != null)
        {
            _autd.Send(new Null());
            _autd.Close();
            _autd.Dispose();
            _autd = null;
            Debug.Log("[HAP_AUTDHardwareManager] AUTD3 connection closed.");
        }
    }
#endif
}
