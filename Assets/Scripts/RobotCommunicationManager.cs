using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class RobotCommunicationManager : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = 6000;

    [Header("Performance Settings")]
    public float sendInterval = 0.05f;

    [Header("Smoothing")]
    public float maxDegreesPerSecond = 120f;

    [Header("Right Arm Safe Position (0-180 degrees)")]
    public float safeShoulderPitch = 90f;
    public float safeShoulderRoll  = 160f;
    public float safeShoulderYaw   = 90f;
    public float safeElbow         = 60f;
    public float safeWrist         = 130f;
    public float safeGrip          = 170f;

    [Header("Left Arm Safe Position (0-180 degrees)")]
    public float safeLeftShoulderPitch = 90f;
    public float safeLeftShoulderRoll  = 20f;
    public float safeLeftShoulderYaw   = 90f;
    public float safeLeftElbow         = 60f;
    public float safeLeftWrist         = 50f;
    public float safeLeftGrip          = 170f;

    [Header("Head Safe Position (0-180 degrees)")]
    public float safeHeadPan  = 90f;
    public float safeHeadTilt = 100f;

    [Header("Debug Settings")]
    public bool enableDebugLogs = true;
    public bool enableNetworking = true;

    public bool IsPaused { get; private set; } = false;
    public bool IsInSafePosition { get; private set; } = false;

    private const int ServoCount = 14;
    private float[] targetServoValues  = new float[ServoCount];
    private float[] currentServoValues = new float[ServoCount];
    private bool initialized = false;
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private float lastSendTime = 0f;

    private static RobotCommunicationManager instance;
    public static RobotCommunicationManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<RobotCommunicationManager>();
            return instance;
        }
    }

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        InitializeNetwork();
        GoToSafePosition();
    }

    void InitializeNetwork()
    {
        if (enableNetworking)
        {
            try
            {
                udpClient = new UdpClient();
                remoteEndPoint = new IPEndPoint(IPAddress.Parse(GlobalVariables.raspberryPiIp), port);

                byte[] test = Encoding.ASCII.GetBytes("90,160,90,60,130,170,90,20,90,60,50,170,90,120");
                udpClient.Send(test, test.Length, remoteEndPoint);

                if (enableDebugLogs)
                    Debug.Log("Communication Manager: Test UDP sent successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError("UDP initialization error: " + ex.Message);
                enableNetworking = false;
            }
        }
    }

    void Update()
    {
        if (Time.time - lastSendTime >= sendInterval)
        {
            SmoothServoValues();
            SendAllServoData();
            lastSendTime = Time.time;
        }
    }

    void SmoothServoValues()
    {
        if (!initialized)
        {
            System.Array.Copy(targetServoValues, currentServoValues, ServoCount);
            initialized = true;
            return;
        }

        float maxStep = maxDegreesPerSecond * sendInterval;
        for (int i = 0; i < ServoCount; i++)
        {
            float diff = targetServoValues[i] - currentServoValues[i];
            currentServoValues[i] += Mathf.Clamp(diff, -maxStep, maxStep);
        }
    }

    public void UpdateArmServos(float shoulderPitch, float shoulderRoll, float shoulderYaw,
                                float elbow, float wrist, float grip)
    {
        if (IsPaused) return;
        targetServoValues[0] = shoulderPitch;
        targetServoValues[1] = shoulderRoll;
        targetServoValues[2] = shoulderYaw;
        targetServoValues[3] = elbow;
        targetServoValues[4] = wrist;
        targetServoValues[5] = grip;
    }

    public void UpdateLeftArmServos(float shoulderPitch, float shoulderRoll, float shoulderYaw,
                                    float elbow, float wrist, float grip)
    {
        if (IsPaused) return;
        targetServoValues[6]  = shoulderPitch;
        targetServoValues[7]  = shoulderRoll;
        targetServoValues[8]  = shoulderYaw;
        targetServoValues[9]  = elbow;
        targetServoValues[10] = wrist;
        targetServoValues[11] = grip;
    }

    // Indices 12-13: head
    public void UpdateHeadServos(float headPan, float headTilt)
    {
        if (IsPaused) return;
        targetServoValues[12] = headPan;
        targetServoValues[13] = headTilt;
    }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        IsInSafePosition = false;
    }

    public void GoToSafePosition()
    {
        targetServoValues[0]  = safeShoulderPitch;
        targetServoValues[1]  = safeShoulderRoll;
        targetServoValues[2]  = safeShoulderYaw;
        targetServoValues[3]  = safeElbow;
        targetServoValues[4]  = safeWrist;
        targetServoValues[5]  = safeGrip;
        targetServoValues[6]  = safeLeftShoulderPitch;
        targetServoValues[7]  = safeLeftShoulderRoll;
        targetServoValues[8]  = safeLeftShoulderYaw;
        targetServoValues[9]  = safeLeftElbow;
        targetServoValues[10] = safeLeftWrist;
        targetServoValues[11] = safeLeftGrip;
        targetServoValues[12] = safeHeadPan;
        targetServoValues[13] = safeHeadTilt;
        IsPaused = true;
        IsInSafePosition = true;
    }

    void SendAllServoData()
    {
        string message = string.Join(",", currentServoValues);

        if (enableNetworking && udpClient != null)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(message);
                udpClient.Send(data, data.Length, remoteEndPoint);

                if (enableDebugLogs)
                    Debug.Log("Sent: " + message);
            }
            catch (Exception ex)
            {
                Debug.LogError("UDP send error: " + ex.Message);
            }
        }
        else if (enableDebugLogs)
        {
            Debug.Log("Would send: " + message);
        }
    }

    void OnDestroy()
    {
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient.Dispose();
        }
    }
}
