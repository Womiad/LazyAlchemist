using System.Collections.Generic;
using Invector.vCharacterController;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

/// <summary>
/// Oculus/Meta-compatible first-person locomotion for the Host in TestStage1_VR.
/// Uses the OpenXR controller layout through Unity's device-agnostic XR API.
/// </summary>
public sealed class TestStage1VRHostController : vThirdPersonInput
{
    private const float CameraFallbackHeight = 1.65f;
    private const float SnapTurnAngle = 30f;
    private const float SnapTurnThreshold = 0.75f;
    private const float TeleportAimThreshold = 0.55f;
    private const float TeleportReleaseThreshold = 0.2f;
    private const float TeleportLaunchSpeed = 7.5f;
    private const float TeleportUpwardSpeed = 1.5f;
    private const float TeleportArcStep = 0.08f;
    private const float TeleportMaxDistance = 15f;
    private const float TeleportWallClearance = 0.65f;
    private const int TeleportArcSegments = 32;
    private const int TeleportClearanceDirections = 16;
    private static readonly float[] TeleportClearanceHeights = { 0.25f, 0.9f, 1.55f };

    private Camera vrCamera;
    private Transform rightHandRoot;
    private LineRenderer teleportArc;
    private Transform teleportMarker;
    private Material teleportMaterial;
    private bool canSnapTurn = true;
    private bool isTeleportAiming;
    private bool hasValidTeleportTarget;
    private bool hasCalibratedHeadsetPosition;
    private Vector3 headsetPositionAtCalibration;
    private Vector3 teleportTarget;
    private Vector3 teleportTargetNormal = Vector3.up;
    private readonly Vector3[] teleportArcPositions = new Vector3[TeleportArcSegments + 1];
    private readonly RaycastHit[] teleportHits = new RaycastHit[16];

    public static Vector2 MoveAxis { get; private set; }

    protected override void InitializeTpCamera()
    {
        tpCamera = FindFirstObjectByType<vThirdPersonCamera>();
        if (tpCamera == null)
        {
            Debug.LogError("TestStage1 VR requires the scene's vThirdPersonCamera.");
            return;
        }

        vrCamera = tpCamera.GetComponent<Camera>();
        tpCamera.enabled = false;

        vrCamera.transform.SetParent(transform, false);
        vrCamera.transform.localPosition = Vector3.up * CameraFallbackHeight;
        vrCamera.transform.localRotation = Quaternion.identity;
        vrCamera.nearClipPlane = 0.03f;
        vrCamera.stereoTargetEye = StereoTargetEyeMask.Both;

        cameraMain = vrCamera;
        cc.rotateTarget = vrCamera.transform;

        SetFloorTrackingOrigin();
        HideLocalPlayerBody();
        CreateRightControllerTrackingAnchor();
        CreateTeleportVisuals();
    }

    protected override void InputHandle()
    {
        MoveInput();
        CameraInput();
        TeleportInput();
    }

    public override void MoveInput()
    {
        MoveAxis = Vector2.zero;
        cc.input.x = 0f;
        cc.input.z = 0f;
    }

    protected override void CameraInput()
    {
        if (vrCamera == null)
        {
            return;
        }

        cc.UpdateMoveDirection(vrCamera.transform);

        var rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out var turnAxis))
        {
            turnAxis = Vector2.zero;
        }

        if (Mathf.Abs(turnAxis.x) < SnapTurnThreshold * 0.5f)
        {
            canSnapTurn = true;
        }
        else if (canSnapTurn && turnAxis.y < TeleportAimThreshold && Mathf.Abs(turnAxis.x) >= SnapTurnThreshold)
        {
            SnapTurn(Mathf.Sign(turnAxis.x) * SnapTurnAngle);
            canSnapTurn = false;
        }
    }

    protected override void SprintInput()
    {
        cc.Sprint(false);
    }

    protected override void JumpInput()
    {
    }

    private void LateUpdate()
    {
        if (vrCamera == null)
        {
            return;
        }

        var headset = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        var hasPosition = headset.TryGetFeatureValue(CommonUsages.devicePosition, out var localPosition);
        var hasRotation = headset.TryGetFeatureValue(CommonUsages.deviceRotation, out var localRotation);

        if (hasPosition)
        {
            if (!hasCalibratedHeadsetPosition)
            {
                headsetPositionAtCalibration = localPosition;
                hasCalibratedHeadsetPosition = true;
            }

            var roomScaleOffset = localPosition - headsetPositionAtCalibration;
            vrCamera.transform.localPosition = new Vector3(
                roomScaleOffset.x,
                CameraFallbackHeight + roomScaleOffset.y,
                roomScaleOffset.z);
        }
        else
        {
            vrCamera.transform.localPosition = Vector3.up * CameraFallbackHeight;
        }

        if (hasRotation)
        {
            vrCamera.transform.localRotation = localRotation;
        }

        UpdateTrackedHand(XRNode.RightHand, rightHandRoot);

        if (isTeleportAiming)
        {
            UpdateTeleportArc();
        }
    }

    private void TeleportInput()
    {
        var rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (!rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis))
        {
            CancelTeleportAim();
            return;
        }

        if (!isTeleportAiming && axis.y >= TeleportAimThreshold)
        {
            isTeleportAiming = true;
            SetTeleportVisualsActive(true);
        }
        else if (isTeleportAiming && axis.y <= TeleportReleaseThreshold)
        {
            if (hasValidTeleportTarget)
            {
                TeleportToTarget();
            }

            CancelTeleportAim();
        }
    }

    private void UpdateTeleportArc()
    {
        if (rightHandRoot == null || !rightHandRoot.gameObject.activeSelf)
        {
            hasValidTeleportTarget = false;
            SetTeleportVisualsActive(false);
            return;
        }

        teleportArc.gameObject.SetActive(true);

        Vector3 start = rightHandRoot.position + rightHandRoot.forward * 0.06f;
        Vector3 velocity = rightHandRoot.forward * TeleportLaunchSpeed + Vector3.up * TeleportUpwardSpeed;
        teleportArcPositions[0] = start;
        hasValidTeleportTarget = false;
        int positionCount = 1;
        float travelledDistance = 0f;

        for (int index = 1; index <= TeleportArcSegments; index++)
        {
            float time = index * TeleportArcStep;
            Vector3 next = start + velocity * time + Physics.gravity * (0.5f * time * time);
            Vector3 segment = next - teleportArcPositions[positionCount - 1];
            float segmentDistance = segment.magnitude;

            if (segmentDistance > 0.001f && TryGetTeleportHit(
                    teleportArcPositions[positionCount - 1],
                    segment / segmentDistance,
                    segmentDistance,
                    out RaycastHit hit))
            {
                teleportArcPositions[positionCount++] = hit.point;
                teleportTarget = hit.point;
                teleportTargetNormal = hit.normal;
                hasValidTeleportTarget = Vector3.Dot(hit.normal, Vector3.up) >= 0.65f
                    && HasEnoughWallClearance(GetTeleportDestination(hit.point));
                break;
            }

            travelledDistance += segmentDistance;
            teleportArcPositions[positionCount++] = next;
            if (travelledDistance >= TeleportMaxDistance)
            {
                break;
            }
        }

        teleportArc.positionCount = positionCount;
        for (int index = 0; index < positionCount; index++)
        {
            teleportArc.SetPosition(index, teleportArcPositions[index]);
        }
        Color arcColor = hasValidTeleportTarget
            ? new Color(0.1f, 0.9f, 1f, 0.9f)
            : new Color(1f, 0.2f, 0.15f, 0.85f);
        teleportArc.startColor = arcColor;
        teleportArc.endColor = new Color(arcColor.r, arcColor.g, arcColor.b, 0.25f);
        teleportMaterial.color = arcColor;

        teleportMarker.gameObject.SetActive(hasValidTeleportTarget);
        if (hasValidTeleportTarget)
        {
            teleportMarker.position = teleportTarget + teleportTargetNormal * 0.015f;
            teleportMarker.rotation = Quaternion.FromToRotation(Vector3.up, teleportTargetNormal);
        }
    }

    private bool TryGetTeleportHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearestHit)
    {
        int count = Physics.RaycastNonAlloc(
            origin,
            direction,
            teleportHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        float nearestDistance = float.PositiveInfinity;
        nearestHit = default;

        for (int index = 0; index < count; index++)
        {
            RaycastHit candidate = teleportHits[index];
            if (candidate.collider == null || candidate.collider.transform.IsChildOf(transform) ||
                candidate.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = candidate.distance;
            nearestHit = candidate;
        }

        return nearestDistance < float.PositiveInfinity;
    }

    private void TeleportToTarget()
    {
        Vector3 destination = GetTeleportDestination(teleportTarget);

        var body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = destination;
        }
        else
        {
            transform.position = destination;
        }

        Physics.SyncTransforms();
    }

    private Vector3 GetTeleportDestination(Vector3 groundPoint)
    {
        Vector3 headOffset = vrCamera.transform.position - transform.position;
        Vector3 destination = groundPoint - new Vector3(headOffset.x, 0f, headOffset.z);
        destination.y += 0.05f;
        return destination;
    }

    private bool HasEnoughWallClearance(Vector3 destination)
    {
        // Check several body heights so low props, walls and overhang edges are all rejected.
        foreach (float height in TeleportClearanceHeights)
        {
            Vector3 origin = destination + Vector3.up * height;
            for (int directionIndex = 0; directionIndex < TeleportClearanceDirections; directionIndex++)
            {
                float angle = directionIndex * (360f / TeleportClearanceDirections);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                int hitCount = Physics.RaycastNonAlloc(
                    origin,
                    direction,
                    teleportHits,
                    TeleportWallClearance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);

                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit hit = teleportHits[hitIndex];
                    if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                    {
                        continue;
                    }

                    // Up-facing geometry is floor or a walkable slope; side-facing geometry is a wall.
                    if (Vector3.Dot(hit.normal, Vector3.up) < 0.65f)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private void CancelTeleportAim()
    {
        isTeleportAiming = false;
        hasValidTeleportTarget = false;
        SetTeleportVisualsActive(false);
    }

    private void SetTeleportVisualsActive(bool active)
    {
        if (teleportArc != null)
        {
            teleportArc.gameObject.SetActive(active);
        }

        if (teleportMarker != null)
        {
            teleportMarker.gameObject.SetActive(false);
        }
    }

    private void CreateRightControllerTrackingAnchor()
    {
        rightHandRoot = new GameObject("VRRightControllerTrackingAnchor").transform;
        rightHandRoot.SetParent(transform, false);
        rightHandRoot.gameObject.SetActive(false);
    }

    private void UpdateTrackedHand(XRNode node, Transform handRoot)
    {
        if (handRoot == null)
        {
            return;
        }

        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        bool isTracked = device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
        bool hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 localPosition);
        bool hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion localRotation);
        bool visible = isTracked && hasPosition && hasRotation && hasCalibratedHeadsetPosition;
        handRoot.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        Vector3 trackingOffset = localPosition - headsetPositionAtCalibration;
        handRoot.localPosition = trackingOffset + Vector3.up * CameraFallbackHeight;
        handRoot.localRotation = localRotation;
    }

    private void CreateTeleportVisuals()
    {
        Shader teleportShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        teleportMaterial = new Material(teleportShader)
        {
            color = new Color(0.1f, 0.9f, 1f, 0.9f)
        };

        var arcObject = new GameObject("VRTeleportArc");
        teleportArc = arcObject.AddComponent<LineRenderer>();
        teleportArc.useWorldSpace = true;
        teleportArc.widthMultiplier = 0.025f;
        teleportArc.numCapVertices = 4;
        teleportArc.numCornerVertices = 3;
        teleportArc.sharedMaterial = teleportMaterial;

        GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        markerObject.name = "VRTeleportMarker";
        markerObject.transform.localScale = new Vector3(0.28f, 0.006f, 0.28f);
        markerObject.GetComponent<Renderer>().sharedMaterial = teleportMaterial;
        Destroy(markerObject.GetComponent<Collider>());
        teleportMarker = markerObject.transform;

        SetTeleportVisualsActive(false);
    }

    private void OnDestroy()
    {
        if (teleportMaterial != null)
        {
            Destroy(teleportMaterial);
        }
    }

    private void SnapTurn(float angle)
    {
        var headPosition = vrCamera.transform.position;
        transform.RotateAround(headPosition, Vector3.up, angle);
    }

    private static void SetFloorTrackingOrigin()
    {
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var subsystem in subsystems)
        {
            if (!subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
            {
                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
                subsystem.TryRecenter();
            }
        }
    }

    private void HideLocalPlayerBody()
    {
        foreach (var bodyRenderer in GetComponentsInChildren<Renderer>(true))
        {
            bodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }
}

public static class TestStage1VRInput
{
    private static int sampledFrame = -1;
    private static bool collectWasPressed;
    private static bool attackWasPressed;
    private static bool collectPressedThisFrame;
    private static bool attackPressedThisFrame;

    public static bool CollectPressedThisFrame
    {
        get
        {
            SampleButtons();
            return collectPressedThisFrame;
        }
    }

    public static bool AttackPressedThisFrame
    {
        get
        {
            SampleButtons();
            return attackPressedThisFrame;
        }
    }

    private static void SampleButtons()
    {
        if (sampledFrame == Time.frameCount)
        {
            return;
        }

        sampledFrame = Time.frameCount;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "TestStage1_VR")
        {
            collectPressedThisFrame = false;
            attackPressedThisFrame = false;
            collectWasPressed = false;
            attackWasPressed = false;
            return;
        }

        var controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        var collectPressed = controller.TryGetFeatureValue(CommonUsages.primaryButton, out var primary)
            && primary;
        var attackPressed = controller.TryGetFeatureValue(CommonUsages.triggerButton, out var triggerButton)
            && triggerButton;

        collectPressedThisFrame = collectPressed && !collectWasPressed;
        attackPressedThisFrame = attackPressed && !attackWasPressed;
        collectWasPressed = collectPressed;
        attackWasPressed = attackPressed;
    }
}
