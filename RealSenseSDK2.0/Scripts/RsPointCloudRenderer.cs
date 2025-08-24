using Intel.RealSense;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RsPointCloudRenderer : MonoBehaviour
{
    public RsDeviceController rsDeviceController;
    public RsProcessingPipe processingPipe;

    [SerializeField] private ComputeShader pointCloudFilterShader;
    [SerializeField] private ComputeShader pointCloudTransformerShader;

    private Texture2D uvmap;
    private Vector3[] rawVertices;
    private Vector3[] globalVertices;
    private Vector3[] newGlobalVerticesBuffer;
    private int newGlobalVerticesCount = 0;

    private Vector3 rsScanRange;

    private Vector3 globalThreshold1, globalThreshold2;

    public int processIntervalFrames = 1;

    private float frameWidth;
    private int rsLength;
    private int frameCounter = 0;

    [HideInInspector]
    public bool IsLocalRangeFilterEnabled = true;

    [SerializeField]
    public bool IsGlobalRangeFilterEnabled = true;

    [SerializeField, HideInInspector]
    private string exportFileName = "currentGlobalVertices.txt";

    public string ExportFileName => exportFileName;

    private Matrix4x4 localToWorld;

    private ComputeBuffer rawVerticesBuffer;
    private ComputeBuffer filteredVerticesBuffer;
    private ComputeBuffer countBuffer;

    FrameQueue q;

    void Start()
    {
        processingPipe.OnStart += OnStartStreaming;
        processingPipe.OnStop += Dispose;

        rsScanRange = rsDeviceController.RealSenseScanRange;
        frameWidth = rsDeviceController.FrameWidth;

        globalThreshold1 = new Vector3(frameWidth, frameWidth, frameWidth);
        globalThreshold2 = new Vector3(rsScanRange.x - frameWidth, rsScanRange.y - frameWidth, rsScanRange.z - frameWidth);

        localToWorld = transform.localToWorldMatrix;

        if (rsDeviceController.adaptIntervalFrame)
        {
            processIntervalFrames = processingPipe.GetProcessIntervalFrames();
        }
    }

    private void OnStartStreaming(PipelineProfile obj)
    {
        q = new FrameQueue(1);

        using (var depth = obj.Streams.FirstOrDefault(s => s.Stream == Intel.RealSense.Stream.Depth && s.Format == Format.Z16).As<VideoStreamProfile>())
        {
            UnityEngine.Debug.Log($"Depth stream: Width => {depth.Width}, Height = {depth.Height}");
            ResetMesh(depth.Width, depth.Height);
        }

        processingPipe.OnNewSample += OnNewSample;
    }

    private void ResetMesh(int width, int height)
    {
        Assert.IsTrue(SystemInfo.SupportsTextureFormat(TextureFormat.RGFloat));
        uvmap = new Texture2D(width, height, TextureFormat.RGFloat, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point,
        };
        GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_UVMap", uvmap);

        rsLength = width * height;

        rawVertices = new Vector3[rsLength];
        globalVertices = new Vector3[rsLength];
        newGlobalVerticesBuffer = new Vector3[rsLength];

        ReleaseBuffers();
        InitializeBuffers();
    }

    private void InitializeBuffers()
    {
        rawVerticesBuffer = new ComputeBuffer(rsLength, sizeof(float) * 3);
        filteredVerticesBuffer = new ComputeBuffer(rsLength, sizeof(float) * 3, ComputeBufferType.Append);
        countBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
    }

    private void ReleaseBuffers()
    {
        rawVerticesBuffer?.Release();
        filteredVerticesBuffer?.Release();
        countBuffer?.Release();
    }

    void OnDestroy()
    {
        Dispose();
    }

    private void Dispose()
    {
        processingPipe.OnNewSample -= OnNewSample;
        q?.Dispose();
        q = null;
        ReleaseBuffers();
    }

    private void OnNewSample(Frame frame)
    {
        if (q == null) return;

        try
        {
            if (frame.IsComposite)
            {
                using (var fs = frame.As<FrameSet>())
                using (var points = fs.FirstOrDefault<Points>(Intel.RealSense.Stream.Depth, Format.Xyz32f))
                {
                    if (points != null) q.Enqueue(points);
                }
                return;
            }
            if (frame.Is(Extension.Points))
            {
                q.Enqueue(frame);
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    protected void LateUpdate()
    {
        frameCounter++;
        if (frameCounter % processIntervalFrames != 0) return;

        if (q == null) return;

        if (q.PollForFrame<Points>(out var points))
            using (points)
            {
                if (points.Count != rsLength)
                {
                    using (var p = points.GetProfile<VideoStreamProfile>())
                        ResetMesh(p.Width, p.Height);
                }

                if (points.TextureData != IntPtr.Zero)
                {
                    uvmap.LoadRawTextureData(points.TextureData, points.Count * sizeof(float) * 2);
                    uvmap.Apply();
                }

                if (points.VertexData != IntPtr.Zero)
                {
                    points.CopyVertices(rawVertices);

                    if (IsLocalRangeFilterEnabled)
                    {
                        FilterWithComputeShader();
                    }
                    else
                    {
                        TransformWithComputeShader();
                    }
                }
            }
    }

    private void FilterWithComputeShader()
    {
        rawVerticesBuffer.SetData(rawVertices);
        filteredVerticesBuffer.SetCounterValue(0);

        int kernel = pointCloudFilterShader.FindKernel("CSMain");
        pointCloudFilterShader.SetBuffer(kernel, "rawVertices", rawVerticesBuffer);
        pointCloudFilterShader.SetBuffer(kernel, "filteredVertices", filteredVerticesBuffer);
        pointCloudFilterShader.SetMatrix("localToWorld", localToWorld);
        pointCloudFilterShader.SetVector("globalThreshold1", globalThreshold1);
        pointCloudFilterShader.SetVector("globalThreshold2", globalThreshold2);
        pointCloudFilterShader.SetInt("vertexCount", rawVertices.Length);

        int threadGroups = Mathf.CeilToInt(rawVertices.Length / 256.0f);
        pointCloudFilterShader.Dispatch(kernel, threadGroups, 1, 1);

        ComputeBuffer.CopyCount(filteredVerticesBuffer, countBuffer, 0);
        int[] countArr = new int[1];
        countBuffer.GetData(countArr);
        newGlobalVerticesCount = countArr[0];

        if (newGlobalVerticesCount > 0)
        {
            filteredVerticesBuffer.GetData(newGlobalVerticesBuffer, 0, 0, newGlobalVerticesCount);
        }
    }

    private void TransformWithComputeShader()
    {
        rawVerticesBuffer.SetData(rawVertices);
        filteredVerticesBuffer.SetCounterValue(0);

        int kernel = pointCloudTransformerShader.FindKernel("CSMain");
        pointCloudTransformerShader.SetBuffer(kernel, "rawVertices", rawVerticesBuffer);
        pointCloudTransformerShader.SetBuffer(kernel, "filteredVertices", filteredVerticesBuffer);
        pointCloudTransformerShader.SetMatrix("localToWorld", localToWorld);
        pointCloudTransformerShader.SetInt("vertexCount", rawVertices.Length);

        int threadGroups = Mathf.CeilToInt(rawVertices.Length / 256.0f);
        pointCloudTransformerShader.Dispatch(kernel, threadGroups, 1, 1);

        ComputeBuffer.CopyCount(filteredVerticesBuffer, countBuffer, 0);
        int[] countArr = new int[1];
        countBuffer.GetData(countArr);
        newGlobalVerticesCount = countArr[0];

        if (newGlobalVerticesCount > 0)
        {
            filteredVerticesBuffer.GetData(globalVertices, 0, 0, newGlobalVerticesCount);
        }
    }

    public Vector3[] GetFilteredVertices()
    {
        if (!IsLocalRangeFilterEnabled)
        {
            return globalVertices;
        }
        Vector3[] result = new Vector3[newGlobalVerticesCount];
        Array.Copy(newGlobalVerticesBuffer, result, newGlobalVerticesCount);
        return result;
    }
}