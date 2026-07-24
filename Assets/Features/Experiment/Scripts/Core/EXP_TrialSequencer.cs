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
    [Tooltip("試行シーケンスの生成・順序モード（Random: 完全ランダム / ByElementBlock: ブロックごとに要素順次切替 / ByElementRandomBlock: ブロックごとに要素ランダム切替）")]
    public EXP_SequenceMode sequenceMode = EXP_SequenceMode.Random;

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

    /// <summary>現在の試行で実行中（直前に GetNextCondition で取り出された）の条件</summary>
    public EXP_BaseCondition? CurrentCondition => (_currentIndex > 0 && _currentIndex <= _trialSequence.Count)
        ? _trialSequence[_currentIndex - 1]
        : null;

    // =====================================================
    // Private Fields
    // =====================================================

    private List<EXP_BaseCondition> _trialSequence = new();
    private int _currentIndex = 0;

    // =====================================================
    // Public API
    // =====================================================

    /// <summary>
    /// 条件リストおよび sequenceMode に応じた試行リストを生成します。
    /// 既存のシーケンスは破棄されます。
    /// </summary>
    /// <param name="blockCount">総ブロック数（<=0 の場合は Inspector または repetitions を使用）</param>
    /// <param name="trialsPerBlock">1ブロックあたりの試行数（<=0 の場合は repetitions を使用）</param>
    public void BuildSequence(int blockCount = -1, int trialsPerBlock = -1)
    {
        _trialSequence.Clear();
        _currentIndex = 0;

        var validConditions = conditions.Where(c => c != null).ToList();
        if (validConditions.Count == 0)
        {
            Debug.LogWarning("[EXP_TrialSequencer] 登録された条件アセットがありません。デフォルトの 2AFC STMFrequencyCondition を動的適用します。");
            var defaultCond = ScriptableObject.CreateInstance<EXP_STMFrequencyCondition>();
            defaultCond.conditionName = "Default_2AFC_STMFrequency";
            validConditions.Add(defaultCond);
        }

        var rng = randomSeed < 0
            ? new System.Random()
            : new System.Random(randomSeed);

        List<EXP_BaseCondition> generatedSequence = new();

        switch (sequenceMode)
        {
            case EXP_SequenceMode.ByElementBlock:
            case EXP_SequenceMode.ByElementRandomBlock:
                {
                    int totalBlocks = blockCount > 0 ? blockCount : 1;

                    // 各ブロックに割り当てる要素（条件）のインデックスリストを作成
                    List<int> blockElementIndices = new();
                    for (int b = 0; b < totalBlocks; b++)
                    {
                        blockElementIndices.Add(b % validConditions.Count);
                    }

                    if (sequenceMode == EXP_SequenceMode.ByElementRandomBlock)
                    {
                        // ブロック単位の要素割当順序をシャッフル
                        for (int i = blockElementIndices.Count - 1; i > 0; i--)
                        {
                            int j = rng.Next(i + 1);
                            (blockElementIndices[i], blockElementIndices[j]) = (blockElementIndices[j], blockElementIndices[i]);
                        }
                    }

                    for (int b = 0; b < totalBlocks; b++)
                    {
                        int elementIdx = blockElementIndices[b];
                        var cond = validConditions[elementIdx];
                        int count = trialsPerBlock > 0 ? trialsPerBlock : cond.repetitions;

                        for (int t = 0; t < count; t++)
                        {
                            generatedSequence.Add(cond);
                        }
                    }
                }
                break;

            case EXP_SequenceMode.Random:
            default:
                {
                    // 各条件を repetitions 回追加
                    var rawList = validConditions
                        .SelectMany(c => Enumerable.Repeat(c, c.repetitions))
                        .ToList();

                    // Fisher-Yates シャッフル
                    for (int i = rawList.Count - 1; i > 0; i--)
                    {
                        int j = rng.Next(i + 1);
                        (rawList[i], rawList[j]) = (rawList[j], rawList[i]);
                    }

                    generatedSequence = rawList;
                }
                break;
        }

        _trialSequence = generatedSequence;

        Debug.Log($"[EXP_TrialSequencer] シーケンス生成完了 (Mode: {sequenceMode}): "
                + $"{_trialSequence.Count} 試行 / シード: {(randomSeed < 0 ? "ランダム" : randomSeed.ToString())}");
    }

    /// <summary>シーケンスを生成します（BuildSequence のエイリアス）。</summary>
    public void GenerateSequence(int blockCount = -1, int trialsPerBlock = -1) => BuildSequence(blockCount, trialsPerBlock);

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

    /// <summary>次の試行条件を取得します（GetNextCondition のエイリアス）。</summary>
    public EXP_BaseCondition? NextCondition() => GetNextCondition();

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
