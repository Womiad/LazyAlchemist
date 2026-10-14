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
    private Animator avatarAnimator;
    private Transform leftHandRoot;
    private Transform rightHandRoot;
    private Transform avatarHead;
    private readonly Dictionary<Transform, Quaternion> fingerRestRotations = new Dictionary<Transform, Quaternion>();
    private LineRenderer teleportArc;
    private Transform teleportMarker;
    private Material teleportMaterial;
    private bool canSnapTurn = true;
    private bool isTeleportAiming;
    private bool hasValidTeleportTarget;
    private bool hasCalibratedHeadsetPosition;
    private int validTrackingCalibrationSamples;
    private Vector3 pendingHeadsetCalibrationPosition;
    private Vector3 headsetPositionAtCalibration;
    private Vector3 teleportTarget;
    private Vector3 teleportTargetNormal = Vector3.up;
    private Vector3 calibratedAvatarEyeLocalPosition = Vector3.up * CameraFallbackHeight;
    private Vector3 avatarEyeToBodyOffset;
    private readonly Vector3[] teleportArcPositions = new Vector3[TeleportArcSegments + 1];
    private readonly RaycastHit[] teleportHits = new RaycastHit[16];

    public static Vector2 MoveAxis { get; private set; }

    [Header("VR Avatar")]
    [SerializeField, Range(0f, 1f)] private float handIkWeight = 1f;
    [SerializeField] private Vector3 leftHandRotationOffset = new Vector3(0f, 0f, 90f);
    [SerializeField] private Vector3 rightHandRotationOffset = new Vector3(0f, 0f, -90f);
    [SerializeField, Min(1f)] private float movementSpeedMultiplier = 1.6f;
    [SerializeField, Range(1.2f, 2f)] private float playerEyeHeight = 1.8f;
    [SerializeField] private float eyeOffsetFromHeadBone = DefaultEyeOffsetFromHeadBone;
    [SerializeField, Range(0f, 0.5f)] private float bodyBackwardOffset = 0.18f;
    [SerializeField, Range(0f, 90f)] private float fingerCurlAngle = 65f;
    [SerializeField, Range(0f, 60f)] private float thumbCurlAngle = 35f;
    [SerializeField, Range(0f, 60f)] private float thumbOppositionAngle = 28f;

    protected override void InitializeTpCamera()
    {
        tpCamera = FindFirstObjectByType<vThirdPersonCamera>();
        if (tpCamera == null)
        {
            Debug.LogError("TestStage1 VR requires the scene's vThirdPersonCamera.");
            return;
        }

        avatarAnimator = GetComponent<Animator>();
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

    protected override void FixedUpdate()
    {
        // VR locomotion is camera-relative, but the left stick must never rotate the avatar.
        cc.UpdateMotor();
        cc.ControlLocomotionType();
    }

    public override void MoveInput()
    {
        var leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (!leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out var moveAxis))
        {
            moveAxis = Vector2.zero;
        }

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
        UpdateFingerPose(XRNode.LeftHand, true);
        UpdateFingerPose(XRNode.RightHand, false);

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

        AlignAvatarBodyToHeadset();
        ApplyHandIk(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, leftHandRoot, leftHandRotationOffset, -1f);
        ApplyHandIk(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, rightHandRoot, rightHandRotationOffset, 1f);
    }

    private void AlignAvatarBodyToHeadset()
    {
        if (vrCamera == null)
        {
            return;
        }

        // Keep the hips beneath the player's real-world headset offset while preserving
        // the locomotion animation's vertical motion. Hands are solved afterwards, so
        // this body correction does not pull them away from the tracked controllers.
        Vector3 viewForward = Vector3.ProjectOnPlane(vrCamera.transform.forward, Vector3.up);
        if (viewForward.sqrMagnitude > 0.001f)
        {
            Quaternion targetBodyRotation = Quaternion.LookRotation(viewForward.normalized, Vector3.up);
            avatarAnimator.bodyRotation = targetBodyRotation;

            // Animator.bodyPosition is the humanoid mass center, not the hips or feet.
            // Driving its Y from the camera sinks this particular avatar below ground,
            // so preserve the locomotion animation's grounded vertical position.
            Vector3 desiredBodyPosition = vrCamera.transform.position
                - targetBodyRotation * avatarEyeToBodyOffset;
            desiredBodyPosition -= viewForward.normalized * bodyBackwardOffset;
            Vector3 groundedBodyPosition = avatarAnimator.bodyPosition;
            groundedBodyPosition.x = desiredBodyPosition.x;
            groundedBodyPosition.z = desiredBodyPosition.z;
            avatarAnimator.bodyPosition = groundedBodyPosition;
        }
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
        eyePosition = transform.TransformPoint(calibratedAvatarEyeLocalPosition);
        avatarEyeToBodyOffset = Quaternion.Inverse(avatarAnimator.bodyRotation)
            * (eyePosition - avatarAnimator.bodyPosition);
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

        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        float thumb = device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryPressed) && primaryPressed ? 1f : 0f;
        device.TryGetFeatureValue(CommonUsages.trigger, out float index);
        device.TryGetFeatureValue(CommonUsages.grip, out float middle);
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

            finger.localRotation = restRotation * curl;
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
                finger.localRotation = restRotation * curl;
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
