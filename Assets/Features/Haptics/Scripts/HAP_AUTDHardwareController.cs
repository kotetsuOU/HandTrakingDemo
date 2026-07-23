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
using AUTD3Sharp.Gain.Holo;
using AUTD3Sharp.Modulation;
using static AUTD3Sharp.Units;
#endif

#nullable enable

/// <summary>
/// AUTD3デバイス群との物理接続（TwinCAT / SOEM / Simulator）、
/// およびハードウェア設定（Modulation, Silencer, Fan, Temperature）と手動操作APIを管理する専用コントローラー。
/// </summary>
public class HAP_AUTDHardwareController : MonoBehaviour
{
    [Header("Link Settings")]
    [Tooltip("AUTDデバイスとの接続方法を選択します")]
    public AUTDLinkType linkType = AUTDLinkType.TwinCAT;

    [Tooltip("SOEM使用時のネットワークアダプタ名（必要であれば指定）")]
    public string soemAdapterName = "";

    [Header("Hardware Environment")]
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

    // 非Serializeなプライベートサービスクラスインスタンス
    private readonly HAP_AUTDLinkService _linkService = new HAP_AUTDLinkService();
    private HAP_AUTDModulationService? _configService;

    public HAP_AUTDLinkService LinkService => _linkService;
    public object SendLock => _linkService.SendLock;
    public List<AUTD3Device> ConnectedDevices => _linkService.ConnectedDevices;
    public bool IsConnected => _linkService.IsConnected;

#if !USE_AUTD3_LEGACY
    public Client? Client => _linkService.Client;
    public Geometry? Geometry => _linkService.Geometry;
#else
    public Controller? Autd => _linkService.Autd;
#endif

#if !USE_AUTD3_LEGACY
    async void Awake()
#else
    void Awake()
#endif
    {
        _configService = new HAP_AUTDModulationService(_linkService);

#if !USE_AUTD3_LEGACY
        await _linkService.OpenAsync(linkType, soemAdapterName);
#else
        _linkService.Open(linkType, soemAdapterName);
#endif

        if (_linkService.IsConnected)
        {
            _configService.CheckAndApply(
                modulationMode, sineFrequency, staticAmplitude,
                silencerMode, silencerStepPhase, silencerStepAmplitude,
                enableFan, temperature);
        }
    }

    void Update()
    {
        if (!_linkService.IsConnected) return;

        _configService?.CheckAndApply(
            modulationMode, sineFrequency, staticAmplitude,
            silencerMode, silencerStepPhase, silencerStepAmplitude,
            enableFan, temperature);
    }

    public void ApplyModulation() => _configService?.ApplyModulation(modulationMode, sineFrequency, staticAmplitude);
    public void ApplySilencer() => _configService?.ApplySilencer(silencerMode, silencerStepPhase, silencerStepAmplitude);
    public void ApplyFan() => _configService?.ApplyFan(enableFan);
    public void ApplyTemperature() => _configService?.ApplyTemperature(temperature);

    // =========================================================================
    // 手動操作用 API (MANUAL APIs)
    // =========================================================================

    public void SetFan(bool on)
    {
        enableFan = on;
        ApplyFan();
    }

    public void SetNull()
    {
        _linkService.SendNull();
    }

#if USE_AUTD3_LEGACY
    public void Send(IDatagram datagram)
    {
        if (_linkService.Autd == null) return;
        lock (_linkService.SendLock) { _linkService.Autd.Send(datagram); }
    }

    public void SetFocus(Vector3 position, float amplitude = 1f, Vector3 offset = default)
    {
        if (_linkService.Autd == null) return;
        byte intensityVal = (byte)Mathf.Clamp(amplitude * 255f, 0f, 255f);
        var p = new AUTD3Sharp.Utils.Point3(position.x + offset.x, position.y + offset.y, position.z + offset.z);
        lock (_linkService.SendLock) { _linkService.Autd.Send(new Focus(p, new FocusOption { Intensity = new Intensity(intensityVal) })); }
    }

    public void SetHolo(IEnumerable<Vector3> positions, IEnumerable<float> amplitudesPa, HoloAlgorithm algorithm = HoloAlgorithm.GSPAT, Vector3 offset = default)
    {
        if (_linkService.Autd == null) return;
        
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
            lock (_linkService.SendLock) { _linkService.Autd.Send(new GSPAT(activeFoci, new GSPATOption())); }
        else
            lock (_linkService.SendLock) { _linkService.Autd.Send(new Naive(activeFoci, new NaiveOption())); }
    }
#endif

#if !USE_AUTD3_LEGACY
    private async void OnDestroy()
    {
        await _linkService.CloseAsync();
    }
#else
    private void OnDestroy()
    {
        _linkService.Close();
    }
#endif
}
