using UnityEngine;

public class RigReceiver : MonoBehaviour
{
    public enum ReceiverMode
    {
        RightOnly,
        LeftOnly,
        LeftAndRightOnly,
        ChestRotationOnly,
        ChestPositionToRoot,
        FullBody
    }

    [Header("Source")]
    public MonoBehaviour sourceObject;
    public Animator animator;

    [Header("Receiver Mode")]
    public ReceiverMode receiverMode = ReceiverMode.FullBody;

    [Header("Rig Targets")]
    public Transform chestTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("Root Movement")]
    public Transform rootObject;
    public bool useRelativeChestPosition = true;
    public bool lockRootY = true;
    public float rootPositionMultiplier = 1f;
    public float chestPositionThreshold = 0.02f;

    [Header("Body Turn From Chest")]
    public bool rotateBodyWhenChestTurns = false;
    public float bodyTurnThreshold = 45f;
    public float bodyTurnSmoothSpeed = 6f;

    [Header("IoT Hand World Positions")]
    public float handPositionMultiplier = 1f;
    public bool useChestTargetAsHandOrigin = true;
    public bool useChestRelativeHandRotation = true;

    [Header("Hand Anchor Correction")]
    public bool useHandAnchorCorrection = true;

    [Range(0f, 1f)]
    public float wristPull = 0.45f;

    [Header("Smoothing")]
    public float positionSmoothSpeed = 10f;
    public float rotationSmoothSpeed = 12f;

    [Header("Arm Reach Constraint")]
    public bool useArmReachClamp = true;
    public Transform leftShoulderBone;
    public Transform rightShoulderBone;
    public float maxArmReach = 0.6f;

    [Header("Debug Coordinates")]
    public Vector3 globalcoordLeft;
    public Vector3 globalcoordRight;
    public Vector3 globalcoordChest;
    public Vector3 globalcoordRoot;

    private IMotionSource source;
    private bool calibrated;

    private Vector3 startRootPosition;
    private Quaternion startRootRotation;
    private Vector3 startChestPosition;
    private Quaternion startChestRotation;

    private Vector3 rootVelocity;
    private Vector3 leftHandVelocity;
    private Vector3 rightHandVelocity;

    private Vector3 startSourceLeftHandLocalOffset;
    private Vector3 startSourceRightHandLocalOffset;
    private Vector3 startAvatarLeftHandLocalOffset;
    private Vector3 startAvatarRightHandLocalOffset;

    private Transform leftHandBone;
    private Transform rightHandBone;

    private void Awake()
    {
        if (rootObject == null)
        {
            rootObject = transform;
        }
    }

    private void Start()
    {
        source = sourceObject as IMotionSource;

        if (source == null)
        {
            Debug.LogError("RigReceiver: sourceObject does not implement IMotionSource.", this);
            enabled = false;
            return;
        }

        CacheBonesIfMissing();
        Calibrate();
    }

    private void Update()
    {
        if (source == null)
        {
            return;
        }

        switch (receiverMode)
        {
            case ReceiverMode.RightOnly:
                ApplyRightHand();
                break;

            case ReceiverMode.LeftOnly:
                ApplyLeftHand();
                break;

            case ReceiverMode.LeftAndRightOnly:
                ApplyLeftHand();
                ApplyRightHand();
                break;

            case ReceiverMode.ChestRotationOnly:
                ApplyChestRotation();
                ApplyBodyRotationFromChest();
                break;

            case ReceiverMode.ChestPositionToRoot:
                ApplyChestPositionToRoot();
                ApplyBodyRotationFromChest();
                break;

            case ReceiverMode.FullBody:
                ApplyChestPositionToRoot();
                ApplyChestRotation();
                ApplyBodyRotationFromChest();
                ApplyLeftHand();
                ApplyRightHand();
                break;
        }

        UpdateDebugCoordinates();
    }

    [ContextMenu("Calibrate Receiver")]
    public void Calibrate()
    {
        Transform root = rootObject != null ? rootObject : transform;

        startRootPosition = root.position;
        startRootRotation = root.rotation;
        startChestPosition = source != null ? source.ChestPosition : Vector3.zero;
        startChestRotation = source != null ? source.ChestRotation : Quaternion.identity;

        Quaternion avatarReferenceRotation = GetAvatarHandReferenceRotation(root);
        Vector3 avatarOriginPosition = GetAvatarHandOriginPosition(root);

        if (source != null)
        {
            startSourceLeftHandLocalOffset =
                Quaternion.Inverse(startChestRotation) *
                (source.LeftHandPosition - source.ChestPosition);

            startSourceRightHandLocalOffset =
                Quaternion.Inverse(startChestRotation) *
                (source.RightHandPosition - source.ChestPosition);
        }

        if (leftHandTarget != null)
        {
            startAvatarLeftHandLocalOffset =
                Quaternion.Inverse(avatarReferenceRotation) *
                (leftHandTarget.position - avatarOriginPosition);
        }

        if (rightHandTarget != null)
        {
            startAvatarRightHandLocalOffset =
                Quaternion.Inverse(avatarReferenceRotation) *
                (rightHandTarget.position - avatarOriginPosition);
        }

        calibrated = true;

        rootVelocity = Vector3.zero;
        leftHandVelocity = Vector3.zero;
        rightHandVelocity = Vector3.zero;
    }

    private void CacheBonesIfMissing()
    {
        if (animator == null)
        {
            return;
        }

        if (leftShoulderBone == null)
        {
            leftShoulderBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        }

        if (rightShoulderBone == null)
        {
            rightShoulderBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    private void ApplyLeftHand()
    {
        ApplyIoTHandWorldPosition(
            leftHandTarget,
            source.LeftHandPosition,
            source.LeftHandRotation,
            leftShoulderBone,
            leftHandBone,
            startSourceLeftHandLocalOffset,
            startAvatarLeftHandLocalOffset,
            ref leftHandVelocity
        );
    }

    private void ApplyRightHand()
    {
        ApplyIoTHandWorldPosition(
            rightHandTarget,
            source.RightHandPosition,
            source.RightHandRotation,
            rightShoulderBone,
            rightHandBone,
            startSourceRightHandLocalOffset,
            startAvatarRightHandLocalOffset,
            ref rightHandVelocity
        );
    }

    private void ApplyIoTHandWorldPosition(
        Transform handTarget,
        Vector3 iotHandWorldPosition,
        Quaternion iotHandWorldRotation,
        Transform shoulderBone,
        Transform handBone,
        Vector3 startSourceHandLocalOffset,
        Vector3 startAvatarHandLocalOffset,
        ref Vector3 velocity
    )
    {
        if (handTarget == null)
        {
            return;
        }

        Transform root = rootObject != null ? rootObject : transform;

        Vector3 avatarOriginPosition = GetAvatarHandOriginPosition(root);
        Quaternion avatarReferenceRotation = GetAvatarHandReferenceRotation(root);

        Vector3 sourceChestLocalHandOffset =
            Quaternion.Inverse(source.ChestRotation) *
            (iotHandWorldPosition - source.ChestPosition);

        Vector3 sourceLocalDelta =
            sourceChestLocalHandOffset - startSourceHandLocalOffset;

        Vector3 avatarLocalHandOffset =
            startAvatarHandLocalOffset + sourceLocalDelta * handPositionMultiplier;

        Vector3 targetPosition =
            avatarOriginPosition + avatarReferenceRotation * avatarLocalHandOffset;

        if (useHandAnchorCorrection && handBone != null)
        {
            targetPosition = Vector3.Lerp(
                targetPosition,
                handBone.position,
                wristPull
            );
        }

        if (useArmReachClamp && shoulderBone != null)
        {
            targetPosition = ClampToReach(
                targetPosition,
                shoulderBone.position,
                maxArmReach
            );
        }

        Quaternion targetRotation = iotHandWorldRotation;

        if (useChestRelativeHandRotation)
        {
            Quaternion sourceChestLocalHandRotation =
                Quaternion.Inverse(source.ChestRotation) * iotHandWorldRotation;

            targetRotation =
                avatarReferenceRotation * sourceChestLocalHandRotation;
        }

        SmoothSetPositionAndRotation(
            handTarget,
            targetPosition,
            targetRotation,
            ref velocity
        );
    }

    private Vector3 GetAvatarHandOriginPosition(Transform root)
    {
        if (useChestTargetAsHandOrigin && chestTarget != null)
        {
            return chestTarget.position;
        }

        return root.position;
    }

    private Quaternion GetAvatarHandReferenceRotation(Transform root)
    {
        if (chestTarget != null)
        {
            return chestTarget.rotation;
        }

        return root.rotation;
    }

    private void ApplyChestRotation()
    {
        if (chestTarget == null)
        {
            return;
        }

        chestTarget.rotation =
            SmoothRotation(chestTarget.rotation, source.ChestRotation);
    }

    private void ApplyChestPositionToRoot()
    {
        if (!calibrated)
        {
            Calibrate();
        }

        Transform root = rootObject != null ? rootObject : transform;
        Vector3 targetPosition;

        if (useRelativeChestPosition)
        {
            Vector3 chestDelta =
                (source.ChestPosition - startChestPosition) *
                rootPositionMultiplier;

            if (lockRootY)
            {
                chestDelta.y = 0f;
            }

            targetPosition = startRootPosition + chestDelta;
        }
        else
        {
            targetPosition = source.ChestPosition * rootPositionMultiplier;

            if (lockRootY)
            {
                targetPosition.y = startRootPosition.y;
            }
        }

        if (Vector3.Distance(root.position, targetPosition) < chestPositionThreshold)
        {
            return;
        }

        root.position = Vector3.SmoothDamp(
            root.position,
            targetPosition,
            ref rootVelocity,
            GetSmoothTime(positionSmoothSpeed)
        );
    }

    private void ApplyBodyRotationFromChest()
    {
        if (!rotateBodyWhenChestTurns || source == null)
        {
            return;
        }

        Transform root = rootObject != null ? rootObject : transform;

        Vector3 chestForward = source.ChestRotation * Vector3.forward;
        chestForward.y = 0f;

        if (chestForward.sqrMagnitude < 0.001f)
        {
            return;
        }

        chestForward.Normalize();

        Vector3 rootForward = root.forward;
        rootForward.y = 0f;

        if (rootForward.sqrMagnitude < 0.001f)
        {
            return;
        }

        rootForward.Normalize();

        float angle = Vector3.SignedAngle(rootForward, chestForward, Vector3.up);

        if (Mathf.Abs(angle) < bodyTurnThreshold)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(chestForward, Vector3.up);

        float t = 1f - Mathf.Exp(-bodyTurnSmoothSpeed * Time.deltaTime);

        root.rotation = Quaternion.Slerp(
            root.rotation,
            targetRotation,
            t
        );
    }

    private void SmoothSetPositionAndRotation(
        Transform target,
        Vector3 targetPosition,
        Quaternion targetRotation,
        ref Vector3 velocity
    )
    {
        target.position = Vector3.SmoothDamp(
            target.position,
            targetPosition,
            ref velocity,
            GetSmoothTime(positionSmoothSpeed)
        );

        target.rotation =
            SmoothRotation(target.rotation, targetRotation);
    }

    private Quaternion SmoothRotation(Quaternion current, Quaternion target)
    {
        float t = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
        return Quaternion.Slerp(current, target, t);
    }

    private float GetSmoothTime(float speed)
    {
        return 1f / Mathf.Max(0.001f, speed);
    }

    private Vector3 ClampToReach(
        Vector3 targetPosition,
        Vector3 origin,
        float maxDistance
    )
    {
        Vector3 offset = targetPosition - origin;
        float distance = offset.magnitude;

        if (distance <= maxDistance || distance <= 0.001f)
        {
            return targetPosition;
        }

        return origin + offset.normalized * maxDistance;
    }

    private void UpdateDebugCoordinates()
    {
        if (leftHandTarget != null)
        {
            globalcoordLeft = leftHandTarget.position;
        }

        if (rightHandTarget != null)
        {
            globalcoordRight = rightHandTarget.position;
        }

        if (chestTarget != null)
        {
            globalcoordChest = chestTarget.position;
        }

        Transform root = rootObject != null ? rootObject : transform;
        globalcoordRoot = root.position;
    }
}