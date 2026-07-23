using UnityEngine;
using System;

#nullable enable

/// <summary>
/// 全実験パラダイム（2AFC, SingleStimulus, ABX, Adjustment）に対応するキーバインディング構造体。
/// Inspector 上で自由に設定・カスタマイズできます。
/// </summary>
[Serializable]
public class EXP_KeyBindings
{
    [Header("Common System Keys")]
    public KeyCode startKey = KeyCode.Space;
    public KeyCode nextKey = KeyCode.Space;
    public KeyCode abortKey = KeyCode.Escape;

    [Header("2AFC & ABX Keys")]
    public KeyCode choice1Key = KeyCode.Z;
    public KeyCode choice2Key = KeyCode.X;

    [Header("Single Stimulus (Yes/No) Keys")]
    public KeyCode yesKey = KeyCode.Z;
    public KeyCode noKey = KeyCode.X;

    [Header("Adjustment (Method of Adjustment) Keys")]
    public KeyCode upKey = KeyCode.UpArrow;
    public KeyCode downKey = KeyCode.DownArrow;
    public KeyCode confirmKey = KeyCode.Space;
}

/// <summary>
/// 全実験パラダイム対応の参加者入力受付コンポーネント。
/// </summary>
public class EXP_InputHandler : MonoBehaviour
{
    // =====================================================
    // Inspector Settings
    // =====================================================

    [Header("Input Settings")]
    public EXP_InputDevice inputDevice = EXP_InputDevice.Any;

    [Header("Key Bindings")]
    public EXP_KeyBindings keyBindings = new EXP_KeyBindings();

    [Header("Gamepad Settings")]
    public string[] gamepadButtons = new string[] { "buttonSouth", "buttonNorth" };

    [Header("Behavior")]
    public bool blockAfterFirstResponse = true;
    public bool runInBackground = true;

    // =====================================================
    // State (Read-Only)
    // =====================================================

    public bool IsListening { get; private set; } = false;
    public bool HasResponded { get; private set; } = false;

    // =====================================================
    // Events
    // =====================================================

    public event Action<string>? OnResponse;

    // =====================================================
    // Unity Lifecycle
    // =====================================================

    void Awake()
    {
        if (runInBackground)
            Application.runInBackground = true;
    }

    void Update()
    {
        if (!IsListening || (blockAfterFirstResponse && HasResponded)) return;
        CheckKeyboard();
    }

    // =====================================================
    // Public API
    // =====================================================

    public void StartListening() { HasResponded = false; IsListening = true; }
    public void StopListening()  { IsListening = false; }
    public void ResetResponse()  { HasResponded = false; }

    public void TriggerResponse(string responseValue)
    {
        if (!IsListening || (blockAfterFirstResponse && HasResponded)) return;
        Respond(responseValue);
    }

    // =====================================================
    // Private Helpers
    // =====================================================

    private void CheckKeyboard()
    {
        if (inputDevice != EXP_InputDevice.Keyboard && inputDevice != EXP_InputDevice.Any)
            return;

        // 2AFC / ABX
        if (Input.GetKeyDown(keyBindings.choice1Key)) Respond("Z");
        else if (Input.GetKeyDown(keyBindings.choice2Key)) Respond("X");
        // Adjustment
        else if (Input.GetKeyDown(keyBindings.upKey) || Input.GetKeyDown(KeyCode.W)) Respond("Up");
        else if (Input.GetKeyDown(keyBindings.downKey) || Input.GetKeyDown(KeyCode.S)) Respond("Down");
        else if (Input.GetKeyDown(keyBindings.confirmKey)) Respond("Space");
        // System
        else if (Input.GetKeyDown(keyBindings.nextKey)) Respond("Space");
        else if (Input.GetKeyDown(keyBindings.startKey)) Respond("Space");
    }

    private void Respond(string responseValue)
    {
        HasResponded = true;
        OnResponse?.Invoke(responseValue);
    }
}
