using System.Diagnostics;
using System.IO;
using UnityEngine;

public class VideoStreamToTexture : MonoBehaviour
{
    Process ffmpegProcess;
    Texture2D texture;
    byte[] frameBuffer;
    byte[] fullFrameBuffer;
    int textureWidth = 1280;
    int textureHeight = 720;
    int expectedFrameSize;
    int accumulatedDataSize = 0;

    void Start()
    {
        StartFFmpeg();
        texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGB24, false);
        frameBuffer = new byte[32768];
        fullFrameBuffer = new byte[textureWidth * textureHeight * 3];
        expectedFrameSize = fullFrameBuffer.Length;
    }

    void Update()
    {
        if (ffmpegProcess != null && ffmpegProcess.StandardOutput.BaseStream.CanRead)
        {
            int bytesRead = ffmpegProcess.StandardOutput.BaseStream.Read(frameBuffer, 0, frameBuffer.Length);

            if (bytesRead > 0)
            {
                System.Array.Copy(frameBuffer, 0, fullFrameBuffer, accumulatedDataSize, bytesRead);
                accumulatedDataSize += bytesRead;

                if (accumulatedDataSize >= expectedFrameSize)
                {
                    try
                    {
                        texture.LoadRawTextureData(fullFrameBuffer);
                        texture.Apply();
                        GetComponent<Renderer>().material.mainTexture = texture;
                        accumulatedDataSize = 0;
                    }
                    catch
                    {
                        UnityEngine.Debug.LogWarning("Failed to load texture frame.");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"Accumulating frame data: {accumulatedDataSize} bytes.");
                }
            }
        }
    }

    void StartFFmpeg()
    {
        ffmpegProcess = new Process();
        ffmpegProcess.StartInfo.FileName = "ffmpeg";
        ffmpegProcess.StartInfo.Arguments = "-rtsp_transport udp -i rtsp://192.168.0.203:8554/unicast -f rawvideo -pix_fmt rgb24 -vsync 2 -flush_packets 0 -";
        ffmpegProcess.StartInfo.RedirectStandardOutput = true;
        ffmpegProcess.StartInfo.UseShellExecute = false;
        ffmpegProcess.StartInfo.CreateNoWindow = true;
        ffmpegProcess.Start();
    }

    void OnApplicationQuit()
    {
        ffmpegProcess?.Kill();
    }
}
