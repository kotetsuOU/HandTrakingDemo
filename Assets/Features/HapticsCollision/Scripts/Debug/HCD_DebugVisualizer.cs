using System.Collections.Generic;
using UnityEngine;

namespace Features.HapticsCollision.Debug
{
    /// <summary>
    /// HCD 接触判定結果（重心、法線、点群実測/メッシュ投影距離、Age/Forceラベル）を Scene ビュー上に Gizmos 描画する専用デバッグコンポーネント。
    /// HCD_Pipeline コア本体から視覚デバッグ処理を完全に分離します。
    /// </summary>
    [DisallowMultipleComponent]
    public class HCD_DebugVisualizer : MonoBehaviour
    {
        [Tooltip("参照する HCD_Pipeline。null の場合は HCD_Pipeline.Instance を使用します。")]
        public HCD_Pipeline targetPipeline;

        [Tooltip("Gizmos 描画を有効化します")]
        public bool showGizmos = true;

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (!showGizmos) return;

#if UNITY_EDITOR
            // 選択されている場合は OnDrawGizmosSelected で描画されるため重複を避ける
            if (UnityEditor.Selection.activeGameObject == gameObject) return;
#endif

            DrawClusterGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            DrawClusterGizmos();
        }

        private void DrawClusterGizmos()
        {
            var pipeline = targetPipeline != null ? targetPipeline : HCD_Pipeline.Instance;
            if (pipeline == null) return;

            pipeline.GetActiveClusterInfos(out var centroids, out var normals, out var counts, out var precisions, out var rawPositions, out var meshPositions, out var minDistances);

            if (centroids == null || centroids.Count == 0) return;

            var trackedClusters = pipeline.GetTrackedClusters();

            for (int i = 0; i < centroids.Count; i++)
            {
                Vector3 centroid = centroids[i];
                Vector3 normal = normals[i];
                Vector3 rawPos = (i < rawPositions.Count) ? rawPositions[i] : centroid;
                Vector3 meshPos = (i < meshPositions.Count) ? meshPositions[i] : centroid;
                float minDist = (i < minDistances.Count) ? minDistances[i] : 0.0f;

                int age = 1;
                float force = 1.0f;
                int id = i;
                if (trackedClusters != null)
                {
                    foreach (var tc in trackedClusters)
                    {
                        if (tc.IsAlive && (tc.Centroid - centroid).sqrMagnitude < 0.01f)
                        {
                            age = tc.Age;
                            force = tc.Force;
                            id = tc.Id;
                            break;
                        }
                    }
                }

                // Age が大きいほど安定 → マゼンタ、新生 → 黄色
                float stability = Mathf.Clamp01(age / 10.0f);
                Gizmos.color = Color.Lerp(Color.yellow, Color.magenta, stability);
                Gizmos.DrawWireSphere(centroid, 0.02f);

                // 実測点群位置とメッシュ投影位置の差分を線で描画
                if ((rawPos - meshPos).sqrMagnitude > 0.000001f)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(rawPos, 0.005f);
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireSphere(meshPos, 0.005f);
                    Gizmos.color = Color.white;
                    Gizmos.DrawLine(rawPos, meshPos);
                }

                // 法線方向を矢印で描画
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(centroid, normal * 0.04f);

#if UNITY_EDITOR
                // ID・生存フレーム数・Force・最小距離 をラベル表示
                UnityEditor.Handles.Label(
                    centroid + Vector3.up * 0.03f,
                    $"ID:{id} Age:{age} F:{force:F2} MinD:{minDist * 1000f:F1}mm");
#endif
            }
        }
    }
}
