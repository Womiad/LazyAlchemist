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
    private const float DefaultEyeOffsetFromHeadBone = 0.1f;
    private const float MoveDeadZone = 0.15f;
    private const float MinimumValidFloorTrackedHeadHeight = 0.35f;
    private const int RequiredTrackingCalibrationSamples = 5;
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
    private Animator avatarAnimator;
    private Transform leftHandRoot;
    private Transform rightHandRoot;
    private Transform avatarHead;
    private readonly Dictionary<Transform, Quaternion> fingerRestRotations = new Dictionary<Transform, Quaternion>();
    private LineRenderer teleportArc;
    private Transform teleportMarker;
    private Material teleportMaterial;
    private bool isTeleportAiming;
    private bool hasValidTeleportTarget;
    private bool hasCalibratedHeadsetPosition;
    private int validTrackingCalibrationSamples;
    private Vector3 pendingHeadsetCalibrationPosition;
    private Vector3 headsetPositionAtCalibration;
    private Vector3 teleportTarget;
    private Vector3 teleportTargetNormal = Vector3.up;
    private Vector3 calibratedAvatarEyeLocalPosition = Vector3.up * CameraFallbackHeight;
    private readonly Vector3[] teleportArcPositions = new Vector3[TeleportArcSegments + 1];
    private readonly RaycastHit[] teleportHits = new RaycastHit[16];
    private UnityEngine.InputSystem.InputAction moveAction;
    private UnityEngine.InputSystem.InputAction turnAction;
    private UnityEngine.InputSystem.InputAction leftPositionAction;
    private UnityEngine.InputSystem.InputAction leftRotationAction;
    private UnityEngine.InputSystem.InputAction leftTrackedAction;
    private UnityEngine.InputSystem.InputAction rightPositionAction;
    private UnityEngine.InputSystem.InputAction rightRotationAction;
    private UnityEngine.InputSystem.InputAction rightTrackedAction;
    private UnityEngine.InputSystem.InputAction leftTriggerAction;
    private UnityEngine.InputSystem.InputAction leftGripAction;
    private UnityEngine.InputSystem.InputAction leftThumbTouchAction;
    private UnityEngine.InputSystem.InputAction rightTriggerAction;
    private UnityEngine.InputSystem.InputAction rightGripAction;
    private UnityEngine.InputSystem.InputAction rightThumbTouchAction;
    private bool leftGripWasPressed;
    private bool rightGripWasPressed;
    private bool pendingLeftGrab;
    private bool pendingRightGrab;
    private float leftGrabExpiresAt;
    private float rightGrabExpiresAt;

    public static Vector2 MoveAxis { get; private set; }

    private void OnEnable()
    {
        moveAction ??= new UnityEngine.InputSystem.InputAction(
            "VR Move", UnityEngine.InputSystem.InputActionType.Value, "<XRController>{LeftHand}/thumbstick");
        turnAction ??= new UnityEngine.InputSystem.InputAction(
            "VR Turn", UnityEngine.InputSystem.InputActionType.Value, "<XRController>{RightHand}/thumbstick");
        leftPositionAction ??= CreatePoseAction<Vector3>("Left Controller Position", "<XRController>{LeftHand}/devicePosition");
        leftRotationAction ??= CreatePoseAction<Quaternion>("Left Controller Rotation", "<XRController>{LeftHand}/deviceRotation");
        leftTrackedAction ??= CreatePoseAction<float>("Left Controller Tracked", "<XRController>{LeftHand}/isTracked");
        rightPositionAction ??= CreatePoseAction<Vector3>("Right Controller Position", "<XRController>{RightHand}/devicePosition");
        rightRotationAction ??= CreatePoseAction<Quaternion>("Right Controller Rotation", "<XRController>{RightHand}/deviceRotation");
        rightTrackedAction ??= CreatePoseAction<float>("Right Controller Tracked", "<XRController>{RightHand}/isTracked");
        leftTriggerAction ??= CreateValueAction("Left Trigger", "<XRController>{LeftHand}/trigger");
        leftGripAction ??= CreateValueAction("Left Grip", "<XRController>{LeftHand}/grip");
        leftThumbTouchAction ??= CreateThumbTouchAction("Left Thumb Touch", "LeftHand");
        rightTriggerAction ??= CreateValueAction("Right Trigger", "<XRController>{RightHand}/trigger");
        rightGripAction ??= CreateValueAction("Right Grip", "<XRController>{RightHand}/grip");
        rightThumbTouchAction ??= CreateThumbTouchAction("Right Thumb Touch", "RightHand");
        SetActionsEnabled(true);
    }

    private void OnDisable()
    {
        SetActionsEnabled(false);
    }

    private static UnityEngine.InputSystem.InputAction CreatePoseAction<TValue>(string name, string binding)
        where TValue : struct
    {
        return new UnityEngine.InputSystem.InputAction(
            name, UnityEngine.InputSystem.InputActionType.PassThrough, binding,
            expectedControlType: typeof(TValue).Name);
    }

    private static UnityEngine.InputSystem.InputAction CreateValueAction(string name, string binding)
    {
        return new UnityEngine.InputSystem.InputAction(
            name, UnityEngine.InputSystem.InputActionType.Value, binding);
    }

    private static UnityEngine.InputSystem.InputAction CreateThumbTouchAction(string name, string handUsage)
    {
        var action = new UnityEngine.InputSystem.InputAction(name, UnityEngine.InputSystem.InputActionType.Value);
        UnityEngine.InputSystem.InputActionSetupExtensions.AddBinding(
            action, $"<OculusTouchController>{{{handUsage}}}/primaryTouched");
        UnityEngine.InputSystem.InputActionSetupExtensions.AddBinding(
            action, $"<OculusTouchController>{{{handUsage}}}/secondaryTouched");
        UnityEngine.InputSystem.InputActionSetupExtensions.AddBinding(
            action, $"<OculusTouchController>{{{handUsage}}}/thumbstickTouched");
        return action;
    }

    private void SetActionsEnabled(bool enabled)
    {
        UnityEngine.InputSystem.InputAction[] actions =
        {
            moveAction, turnAction, leftPositionAction, leftRotationAction, leftTrackedAction,
            rightPositionAction, rightRotationAction, rightTrackedAction,
            leftTriggerAction, leftGripAction, leftThumbTouchAction,
            rightTriggerAction, rightGripAction, rightThumbTouchAction
        };
        foreach (var action in actions)
            if (enabled) action?.Enable(); else action?.Disable();
    }

    [Header("VR Avatar")]
    [SerializeField, Range(0f, 1f)] private float handIkWeight = 1f;
    [SerializeField] private Vector3 leftHandRotationOffset = new Vector3(0f, 0f, 90f);
    [SerializeField] private Vector3 rightHandRotationOffset = new Vector3(0f, 0f, -90f);
    [SerializeField, Min(1f)] private float movementSpeedMultiplier = 1.6f;
    [SerializeField, Range(1.2f, 2f)] private float playerEyeHeight = 1.8f;
    [SerializeField] private float eyeOffsetFromHeadBone = DefaultEyeOffsetFromHeadBone;
    [SerializeField, Range(0f, 0.5f)] private float firstPersonCameraForwardOffset = 0.22f;
    [SerializeField, Range(0f, 90f)] private float fingerCurlAngle = 65f;
    [SerializeField, Range(0f, 60f)] private float thumbCurlAngle = 35f;
    [SerializeField, Range(0f, 60f)] private float thumbOppositionAngle = 28f;
    [SerializeField, Min(0.1f)] private float bodyFacingSmoothSpeed = 5f;

    protected override void InitializeTpCamera()
    {
        tpCamera = FindFirstObjectByType<vThirdPersonCamera>();
        if (tpCamera == null)
        {
            Debug.LogError("TestStage1 VR requires the scene's vThirdPersonCamera.");
            return;
        }

        avatarAnimator = GetComponent<Animator>();
        // In first-person VR the avatar can be outside its own camera frustum. Never
        // let Animator culling freeze locomotion or controller-driven hand IK.
        if (avatarAnimator != null) avatarAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        CalibrateAvatarEyeHeight();
        CacheFingerRestPose();
        IncreaseVrMovementSpeed();

        vrCamera = tpCamera.GetComponent<Camera>();
        tpCamera.enabled = false;

        vrCamera.transform.SetParent(transform, false);
        vrCamera.transform.localPosition = calibratedAvatarEyeLocalPosition;
        vrCamera.transform.localRotation = Quaternion.identity;
        vrCamera.nearClipPlane = 0.03f;

        cameraMain = vrCamera;
        cc.rotateTarget = vrCamera.transform;
        SetFloorTrackingOrigin();
        PrepareFirstPersonAvatar();
        CreateControllerTrackingAnchors();
        CreateTeleportVisuals();
    }

    protected override void InputHandle()
    {
        MoveInput();
        CameraInput();
        TeleportInput();
    }

    protected override void Update()
    {
        base.Update();
        SampleGrabGestures();
    }

    private void SampleGrabGestures()
    {
        bool leftPressed = ReadAnalogControl(leftTriggerAction, XRNode.LeftHand, CommonUsages.trigger) >= 0.65f
            || ReadAnalogControl(leftGripAction, XRNode.LeftHand, CommonUsages.grip) >= 0.65f;
        bool rightPressed = ReadAnalogControl(rightTriggerAction, XRNode.RightHand, CommonUsages.trigger) >= 0.65f
            || ReadAnalogControl(rightGripAction, XRNode.RightHand, CommonUsages.grip) >= 0.65f;
        if (leftPressed && !leftGripWasPressed) { pendingLeftGrab = true; leftGrabExpiresAt = Time.time + .3f; }
        if (rightPressed && !rightGripWasPressed) { pendingRightGrab = true; rightGrabExpiresAt = Time.time + .3f; }
        leftGripWasPressed = leftPressed;
        rightGripWasPressed = rightPressed;
    }

    private static float ReadAnalogControl(
        UnityEngine.InputSystem.InputAction action,
        XRNode node,
        InputFeatureUsage<float> fallbackUsage)
    {
        if (action != null && action.controls.Count > 0) return action.ReadValue<float>();
        var device = InputDevices.GetDeviceAtXRNode(node);
        return device.TryGetFeatureValue(fallbackUsage, out float value) ? value : 0f;
    }

    public static bool TryConsumeGrabNear(Vector3 target, float radius)
    {
        var controller = FindFirstObjectByType<TestStage1VRHostController>();
        if (controller == null) return false;
        if (Time.time > controller.leftGrabExpiresAt) controller.pendingLeftGrab = false;
        if (Time.time > controller.rightGrabExpiresAt) controller.pendingRightGrab = false;
        float radiusSquared = radius * radius;
        if (controller.pendingLeftGrab && controller.leftHandRoot != null
            && controller.leftHandRoot.gameObject.activeSelf
            && (controller.leftHandRoot.position - target).sqrMagnitude <= radiusSquared)
        {
            controller.pendingLeftGrab = false;
            return true;
        }
        if (controller.pendingRightGrab && controller.rightHandRoot != null
            && controller.rightHandRoot.gameObject.activeSelf
            && (controller.rightHandRoot.position - target).sqrMagnitude <= radiusSquared)
        {
            controller.pendingRightGrab = false;
            return true;
        }
        return false;
    }

    protected override void FixedUpdate()
    {
        // VR locomotion is camera-relative, but the left stick must never rotate the avatar.
        cc.UpdateMotor();
        cc.ControlLocomotionType();
        // Invector updates its smoothed movement input inside ControlRotationType.
        // Run that calculation, but restore the original yaw so the left stick only
        // translates the VR player and never turns their view/body.
        Quaternion rotationBeforeMovementUpdate = transform.rotation;
        cc.ControlRotationType();
        transform.rotation = rotationBeforeMovementUpdate;
    }

    public override void MoveInput()
    {
        TryReadControllerAxis(XRNode.LeftHand, out var moveAxis);

        if (moveAxis.magnitude < MoveDeadZone)
        {
            moveAxis = Vector2.zero;
        }

        MoveAxis = Vector2.ClampMagnitude(moveAxis, 1f);
        cc.input.x = MoveAxis.x;
        cc.input.z = MoveAxis.y;
    }

    protected override void CameraInput()
    {
        if (vrCamera == null)
        {
            return;
        }

        cc.UpdateMoveDirection(vrCamera.transform);
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
            if (!hasCalibratedHeadsetPosition && localPosition.y >= MinimumValidFloorTrackedHeadHeight)
            {
                if (validTrackingCalibrationSamples == 0
                    || Mathf.Abs(localPosition.y - pendingHeadsetCalibrationPosition.y) > 0.15f)
                {
                    pendingHeadsetCalibrationPosition = localPosition;
                    validTrackingCalibrationSamples = 1;
                }
                else
                {
                    pendingHeadsetCalibrationPosition = Vector3.Lerp(
                        pendingHeadsetCalibrationPosition,
                        localPosition,
                        0.5f);
                    validTrackingCalibrationSamples++;
                }

                if (validTrackingCalibrationSamples >= RequiredTrackingCalibrationSamples)
                {
                    headsetPositionAtCalibration = pendingHeadsetCalibrationPosition;
                    hasCalibratedHeadsetPosition = true;
                }
            }

            // OpenXR can report (0,0,0) for several startup frames before the floor
            // tracking origin is ready. Never add that transition to the avatar eye height.
            var roomScaleOffset = hasCalibratedHeadsetPosition
                ? localPosition - headsetPositionAtCalibration
                : Vector3.zero;
            vrCamera.transform.localPosition = calibratedAvatarEyeLocalPosition + roomScaleOffset;
        }
        else
        {
            vrCamera.transform.localPosition = calibratedAvatarEyeLocalPosition;
        }

        if (hasRotation)
        {
            vrCamera.transform.localRotation = localRotation;
        }

        UpdateTrackedHand(XRNode.LeftHand, leftHandRoot);
        UpdateTrackedHand(XRNode.RightHand, rightHandRoot);
        if (isTeleportAiming)
        {
            UpdateTeleportArc();
        }
    }

    private void TeleportInput()
    {
        if (!TryReadControllerAxis(XRNode.RightHand, out var axis))
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

    private bool TryReadControllerAxis(XRNode node, out Vector2 axis)
    {
        var action = node == XRNode.LeftHand ? moveAction : turnAction;
        if (action != null && action.enabled)
        {
            axis = action.ReadValue<Vector2>();
            return true;
        }

        var xrDevice = InputDevices.GetDeviceAtXRNode(node);
        if (xrDevice.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis))
        {
            return true;
        }

        // Meta OpenXR controllers are exposed primarily through the new Input System.
        // Keep the legacy XR path above for other runtimes, and fall back to the
        // OpenXR "thumbstick" control so Quest/Touch controllers always drive movement.
        var inputDevice = FindInputSystemController(node);
        if (inputDevice != null)
        {
            var stick = inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("thumbstick")
                ?? inputDevice.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector2Control>("primary2DAxis");
            if (stick != null)
            {
                axis = stick.ReadValue();
                return true;
            }
        }

        axis = Vector2.zero;
        return false;
    }

    private static UnityEngine.InputSystem.InputDevice FindInputSystemController(XRNode node)
    {
        var wantedUsage = node == XRNode.LeftHand
            ? UnityEngine.InputSystem.CommonUsages.LeftHand
            : UnityEngine.InputSystem.CommonUsages.RightHand;
        foreach (var inputDevice in UnityEngine.InputSystem.InputSystem.devices)
            foreach (var usage in inputDevice.usages)
                if (usage == wantedUsage) return inputDevice;
        return null;
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

    private void CreateControllerTrackingAnchors()
    {
        leftHandRoot = new GameObject("VRLeftControllerTrackingAnchor").transform;
        leftHandRoot.SetParent(transform, false);
        leftHandRoot.gameObject.SetActive(false);

        rightHandRoot = new GameObject("VRRightControllerTrackingAnchor").transform;
        rightHandRoot.SetParent(transform, false);
        rightHandRoot.gameObject.SetActive(false);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (avatarAnimator == null)
        {
            return;
        }

        AlignAvatarBodyToHands();
        ApplyHandIk(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, leftHandRoot, leftHandRotationOffset, -1f);
        ApplyHandIk(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, rightHandRoot, rightHandRotationOffset, 1f);
        UpdateFingerPose(XRNode.LeftHand, true);
        UpdateFingerPose(XRNode.RightHand, false);
    }

    private void AlignAvatarBodyToHands()
    {
        if (vrCamera == null) return;
        Vector3 direction = Vector3.ProjectOnPlane(vrCamera.transform.forward, Vector3.up);
        if (direction.sqrMagnitude < .001f) return;
        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        float blend = 1f - Mathf.Exp(-bodyFacingSmoothSpeed * Time.deltaTime);
        avatarAnimator.bodyRotation = Quaternion.Slerp(avatarAnimator.bodyRotation, target, blend);
    }

    private void ApplyHandIk(
        AvatarIKGoal goal,
        AvatarIKHint hint,
        Transform trackingTarget,
        Vector3 rotationOffset,
        float side)
    {
        bool tracked = trackingTarget != null && trackingTarget.gameObject.activeSelf;
        float weight = tracked ? handIkWeight : 0f;
        avatarAnimator.SetIKPositionWeight(goal, weight);
        avatarAnimator.SetIKRotationWeight(goal, weight);
        avatarAnimator.SetIKHintPositionWeight(hint, weight);

        if (!tracked)
        {
            return;
        }

        avatarAnimator.SetIKPosition(goal, trackingTarget.position);
        avatarAnimator.SetIKRotation(goal, trackingTarget.rotation * Quaternion.Euler(rotationOffset));

        // Keep the elbows outside the torso so the humanoid solver bends naturally.
        Vector3 elbowHint = trackingTarget.position
            + transform.right * (0.35f * side)
            + transform.up * -0.18f
            + transform.forward * -0.08f;
        avatarAnimator.SetIKHintPosition(hint, elbowHint);
    }

    private void UpdateTrackedHand(XRNode node, Transform handRoot)
    {
        if (handRoot == null)
        {
            return;
        }

        var positionAction = node == XRNode.LeftHand ? leftPositionAction : rightPositionAction;
        var rotationAction = node == XRNode.LeftHand ? leftRotationAction : rightRotationAction;
        var trackedAction = node == XRNode.LeftHand ? leftTrackedAction : rightTrackedAction;
        bool hasActionPose = positionAction != null && rotationAction != null
            && positionAction.controls.Count > 0 && rotationAction.controls.Count > 0;
        Vector3 localPosition = hasActionPose ? positionAction.ReadValue<Vector3>() : default;
        Quaternion localRotation = hasActionPose ? rotationAction.ReadValue<Quaternion>() : Quaternion.identity;
        // Oculus PC can expose a valid pose while its isTracked control stays at zero.
        bool isTracked = hasActionPose;
        bool hasPosition = hasActionPose;
        bool hasRotation = hasActionPose;
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!hasActionPose)
        {
            isTracked = device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked;
            hasPosition = device.TryGetFeatureValue(CommonUsages.devicePosition, out localPosition);
            hasRotation = device.TryGetFeatureValue(CommonUsages.deviceRotation, out localRotation);
        }
        if (!isTracked || !hasPosition || !hasRotation)
        {
            var inputDevice = FindInputSystemController(node);
            var trackedControl = inputDevice?.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("isTracked");
            var positionControl = inputDevice?.TryGetChildControl<UnityEngine.InputSystem.Controls.Vector3Control>("devicePosition");
            var rotationControl = inputDevice?.TryGetChildControl<UnityEngine.InputSystem.Controls.QuaternionControl>("deviceRotation");
            if (positionControl != null && rotationControl != null)
            {
                isTracked = trackedControl == null || trackedControl.isPressed;
                localPosition = positionControl.ReadValue();
                localRotation = rotationControl.ReadValue();
                hasPosition = hasRotation = true;
            }
        }
        bool visible = isTracked && hasPosition && hasRotation && hasCalibratedHeadsetPosition;
        handRoot.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        Vector3 trackingOffset = localPosition - headsetPositionAtCalibration;
        handRoot.localPosition = trackingOffset + calibratedAvatarEyeLocalPosition;
        handRoot.localRotation = localRotation;
    }

    private void CalibrateAvatarEyeHeight()
    {
        if (avatarAnimator == null || !avatarAnimator.isHuman)
        {
            calibratedAvatarEyeLocalPosition = Vector3.up * CameraFallbackHeight;
            return;
        }

        avatarHead = avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
        if (avatarHead == null)
        {
            calibratedAvatarEyeLocalPosition = Vector3.up * CameraFallbackHeight;
            return;
        }

        Vector3 eyePosition = avatarHead.position + avatarHead.up * eyeOffsetFromHeadBone;
        calibratedAvatarEyeLocalPosition = transform.InverseTransformPoint(eyePosition);
        // The imported avatar is nearly 1.92 m tall and its head bone can exceed two
        // metres in some animation poses. Camera height belongs to the VR player, not
        // to that animated bone, so only use the bone for its fore/aft eye placement.
        calibratedAvatarEyeLocalPosition.y = playerEyeHeight;
        calibratedAvatarEyeLocalPosition.z += firstPersonCameraForwardOffset;
        eyePosition = transform.TransformPoint(calibratedAvatarEyeLocalPosition);
    }

    private void IncreaseVrMovementSpeed()
    {
        if (cc == null)
        {
            return;
        }

        MultiplyMovementSpeed(cc.freeSpeed, movementSpeedMultiplier);
        MultiplyMovementSpeed(cc.strafeSpeed, movementSpeedMultiplier);
    }

    private static void MultiplyMovementSpeed(vThirdPersonMotor.vMovementSpeed speed, float multiplier)
    {
        speed.walkSpeed *= multiplier;
        speed.runningSpeed *= multiplier;
        speed.sprintSpeed *= multiplier;
    }

    private void CacheFingerRestPose()
    {
        if (avatarAnimator == null || !avatarAnimator.isHuman)
        {
            return;
        }

        CacheFingerRestPose(LeftThumbBones);
        CacheFingerRestPose(RightThumbBones);
        CacheFingerRestPose(LeftIndexBones);
        CacheFingerRestPose(RightIndexBones);
        CacheFingerRestPose(LeftGripBones);
        CacheFingerRestPose(RightGripBones);
    }

    private void CacheFingerRestPose(HumanBodyBones[] bones)
    {
        foreach (HumanBodyBones bone in bones)
        {
            Transform finger = avatarAnimator.GetBoneTransform(bone);
            if (finger != null)
            {
                fingerRestRotations[finger] = finger.localRotation;
            }
        }
    }

    private void UpdateFingerPose(XRNode node, bool isLeft)
    {
        if (avatarAnimator == null || !avatarAnimator.isHuman)
        {
            return;
        }

        var triggerAction = isLeft ? leftTriggerAction : rightTriggerAction;
        var gripAction = isLeft ? leftGripAction : rightGripAction;
        var thumbAction = isLeft ? leftThumbTouchAction : rightThumbTouchAction;
        bool hasActionInput = triggerAction != null && triggerAction.controls.Count > 0;
        float index = hasActionInput ? triggerAction.ReadValue<float>() : 0f;
        float middle = gripAction != null && gripAction.controls.Count > 0 ? gripAction.ReadValue<float>() : 0f;
        float thumb = thumbAction != null && thumbAction.controls.Count > 0 && thumbAction.ReadValue<float>() > 0.01f
            ? 0.65f : 0f;

        if (!hasActionInput)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            bool primaryTouched = device.TryGetFeatureValue(CommonUsages.primaryTouch, out bool primaryTouch) && primaryTouch;
            bool secondaryTouched = device.TryGetFeatureValue(CommonUsages.secondaryTouch, out bool secondaryTouch) && secondaryTouch;
            bool stickTouched = device.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out bool axisTouch) && axisTouch;
            thumb = primaryTouched || secondaryTouched || stickTouched ? 0.65f : 0f;
            device.TryGetFeatureValue(CommonUsages.trigger, out index);
            device.TryGetFeatureValue(CommonUsages.grip, out middle);
        }
        // This avatar's mirrored Mixamo finger bones use the same positive local-X curl
        // direction on both hands. Negating the left side bends it through the hand back.
        const float direction = 1f;

        ApplyThumbCurl(isLeft ? LeftThumbBones : RightThumbBones, thumb, isLeft);
        ApplyFingerCurl(isLeft ? LeftIndexBones : RightIndexBones, index, direction);
        ApplyFingerCurl(isLeft ? LeftGripBones : RightGripBones, middle, direction);
    }

    private void ApplyThumbCurl(HumanBodyBones[] bones, float amount, bool isLeft)
    {
        float curlAmount = Mathf.Clamp01(amount);
        float oppositionDirection = isLeft ? -1f : 1f;

        for (int index = 0; index < bones.Length; index++)
        {
            Transform finger = avatarAnimator.GetBoneTransform(bones[index]);
            if (finger == null || !fingerRestRotations.TryGetValue(finger, out Quaternion restRotation))
            {
                continue;
            }

            Quaternion curl = Quaternion.AngleAxis(thumbCurlAngle * curlAmount, Vector3.right);
            if (index == 0)
            {
                Quaternion opposition = Quaternion.AngleAxis(
                    thumbOppositionAngle * curlAmount * oppositionDirection,
                    Vector3.forward);
                curl = opposition * curl;
            }

            avatarAnimator.SetBoneLocalRotation(bones[index], restRotation * curl);
        }
    }

    private void ApplyFingerCurl(HumanBodyBones[] bones, float amount, float direction)
    {
        Quaternion curl = Quaternion.AngleAxis(fingerCurlAngle * Mathf.Clamp01(amount) * direction, Vector3.right);
        foreach (HumanBodyBones bone in bones)
        {
            Transform finger = avatarAnimator.GetBoneTransform(bone);
            if (finger != null && fingerRestRotations.TryGetValue(finger, out Quaternion restRotation))
            {
                avatarAnimator.SetBoneLocalRotation(bone, restRotation * curl);
            }
        }
    }

    private static readonly HumanBodyBones[] LeftThumbBones =
    {
        HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal
    };
    private static readonly HumanBodyBones[] RightThumbBones =
    {
        HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal
    };
    private static readonly HumanBodyBones[] LeftIndexBones =
    {
        HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal
    };
    private static readonly HumanBodyBones[] RightIndexBones =
    {
        HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal
    };
    private static readonly HumanBodyBones[] LeftGripBones =
    {
        HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal
    };
    private static readonly HumanBodyBones[] RightGripBones =
    {
        HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal
    };

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

    private void PrepareFirstPersonAvatar()
    {
        // The PC and VR scenes share this complete avatar. Keep its body visible in VR,
        // but collapse the local head so it cannot cover the headset camera.
        foreach (var bodyRenderer in GetComponentsInChildren<Renderer>(true))
        {
            bodyRenderer.shadowCastingMode = ShadowCastingMode.On;
        }

        if (avatarAnimator != null && avatarAnimator.isHuman)
        {
            avatarHead ??= avatarAnimator.GetBoneTransform(HumanBodyBones.Head);
            if (avatarHead != null)
            {
                avatarHead.localScale = Vector3.zero;
            }
        }
    }
}

public static class TestStage1VRInput
{
    private static int sampledFrame = -1;
    private static bool collectWasPressed;
    private static bool attackWasPressed;
    private static bool grabWasPressed;
    private static bool rightGrabWasPressed;
    private static bool collectPressedThisFrame;
    private static bool attackPressedThisFrame;
    private static bool grabPressedThisFrame;
    private static bool rightGrabPressedThisFrame;
    private static bool rightGrabHeld;

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

    public static bool GrabPressedThisFrame { get { SampleButtons(); return grabPressedThisFrame; } }
    public static bool RightGrabPressedThisFrame { get { SampleButtons(); return rightGrabPressedThisFrame; } }
    public static bool RightGrabHeld { get { SampleButtons(); return rightGrabHeld; } }

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
            grabPressedThisFrame = rightGrabPressedThisFrame = false;
            rightGrabHeld = false;
            collectWasPressed = false;
            attackWasPressed = false;
            grabWasPressed = rightGrabWasPressed = false;
            return;
        }

        var controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        var collectPressed = controller.TryGetFeatureValue(CommonUsages.primaryButton, out var primary)
            && primary;
        var attackPressed = controller.TryGetFeatureValue(CommonUsages.triggerButton, out var triggerButton)
            && triggerButton;
        bool leftGrabPressed = ReadTriggerOrGrip(XRNode.LeftHand);
        bool rightGrabPressed = ReadTriggerOrGrip(XRNode.RightHand);
        rightGrabHeld = rightGrabPressed;
        bool grabPressed = leftGrabPressed || rightGrabPressed;

        collectPressedThisFrame = collectPressed && !collectWasPressed;
        attackPressedThisFrame = attackPressed && !attackWasPressed;
        grabPressedThisFrame = grabPressed && !grabWasPressed;
        rightGrabPressedThisFrame = rightGrabPressed && !rightGrabWasPressed;
        collectWasPressed = collectPressed;
        attackWasPressed = attackPressed;
        grabWasPressed = grabPressed;
        rightGrabWasPressed = rightGrabPressed;
    }

    private static bool ReadTriggerOrGrip(XRNode node)
    {
        var xrDevice = InputDevices.GetDeviceAtXRNode(node);
        bool legacyTrigger = xrDevice.TryGetFeatureValue(CommonUsages.trigger, out float trigger) && trigger >= .65f;
        bool legacyGrip = xrDevice.TryGetFeatureValue(CommonUsages.grip, out float grip) && grip >= .65f;
        if (legacyTrigger || legacyGrip) return true;

        var wantedUsage = node == XRNode.LeftHand
            ? UnityEngine.InputSystem.CommonUsages.LeftHand
            : UnityEngine.InputSystem.CommonUsages.RightHand;
        foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
        {
            bool correctHand = false;
            foreach (var usage in device.usages) if (usage == wantedUsage) { correctHand = true; break; }
            if (!correctHand) continue;
            var triggerControl = device.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
            var gripControl = device.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("grip");
            return (triggerControl != null && triggerControl.ReadValue() >= .65f)
                || (gripControl != null && gripControl.ReadValue() >= .65f);
        }
        return false;
    }
}
