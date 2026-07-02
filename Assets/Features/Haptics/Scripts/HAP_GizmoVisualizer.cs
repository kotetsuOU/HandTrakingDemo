using UnityEngine;
using System.Linq;
using System.Collections.Generic;

#nullable enable

/// <summary>
/// AUTDデバイスの可視化および仮想オブジェクトへのグルーピングマッピングをGizmoとして描画するユーティリティクラス
/// </summary>
public static class HAP_GizmoVisualizer
{
    public static void DrawDevicesAndGroupings(
        AUTD3Device[] devices, 
        bool enableDirectionalGrouping, 
        float directionalAngleThreshold, 
        HCD_Pipeline? hcdPipeline)
    {
        var sortedDevices = devices.OrderBy(d => d.ID).ToArray();

        // 同じ向きのデバイスを1つのグループとしてまとめる
        List<List<AUTD3Device>> deviceGroups = new List<List<AUTD3Device>>();
        foreach (var device in sortedDevices)
        {
            bool added = false;
            foreach (var group in deviceGroups)
            {
                if (Vector3.Angle(group[0].transform.forward, device.transform.forward) < 1.0f)
                {
                    group.Add(device);
                    added = true;
                    break;
                }
            }
            if (!added)
            {
                deviceGroups.Add(new List<AUTD3Device> { device });
            }
        }

        for (int groupIndex = 0; groupIndex < deviceGroups.Count; groupIndex++)
        {
            var group = deviceGroups[groupIndex];

            // グループごとに固有の色を割り当て（グルーピング有効時）
            Color groupColor = enableDirectionalGrouping && deviceGroups.Count > 0 
                ? Color.HSVToRGB((float)groupIndex / deviceGroups.Count, 0.8f, 1f) 
                : new Color(0.2f, 0.2f, 0.8f, 1f);
                
            groupColor.a = 0.5f;

            foreach (var device in group)
            {
                // AUTDのマテリアル色を変更（子オブジェクトのRendererに対してMaterialPropertyBlockを適用）
                var renderers = device.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    
                    if (enableDirectionalGrouping)
                    {
                        // 不透明でしっかり色を塗る
                        Color solidColor = new Color(groupColor.r, groupColor.g, groupColor.b, 1f);
                        block.SetColor("_Color", solidColor); // Standard/Built-in
                        block.SetColor("_BaseColor", solidColor); // URP/HDRP
                    }
                    else
                    {
                        // グルーピング無効時は設定をクリアして元のマテリアル色に戻す
                        renderer.SetPropertyBlock(null);
                        continue;
                    }
                    
                    renderer.SetPropertyBlock(block);
                }
            }
        }

        if (hcdPipeline == null)
        {
            hcdPipeline = Object.FindFirstObjectByType<HCD_Pipeline>(FindObjectsInactive.Include);
        }

        // 仮想オブジェクトの凸包（BoxCollider）表面の割当て可視化
        if (enableDirectionalGrouping)
        {
            GameObject? activeObj = null;

            if (hcdPipeline != null && hcdPipeline.distanceProcessor != null)
            {
                var dp = hcdPipeline.distanceProcessor;
                
                // HCD_Pipeline の設定からターゲットオブジェクトを取得
                if (dp.detectionMode == HCD_DistanceProcessor.DetectionMode.TransformOnly && dp.targetObject != null)
                {
                    activeObj = dp.targetObject.gameObject;
                }
                else if (dp.detectionMode == HCD_DistanceProcessor.DetectionMode.SkinnedMeshRenderer && dp.targetSkinnedMeshes != null && dp.targetSkinnedMeshes.Length > 0 && dp.targetSkinnedMeshes[0] != null)
                {
                    activeObj = dp.targetSkinnedMeshes[0].gameObject;
                }
                else if (dp.detectionMode == HCD_DistanceProcessor.DetectionMode.MeshFilter && dp.targetMeshFilters != null && dp.targetMeshFilters.Length > 0 && dp.targetMeshFilters[0] != null)
                {
                    activeObj = dp.targetMeshFilters[0].gameObject;
                }
            }

            // HCD_Pipelineからターゲットが取得できなかった場合（エディタの非再生時など）、AnimationControllerから直接取得を試みる
            if (activeObj == null)
            {
                var animControllers = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var mc in animControllers)
                {
                    if (mc.GetType().Name == "AnimationController")
                    {
                        var toggleObjectsField = mc.GetType().GetField("toggleObjects");
                        var indexField = mc.GetType().GetField("currentActiveIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (toggleObjectsField != null && indexField != null)
                        {
                            var toggleObjects = toggleObjectsField.GetValue(mc) as GameObject[];
                            var activeIndex = (int)indexField.GetValue(mc);
                            if (toggleObjects != null && activeIndex >= 0 && activeIndex < toggleObjects.Length)
                            {
                                activeObj = toggleObjects[activeIndex];
                                break;
                            }
                        }
                    }
                }
            }

            if (activeObj != null)
            {
                DrawVirtualObjectSurfaceMapping(activeObj, deviceGroups, directionalAngleThreshold);
            }
        }
    }

    private static void DrawVirtualObjectSurfaceMapping(GameObject obj, List<List<AUTD3Device>> deviceGroups, float directionalAngleThreshold)
    {
        // 余計なオブジェクトを付けず、全ての子メッシュから「仮想的なバウンディングボックス」を計算する
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return;

        Bounds localBounds = new Bounds();
        bool hasBounds = false;

        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;

            Bounds meshBounds;
            Transform? rendererTransform = r.transform;

            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                meshBounds = smr.sharedMesh.bounds;
            }
            else if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    meshBounds = mf.sharedMesh.bounds;
                }
                else
                {
                    meshBounds = r.bounds; // Fallback
                    rendererTransform = null; // Already in world space
                }
            }
            else
            {
                meshBounds = r.bounds;
                rendererTransform = null; // Already in world space
            }

            if (meshBounds.size.sqrMagnitude < 0.0001f) continue;

            Vector3 rc = meshBounds.center;
            Vector3 re = meshBounds.extents;
            
            Vector3[] corners = {
                rc + new Vector3(re.x, re.y, re.z),
                rc + new Vector3(re.x, re.y, -re.z),
                rc + new Vector3(re.x, -re.y, re.z),
                rc + new Vector3(re.x, -re.y, -re.z),
                rc + new Vector3(-re.x, re.y, re.z),
                rc + new Vector3(-re.x, re.y, -re.z),
                rc + new Vector3(-re.x, -re.y, re.z),
                rc + new Vector3(-re.x, -re.y, -re.z)
            };

            foreach (var corner in corners)
            {
                // meshBounds がローカル空間の場合、まずワールド空間に変換してから obj のローカル空間に変換する
                Vector3 worldCorner = rendererTransform != null ? rendererTransform.TransformPoint(corner) : corner;
                Vector3 localCorner = obj.transform.InverseTransformPoint(worldCorner);

                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        if (!hasBounds) return;

        // オブジェクトの回転・スケールを反映したローカル空間(OBB)として扱う
        Gizmos.matrix = obj.transform.localToWorldMatrix;
        Vector3 center = localBounds.center;
        Vector3 size = localBounds.size;
        Vector3 extents = localBounds.extents;

        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        Vector3[] centers = {
            center + Vector3.right * extents.x,
            center + Vector3.left * extents.x,
            center + Vector3.up * extents.y,
            center + Vector3.down * extents.y,
            center + Vector3.forward * extents.z,
            center + Vector3.back * extents.z
        };
        Vector3[] sizes = {
            new Vector3(0.005f, size.y, size.z),
            new Vector3(0.005f, size.y, size.z),
            new Vector3(size.x, 0.005f, size.z),
            new Vector3(size.x, 0.005f, size.z),
            new Vector3(size.x, size.y, 0.005f),
            new Vector3(size.x, size.y, 0.005f)
        };

        for (int i = 0; i < 6; i++)
        {
            // OBBのローカル法線をワールド法線に変換
            Vector3 worldNormal = obj.transform.TransformDirection(normals[i]).normalized;
            
            List<Color> qualifyingColors = new List<Color>();

            // 全デバイスグループについて、面の法線と向きが閾値以内か判定する
            for (int g = 0; g < deviceGroups.Count; g++)
            {
                var groupForward = deviceGroups[g][0].transform.forward;
                // デバイスが面の「外側」から「内側」に向かっている場合、groupForward と -worldNormal のなす角が小さくなる
                float angle = Vector3.Angle(groupForward, -worldNormal);
                if (angle <= directionalAngleThreshold)
                {
                    Color c = Color.HSVToRGB((float)g / deviceGroups.Count, 0.8f, 1f);
                    c.a = 0.6f; // よりハッキリと面を塗りつぶすために不透明度を上げる
                    qualifyingColors.Add(c);
                }
            }

            // 担当デバイスグループがあれば色を塗る（複数ならストライプ）
            if (qualifyingColors.Count > 0)
            {
                DrawStripedFace(centers[i], sizes[i], i, qualifyingColors);
            }
        }
    }

    private static void DrawStripedFace(Vector3 center, Vector3 size, int faceIndex, List<Color> colors)
    {
        if (colors.Count == 1)
        {
            Gizmos.color = colors[0];
            Gizmos.DrawCube(center, size);
            return;
        }

        // 複数ある場合はストライプ模様（10分割）にする
        int numStripes = 10;
        Vector3 step = Vector3.zero;
        Vector3 startOffset = Vector3.zero;
        Vector3 stripeSize = size;

        // faceIndex: 0=Right, 1=Left, 2=Up, 3=Down, 4=Forward, 5=Back
        if (faceIndex == 0 || faceIndex == 1)
        {
            // 右・左面 (Xが薄い) -> Y軸方向に分割
            stripeSize.y = size.y / numStripes;
            step = new Vector3(0, stripeSize.y, 0);
            startOffset = new Vector3(0, -size.y / 2f + stripeSize.y / 2f, 0);
        }
        else if (faceIndex == 2 || faceIndex == 3)
        {
            // 上・下面 (Yが薄い) -> X軸方向に分割
            stripeSize.x = size.x / numStripes;
            step = new Vector3(stripeSize.x, 0, 0);
            startOffset = new Vector3(-size.x / 2f + stripeSize.x / 2f, 0, 0);
        }
        else
        {
            // 前・後面 (Zが薄い) -> X軸方向に分割
            stripeSize.x = size.x / numStripes;
            step = new Vector3(stripeSize.x, 0, 0);
            startOffset = new Vector3(-size.x / 2f + stripeSize.x / 2f, 0, 0);
        }

        Vector3 startPos = center + startOffset;

        for (int i = 0; i < numStripes; i++)
        {
            Gizmos.color = colors[i % colors.Count];
            Gizmos.DrawCube(startPos + step * i, stripeSize);
        }
    }
}
