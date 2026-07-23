using UnityEngine;
using System;
using System.Collections.Generic;

#nullable enable

/// <summary>
/// キーボード・ゲームパッド両対応の参加者入力受付コンポーネント。
/// <para>
/// - キーボード: Unity 旧 Input API（<see cref="Input.GetKeyDown"/>）で処理します。
/// - ゲームパッド: Unity 新 Input System（<see cref="UnityEngine.InputSystem.Gamepad"/>）で処理します。
///   新 Input System が有効でない場合は Gamepad 入力は無効になります。
/// </para>
/// <para>
/// 応答を受け取ったときは <see cref="OnResponse"/> イベントが発火します。
/// <see cref="EXP_ExperimentManager"/> から <see cref="StartListening"/> /
/// <see cref="StopListening"/> を呼んで受付を制御してください。
/// </para>
/// </summary>
public class EXP_InputHandler : MonoBehaviour
{
    // =====================================================
    // Inspector Settings
    // =====================================================

    [Header("Input Settings")]
    [Tooltip("使用するデバイス種別")]
    public EXP_InputDevice inputDevice = EXP_InputDevice.Any;

    [Tooltip("キーボード使用時の応答キーリスト（複数のキーを同時に登録できます）")]
    public KeyCode[] responseKeys = new KeyCode[] { KeyCode.Z, KeyCode.X };

    [Tooltip("ゲームパッド使用時のボタン名リスト（InputSystem の Control 名）\n"
           + "例: buttonSouth, buttonNorth, buttonEast, buttonWest,\n"
           + "    leftTrigger, rightTrigger, leftShoulder, rightShoulder")]
    public string[] gamepadButtons = new string[] { "buttonSouth", "buttonNorth" };

    [Header("Behavior")]
    [Tooltip("1試行中の二重入力を防ぐ（最初の入力のみ有効）")]
    public bool blockAfterFirstResponse = true;

    // =====================================================
    // State (Read-Only)
    // =====================================================

    /// <summary>現在入力を受け付けているかどうか</summary>
    public bool IsListening { get; private set; } = false;

    /// <summary>すでに応答を受け取ったかどうか</summary>
    public bool HasResponded { get; private set; } = false;

    // =====================================================
    // Events
    // =====================================================

    /// <summary>
    /// 参加者が応答したときに発火します。
    /// 引数は応答したキーコード名またはゲームパッドボタン名です。
    /// </summary>
    public event Action<string>? OnResponse;

    // =====================================================
    // Unity Lifecycle
    // =====================================================

    void Update()
    {
        if (!IsListening || (blockAfterFirstResponse && HasResponded)) return;

        CheckKeyboard();
        CheckGamepad();
    }

    // =====================================================
    // Public API
    // =====================================================

    /// <summary>入力受け付けを開始します。HasResponded をリセットします。</summary>
    public void StartListening()
    {
        HasResponded = false;
        IsListening  = true;
    }

    /// <summary>入力受け付けを停止します。</summary>
    public void StopListening()
    {
        IsListening = false;
    }

    /// <summary>HasResponded フラグのみをリセットします（IsListening は変更しません）。</summary>
    public void ResetResponse()
    {
        HasResponded = false;
    }

    // =====================================================
    // Private Input Polling
    // =====================================================

    private void CheckKeyboard()
    {
        if (inputDevice != EXP_InputDevice.Keyboard && inputDevice != EXP_InputDevice.Any)
            return;

        foreach (var key in responseKeys)
        {
            if (Input.GetKeyDown(key))
            {
                Respond(key.ToString());
                return;
            }
        }
    }

    private void CheckGamepad()
    {
        if (inputDevice != EXP_InputDevice.Gamepad && inputDevice != EXP_InputDevice.Any)
            return;

#if ENABLE_INPUT_SYSTEM
        var gamepad = UnityEngine.InputSystem.Gamepad.current;
        if (gamepad == null) return;

        foreach (var btnName in gamepadButtons)
        {
            var control = gamepad.FindControl(btnName)
                as UnityEngine.InputSystem.Controls.ButtonControl;
            if (control != null && control.wasPressedThisFrame)
            {
                Respond(btnName);
                return;
            }
        }
#endif
    }

    private void Respond(string responseValue)
    {
        if (blockAfterFirstResponse) HasResponded = true;
        OnResponse?.Invoke(responseValue);
    }
}
