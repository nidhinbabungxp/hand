using UnityEngine;

/// Reads world rotations + positions of 3 bones on the source character.
/// Implements IMotionSource so any receiver can consume it.
/// Later, swap for TargetDataSource or a UDP/Serial IMU receiver.
public class DataSourceViaRig : MonoBehaviour, IMotionSource
{
    public Quaternion ChestRotation { get; private set; }
    public Quaternion LeftHandRotation { get; private set; }
    public Quaternion RightHandRotation { get; private set; }

    public Vector3 ChestPosition { get; private set; }
    public Vector3 LeftHandPosition { get; private set; }
    public Vector3 RightHandPosition { get; private set; }

    Animator animator;
    Transform chest, leftHand, rightHand;

    void Start()
    {
        animator = GetComponent<Animator>();
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    void LateUpdate()
    {
        ChestRotation = chest.rotation;
        LeftHandRotation = leftHand.rotation;
        RightHandRotation = rightHand.rotation;

        ChestPosition = chest.position;
        LeftHandPosition = leftHand.position;
        RightHandPosition = rightHand.position;
    }
}