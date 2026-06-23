using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ContactClusterTracking: フレームをまたいでクラスタの同一性を追跡します。
/// 最近傍マッチングにより、各クラスタに安定した ID と生存期間を付与します。
///
/// 計算コスト: O(N * M)  N = 現フレームクラスタ数, M = 追跡中クラスタ数
/// 実運用では両者とも <= 20 程度（両手 10 指）なので CPU 負荷は無視できます。
/// 追加メモリ: TrackedCluster × 最大数 × ~40 バイト = 数百バイト程度。
/// </summary>
[System.Serializable]
public class HCD_ClusterTracker
{
    // ─── Settings ─────────────────────────────────────────────────────────

    [Tooltip("フレーム間で同一クラスタと見なす最大移動距離 (m)")]
    public float matchRadius = 0.05f;

    [Tooltip("何フレーム連続でマッチしなかったらクラスタを消滅させるか")]
    public int maxMissingFrames = 3;

    // ─── Public Result ─────────────────────────────────────────────────────

    /// <summary>現在追跡中の全クラスタ（alive + missing 含む）</summary>
    public IReadOnlyList<TrackedCluster> TrackedClusters => _tracked;

    // ─── Internal State ────────────────────────────────────────────────────

    private readonly List<TrackedCluster> _tracked = new List<TrackedCluster>();
    private int _nextId = 0;

    // ─── Update ────────────────────────────────────────────────────────────

    /// <summary>
    /// 新しいフレームのクラスタ重心リストを受け取り、フレーム間追跡を更新します。
    /// </summary>
    /// <param name="newCentroids">今フレームの表面接触クラスタ重心リスト</param>
    public void Update(List<Vector3> newCentroids)
    {
        int newCount = newCentroids.Count;
        bool[] newMatched = new bool[newCount]; // 新規重心がマッチ済みかどうか

        // ── Step 1: 既存クラスタを新規重心に対してマッチング ────────────────
        for (int t = 0; t < _tracked.Count; t++)
        {
            var cluster = _tracked[t];
            int bestIdx = -1;
            float bestDistSqr = matchRadius * matchRadius;

            for (int n = 0; n < newCount; n++)
            {
                if (newMatched[n]) continue;
                float dSqr = (newCentroids[n] - cluster.Centroid).sqrMagnitude;
                if (dSqr < bestDistSqr)
                {
                    bestDistSqr = dSqr;
                    bestIdx = n;
                }
            }

            if (bestIdx >= 0)
            {
                // マッチ成功: 重心を更新し、欠損カウントをリセット
                newMatched[bestIdx] = true;
                cluster.Centroid = newCentroids[bestIdx];
                cluster.Age++;
                cluster.MissingFrames = 0;
                cluster.IsAlive = true;
                _tracked[t] = cluster;
            }
            else
            {
                // マッチ失敗: 欠損カウントを増やす
                cluster.MissingFrames++;
                cluster.IsAlive = false;
                _tracked[t] = cluster;
            }
        }

        // ── Step 2: マッチしなかった新規重心を新クラスタとして追加 ──────────
        for (int n = 0; n < newCount; n++)
        {
            if (!newMatched[n])
            {
                _tracked.Add(new TrackedCluster
                {
                    Id            = _nextId++,
                    Centroid      = newCentroids[n],
                    Age           = 1,
                    MissingFrames = 0,
                    IsAlive       = true,
                });
            }
        }

        // ── Step 3: 長すぎる欠損クラスタを除去 ─────────────────────────────
        _tracked.RemoveAll(c => c.MissingFrames > maxMissingFrames);
    }

    /// <summary>現在生きているクラスタの重心リストを返します</summary>
    public List<Vector3> GetAliveCentroids()
    {
        var result = new List<Vector3>(_tracked.Count);
        foreach (var c in _tracked)
        {
            if (c.IsAlive) result.Add(c.Centroid);
        }
        return result;
    }

    /// <summary>トラッカーの状態をリセットします</summary>
    public void Reset()
    {
        _tracked.Clear();
        _nextId = 0;
    }
}

/// <summary>
/// フレーム間追跡中の単一クラスタを表す値型。
/// </summary>
public struct TrackedCluster
{
    /// <summary>このセッション内でユニークな ID（生成順・一意）</summary>
    public int Id;

    /// <summary>現在の重心座標</summary>
    public Vector3 Centroid;

    /// <summary>このクラスタが生存し続けているフレーム数</summary>
    public int Age;

    /// <summary>マッチしなかった連続フレーム数</summary>
    public int MissingFrames;

    /// <summary>今フレームで生きているか</summary>
    public bool IsAlive;
}
