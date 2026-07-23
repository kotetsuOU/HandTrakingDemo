using UnityEngine;
using UnityEngine.UI;
using System;

#nullable enable

// TextMeshPro が存在しない環境でもコンパイルできるよう conditional using
#if !UNITY_EDITOR && UNITY_STANDALONE
using TMPro;
#else
using TMPro;
#endif

/// <summary>
/// 被験者向け UI の制御コンポーネント。
/// <para>
/// <see cref="EXP_ExperimentConfig.useUnityUI"/> が false の場合、
/// Unity UI への操作は一切スキップし、イベント（<see cref="OnMessageChanged"/> など）のみを発火します。
/// 外部表示システム（別ウィンドウ・別PCなど）と連携する場合はイベントを購読してください。
/// </para>
/// <para>
/// TextMeshPro が存在しない場合は <c>TMP_Text</c> の代わりに通常の <c>Text</c> を参照してください。
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

    /// <summary>
    /// メッセージテキストを設定します。
    /// </summary>
    /// <param name="message">表示するテキスト（空文字列で非表示）</param>
    public void SetMessage(string message)
    {
        OnMessageChanged?.Invoke(message);

        if (!useUnityUI || messageText == null) return;
        messageText.text = message;
        messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    /// <summary>
    /// 固視点の表示状態を設定します。
    /// </summary>
    public void SetFixation(bool visible)
    {
        OnFixationChanged?.Invoke(visible);

        if (!useUnityUI || fixationCross == null) return;
        fixationCross.SetActive(visible);
    }

    /// <summary>
    /// フィードバックテキストを設定します。
    /// </summary>
    /// <param name="feedback">表示テキスト（空文字列で非表示）</param>
    /// <param name="color">テキストカラー（null = 変更なし）</param>
    public void SetFeedback(string feedback, Color? color = null)
    {
        OnFeedbackChanged?.Invoke(feedback);

        if (!useUnityUI || feedbackText == null) return;
        feedbackText.text = feedback;
        if (color.HasValue) feedbackText.color = color.Value;
        feedbackText.gameObject.SetActive(!string.IsNullOrEmpty(feedback));
    }

    /// <summary>
    /// 進捗バーを更新します。
    /// </summary>
    /// <param name="value">進捗（0.0〜1.0）</param>
    public void SetProgress(float value)
    {
        OnProgressChanged?.Invoke(value);

        if (!useUnityUI || progressBar == null) return;
        progressBar.value = Mathf.Clamp01(value);
    }

    /// <summary>
    /// フェードパネルの表示状態を設定します（即時切替）。
    /// </summary>
    /// <param name="black">true = 黒画面、false = 透明</param>
    public void SetFade(bool black)
    {
        OnFadeChanged?.Invoke(black);

        if (!useUnityUI || fadePanel == null) return;
        fadePanel.alpha = black ? 1f : 0f;
        fadePanel.blocksRaycasts = black;
    }

    /// <summary>
    /// 全 UI 要素を非表示にします。試行開始時のクリーンアップに使用してください。
    /// </summary>
    public void ClearAll()
    {
        SetMessage("");
        SetFixation(false);
        SetFeedback("");
    }

    // =====================================================
    // Convenience Methods
    // =====================================================

    /// <summary>正解フィードバックを表示します（緑の「◯」）。</summary>
    public void ShowCorrect()
        => SetFeedback("◯", Color.green);

    /// <summary>不正解フィードバックを表示します（赤の「✕」）。</summary>
    public void ShowIncorrect()
        => SetFeedback("✕", Color.red);

    /// <summary>タイムアウトフィードバックを表示します（黄色の「Too Slow」）。</summary>
    public void ShowTimeout()
        => SetFeedback("Too Slow", Color.yellow);

    /// <summary>
    /// 応答種別に応じたフィードバックを自動表示します。
    /// </summary>
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
}
