using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
#if !USE_AUTD3_LEGACY
using System.Threading.Tasks;
using AUTD3;
using AUTD3.Holo;
#else
using AUTD3Sharp;
using AUTD3Sharp.Link;
using AUTD3Sharp.Driver.Datagram;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
#endif

#nullable enable

/// <summary>
/// AUTD3デバイス群との物理接続（TwinCAT / SOEM / Simulator）のライフサイクル管理、
/// およびデータ送信を担当する純粋なC#サービスクラス (非MonoBehaviour)。
/// </summary>
public class HAP_AUTDLinkService
{
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

    private readonly object _sendLock = new object();
    public object SendLock => _sendLock;

    public List<AUTD3Device> ConnectedDevices { get; private set; } = new List<AUTD3Device>();

    /// <summary>
    /// AUTD3デバイスへ接続を開始します。
    /// </summary>
#if !USE_AUTD3_LEGACY
    public async Task OpenAsync(AUTDLinkType linkType, string soemAdapterName)
#else
    public void Open(AUTDLinkType linkType, string soemAdapterName)
#endif
    {
        ConnectedDevices = UnityEngine.Object.FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None)
            .OrderBy(obj => obj.ID)
            .ToList();

#if !USE_AUTD3_LEGACY
        var devices = ConnectedDevices.Select(obj => new Autd3(obj.transform.position, obj.transform.rotation)).ToList();
        _geometry = new Geometry(devices);
#else
        var devices = ConnectedDevices.Select(obj => new AUTD3Sharp.AUTD3(pos: obj.transform.position, rot: obj.transform.rotation)).ToList();
#endif

        Debug.Log($"[HAP_AUTDLinkService] Attempting to connect to AUTD3. Found {devices.Count} AUTD3Device components.");

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
                    Debug.Log("[HAP_AUTDLinkService] Successfully connected via TwinCAT.");
                    break;

#if USE_AUTD3_LEGACY
                case AUTDLinkType.SOEM:
                    Debug.LogWarning("[HAP_AUTDLinkService] SOEM link requires SOEM package.");
                    break;
#endif

                case AUTDLinkType.Simulator:
#if !USE_AUTD3_LEGACY
                    Debug.LogWarning("[HAP_AUTDLinkService] Simulator link is not available in v31.");
#else
                    var simLink = new AUTD3Sharp.Link.Remote(new System.Net.IPEndPoint(System.Net.IPAddress.Parse("127.0.0.1"), 8080), new AUTD3Sharp.Link.RemoteOption());
                    _autd = Controller.OpenWithOption(devices, simLink, option);
                    Debug.Log("[HAP_AUTDLinkService] Successfully connected via Simulator.");
#endif
                    break;
            }

            if (IsConnected)
            {
                SendNull();
                Debug.Log("[HAP_AUTDLinkService] Initialization complete.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            Debug.LogError("[HAP_AUTDLinkService] Failed to connect to AUTD3.");
        }
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
            Debug.LogWarning($"[HAP_AUTDLinkService] Failed to send Null: {ex.Message}");
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
            Debug.LogWarning($"[HAP_AUTDLinkService] Failed to send Null: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// デバイス接続を破棄・クローズします。
    /// </summary>
#if !USE_AUTD3_LEGACY
    public async Task CloseAsync()
    {
        if (_client != null)
        {
            SendNull();
            await _client.CloseAsync();
            _client.Dispose();
            _client = null;
            Debug.Log("[HAP_AUTDLinkService] AUTD3 connection closed.");
        }
        if (_geometry != null)
        {
            _geometry.Dispose();
            _geometry = null;
        }
    }
#else
    public void Close()
    {
        if (_autd != null)
        {
            _autd.Send(new Null());
            _autd.Close();
            _autd.Dispose();
            _autd = null;
            Debug.Log("[HAP_AUTDLinkService] AUTD3 connection closed.");
        }
    }
#endif
}
