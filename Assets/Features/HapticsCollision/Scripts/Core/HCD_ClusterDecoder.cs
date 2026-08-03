using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Features.HapticsCollision.Core
{
    /// <summary>
    /// GPU から Readback されたバイナリ構造体（固定小数点数）のデコードおよびデータ抽出を担当するクラス。
    /// </summary>
    public class HCD_ClusterDecoder
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct ClusterData
        {
            public int count;
            public int weightSum;
            public int posX;
            public int posY;
            public int posZ;
            public int normalX;
            public int normalY;
            public int normalZ;
            public int rawPosX;
            public int rawPosY;
            public int rawPosZ;
            public int meshPosX;
            public int meshPosY;
            public int meshPosZ;
            public int minDistInt;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ClusterPrecisionDataRaw
        {
            public int covXX, covYY, covZZ, covXY, covXZ, covYZ;
            public int rp00X, rp00Y, rp00Z;
            public int rp01X, rp01Y, rp01Z;
            public int rp02X, rp02Y, rp02Z;
            public int rp03X, rp03Y, rp03Z;
            public int rp04X, rp04Y, rp04Z;
            public int rp05X, rp05Y, rp05Z;
            public int rp06X, rp06Y, rp06Z;
            public int rp07X, rp07Y, rp07Z;
            public int rp08X, rp08Y, rp08Z;
            public int rp09X, rp09Y, rp09Z;
            public int rp10X, rp10Y, rp10Z;
            public int rp11X, rp11Y, rp11Z;
            public int rp12X, rp12Y, rp12Z;
            public int rp13X, rp13Y, rp13Z;
            public int rp14X, rp14Y, rp14Z;
            public int rp15X, rp15Y, rp15Z;
        }

        private ClusterData[] _clusterResults;
        private ClusterPrecisionDataRaw[] _precisionResults;

        public ClusterData[] ClusterResults => _clusterResults;
        public ClusterPrecisionDataRaw[] PrecisionResults => _precisionResults;

        public void AllocateBuffers(int maxClusters)
        {
            if (_clusterResults == null || _clusterResults.Length != maxClusters)
            {
                _clusterResults = new ClusterData[maxClusters];
                _precisionResults = new ClusterPrecisionDataRaw[maxClusters];
            }
        }

        public void DecodeActiveClusterInfos(
            bool precisionMode,
            out List<Vector3> centroids,
            out List<Vector3> normals,
            out List<int> counts,
            out List<ClusterPrecision> precisions,
            out List<Vector3> rawPositions,
            out List<Vector3> meshPositions,
            out List<float> minDistances)
        {
            centroids     = new List<Vector3>();
            normals       = new List<Vector3>();
            counts        = new List<int>();
            precisions    = new List<ClusterPrecision>();
            rawPositions  = new List<Vector3>();
            meshPositions = new List<Vector3>();
            minDistances  = new List<float>();

            if (_clusterResults == null) return;

            for (int i = 0; i < _clusterResults.Length; i++)
            {
                var data = _clusterResults[i];
                if (data.count > 0)
                {
                    float invScale = (data.weightSum > 0)
                        ? (1.0f / (float)data.weightSum)
                        : (1.0f / (data.count * 100000.0f));

                    centroids.Add(new Vector3(data.posX, data.posY, data.posZ) * invScale);
                    rawPositions.Add(new Vector3(data.rawPosX, data.rawPosY, data.rawPosZ) * invScale);
                    meshPositions.Add(new Vector3(data.meshPosX, data.meshPosY, data.meshPosZ) * invScale);

                    var avgNormal = new Vector3(data.normalX, data.normalY, data.normalZ) * invScale;
                    normals.Add(avgNormal.sqrMagnitude > 0.0001f ? avgNormal.normalized : Vector3.up);
                    counts.Add(data.count);

                    float minDist = (data.minDistInt < 2147483640) ? (data.minDistInt / 1000000.0f) : 0.0f;
                    minDistances.Add(minDist);

                    if (precisionMode && _precisionResults != null)
                    {
                        var pData = _precisionResults[i];
                        float inv1e6 = 1.0f / 1000000.0f;
                        float inv1e4 = 1.0f / 10000.0f;
                        precisions.Add(new ClusterPrecision {
                            covXX = pData.covXX * inv1e6,
                            covYY = pData.covYY * inv1e6,
                            covZZ = pData.covZZ * inv1e6,
                            covXY = pData.covXY * inv1e6,
                            covXZ = pData.covXZ * inv1e6,
                            covYZ = pData.covYZ * inv1e6,
                            rp00 = new Vector3(pData.rp00X, pData.rp00Y, pData.rp00Z) * inv1e4,
                            rp01 = new Vector3(pData.rp01X, pData.rp01Y, pData.rp01Z) * inv1e4,
                            rp02 = new Vector3(pData.rp02X, pData.rp02Y, pData.rp02Z) * inv1e4,
                            rp03 = new Vector3(pData.rp03X, pData.rp03Y, pData.rp03Z) * inv1e4,
                            rp04 = new Vector3(pData.rp04X, pData.rp04Y, pData.rp04Z) * inv1e4,
                            rp05 = new Vector3(pData.rp05X, pData.rp05Y, pData.rp05Z) * inv1e4,
                            rp06 = new Vector3(pData.rp06X, pData.rp06Y, pData.rp06Z) * inv1e4,
                            rp07 = new Vector3(pData.rp07X, pData.rp07Y, pData.rp07Z) * inv1e4,
                            rp08 = new Vector3(pData.rp08X, pData.rp08Y, pData.rp08Z) * inv1e4,
                            rp09 = new Vector3(pData.rp09X, pData.rp09Y, pData.rp09Z) * inv1e4,
                            rp10 = new Vector3(pData.rp10X, pData.rp10Y, pData.rp10Z) * inv1e4,
                            rp11 = new Vector3(pData.rp11X, pData.rp11Y, pData.rp11Z) * inv1e4,
                            rp12 = new Vector3(pData.rp12X, pData.rp12Y, pData.rp12Z) * inv1e4,
                            rp13 = new Vector3(pData.rp13X, pData.rp13Y, pData.rp13Z) * inv1e4,
                            rp14 = new Vector3(pData.rp14X, pData.rp14Y, pData.rp14Z) * inv1e4,
                            rp15 = new Vector3(pData.rp15X, pData.rp15Y, pData.rp15Z) * inv1e4,
                        });
                    }
                    else
                    {
                        precisions.Add(default);
                    }
                }
            }
        }
    }
}
