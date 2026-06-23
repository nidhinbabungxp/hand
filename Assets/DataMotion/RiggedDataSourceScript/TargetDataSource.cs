using UnityEngine;

/// Reads position + rotation from 3 Animation Rigging target transforms.
/// Drop this on the source character. Drag the constraint targets into the slots.
/// Implements IMotionSource so any receiver can consume it.
public class TargetDataSource : MonoBehaviour, IMotionSource
{
    [Header("Drag your Animation Rigging targets here")]
    public Transform chestTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    // IMotionSource — rotations
    public Quaternion ChestRotation { get; private set; }
    public Quaternion LeftHandRotation { get; private set; }
    public Quaternion RightHandRotation { get; private set; }

    // IMotionSource — positions
    public Vector3 ChestPosition { get; private set; }
    public Vector3 LeftHandPosition { get; private set; }
    public Vector3 RightHandPosition { get; private set; }

    void LateUpdate()
    {
        if (chestTarget == null || leftHandTarget == null || rightHandTarget == null) return;

        ChestRotation = chestTarget.rotation;
        LeftHandRotation = leftHandTarget.rotation;
        RightHandRotation = rightHandTarget.rotation;

        ChestPosition = chestTarget.position;
        LeftHandPosition = leftHandTarget.position;
        RightHandPosition = rightHandTarget.position;
    }
}