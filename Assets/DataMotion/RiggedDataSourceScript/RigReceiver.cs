using UnityEngine;

/// <summary>
/// Receives motion data from any IMotionSource and drives selected Animation Rigging targets.
/// Supports left-only, right-only, left+chest, right+chest, both-hands-only,
/// chest-rotation-only, chest-position-to-root, and full-body modes.
/// </summary>
public class RigReceiver : MonoBehaviour
{
    public enum ReceiverMode
    {
        RightOnly,
        LeftOnly,
        LeftAndRightOnly,
        LeftAndChest,
        RightAndChest,
        ChestRotationOnly,
        ChestPositionToRoot,
        FullBody
    }

    [Header("Source")]
    [Tooltip("Component that implements IMotionSource, for example IMUDataSource.")]
    public MonoBehaviour sourceObject;
    public Animator animator;

    [Header("Receiver Mode")]
    public ReceiverMode receiverMode = ReceiverMode.FullBody;

    [Header("Rig Targets")]
    public Transform chestTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("Root Movement")]
    [Tooltip("Object that should move from chest position. If empty, this GameObject is used.")]
    public Transform rootObject;

    [Tooltip("Use relative chest movement from the calibration pose instead of raw world position.")]
    public bool useRelativeChestPosition = true;

    [Tooltip("Ignore source Y movement when moving the root object.")]
    public bool lockRootY = true;

    [Tooltip("Multiplier for chest position movement.")]
    public float rootPositionMultiplier = 1f;

    [Tooltip("Minimum chest movement needed before root movement is applied.")]
    public float chestPositionThreshold = 0.02f;

    [Header("Smoothing")]
    [Tooltip("Higher value = faster position follow.")]
    public float positionSmoothSpeed = 10f;

    [Tooltip("Higher value = faster rotation follow.")]
    public float rotationSmoothSpeed = 12f;

    [Header("Arm Reach Constraint")]
    public bool useArmReachClamp;
    public Transform leftShoulderBone;
    public Transform rightShoulderBone;
    public float maxArmReach = 0.55f;

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

    [Header("Body Turn From Chest")]
public bool rotateRootFromChestYaw = true;
public float chestTurnThresholdDegrees = 45f;
public float rootTurnSmoothSpeed = 6f;

    

    private void Awake()
    {
        if (rootObject == null)
        {
            rootObject = transform;
        }
    }

    private void Start()
    {
        if (sourceObject == null)
        {
            Debug.LogError("RigReceiver: sourceObject is not assigned.", this);
            enabled = false;
            return;
        }

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

            case ReceiverMode.LeftAndChest:
                ApplyChestRotation();
                ApplyLeftHand();
                break;

            case ReceiverMode.RightAndChest:
                ApplyChestRotation();
                ApplyRightHand();
                break;

            case ReceiverMode.ChestRotationOnly:
    ApplyChestRotation();
    ApplyRootRotationFromChest();
    break;

            case ReceiverMode.ChestPositionToRoot:
                ApplyChestPositionToRoot();
                
                break;

         case ReceiverMode.FullBody:
    ApplyRootRotationFromChest();
    ApplyChestPositionToRoot();

    ApplyChestRotation();
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
    }

    private void ApplyLeftHand()
    {
        if (leftHandTarget == null)
        {
            return;
        }

        Vector3 targetPosition = source.LeftHandPosition;

        if (useArmReachClamp && leftShoulderBone != null)
        {
            targetPosition = ClampToReach(targetPosition, leftShoulderBone.position, maxArmReach);
        }

        SmoothSetPositionAndRotation(
            leftHandTarget,
            targetPosition,
            source.LeftHandRotation,
            ref leftHandVelocity
        );
    }

    private void ApplyRightHand()
    {
        if (rightHandTarget == null)
        {
            return;
        }

        Vector3 targetPosition = source.RightHandPosition;

        if (useArmReachClamp && rightShoulderBone != null)
        {
            targetPosition = ClampToReach(targetPosition, rightShoulderBone.position, maxArmReach);
        }

        SmoothSetPositionAndRotation(
            rightHandTarget,
            targetPosition,
            source.RightHandRotation,
            ref rightHandVelocity
        );
    }

    private void ApplyChestRotation()
    {
        if (chestTarget == null)
        {
            return;
        }

        chestTarget.rotation = SmoothRotation(chestTarget.rotation, source.ChestRotation);
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
            Vector3 chestDelta = (source.ChestPosition - startChestPosition) * rootPositionMultiplier;

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

        target.rotation = SmoothRotation(target.rotation, targetRotation);
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

    private Vector3 ClampToReach(Vector3 targetPosition, Vector3 origin, float maxDistance)
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

private void ApplyRootRotationFromChest()
{
    if (!rotateRootFromChestYaw || !calibrated)
        return;

    Transform root = rootObject != null ? rootObject : transform;

    // After your quaternion swap, chest yaw should be Unity Y
    float chestYaw = Mathf.DeltaAngle(
        startChestRotation.eulerAngles.y,
        source.ChestRotation.eulerAngles.y
    );

    if (Mathf.Abs(chestYaw) < chestTurnThresholdDegrees)
        return;

    Quaternion targetRootRotation =
        startRootRotation * Quaternion.Euler(0f, chestYaw, 0f);

    float t = 1f - Mathf.Exp(-rootTurnSmoothSpeed * Time.deltaTime);
    root.rotation = Quaternion.Slerp(root.rotation, targetRootRotation, t);
}
}