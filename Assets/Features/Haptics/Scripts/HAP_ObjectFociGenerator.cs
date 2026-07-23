using UnityEngine;
using System.Collections.Generic;

#if !USE_AUTD3_LEGACY
using AUTD3;
using AUTD3.Holo;
using static AUTD3.Units;
#else
using AUTD3Sharp;
using AUTD3Sharp.Gain.Holo;
using static AUTD3Sharp.Units;
#endif

#nullable enable

/// <summary>
/// HAP_BaseObjectHapticsController（足、尻尾、関節などのオブジェクト部位ターゲット）から
/// 焦点データ（ClusterFociData）やシーケンシャルSTMフレームを生成する専用の純粋計算クラスです。
/// </summary>
public static class HAP_ObjectFociGenerator
{
    /// <summary>
    /// オブジェクトコントローラーの設定とターゲット一覧から焦点データリストを生成します。
    /// </summary>
    public static List<HAP_FociGenerator.ClusterFociData> Generate(
        HAP_BaseObjectHapticsController controller,
        float defaultIntensityPascal,
        Vector3 offset)
    {
        var result = new List<HAP_FociGenerator.ClusterFociData>();
        if (controller == null) return result;

        bool useCustomCycle = controller.autdController != null 
            && (controller.stmMode == HapticsSTMMode.FociSTM || (controller.stmMode == HapticsSTMMode.GainSTM && controller.trackMode == HapticsTrackMode.Sequential));

        if (useCustomCycle)
        {
            var activeCandidates = new List<Transform>();
            foreach (var info in controller.TargetInfos)
            {
                if (info.Transform != null && controller.IsTargetActive(info.Transform, info.IsEnabled, info.IsTail))
                {
                    activeCandidates.Add(info.Transform);
                }
            }

            if (activeCandidates.Count > 0)
            {
                TrackedCluster dummyCluster = new TrackedCluster
                {
                    Centroid = activeCandidates[0].position,
                    Normal = controller.footTargetNormal.normalized,
                    Force = 1.0f,
                    IsAlive = true
                };

                var fociData = new HAP_FociGenerator.ClusterFociData(dummyCluster);
                fociData.UseSTM = true;
                fociData.IsGainSTM = (controller.stmMode == HapticsSTMMode.GainSTM);
                fociData.STMFrequency = controller.sequentialSTMFrequency;

                foreach (var target in activeCandidates)
                {
                    Vector3 pos = target.position;
                    fociData.STMFrames.Add(new List<Vector3> { 
                        new Vector3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z) 
                    });
                }
                
                result.Add(fociData);
            }
        }
        else
        {
            foreach (var info in controller.TargetInfos)
            {
                if (info.Transform == null) continue;
                if (!controller.IsTargetActive(info.Transform, info.IsEnabled, info.IsTail)) continue;

                Vector3 pos = info.Transform.position;
                TrackedCluster dummyCluster = new TrackedCluster
                {
                    Centroid = pos,
                    Normal = controller.footTargetNormal.normalized,
                    Force = 1.0f,
                    IsAlive = true
                };

                var fociData = new HAP_FociGenerator.ClusterFociData(dummyCluster);

#if !USE_AUTD3_LEGACY
                fociData.SequentialFoci.Add(new AUTD3.Holo.ControlPoint(
                    new Vector3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z),
                    Amplitude.FromPascal(defaultIntensityPascal)
                ));
#else
                fociData.SequentialFoci.Add((
                    new AUTD3Sharp.Utils.Point3(pos.x + offset.x, pos.y + offset.y, pos.z + offset.z),
                    defaultIntensityPascal * Pa
                ));
#endif

                result.Add(fociData);
            }
        }

        return result;
    }
}
