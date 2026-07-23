using UnityEngine;

#nullable enable

/// <summary>
/// 被験者実験の教示・説明・同意文書テキストを保持・管理する ScriptableObject。
/// Project ウィンドウで右クリック ➔ Create ➔ EXP ➔ InstructionTextConfig から作成できます。
/// </summary>
[CreateAssetMenu(fileName = "NewInstructionTextConfig", menuName = "EXP/InstructionTextConfig")]
public class EXP_InstructionTextConfig : ScriptableObject
{
    [Header("1. Informed Consent (同意文書)")]
    [TextArea(3, 6)]
    public string consentTitle = "【実験協力・同意のお願い】";

    [TextArea(5, 12)]
    public string consentBody = "本実験は触覚知覚特性の測定を目的としています。\n"
                              + "収集されたデータは完全匿名化されて研究目的のみに使用されます。\n"
                              + "実験への参加は任意であり、途中で自由に中断できます。";

    [Header("2. Main Instruction (全体教示・説明)")]
    [TextArea(3, 6)]
    public string mainInstructionTitle = "【実験の説明】";

    [TextArea(5, 12)]
    public string mainInstructionBody = "これより触覚刺激の比較実験を開始します。\n"
                                      + "提示される刺激を注意深く確認し、選択ボタンを押して回答してください。\n"
                                      + "準備ができたら「次へ進む」を押してください。";

    [Header("3. Practice Instruction (練習試行の教示)")]
    [TextArea(3, 6)]
    public string practiceInstructionTitle = "【練習セッション】";

    [TextArea(4, 8)]
    public string practiceInstructionBody = "これから本試行の前に練習試行を行います。\n"
                                          + "操作方法や刺激の感じ方を確認してください。";

    [Header("4. Completion Message (全試行完了案内)")]
    [TextArea(4, 10)]
    public string completionText = "【全試行完了】\n\n"
                                 + "実験が終了しました。ご協力ありがとうございました。\n"
                                 + "データは正常に保存されました。";
}
