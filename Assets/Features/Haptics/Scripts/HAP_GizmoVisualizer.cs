using UnityEngine;
using System.Linq;
using System.Collections.Generic;

#nullable enable

/// <summary>
/// AUTDデバイスの可視化および仮想オブジェクトへのグルーピングマッピングをGizmoとして描画するユーティリティクラス
/// </summary>
public static partial class HAP_GizmoVisualizer
{
    public static void DrawDevicesAndGroupings(
        AUTD3Device[] devices, 
        bool enableDirectionalGrouping, 
        float directionalAngleThreshold, 
        HCD_Pipeline? hcdPipeline,
        HAP_AUTDDebugDisabler? debugDisabler = null)
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
                bool isDisabled = debugDisabler != null && debugDisabler.IsDisabled(device.ID);

                // Gizmoの描画
                Gizmos.matrix = Matrix4x4.TRS(device.transform.position, device.transform.rotation, Vector3.one);
                Gizmos.color = isDisabled ? new Color(0.3f, 0.3f, 0.3f, 0.8f) : groupColor;
                
                // AUTD3デバイスの簡易描画 (目安として 192mm x 151mm)
                Gizmos.DrawWireCube(new Vector3(0.096f, 0.075f, 0), new Vector3(0.192f, 0.151f, 0.01f));
                
                // AUTDのマテリアル色を変更（子オブジェクトのRendererに対してMaterialPropertyBlockを適用）
                var renderers = device.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    
                    if (isDisabled)
                    {
                        // Disabled devices are drawn in dark grey
                        Color disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                        block.SetColor("_Color", disabledColor);
                        block.SetColor("_BaseColor", disabledColor);
                    }
                    else if (enableDirectionalGrouping)
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
                DrawVirtualObjectSurfaceMapping(activeObj, deviceGroups, directionalAngleThreshold, debugDisabler);
            }
        }
    }
}
