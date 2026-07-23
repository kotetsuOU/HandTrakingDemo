using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

#nullable enable

/// <summary>
/// 被験者向け UI および インゲームコントロールパネルの制御コンポーネント。
/// <para>
/// <see cref="EXP_ExperimentConfig.useUnityUI"/> が false の場合、
/// Unity UI への操作はスキップし、イベントのみを発火します。
/// </para>
/// </summary>
public class EXP_UIController : MonoBehaviour
{
    // =====================================================
    // Inspector Settings
    // =====================================================

    [Header("Mode")]
    [Tooltip("false にすると全 Unity UI 操作をスキップ（外部ディスプレイ使用時など）")]
    public bool useUnityUI = true;

    [Header("UI References")]
    [Tooltip("メッセージ / 教示テキスト（TMP_Text）")]
    public TMP_Text? messageText;

    [Tooltip("固視点オブジェクト（表示 / 非表示を切り替えます）")]
    public GameObject? fixationCross;

    [Tooltip("フィードバックテキスト（TMP_Text）")]
    public TMP_Text? feedbackText;

    [Tooltip("ブロック進捗スライダー（0〜1）")]
    public Slider? progressBar;

    [Tooltip("フェード用 CanvasGroup（alpha: 0=透明, 1=黒）")]
    public CanvasGroup? fadePanel;

    [Tooltip("被験者向け応答ボタンの親オブジェクト / CanvasGroup（応答フェーズ中のみ表示・有効化）")]
    public CanvasGroup? responseButtonPanel;

    [Header("In-Game Control Panel (Build Standalone Support)")]
    [Tooltip("Build 後 (exe 実行時) でも画面上で実験操作パネルを表示・開閉できるコンポーネント")]
    public EXP_InGameControlPanel? inGameControlPanel;

    // =====================================================
    // Events（外部システム連携用）
    // =====================================================

    /// <summary>メッセージテキストが更新されたときに発火</summary>
    public event Action<string>? OnMessageChanged;

    /// <summary>固視点の表示状態が変化したときに発火</summary>
    public event Action<bool>? OnFixationChanged;

    /// <summary>フィードバックテキストが更新されたときに発火</summary>
    public event Action<string>? OnFeedbackChanged;

    /// <summary>進捗値が更新されたときに発火（0〜1）</summary>
    public event Action<float>? OnProgressChanged;

    /// <summary>フェード状態が変化したときに発火（true = 黒）</summary>
    public event Action<bool>? OnFadeChanged;

    // =====================================================
    // Public API
    // =====================================================

    public void SetMessage(string message)
    {
        OnMessageChanged?.Invoke(message);

        if (!useUnityUI || messageText == null) return;
        messageText.text = message;
        messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    public void SetFixation(bool visible)
    {
        OnFixationChanged?.Invoke(visible);

        if (!useUnityUI || fixationCross == null) return;
        fixationCross.SetActive(visible);
    }

    public void SetFeedback(string feedback, Color? color = null)
    {
        OnFeedbackChanged?.Invoke(feedback);

        if (!useUnityUI || feedbackText == null) return;
        feedbackText.text = feedback;
        if (color.HasValue) feedbackText.color = color.Value;
        feedbackText.gameObject.SetActive(!string.IsNullOrEmpty(feedback));
    }

    public void SetProgress(float value)
    {
        OnProgressChanged?.Invoke(value);

        if (!useUnityUI || progressBar == null) return;
        progressBar.value = Mathf.Clamp01(value);
    }

    public void SetFade(bool black)
    {
        OnFadeChanged?.Invoke(black);

        if (!useUnityUI || fadePanel == null) return;
        fadePanel.alpha = black ? 1f : 0f;
        fadePanel.blocksRaycasts = black;
    }

    public void SetResponseButtonsActive(bool active)
    {
        if (!useUnityUI || responseButtonPanel == null) return;
        responseButtonPanel.alpha = active ? 1f : 0f;
        responseButtonPanel.interactable = active;
        responseButtonPanel.blocksRaycasts = active;
    }

    public void ClearAll()
    {
        SetMessage("");
        SetFixation(false);
        SetFeedback("");
        SetResponseButtonsActive(false);
    }

    public void ShowCorrect() => SetFeedback("◯", Color.green);
    public void ShowIncorrect() => SetFeedback("✕", Color.red);
    public void ShowTimeout() => SetFeedback("Too Slow", Color.yellow);

    public void ShowFeedback(EXP_ResponseType responseType)
    {
        switch (responseType)
        {
            case EXP_ResponseType.Correct:   ShowCorrect();   break;
            case EXP_ResponseType.Incorrect: ShowIncorrect(); break;
            case EXP_ResponseType.Timeout:   ShowTimeout();   break;
            default:                         SetFeedback(""); break;
        }
    }

    void Awake()
    {
        if (inGameControlPanel == null)
            inGameControlPanel = GetComponent<EXP_InGameControlPanel>() ?? gameObject.AddComponent<EXP_InGameControlPanel>();
    }

    public void ToggleInGameControlPanel()
    {
        if (inGameControlPanel != null)
        {
            inGameControlPanel.ToggleVisibility();
        }
    }
}
