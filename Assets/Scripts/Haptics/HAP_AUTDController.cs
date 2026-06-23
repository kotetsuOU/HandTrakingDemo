using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using AUTD3Sharp;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
using AUTD3Sharp.Gain.Holo;
using static AUTD3Sharp.Units;

#nullable enable

/// <summary>
/// HCD_Pipeline によって計算された接触重心を受け取り、
/// AUTD3デバイス群に GSPAT (Acoustic Holography) を用いてマルチフォーカス出力を行うコントローラー。
/// </summary>
public class HAP_AUTDController : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("重心座標を提供する HCD_Pipeline")]
    public HCD_Pipeline hcdPipeline = null!;

    [Header("Acoustic Settings")]
    [Tooltip("超音波の出力強度 (Pascal)")]
    public float focusIntensityPascal = 10000f;
    
    [Tooltip("変調周波数 (Hz)")]
    public float modulationFrequency = 150f;

    private Controller? _autd = null;
    private bool _isCurrentlyOff = true;

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
            
            // 基本の変調（サイン波）を送信
            _autd.Send(new Sine(freq: modulationFrequency * Hz, option: new SineOption()));
            
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
        if (_autd == null || hcdPipeline == null) return;

        // トラッカーから安定化・追跡済みのクラスタリストを取得
        var trackedClusters = hcdPipeline.GetTrackedClusters();

        // 生存しており、かつ Force が有効なクラスタを抽出
        var activeFoci = trackedClusters
            .Where(c => c.IsAlive && c.Force > 0.01f)
            .Select(c => (
                new AUTD3Sharp.Utils.Point3(c.Centroid.x, c.Centroid.y, c.Centroid.z), 
                (focusIntensityPascal * c.Force) * Pa
            )).ToArray();

        if (activeFoci.Length > 0)
        {
            // 接触している箇所がある場合、GSPAT でフォーカスを生成 (Force値で個別の振幅を制御)
            var gspat = new GSPAT(activeFoci, new GSPATOption());
            
            _autd.Send(gspat);
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

    private void OnDestroy()
    {
        if (_autd != null)
        {
            // 終了時に出力を停止して切断
            _autd.Send(new Null());
            _autd.Close();
            _autd.Dispose();
            _autd = null;
            Debug.Log("[HAP_AUTDController] AUTD3 connection closed.");
        }
    }
}
