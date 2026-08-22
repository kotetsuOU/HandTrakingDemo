using System;
using System.Collections.Generic;
using System.Linq;
using AUTD3Sharp;
using AUTD3Sharp.Driver.Datagram;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
using UnityEngine;
using static AUTD3Sharp.Units;

#nullable enable

public class MultiAUTD3Controller : MonoBehaviour
{
    public enum ControlMode
    {
        TargetOnly,         // 常に Target の位置にフォーカスを出力するモード
        CollisionBased,     // HapCollisionDetectors の接触判定を使うモード
        IndependentFocus    // 左右のデバイスがそれぞれ独立してフォーカスを出力するモード
    }

    public enum OutputSide
{
    Both,       // 両方出す
    UpperOnly,   // 左グループ（0,1,6,7）だけ
    DownOnly,   // 右グループ（2,3,4,5）だけ
    None
}
    [Tooltip("実行中にA/S/Dキーでモード切替を許可する（実験集はオフ）")]
    public bool enableDebugKeys = false;

    public enum NonCollisionBehavior
    {
        KeepLastHit,        // 最後に接触した位置にフォーカスを維持する
        KeepTarget,         // Target GameObject の位置に移動させる
        TurnOff             // 出力を停止する (Nullゲインなどを送る)
    }

    [Header("Debug Settings")]
    [Tooltip("TwinCATなしでテストする場合はONにする（Nopリンクを使用）")]
    public bool useMock = false;

    [Header("Mode Settings")]
    [Tooltip("出力モードの設定")]
    public ControlMode mode = ControlMode.TargetOnly;

    [Tooltip("CollisionBased モード時、接触していない場合の挙動")]
    public NonCollisionBehavior nonCollisionBehavior = NonCollisionBehavior.TurnOff;

    [Header("Target Settings")]
    public GameObject? Target = null;

    [Tooltip("IndependentFocus モード時の第2フォーカス位置")]
    public GameObject? Target2 = null;

    [Header("Independent Focus Settings")]
    [Tooltip("Target に対応するデバイスのインデックス一覧（左側グループ）")]
    public int[] upperDeviceIndices = new[] { 0, 1, 6, 7 };

    [Tooltip("Target2 に対応するデバイスのインデックス一覧（右側グループ）")]
    public int[] downDeviceIndices = new[] { 2, 3, 4, 5 };

    [Tooltip("実行中に 1 / 2 / 3 キーで切り替え可能")]
    public OutputSide outputSide = OutputSide.Both; 

    private Controller? _autd = null;
    private Vector3? _oldPosition;
    private Vector3? _oldPosition2;
    private HapCollisionDetectors? _collisionDetector;
    private bool _isCurrentlyOff = false;

    void Awake()
    {
        _collisionDetector = GetComponent<HapCollisionDetectors>();

        try
        {
            var devices = FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None)
                .OrderBy(obj => obj.ID)
                .Select(obj => new AUTD3(pos: obj.transform.position, rot: obj.transform.rotation));

            if (useMock)
            {
                _autd = Controller.Open(devices, new AUTD3Sharp.Link.Nop());
                UnityEngine.Debug.Log("AUTD3: Nopリンク（モック）で起動しました。");
            }
            else
            {
                _autd = Controller.Open(devices, new AUTD3Sharp.Link.TwinCAT());
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError(ex);
            UnityEngine.Debug.LogError("Failed to connect to real device via TwinCAT. Please ensure TwinCAT is running and configured correctly.");
            return;
        }

        _autd.Send(new Sine(freq: 150 * Hz, option: new SineOption()));

        if (mode == ControlMode.TargetOnly && Target != null)
        {
            _autd.Send(new Focus(pos: Target.transform.position, option: new FocusOption()));
            _oldPosition = Target.transform.position;
        }
        else if (mode == ControlMode.IndependentFocus && (Target != null || Target2 != null))
        {
            SendIndependentFocus(Target?.transform.position, Target2?.transform.position);
            _oldPosition = Target?.transform.position;
            _oldPosition2 = Target2?.transform.position;
        }
    }

    private void SendIndependentFocus(Vector3? pos1, Vector3? pos2)
    {
        var leftSet = new HashSet<int>(upperDeviceIndices);
        var rightSet = new HashSet<int>(downDeviceIndices);

        // ① 指示書を空で作って、必要なぶんだけ入れる
        var gainMap = new Dictionary<object, IGain>();
        if (pos1.HasValue)
            gainMap["left"] = new Focus(pos: pos1.Value, option: new FocusOption());
        if (pos2.HasValue)
            gainMap["right"] = new Focus(pos: pos2.Value, option: new FocusOption());

        // ② 両方ないなら全停止
        if (gainMap.Count == 0)
        {
            _autd!.Send(new Null());
            return;
        }

        // ③ 名札付け。座標がない側には名札を付けない
        var gain = new GainGroup(
            keyMap: dev => tr => (pos1.HasValue && leftSet.Contains(dev.Idx()))  ? "left"
                            : (pos2.HasValue && rightSet.Contains(dev.Idx())) ? "right"
                            : null,
            gainMap: gainMap
        );
        _autd!.Send(gain);
    }
        private Vector3? CurrentPos1()
    {
        if(outputSide == OutputSide.None) return null;
        if (outputSide == OutputSide.DownOnly) return null;   // 右だけモードなら左は出さない
        if (Target == null) return null;                       // 設定し忘れ対策
        return Target.transform.position;
    }

    private Vector3? CurrentPos2()
    {
        if(outputSide == OutputSide.None) return null;
        if (outputSide == OutputSide.UpperOnly) return null;    // 左だけモードなら右は出さない
        if (Target2 == null) return null;
        return Target2.transform.position;
    }
    public void SetMode(OutputSide side)
    {
        outputSide = side;
        if (_autd == null) return;

        var pos1 = CurrentPos1();
        var pos2 = CurrentPos2();

        SendIndependentFocus(pos1, pos2);

        _oldPosition  = pos1;
        _oldPosition2 = pos2;
    }

    public void StopOutput()
    {
        SetMode(OutputSide.None);
    }

    private void Update()
    {
        if (enableDebugKeys)
        {
            if (Input.GetKeyDown(KeyCode.A)) SetMode(OutputSide.UpperOnly);
            if (Input.GetKeyDown(KeyCode.S)) SetMode(OutputSide.DownOnly);
            if (Input.GetKeyDown(KeyCode.D)) SetMode(OutputSide.Both);
        }

        if (_autd == null) return;

        if (mode == ControlMode.IndependentFocus)
        {
            var pos1 = CurrentPos1();
            var pos2 = CurrentPos2();

            if (pos1 != _oldPosition || pos2 != _oldPosition2)
            {
                SendIndependentFocus(pos1, pos2);
                _oldPosition = pos1;
                _oldPosition2 = pos2;
            }
            return;
        }

        Vector3 currentFocusPos = Vector3.zero;
        bool shouldUpdateFocus = false;
        bool shouldTurnOff = false;

        if (mode == ControlMode.TargetOnly)
        {
            if (Target != null)
            {
                currentFocusPos = Target.transform.position;
                shouldUpdateFocus = true;
            }
        }
        else if (mode == ControlMode.CollisionBased)
        {
            if (_collisionDetector != null && _collisionDetector.IsColliding)
            {
                currentFocusPos = _collisionDetector.HitPosition;
                shouldUpdateFocus = true;
                _isCurrentlyOff = false;
            }
            else
            {
                switch (nonCollisionBehavior)
                {
                    case NonCollisionBehavior.KeepLastHit:
                        break;

                    case NonCollisionBehavior.KeepTarget:
                        if (Target != null)
                        {
                            currentFocusPos = Target.transform.position;
                            shouldUpdateFocus = true;
                        }
                        _isCurrentlyOff = false;
                        break;

                    case NonCollisionBehavior.TurnOff:
                        if (!_isCurrentlyOff)
                        {
                            shouldTurnOff = true;
                            _isCurrentlyOff = true;
                        }
                        break;
                }
            }
        }

        if (shouldTurnOff)
        {
            _autd.Send(new Null());
            _oldPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        }
        else if (shouldUpdateFocus && currentFocusPos != _oldPosition)
        {
            _autd.Send(new Focus(pos: currentFocusPos, option: new FocusOption()));
            _oldPosition = currentFocusPos;
        }
    }
    private void OnApplicationQuit()
    {
        _autd?.Dispose();
    }
}

#nullable restore
