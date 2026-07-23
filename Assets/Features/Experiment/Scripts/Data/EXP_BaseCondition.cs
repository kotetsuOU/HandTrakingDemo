using UnityEngine;

#nullable enable

/// <summary>
/// 実験条件の基底 ScriptableObject。
/// 実験固有の条件クラスはこのクラスを継承して実装してください。
/// <para>
/// 使用例:
/// <code>
/// [CreateAssetMenu(menuName = "EXP/Conditions/MyCondition")]
/// public class MyCondition : EXP_BaseCondition
/// {
///     public float intensity = 1.0f;
///
///     public override void Apply(EXP_TrialData trial)
///     {
///         // 刺激提示処理をここに記述
///         trial.metadata["intensity"] = intensity.ToString();
///     }
/// }
/// </code>
/// </para>
/// </summary>
public abstract class EXP_BaseCondition : ScriptableObject
{
    // =====================================================
    // Condition Info
    // =====================================================
    [Header("Condition Info")]
    [Tooltip("条件の識別名（データ記録ファイルの conditionName 列に記録されます）")]
    public string conditionName = "Condition";

    [Tooltip("この条件を試行シーケンスで何回繰り返すか")]
    [Min(1)]
    public int repetitions = 1;

    [Tooltip("条件の説明メモ（データには出力されません）")]
    [TextArea(2, 5)]
    public string description = "";

    // =====================================================
    // Abstract Interface
    // =====================================================

    /// <summary>
    /// この条件を1試行に適用します。刺激提示・ハプティクス起動などをここに記述してください。
    /// EXP_ExperimentManager の Stimulus フェーズ開始時に呼ばれます。
    /// </summary>
    /// <param name="trial">現在の試行データ。metadata などを書き込めます。</param>
    public abstract void Apply(EXP_TrialData trial);

    // =====================================================
    // Virtual Interface
    // =====================================================

    /// <summary>
    /// 試行終了後のリセット処理。次の試行に向けた後片付けをここに記述してください。
    /// デフォルトでは何もしません。
    /// </summary>
    /// <param name="trial">完了した試行データ（応答情報も含む）</param>
    public virtual void OnTrialEnd(EXP_TrialData trial) { }

    /// <summary>
    /// 応答を受け取った直後に呼ばれます。リアルタイムな判定・後処理などに使用してください。
    /// デフォルトでは何もしません。
    /// </summary>
    /// <param name="trial">応答情報が書き込まれた試行データ</param>
    /// <returns>正誤判定結果（null = 判定なし）</returns>
    public virtual bool? EvaluateResponse(EXP_TrialData trial) => null;
}
