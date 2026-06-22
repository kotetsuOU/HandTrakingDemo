using System;
using System.Linq;
using System.Net;
using AUTD3Sharp;
using AUTD3Sharp.Gain;
using AUTD3Sharp.Modulation;
using UnityEngine;
using static AUTD3Sharp.Units;

#nullable enable

public class MultiAUTD3Controller : MonoBehaviour
{
    public enum ControlMode
    {
        TargetOnly,       // 常に Target の位置にフォーカスを出力するモード
        CollisionBased    // HapCollisionDetectors の接触判定を使うモード
    }

    public enum NonCollisionBehavior
    {
        KeepLastHit,      // 最後に接触した位置にフォーカスを維持する
        KeepTarget,       // Target GameObject の位置に移動させる
        TurnOff           // 出力を停止する (Nullゲインなどを送る)
    }

    [Header("Mode Settings")]
    [Tooltip("出力モードの設定")]
    public ControlMode mode = ControlMode.TargetOnly;

    [Tooltip("CollisionBased モード時、接触していない場合の挙動")]
    public NonCollisionBehavior nonCollisionBehavior = NonCollisionBehavior.TurnOff;

    private Controller? _autd = null;
    public GameObject? Target = null;
    private Vector3 _oldPosition;
    private HapCollisionDetectors? _collisionDetector;
    private bool _isCurrentlyOff = false;

    void Awake()
    {
        _collisionDetector = GetComponent<HapCollisionDetectors>();

        try
        {
            _autd = Controller.Open(
                    FindObjectsByType<AUTD3Device>(FindObjectsSortMode.None).OrderBy(obj => obj.ID).Select(obj => new AUTD3(pos: obj.transform.position, rot: obj.transform.rotation)),
                    new AUTD3Sharp.Link.TwinCAT()
                );
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError(ex);
            UnityEngine.Debug.LogError("Failed to connect to real device via TwinCAT. Please ensure TwinCAT is running and configured correctly.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE
            UnityEngine.Application.Quit();
#endif
            return;
        }

        _autd!.Send(new Sine(freq: 150 * Hz, option: new SineOption()));

        if (Target != null && mode == ControlMode.TargetOnly)
        {
            _autd!.Send(new Focus(pos: Target.transform.position, option: new FocusOption()));
            _oldPosition = Target.transform.position;
        }
    }

    private void Update()
    {
        if (_autd == null) return;

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
                // 接触中：当接位置へフォーカス
                currentFocusPos = _collisionDetector.HitPosition;
                shouldUpdateFocus = true;
                _isCurrentlyOff = false;
            }
            else
            {
                // 接触していない時の挙動分岐
                switch (nonCollisionBehavior)
                {
                    case NonCollisionBehavior.KeepLastHit:
                        // 前回フォーカスした位置 (_oldPosition) をそのまま持続
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
                        // ここで出力を停止する。1度だけNullなどを送るよう管理
                        if (!_isCurrentlyOff)
                        {
                            shouldTurnOff = true;
                            _isCurrentlyOff = true;
                        }
                        break;
                }
            }
        }

        // デバイスへの送信処理
        if (shouldTurnOff)
        {
            // Nullゲイン（フォーカス無し）を送って超音波出力を止める
            _autd.Send(new Null());
            _oldPosition = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue); // リセット
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
