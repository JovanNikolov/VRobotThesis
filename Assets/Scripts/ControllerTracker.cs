using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using Valve.VR;

public class ControllerTracker : MonoBehaviour
{
    [Header("VR Components")]
    public SteamVR_Behaviour_Pose rightController;
    public SteamVR_Behaviour_Pose leftController;
    public SteamVR_Action_Single gripAction;
    public SteamVR_Action_Single leftGripAction;

    [Header("Calibration")]
    public Transform headsetTransform;
    public SteamVR_Action_Boolean recalibrateButton;
    public Vector3 shoulderOffsetFromHead = new Vector3(0.20f, -0.25f, 0f);
    public float rightShoulderXOffset = 0.1f;

    [Header("Safe Mode Button")]
    public SteamVR_Action_Boolean safeModeButton;
    public float holdThreshold = 0.5f;

    [Header("Robot Dimensions")]
    public float upperArmLength = 0.08f;
    public float forearmLength = 0.08f;
    public float servoOffset = 0.05f;

    [Header("Network Settings")]
    public int port = 6000;

    [Header("Performance Settings")]
    public float sendInterval = 0.05f;

    [Header("Debug Settings")]
    public bool enableDebugLogs = true;
    public bool enableGizmos = true;
    public bool enableNetworking = true;

    [Header("Transforms")]
    public Transform shoulderTransform;
    public Transform elbowTransform;
    public Transform wristTransform;

    [Header("Wrist Settings")]
    public Vector3 controllerToWristOffset = new Vector3(0f, -0.08f, 0.02f);
    public float wristSmoothingFactor = 0.15f;

    [Header("Servo Calibration")]
    public float shoulderPitchCalibOffset = 10f;
    public float leftShoulderPitchCalibOffset = -10f;
    public float shoulderRollCalibOffset = 60f;
    public float leftShoulderRollCalibOffset = -30f;
    public float wristOffset = 60f;
    public float leftWristOffset = -90f;

    public float elbowMin = 90f;
    public float elbowMax = 180f;
    public float gripCalibOffset = 20f;
    public float gripMin = 110f;
    public float gripMax = 180f;

    private float rightPrevWrist, rightSmoothedWrist;
    private bool rightWristInit;
    private Vector3 rightShoulderPos;

    private float leftPrevWrist, leftSmoothedWrist;
    private bool leftWristInit;
    private Vector3 leftShoulderPos;

    private float safeModeHoldTimer = 0f;
    private bool longPressTriggered = false;
    private HeadTracker headTracker;
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private float lastSendTime = 0f;

    void Start()
    {
        if (enableDebugLogs)
            Debug.Log("ControllerTracker Start called on: " + gameObject.name);

        if (enableNetworking)
        {
            try
            {
                udpClient = new UdpClient();
                remoteEndPoint = new IPEndPoint(IPAddress.Parse(GlobalVariables.raspberryPiIp), port);
                if (enableDebugLogs)
                    Debug.Log("ControllerTracker UDP initialized");
            }
            catch (Exception ex)
            {
                Debug.LogError("UDP initialization error: " + ex.Message);
                enableNetworking = false;
            }
        }

        headTracker = FindObjectOfType<HeadTracker>();
        InitShoulderPositions();
        InitializeTransforms();
    }

    void InitShoulderPositions()
    {
        Transform head = GetHead();
        Vector3 headPos = head != null ? head.position : new Vector3(0f, 1.55f, 0f);
        rightShoulderPos = headPos + shoulderOffsetFromHead + new Vector3(rightShoulderXOffset, 0f, 0f);
        leftShoulderPos  = headPos + MirroredOffset(shoulderOffsetFromHead);
    }

    Transform GetHead()
    {
        if (headsetTransform != null) return headsetTransform;
        if (headTracker != null)
            return headTracker.headPose != null ? headTracker.headPose.transform : headTracker.headset;
        return null;
    }

    Vector3 MirroredOffset(Vector3 offset) => new Vector3(-offset.x, offset.y, offset.z);

    void InitializeTransforms()
    {
        if (shoulderTransform == null)
        {
            GameObject s = new GameObject("ShoulderSocket");
            s.transform.position = rightShoulderPos;
            shoulderTransform = s.transform;
        }
        if (elbowTransform == null)
        {
            GameObject e = new GameObject("Elbow");
            e.transform.parent = shoulderTransform;
            elbowTransform = e.transform;
        }
        if (wristTransform == null)
        {
            GameObject w = new GameObject("Wrist");
            w.transform.parent = elbowTransform;
            wristTransform = w.transform;
        }
    }

    void Update()
    {
        bool hasRight = rightController != null;
        bool hasLeft  = leftController  != null;
        if (!hasRight && !hasLeft) return;

        if (recalibrateButton != null)
        {
            if (hasRight && recalibrateButton.GetStateDown(SteamVR_Input_Sources.RightHand))
                RecalibrateShoulderPos(isLeft: false);
            if (hasLeft && recalibrateButton.GetStateDown(SteamVR_Input_Sources.LeftHand))
                RecalibrateShoulderPos(isLeft: true);
        }

        HandleSafeModeButton(hasRight, hasLeft);

        if (Time.time - lastSendTime >= sendInterval)
        {
            if (hasRight) ComputeAndSend(rightController, isLeft: false);
            if (hasLeft)  ComputeAndSend(leftController,  isLeft: true);
            lastSendTime = Time.time;
        }

#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    void HandleSafeModeButton(bool hasRight, bool hasLeft)
    {
        if (safeModeButton == null) return;

        bool stateDown = (hasRight && safeModeButton.GetStateDown(SteamVR_Input_Sources.RightHand)) ||
                         (hasLeft  && safeModeButton.GetStateDown(SteamVR_Input_Sources.LeftHand));
        bool stateHeld = (hasRight && safeModeButton.GetState(SteamVR_Input_Sources.RightHand)) ||
                         (hasLeft  && safeModeButton.GetState(SteamVR_Input_Sources.LeftHand));
        bool stateUp   = (hasRight && safeModeButton.GetStateUp(SteamVR_Input_Sources.RightHand)) ||
                         (hasLeft  && safeModeButton.GetStateUp(SteamVR_Input_Sources.LeftHand));

        if (stateDown) { safeModeHoldTimer = 0f; longPressTriggered = false; }

        if (stateHeld)
        {
            safeModeHoldTimer += Time.deltaTime;
            if (!longPressTriggered && safeModeHoldTimer >= holdThreshold)
            {
                longPressTriggered = true;
                RobotCommunicationManager.Instance?.GoToSafePosition();
                if (enableDebugLogs) Debug.Log("Safe position activated (hold)");
            }
        }

        if (stateUp && !longPressTriggered)
        {
            RobotCommunicationManager.Instance?.TogglePause();
            if (enableDebugLogs)
                Debug.Log("Safe mode toggled: " + RobotCommunicationManager.Instance?.IsPaused);
        }
    }

    void RecalibrateShoulderPos(bool isLeft)
    {
        Transform head = GetHead();
        if (head == null)
        {
            Debug.LogWarning("RecalibrateBasePosition: no headset transform found.");
            return;
        }

        if (isLeft)
        {
            leftShoulderPos = head.position + MirroredOffset(shoulderOffsetFromHead);
            leftWristInit = false;
        }
        else
        {
            rightShoulderPos = head.position + shoulderOffsetFromHead + new Vector3(rightShoulderXOffset, 0f, 0f);
            rightWristInit = false;
        }

        if (headTracker != null) headTracker.RecalibrateCenter();

        if (enableDebugLogs)
            Debug.Log($"{(isLeft ? "Left" : "Right")} shoulder recalibrated: {(isLeft ? leftShoulderPos : rightShoulderPos)}");
    }

    public void RecalibrateBasePosition()
    {
        Transform head = GetHead();
        if (head == null) { Debug.LogWarning("RecalibrateBasePosition: no headset transform found."); return; }
        rightShoulderPos = head.position + shoulderOffsetFromHead + new Vector3(rightShoulderXOffset, 0f, 0f);
        leftShoulderPos  = head.position + MirroredOffset(shoulderOffsetFromHead);
        rightWristInit = leftWristInit = false;
        if (headTracker != null) headTracker.RecalibrateCenter();
        if (enableDebugLogs) Debug.Log($"Both shoulders recalibrated. R:{rightShoulderPos} L:{leftShoulderPos}");
    }

    void ComputeAndSend(SteamVR_Behaviour_Pose controller, bool isLeft)
    {
        float mirrorSign = isLeft ? -1f : 1f;
        Vector3 shoulderPos = isLeft ? leftShoulderPos : rightShoulderPos;
        SteamVR_Input_Sources inputSource = isLeft ? SteamVR_Input_Sources.LeftHand : SteamVR_Input_Sources.RightHand;

        Vector3 controllerOffset = controller.transform.rotation * new Vector3(0f, -0.08f, 0.02f);
        Vector3 targetWristPos = controller.transform.position + controllerOffset;

        Vector3 toTargetFromShoulder = targetWristPos - shoulderPos;
        float shoulderPitchDeg = Mathf.Atan2(toTargetFromShoulder.y, toTargetFromShoulder.z) * Mathf.Rad2Deg;

        Vector3 rollPos = shoulderPos + new Vector3(0.03f * mirrorSign, 0f, 0f);
        Vector3 toTargetFromRoll = targetWristPos - rollPos;
        float shoulderRollDeg = Mathf.Atan2(toTargetFromRoll.x, toTargetFromRoll.z) * Mathf.Rad2Deg;

        Vector3 pitchPos = rollPos + new Vector3(0f, 0f, 0.04f);
        Vector3 toTargetFromPitch = targetWristPos - pitchPos;
        float shoulderYawDeg = Mathf.Atan2(toTargetFromPitch.x, toTargetFromPitch.z) * Mathf.Rad2Deg;

        Vector3 elbowDir = toTargetFromPitch.normalized;
        Vector3 elbowPos = pitchPos + elbowDir * 0.03f;
        float reach = Mathf.Min((targetWristPos - elbowPos).magnitude, forearmLength - 0.001f);
        float elbowRad = Mathf.Acos(
            Mathf.Clamp(
                (forearmLength * forearmLength + 0.03f * 0.03f - reach * reach) /
                (2 * forearmLength * 0.03f),
                -1f, 1f
            )
        );
        float elbowDeg = elbowRad * Mathf.Rad2Deg;

        Vector3 wristDir = (targetWristPos - elbowPos).normalized;
        Vector3 wristPos = elbowPos + wristDir * forearmLength;

        float wristRotationDeg = isLeft
            ? ComputeSmoothWrist(controller, mirrorSign, shoulderPos, ref leftPrevWrist,  ref leftSmoothedWrist,  ref leftWristInit)
            : ComputeSmoothWrist(controller, mirrorSign, shoulderPos, ref rightPrevWrist, ref rightSmoothedWrist, ref rightWristInit);

        SteamVR_Action_Single activeGrip = isLeft ? leftGripAction : gripAction;
        float gripServo = activeGrip != null ? Map(activeGrip.GetAxis(inputSource), 0, 1, 0, 180) : 0f;

        float servoShoulderPitch = isLeft
            ? Map(shoulderPitchDeg, -90, 90, 180, 0)
            : Map(shoulderPitchDeg, -90, 90, 0, 180);
        float servoShoulderRoll = isLeft
            ? Map(shoulderRollDeg,   -90, 90, 180, 0)
            : Map(shoulderRollDeg,   -90, 90, 180, 0);
        float servoShoulderYaw = isLeft
            ? Map(shoulderYawDeg,   -90, 90, 0, 180)
            : Map(shoulderYawDeg,   -90, 90, 0, 180);
        float servoElbow         = Map(elbowDeg, 0, 135, 0, 180);
        float servoWrist         = isLeft
            ? Map(wristRotationDeg, -90, 90, 0, 180)
            : Map(wristRotationDeg, -90, 90, 0, 180);

        if (enableDebugLogs)
        {
            string h = isLeft ? "L" : "R";
            Debug.Log($"[{h}] Yaw:{servoShoulderYaw:F1} Pitch:{servoShoulderPitch:F1} Roll:{servoShoulderRoll:F1} Elbow:{servoElbow:F1} Wrist:{servoWrist:F1} Grip:{gripServo:F1}");
        }

        var calibratedPitch = Mathf.Clamp(servoShoulderPitch + shoulderPitchCalibOffset, 0, 180);
        var calibratedRoll  = Mathf.Clamp(servoShoulderRoll  + shoulderRollCalibOffset,  0, 180);
        var calibratedYaw = servoShoulderYaw;
        var calibratedElbow = Mathf.Clamp(servoElbow, elbowMin, elbowMax);
        var calibratedGrip  = Mathf.Clamp(gripServo + gripCalibOffset, gripMin, gripMax);
        var calibratedWrist = Mathf.Clamp(servoWrist + wristOffset, 0, 180);

        var calibratedLeftPitch = Mathf.Clamp(servoShoulderPitch + leftShoulderPitchCalibOffset, 0, 180);
        var calibratedLeftRoll  = Mathf.Clamp(servoShoulderRoll  + leftShoulderRollCalibOffset,  0, 180);
        var calibratedLeftWrist = Mathf.Clamp(servoWrist + leftWristOffset, 0, 180);


        if (RobotCommunicationManager.Instance != null)
        {
            if (isLeft)
                RobotCommunicationManager.Instance.UpdateLeftArmServos(
                    calibratedLeftPitch,
                    calibratedLeftRoll,
                    calibratedYaw,
                    calibratedElbow,
                    calibratedLeftWrist,
                    calibratedGrip);
            else
                RobotCommunicationManager.Instance.UpdateArmServos(
                    calibratedPitch,
                    calibratedRoll,
                    calibratedYaw,
                    calibratedElbow,
                    calibratedWrist,
                    calibratedGrip);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("RobotCommunicationManager not found!");
        }

        if (!isLeft || rightController == null)
            UpdateVisualization(shoulderPos, elbowPos, wristPos);
    }

    float ComputeSmoothWrist(SteamVR_Behaviour_Pose controller, float mirrorSign, Vector3 shoulderPos,
        ref float prevAngle, ref float smoothedAngle, ref bool initialized)
    {
        Vector3 controllerOffset = controller.transform.rotation * controllerToWristOffset;
        Vector3 targetWristPos = controller.transform.position + controllerOffset;
        Vector3 rollPos = shoulderPos + new Vector3(0.03f * mirrorSign, 0f, 0f);
        Vector3 yawPos = rollPos + new Vector3(0f, 0f, 0.04f);
        Vector3 toTargetFromYaw = targetWristPos - yawPos;
        Vector3 elbowPos = yawPos + toTargetFromYaw.normalized * 0.03f;

        Vector3 forearmDir = (targetWristPos - elbowPos).normalized;
        Vector3 controllerUp = controller.transform.up;

        Vector3 twistUp = controllerUp - Vector3.Project(controllerUp, forearmDir);
        twistUp.Normalize();

        Vector3 referenceUp = Vector3.up - Vector3.Project(Vector3.up, forearmDir);
        if (referenceUp.magnitude < 0.1f)
            referenceUp = Vector3.forward - Vector3.Project(Vector3.forward, forearmDir);
        referenceUp.Normalize();

        float angle = -Vector3.SignedAngle(referenceUp, twistUp, forearmDir);
        angle *= 0.75f;

        if (!initialized)
        {
            smoothedAngle = angle;
            prevAngle = angle;
            initialized = true;
            return angle;
        }

        float delta = Mathf.DeltaAngle(prevAngle, angle);
        float target = prevAngle + delta;
        smoothedAngle = Mathf.Lerp(smoothedAngle, target, 1f - wristSmoothingFactor);
        prevAngle = smoothedAngle;
        return smoothedAngle;
    }

    void UpdateVisualization(Vector3 shoulderPos, Vector3 elbowPos, Vector3 wristPos)
    {
        if (shoulderTransform != null) shoulderTransform.position = shoulderPos;
        if (elbowTransform != null) elbowTransform.position = elbowPos;
        if (wristTransform != null) wristTransform.position = wristPos;
    }

    float Map(float value, float inMin, float inMax, float outMin, float outMax)
    {
        float t = Mathf.InverseLerp(inMin, inMax, value);
        return Mathf.Lerp(outMin, outMax, t);
    }

    void OnDrawGizmos()
    {
        if (!enableGizmos) return;
        DrawHandGizmos(rightController, isLeft: false);
        DrawHandGizmos(leftController,  isLeft: true);
    }

    void DrawHandGizmos(SteamVR_Behaviour_Pose controller, bool isLeft)
    {
        if (controller == null) return;

        float mirrorSign = isLeft ? -1f : 1f;

        Vector3 controllerOffset = controller.transform.rotation * controllerToWristOffset;
        Vector3 targetWristPos = controller.transform.position + controllerOffset;

        Vector3 shoulderPos = Application.isPlaying
            ? (isLeft ? leftShoulderPos : rightShoulderPos)
            : new Vector3(0.20f * mirrorSign, 1.3f, 0f);

        Vector3 rollPos = shoulderPos + new Vector3(0.03f * mirrorSign, 0f, 0f);
        Vector3 yawPos = rollPos + new Vector3(0f, 0f, 0.04f);
        Vector3 toTargetFromYaw = targetWristPos - yawPos;
        Vector3 elbowPos = yawPos + toTargetFromYaw.normalized * 0.03f;
        Vector3 wristPos = elbowPos + (targetWristPos - elbowPos).normalized * forearmLength;

        Gizmos.color = Color.red;     Gizmos.DrawSphere(shoulderPos, 0.02f);
        Gizmos.color = Color.green;   Gizmos.DrawSphere(rollPos, 0.02f);
        Gizmos.color = Color.blue;    Gizmos.DrawSphere(yawPos, 0.02f);
        Gizmos.color = Color.magenta; Gizmos.DrawSphere(elbowPos, 0.02f);
        Gizmos.color = Color.yellow;  Gizmos.DrawSphere(wristPos, 0.02f);
        Gizmos.color = Color.cyan;    Gizmos.DrawSphere(targetWristPos, 0.02f);
        Gizmos.color = new Color(1, 0.5f, 0, 0.5f);
        Gizmos.DrawWireSphere(controller.transform.position, 0.015f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(shoulderPos, rollPos);
        Gizmos.DrawLine(rollPos, yawPos);
        Gizmos.DrawLine(yawPos, elbowPos);
        Gizmos.DrawLine(elbowPos, wristPos);
        Gizmos.DrawLine(wristPos, targetWristPos);

        Gizmos.color = new Color(1, 0, 0, 0.15f);
        Gizmos.DrawWireSphere(shoulderPos, upperArmLength + forearmLength);

        Transform head = GetHead();
        if (head != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(head.position, 1f);
            Gizmos.DrawRay(head.position, head.forward * 0.3f);
            Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
            Gizmos.DrawLine(head.position, shoulderPos);
        }
    }

    void OnGUI()
    {
        if (!enableDebugLogs || !Application.isPlaying) return;

        Transform head = GetHead();
        if (head != null)
        {
            Vector3 p = head.position;
            GUI.Label(new Rect(10, 90,  400, 20), $"Head Pos:       ({p.x:F2}, {p.y:F2}, {p.z:F2})");
            GUI.Label(new Rect(10, 110, 400, 20), $"Right Shoulder: ({rightShoulderPos.x:F2}, {rightShoulderPos.y:F2}, {rightShoulderPos.z:F2})");
            GUI.Label(new Rect(10, 130, 400, 20), $"Left Shoulder:  ({leftShoulderPos.x:F2}, {leftShoulderPos.y:F2}, {leftShoulderPos.z:F2})");
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
