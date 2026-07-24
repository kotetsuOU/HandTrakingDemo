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
    public KeyCode startKey = KeyCode.F1;
    public KeyCode nextKey = KeyCode.F1;
    public KeyCode abortKey = KeyCode.Escape;

    [Header("2AFC & ABX Keys")]
    public KeyCode choice1Key = KeyCode.F2;
    public KeyCode choice2Key = KeyCode.F3;

    [Header("Single Stimulus (Yes/No) Keys")]
    public KeyCode yesKey = KeyCode.F2;
    public KeyCode noKey = KeyCode.F3;

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

    /// <summary>
    /// Unity Editor の EditorWindow (IMGUI) にフォーカスが当たっている場合、
    /// Input.GetKeyDown() はキーイベントを受け取れません。
    /// OnGUI() 内で Event.current を使うことで、EditorWindow フォーカス中でも
    /// キーボード入力を確実にキャッチします。
    /// </summary>
    void OnGUI()
    {
        if (!IsListening || (blockAfterFirstResponse && HasResponded)) return;
        if (inputDevice != EXP_InputDevice.Keyboard && inputDevice != EXP_InputDevice.Any) return;

        var e = Event.current;
        if (e == null || e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;

        string? response = GetResponseForKeyCode(e.keyCode);
        if (response != null)
        {
            e.Use();
            Debug.Log($"[EXP_InputHandler] OnGUI キー検知: {e.keyCode} → '{response}'");
            Respond(response);
        }
    }

    // =====================================================
    // Public API
    // =====================================================

    public void StartListening() 
    { 
        HasResponded = false; 
        IsListening = true; 
        Debug.Log($"[EXP_InputHandler] StartListening 開始。inputDevice={inputDevice}, blockAfterFirstResponse={blockAfterFirstResponse}");
    }
    public void StopListening()  
    { 
        IsListening = false; 
        Debug.Log("[EXP_InputHandler] StopListening");
    }
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
        if (Input.GetKeyDown(keyBindings.choice1Key)) { Debug.Log($"[EXP_InputHandler] キー検知: choice1Key={keyBindings.choice1Key}"); Respond("Z"); }
        else if (Input.GetKeyDown(keyBindings.choice2Key)) { Debug.Log($"[EXP_InputHandler] キー検知: choice2Key={keyBindings.choice2Key}"); Respond("X"); }
        // Adjustment
        else if (Input.GetKeyDown(keyBindings.upKey) || Input.GetKeyDown(KeyCode.W)) { Debug.Log($"[EXP_InputHandler] キー検知: upKey"); Respond("Up"); }
        else if (Input.GetKeyDown(keyBindings.downKey) || Input.GetKeyDown(KeyCode.S)) { Debug.Log($"[EXP_InputHandler] キー検知: downKey"); Respond("Down"); }
        else if (Input.GetKeyDown(keyBindings.confirmKey)) { Debug.Log($"[EXP_InputHandler] キー検知: confirmKey={keyBindings.confirmKey}"); Respond("Space"); }
        // System
        else if (Input.GetKeyDown(keyBindings.nextKey)) { Debug.Log($"[EXP_InputHandler] キー検知: nextKey={keyBindings.nextKey}"); Respond("Space"); }
        else if (Input.GetKeyDown(keyBindings.startKey)) { Debug.Log($"[EXP_InputHandler] キー検知: startKey={keyBindings.startKey}"); Respond("Space"); }
    }

    /// <summary>
    /// 指定キーコードに対応するレスポンス文字列を返します。
    /// Update (Input.GetKeyDown) と OnGUI (Event.current) の両方から共用されます。
    /// </summary>
    private string? GetResponseForKeyCode(KeyCode key)
    {
        // 2AFC / ABX
        if (key == keyBindings.choice1Key) return "Z";
        if (key == keyBindings.choice2Key) return "X";
        // Adjustment
        if (key == keyBindings.upKey || key == KeyCode.W) return "Up";
        if (key == keyBindings.downKey || key == KeyCode.S) return "Down";
        if (key == keyBindings.confirmKey) return "Space";
        // System
        if (key == keyBindings.nextKey) return "Space";
        if (key == keyBindings.startKey) return "Space";
        return null;
    }

    private void Respond(string responseValue)
    {
        Debug.Log($"[EXP_InputHandler] Respond('{responseValue}') 発火。IsListening={IsListening}, HasResponded={HasResponded}, OnResponseリスナー数={OnResponse?.GetInvocationList()?.Length ?? 0}");
        HasResponded = true;
        OnResponse?.Invoke(responseValue);
    }
}
