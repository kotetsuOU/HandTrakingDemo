using UnityEngine;
using System;

#nullable enable

/// <summary>
/// 実験で使用する全キーバインディングの設定構造体。
/// Inspector で自由にカスタムキーを設定・カスタマイズできます。
/// </summary>
[Serializable]
public class EXP_KeyBindings
{
    [Tooltip("実験開始キー（デフォルト: Space）")]
    public KeyCode startKey = KeyCode.Space;

    [Tooltip("次へ進む / 準備完了キー（デフォルト: Space）")]
    public KeyCode nextKey = KeyCode.Space;

    [Tooltip("実験中断キー（デフォルト: Escape）")]
    public KeyCode abortKey = KeyCode.Escape;

    [Tooltip("第 1 刺激選択キー（デフォルト: Z）")]
    public KeyCode choice1Key = KeyCode.Z;

    [Tooltip("第 2 刺激選択キー（デフォルト: X）")]
    public KeyCode choice2Key = KeyCode.X;
}

/// <summary>
/// キーボード・ゲームパッド両対応の参加者入力受付コンポーネント。
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

        if (Input.GetKeyDown(keyBindings.choice1Key)) Respond("Z");
        else if (Input.GetKeyDown(keyBindings.choice2Key)) Respond("X");
        else if (Input.GetKeyDown(keyBindings.nextKey)) Respond("Space");
        else if (Input.GetKeyDown(keyBindings.startKey)) Respond("Space");
    }

    private void Respond(string responseValue)
    {
        HasResponded = true;
        OnResponse?.Invoke(responseValue);
    }
}
