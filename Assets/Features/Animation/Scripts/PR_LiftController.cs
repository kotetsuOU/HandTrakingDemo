using UnityEngine;
using System.Collections.Generic;

namespace Features.Animation
{
    public class PR_LiftController : MonoBehaviour
    {
        [Header("Target Settings")]
        [Tooltip("移動させる対象（Fox全体）")]
        public Transform targetTransform;
        
        [Header("Foot Bone Transforms")]
        public Transform frontLeftFoot;
        public Transform frontRightFoot;
        public Transform backLeftFoot;
        public Transform backRightFoot;

        [Header("Foot Toggles")]
        public bool enableFrontLeft = true;
        public bool enableFrontRight = true;
        public bool enableBackLeft = true;
        public bool enableBackRight = true;

        [Header("Lift Settings")]
        [Tooltip("平面からの距離がこの値以下の場合に接触とみなす")]
        public float contactThreshold = 0.05f;
        
        [Tooltip("持ち上げ時の追従感度")]
        public float liftSensitivity = 1.0f;

        [Header("Fall Settings")]
        [Tooltip("手が離れた際に落下・復帰する目標ポイント。指定がない場合は初期位置（起動時の位置）を目標にします。")]
        public Transform fallbackPoint;
        
        [Tooltip("落下・復帰の速度（m/s）")]
        public float fallSpeed = 2.0f;

        private bool isContacting = false;
        private Vector3 previousCentroid;
        private Vector3 initialPosition;

        private void Reset()
        {
            if (targetTransform == null)
            {
                targetTransform = this.transform;
            }
            AutoDetectBones();
        }

        private void Awake()
        {
            if (targetTransform == null)
            {
                targetTransform = this.transform;
            }
            if (targetTransform != null)
            {
                initialPosition = targetTransform.position;
            }
            AutoDetectBones();
        }

        void Update()
        {
            if (targetTransform == null || HCD_Pipeline.Instance == null) return;
            if (frontLeftFoot == null || frontRightFoot == null || backLeftFoot == null || backRightFoot == null) return;

            // 1. 平面の計算
            List<Vector3> activeFeet = new List<Vector3>();
            if (enableFrontLeft && frontLeftFoot != null) activeFeet.Add(frontLeftFoot.position);
            if (enableFrontRight && frontRightFoot != null) activeFeet.Add(frontRightFoot.position);
            if (enableBackLeft && backLeftFoot != null) activeFeet.Add(backLeftFoot.position);
            if (enableBackRight && backRightFoot != null) activeFeet.Add(backRightFoot.position);

            if (activeFeet.Count == 0) return;

            Vector3 planeOrigin = Vector3.zero;
            foreach (var pos in activeFeet)
            {
                planeOrigin += pos;
            }
            planeOrigin /= activeFeet.Count;
            
            Vector3 planeNormal = Vector3.up;

            if (activeFeet.Count == 4)
            {
                Vector3 diag1 = frontLeftFoot.position - backRightFoot.position;
                Vector3 diag2 = frontRightFoot.position - backLeftFoot.position;
                planeNormal = Vector3.Cross(diag1, diag2).normalized;
            }
            else if (activeFeet.Count >= 3)
            {
                Vector3 edge1 = activeFeet[1] - activeFeet[0];
                Vector3 edge2 = activeFeet[2] - activeFeet[0];
                planeNormal = Vector3.Cross(edge1, edge2).normalized;
            }

            // 法線が下を向いている場合は上に向ける
            if (planeNormal.y < 0)
            {
                planeNormal = -planeNormal;
            }

            // 2. クラスタの取得と重心・距離計算
            var clusters = HCD_Pipeline.Instance.GetTrackedClusters();
            if (clusters == null || clusters.Count == 0)
            {
                // 手が消えた場合は接触リセットして落下処理を行う
                isContacting = false;
                ApplyFallBehavior();
                return;
            }

            int validClusterCount = 0;
            float minDistanceToPlane = float.MaxValue;
            float minDistanceToPrev = float.MaxValue;
            Vector3 closestToPlaneCentroid = Vector3.zero;
            Vector3 bestCentroid = Vector3.zero;

            foreach (var cluster in clusters)
            {
                if (!cluster.IsAlive) continue;

                // 平面との距離計算: dot(point - origin, normal)
                float dist = Vector3.Dot(cluster.Centroid - planeOrigin, planeNormal);
                
                // 平面の「上」や「下」を区別せず、単に絶対的な距離を使用する
                float absDist = Mathf.Abs(dist);
                if (absDist < minDistanceToPlane)
                {
                    minDistanceToPlane = absDist;
                    closestToPlaneCentroid = cluster.Centroid;
                }

                if (isContacting)
                {
                    float distToPrev = Vector3.Distance(cluster.Centroid, previousCentroid);
                    if (distToPrev < minDistanceToPrev)
                    {
                        minDistanceToPrev = distToPrev;
                        bestCentroid = cluster.Centroid;
                    }
                }

                validClusterCount++;
            }

            if (validClusterCount == 0)
            {
                isContacting = false;
                ApplyFallBehavior();
                return;
            }

            // 急激な重心のジャンプ（別クラスタへの飛び移りや追跡ロスト）を防ぐ
            if (isContacting && minDistanceToPrev > 0.2f)
            {
                isContacting = false;
            }

            // 接触中であれば前回位置に最も近いクラスタを、そうでなければ平面に最も近いクラスタを重心とする
            Vector3 currentCentroid = isContacting ? bestCentroid : closestToPlaneCentroid;

            // 3. 状態判定と移動処理
            float currentDist = Vector3.Dot(currentCentroid - planeOrigin, planeNormal);
            bool currentContact = (Mathf.Abs(currentDist) <= contactThreshold);

            if (currentContact)
            {
                if (!isContacting)
                {
                    // 接触開始
                    isContacting = true;
                    previousCentroid = currentCentroid;
                }
                else
                {
                    // 接触中（追従）
                    // 重心の変位
                    Vector3 delta = currentCentroid - previousCentroid;
                    
                    // 変位を法線方向に射影（平面に対して垂直にのみ移動）
                    float liftAmount = Vector3.Dot(delta, planeNormal);
                    
                    // ターゲットを移動させる
                    targetTransform.position += planeNormal * (liftAmount * liftSensitivity);

                    // 追従後は重心の絶対座標が変わるため更新
                    previousCentroid = currentCentroid;
                }
            }
            else
            {
                // 非接触（手が離れた、または素早く引き抜かれた）
                isContacting = false;
                ApplyFallBehavior();
            }
        }

        private void ApplyFallBehavior()
        {
            if (targetTransform == null) return;

            Vector3 targetPos = fallbackPoint != null ? fallbackPoint.position : initialPosition;
            
            // 目標位置へ向かって一定速度で移動（自由落下・復帰）
            if (Vector3.Distance(targetTransform.position, targetPos) > 0.001f)
            {
                targetTransform.position = Vector3.MoveTowards(
                    targetTransform.position, 
                    targetPos, 
                    fallSpeed * Time.deltaTime
                );
            }
        }
        
        public void AutoDetectBones(Transform searchRoot = null)
        {
            if (targetTransform == null)
            {
                // AnimationControllerを探して、現在アクティブなターゲットを取得する
                AnimationController animCtrl = FindAnyObjectByType<AnimationController>();
                if (animCtrl != null && animCtrl.toggleObjects != null)
                {
                    foreach (var obj in animCtrl.toggleObjects)
                    {
                        if (obj != null && obj.activeInHierarchy)
                        {
                            targetTransform = obj.transform;
                            break;
                        }
                    }
                    if (targetTransform == null && animCtrl.toggleObjects.Length > 0 && animCtrl.toggleObjects[0] != null)
                    {
                        targetTransform = animCtrl.toggleObjects[0].transform;
                    }
                }
            }

            if (searchRoot == null)
            {
                searchRoot = targetTransform != null ? targetTransform : this.transform;
            }
            
            if (frontLeftFoot == null)
                frontLeftFoot = FindChildRecursive(searchRoot, name => name.Contains("F_LLegDigit11") || name.Contains("Fox_F_LLegDigit11") || (name.ToLower().Contains("front") && name.ToLower().Contains("left") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit"))));
            
            if (frontRightFoot == null)
                frontRightFoot = FindChildRecursive(searchRoot, name => name.Contains("F_RLegDigit11") || name.Contains("Fox_F_RLegDigit11") || (name.ToLower().Contains("front") && name.ToLower().Contains("right") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit"))));

            if (backLeftFoot == null)
                backLeftFoot = FindChildRecursive(searchRoot, name => (name.Contains("LLegDigit11") && !name.Contains("F_")) || (name.ToLower().Contains("left") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit")) && !name.ToLower().Contains("front")));

            if (backRightFoot == null)
                backRightFoot = FindChildRecursive(searchRoot, name => (name.Contains("RLegDigit11") && !name.Contains("F_")) || (name.ToLower().Contains("right") && (name.ToLower().Contains("foot") || name.ToLower().Contains("digit")) && !name.ToLower().Contains("front")));

            // 検出できなかった場合のフォールバックとして Ankle を探す
            if (frontLeftFoot == null)
                frontLeftFoot = FindChildRecursive(searchRoot, name => name.Contains("F_LLegAnkle") || (name.ToLower().Contains("front") && name.ToLower().Contains("left") && name.ToLower().Contains("ankle")));
            if (frontRightFoot == null)
                frontRightFoot = FindChildRecursive(searchRoot, name => name.Contains("F_RLegAnkle") || (name.ToLower().Contains("front") && name.ToLower().Contains("right") && name.ToLower().Contains("ankle")));
            if (backLeftFoot == null)
                backLeftFoot = FindChildRecursive(searchRoot, name => (name.Contains("LLegAnkle") && !name.Contains("F_")) || (name.ToLower().Contains("left") && (name.ToLower().Contains("ankle") && !name.ToLower().Contains("front"))));
            if (backRightFoot == null)
                backRightFoot = FindChildRecursive(searchRoot, name => (name.Contains("RLegAnkle") && !name.Contains("F_")) || (name.ToLower().Contains("right") && (name.ToLower().Contains("ankle") && !name.ToLower().Contains("front"))));
        }

        private Transform FindChildRecursive(Transform parent, System.Func<string, bool> predicate)
        {
            if (predicate(parent.name)) return parent;
            foreach (Transform child in parent)
            {
                var found = FindChildRecursive(child, predicate);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDrawGizmos()
        {
            List<Vector3> activeFeet = new List<Vector3>();
            if (enableFrontLeft && frontLeftFoot != null) activeFeet.Add(frontLeftFoot.position);
            if (enableFrontRight && frontRightFoot != null) activeFeet.Add(frontRightFoot.position);
            if (enableBackLeft && backLeftFoot != null) activeFeet.Add(backLeftFoot.position);
            if (enableBackRight && backRightFoot != null) activeFeet.Add(backRightFoot.position);

            if (activeFeet.Count > 0)
            {
                Vector3 origin = Vector3.zero;
                foreach (var pos in activeFeet) origin += pos;
                origin /= activeFeet.Count;

                Vector3 normal = Vector3.up;
                if (activeFeet.Count == 4)
                {
                    Vector3 diag1 = frontLeftFoot.position - backRightFoot.position;
                    Vector3 diag2 = frontRightFoot.position - backLeftFoot.position;
                    normal = Vector3.Cross(diag1, diag2).normalized;
                }
                else if (activeFeet.Count >= 3)
                {
                    normal = Vector3.Cross(activeFeet[1] - activeFeet[0], activeFeet[2] - activeFeet[0]).normalized;
                }
                if (normal.y < 0) normal = -normal;

                // 描画：法線
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(origin, normal * 0.1f);
                
                // 描画：平面の輪郭
                Gizmos.color = isContacting ? Color.green : Color.yellow;
                for (int i = 0; i < activeFeet.Count; i++)
                {
                    Gizmos.DrawLine(activeFeet[i], activeFeet[(i + 1) % activeFeet.Count]);
                }
            }
        }
    }
}
