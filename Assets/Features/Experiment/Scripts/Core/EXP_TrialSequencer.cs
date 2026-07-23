using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#nullable enable

/// <summary>
/// 試行シーケンスを生成・管理するコンポーネント。
/// <para>
/// 登録された <see cref="EXP_BaseCondition"/> の全 repetitions の合計分を
/// Fisher-Yates アルゴリズムで完全ランダム化したリストを生成します。
/// </para>
/// <para>
/// 使用フロー:
/// <code>
/// sequencer.conditions = myConditions;
/// sequencer.BuildSequence();
/// while (!sequencer.IsFinished)
/// {
///     var cond = sequencer.GetNextCondition();
///     // ... 試行実行
/// }
/// </code>
/// </para>
/// </summary>
public class EXP_TrialSequencer : MonoBehaviour
{
    // =====================================================
    // Inspector Settings
    // =====================================================

    [Header("Conditions")]
    [Tooltip("実験条件のリスト。EXP_BaseCondition を継承した ScriptableObject を登録してください。")]
    public List<EXP_BaseCondition> conditions = new();

    [Header("Sequence Settings")]
    [Tooltip("乱数シード（-1 = 実行ごとに異なるランダムシード）")]
    public int randomSeed = -1;

    // =====================================================
    // State (Read-Only)
    // =====================================================

    /// <summary>生成されたシーケンスの総試行数</summary>
    public int TotalTrials => _trialSequence.Count;

    /// <summary>残り試行数</summary>
    public int RemainingTrials => TotalTrials - _currentIndex;

    /// <summary>シーケンスが終了しているか</summary>
    public bool IsFinished => _currentIndex >= TotalTrials;

    /// <summary>現在の試行インデックス（0始まり）</summary>
    public int CurrentIndex => _currentIndex;

    // =====================================================
    // Private Fields
    // =====================================================

    private List<EXP_BaseCondition> _trialSequence = new();
    private int _currentIndex = 0;

    // =====================================================
    // Public API
    // =====================================================

    /// <summary>
    /// 全条件 × repetitions 分の試行リストを生成し、完全ランダムにシャッフルします。
    /// 既存のシーケンスは破棄されます。
    /// </summary>
    public void BuildSequence()
    {
        _trialSequence.Clear();
        _currentIndex = 0;

        var validConditions = conditions.Where(c => c != null).ToList();
        if (validConditions.Count == 0)
        {
            Debug.LogWarning("[EXP_TrialSequencer] 登録された条件がありません。conditions リストを確認してください。");
            return;
        }

        // 各条件を repetitions 回追加
        var rawList = validConditions
            .SelectMany(c => Enumerable.Repeat(c, c.repetitions))
            .ToList();

        // Fisher-Yates シャッフル
        var rng = randomSeed < 0
            ? new System.Random()
            : new System.Random(randomSeed);

        for (int i = rawList.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (rawList[i], rawList[j]) = (rawList[j], rawList[i]);
        }

        _trialSequence = rawList;

        Debug.Log($"[EXP_TrialSequencer] シーケンス生成完了: "
                + $"{_trialSequence.Count} 試行 / シード: {(randomSeed < 0 ? "ランダム" : randomSeed.ToString())}");
    }

    /// <summary>
    /// 次の試行の条件を返し、内部インデックスを 1 進めます。
    /// シーケンスが終了している場合は null を返します。
    /// </summary>
    public EXP_BaseCondition? GetNextCondition()
    {
        if (IsFinished)
        {
            Debug.LogWarning("[EXP_TrialSequencer] シーケンスが終了しています。");
            return null;
        }
        return _trialSequence[_currentIndex++];
    }

    /// <summary>
    /// 指定インデックスの条件を返します（インデックスは進めません）。
    /// </summary>
    public EXP_BaseCondition? PeekCondition(int index)
    {
        if (index < 0 || index >= _trialSequence.Count) return null;
        return _trialSequence[index];
    }

    /// <summary>インデックスを先頭にリセットします（シーケンスは保持されます）。</summary>
    public void ResetIndex() => _currentIndex = 0;

    /// <summary>生成されたシーケンスを読み取り専用で返します。</summary>
    public IReadOnlyList<EXP_BaseCondition> GetSequence() => _trialSequence.AsReadOnly();
}
