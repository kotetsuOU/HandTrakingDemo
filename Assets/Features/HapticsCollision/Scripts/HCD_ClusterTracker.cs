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

    // ─── ContactForceReduction Settings ────────────────────────────────────

    [Tooltip("この接触点数以上で Force = 1.0（最大振幅）とする")]
    public int forceMaxCount = 500;

    [Tooltip("この接触点数未満では Force = 0.0（ノイズ除去閾値）")]
    public int forceMinCount = 5;

    [Tooltip("Force の時間的スムージング係数（0.0=スムーズなし, 1.0=即応答）")]
    [Range(0.01f, 1.0f)]
    public float forceSmoothingFactor = 0.3f;

    [Tooltip("速度（Velocity）計算の時間的スムージング係数")]
    [Range(0.01f, 1.0f)]
    public float velocitySmoothingFactor = 0.2f;

    // ─── Public Result ─────────────────────────────────────────────────────

    /// <summary>現在追跡中の全クラスタ（alive + missing 含む）</summary>
    public IReadOnlyList<TrackedCluster> TrackedClusters => _tracked;

    // ─── Internal State ────────────────────────────────────────────────────

    private readonly List<TrackedCluster> _tracked = new List<TrackedCluster>();
    private int _nextId = 0;

    // ─── Update ────────────────────────────────────────────────────────────

    /// <summary>
    /// 新しいフレームのクラスタ重心リストを受け取り、フレーム間追跡を更新します（位置のみ版）。
    /// </summary>
    public void Update(List<Vector3> newCentroids)
    {
        Update(newCentroids, null, null);
    }

    /// <summary>
    /// 新しいフレームのクラスタ重心リストを受け取り、フレーム間追跡を更新します。
    /// </summary>
    /// <param name="newCentroids">今フレームの表面接触クラスタ重心リスト</param>
    /// <param name="newNormals">重心に対応する平均法線リスト（省略時は null）</param>
    /// <param name="newCounts">各クラスタの接触点数リスト（ContactForceReduction 用、省略時は null）</param>
    /// <param name="newPrecisions">精密モードの追加データ（共分散・ランダム点）</param>
    public void Update(List<Vector3> newCentroids, List<Vector3> newNormals, List<int> newCounts = null, List<ClusterPrecision> newPrecisions = null)
    {
        int newCount = newCentroids.Count;
        bool[] newMatched = new bool[newCount];

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
                newMatched[bestIdx] = true;
                
                // Velocity の計算 (変位 / dt)
                float dt = Mathf.Max(0.001f, Time.deltaTime);
                Vector3 instVelocity = (newCentroids[bestIdx] - cluster.Centroid) / dt;
                cluster.Velocity = Vector3.Lerp(cluster.Velocity, instVelocity, velocitySmoothingFactor);

                cluster.Centroid = newCentroids[bestIdx];
                if (newNormals != null && bestIdx < newNormals.Count)
                    cluster.Normal = newNormals[bestIdx];

                // ContactForceReduction: 接触点数 → Force (0-1)
                if (newCounts != null && bestIdx < newCounts.Count)
                {
                    cluster.ContactCount = newCounts[bestIdx];
                    float rawForce = ComputeRawForce(newCounts[bestIdx]);
                    // 時間的スムージング: 急激な変化を抑え、安定した振幅制御を実現
                    cluster.Force = Mathf.Lerp(cluster.Force, rawForce, forceSmoothingFactor);
                }

                // Precision データの更新
                if (newPrecisions != null && bestIdx < newPrecisions.Count)
                {
                    cluster.Precision = newPrecisions[bestIdx];
                }

                cluster.Age++;
                cluster.MissingFrames = 0;
                cluster.IsAlive = true;
                _tracked[t] = cluster;
            }
            else
            {
                cluster.MissingFrames++;
                cluster.IsAlive = false;
                // Force を減衰させる（欠損中にフェードアウト）
                cluster.Force = Mathf.Lerp(cluster.Force, 0f, forceSmoothingFactor);
                _tracked[t] = cluster;
            }
        }

        // ── Step 2: マッチしなかった新規重心を新クラスタとして追加 ──────────
        for (int n = 0; n < newCount; n++)
        {
            if (!newMatched[n])
            {
                int cnt = (newCounts != null && n < newCounts.Count) ? newCounts[n] : 0;
                _tracked.Add(new TrackedCluster
                {
                    Id            = _nextId++,
                    Centroid      = newCentroids[n],
                    Normal        = (newNormals != null && n < newNormals.Count)
                                    ? newNormals[n] : Vector3.up,
                    ContactCount  = cnt,
                    Force         = ComputeRawForce(cnt),
                    Age           = 1,
                    MissingFrames = 0,
                    IsAlive       = true,
                    Velocity      = Vector3.zero,
                    Precision     = (newPrecisions != null && n < newPrecisions.Count) ? newPrecisions[n] : default
                });
            }
        }

        // ── Step 3: 長すぎる欠損クラスタを除去 ─────────────────────────────
        _tracked.RemoveAll(c => c.MissingFrames > maxMissingFrames);
    }

    // ─── ContactForceReduction ──────────────────────────────────────────────

    /// <summary>
    /// 接触点数を 0.0〜1.0 の振幅値にマッピングします。
    /// forceMinCount 未満はノイズとみなし 0、forceMaxCount 以上で 1.0（クランプ）。
    /// </summary>
    private float ComputeRawForce(int contactCount)
    {
        if (contactCount < forceMinCount) return 0f;
        float range = Mathf.Max(1f, forceMaxCount - forceMinCount);
        return Mathf.Clamp01((contactCount - forceMinCount) / range);
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

    /// <summary>接触パッチの平均表面法線（正規化済み）</summary>
    public Vector3 Normal;

    /// <summary>接触点数（クラスタ内の GPU 点数）</summary>
    public int ContactCount;

    /// <summary>接触の強さ（0.0〜1.0）。AUTD3 の振幅制御に直接使用可能。
    /// ContactForceReduction により接触点数からマッピングされ、時間的スムージングで安定化されています。</summary>
    public float Force;

    /// <summary>このクラスタが生存し続けているフレーム数</summary>
    public int Age;

    /// <summary>マッチしなかった連続フレーム数</summary>
    public int MissingFrames;

    /// <summary>今フレームで生きているか</summary>
    public bool IsAlive;

    /// <summary>クラスタの移動速度 (m/s)。平滑化されています。</summary>
    public Vector3 Velocity;

    /// <summary>精密モード用の追加データ（共分散、ランダム点）</summary>
    public ClusterPrecision Precision;
}

/// <summary>
/// Precisionモードで計算される高度なクラスタデータ
/// </summary>
public struct ClusterPrecision
{
    public float covXX, covYY, covZZ, covXY, covXZ, covYZ;
    public Vector3 rp00, rp01, rp02, rp03;
    public Vector3 rp04, rp05, rp06, rp07;
    public Vector3 rp08, rp09, rp10, rp11;
    public Vector3 rp12, rp13, rp14, rp15;
}
