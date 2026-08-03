using System;
using System.Runtime.InteropServices;
using Intel.RealSense;
using UnityEngine;
using Core.Logging;

namespace RealSense.DummyPointCloud
{
    /// <summary>
    /// RealSense SDK の SoftwareDevice を用いて、
    /// Unityの3Dオブジェクトから抽出したサンプリング点群を RealSense 互換の FrameSet / DepthFrame ストリームへ変換・発行するヘルパー。
    /// </summary>
    public class RsDummySoftwareDevice : IDisposable
    {
        private SoftwareDevice _softwareDevice;
        private SoftwareSensor _depthSensor;
        private VideoStreamProfile _depthProfile;

        private int _width;
        private int _height;
        private int _fps;
        private ushort[] _depthBuffer;
        private int _frameCounter = 0;

        public VideoStreamProfile DepthProfile => _depthProfile;
        public PipelineProfile ActiveProfile => null;
        public bool IsRunning { get; private set; }

        public event Action<Frame> OnFrameAvailable;

        public void Initialize(int width = 640, int height = 480, int fps = 30)
        {
            _width = width;
            _height = height;
            _fps = fps;
            _depthBuffer = new ushort[_width * _height];

            try
            {
                _softwareDevice = new SoftwareDevice();
                _depthSensor = _softwareDevice.AddSensor("Depth");

                var intrinsics = new Intrinsics
                {
                    width = _width,
                    height = _height,
                    ppx = _width / 2f,
                    ppy = _height / 2f,
                    fx = _width / 2f,
                    fy = _height / 2f,
                    model = Distortion.InverseBrownConrady,
                    coeffs = new float[5]
                };

                var vStream = new SoftwareVideoStream
                {
                    type = Stream.Depth,
                    index = 0,
                    uid = 0,
                    width = _width,
                    height = _height,
                    fps = _fps,
                    format = Format.Z16,
                    intrinsics = intrinsics
                };

                _depthProfile = _depthSensor.AddVideoStream(vStream);
                _depthSensor.Open(_depthProfile);
                _depthSensor.Start(OnSensorFrame);

                IsRunning = true;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("DPC_SoftwareDevice", $"Failed to initialize SoftwareDevice: {ex.Message}");
                Dispose();
            }
        }

        private void OnSensorFrame(Frame frame)
        {
            if (frame != null)
            {
                OnFrameAvailable?.Invoke(frame);
            }
        }

        /// <summary>
        /// サンプリングされたワールド座標系 3D 点群を RealSense フレームとして publish します。
        /// useCameraPerspective が true の場合はカメラ視点の画角・カリングを適用、false の場合は全方向・全周の全点群を出力します。
        /// </summary>
        public void PublishPointCloudAsDepthFrame(Vector3[] worldPositions, Transform cameraTransform, bool useCameraPerspective)
        {
            if (!IsRunning || _depthSensor == null || _depthBuffer == null) return;

            Array.Clear(_depthBuffer, 0, _depthBuffer.Length);

            if (worldPositions != null && worldPositions.Length > 0)
            {
                if (useCameraPerspective && cameraTransform != null)
                {
                    // --- カメラ視点モード (画角・視錐台カリングあり) ---
                    Matrix4x4 worldToCam = cameraTransform.worldToLocalMatrix;
                    float fovX = _width / 2f;
                    float fovY = _height / 2f;
                    float cx = _width / 2f;
                    float cy = _height / 2f;

                    for (int i = 0; i < worldPositions.Length; i++)
                    {
                        Vector3 pWorld = worldPositions[i];
                        Vector3 pCam = worldToCam.MultiplyPoint3x4(pWorld);

                        pCam.y = -pCam.y; // RealSense Y軸反転

                        if (pCam.z <= 0.05f || pCam.z > 10.0f) continue;

                        int u = Mathf.RoundToInt(cx + (pCam.x * fovX) / pCam.z);
                        int v = Mathf.RoundToInt(cy + (pCam.y * fovY) / pCam.z);

                        if (u >= 0 && u < _width && v >= 0 && v < _height)
                        {
                            int index = v * _width + u;
                            ushort zMm = (ushort)Mathf.Clamp(Mathf.RoundToInt(pCam.z * 1000f), 1, 65535);

                            if (_depthBuffer[index] == 0 || zMm < _depthBuffer[index])
                            {
                                _depthBuffer[index] = zMm;
                            }
                        }
                    }
                }
                else
                {
                    // --- 全方向モード (カメラ向き・画角不問で全点群を出力) ---
                    Vector3 referencePos = cameraTransform != null ? cameraTransform.position : Vector3.zero;
                    int maxCount = Mathf.Min(worldPositions.Length, _depthBuffer.Length);

                    for (int i = 0; i < maxCount; i++)
                    {
                        Vector3 pWorld = worldPositions[i];
                        float distMeters = Vector3.Distance(referencePos, pWorld);
                        ushort zMm = (ushort)Mathf.Clamp(Mathf.RoundToInt(distMeters * 1000f), 1, 65535);

                        _depthBuffer[i] = zMm;
                    }
                }
            }

            _frameCounter++;
            int stride = _width * sizeof(ushort);

            _depthSensor.AddVideoFrame<ushort>(
                _depthBuffer,
                stride,
                sizeof(ushort),
                (double)Time.realtimeSinceStartup * 1000.0,
                TimestampDomain.SystemTime,
                _frameCounter,
                _depthProfile);
        }

        public void Dispose()
        {
            IsRunning = false;

            if (_depthSensor != null)
            {
                try
                {
                    _depthSensor.Stop();
                    _depthSensor.Close();
                    _depthSensor.Dispose();
                }
                catch { }
                _depthSensor = null;
            }

            if (_softwareDevice != null)
            {
                try
                {
                    _softwareDevice.Dispose();
                }
                catch { }
                _softwareDevice = null;
            }
        }
    }
}
