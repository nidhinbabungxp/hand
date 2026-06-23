using UnityEngine;

/// Reads world rotations of 3 bones on the source character.
/// Later, swap this script for a UDP/Serial IMU receiver exposing the same 3 fields.
public class DataSource : MonoBehaviour
{
    public Quaternion ChestRotation { get; private set; }
    public Quaternion LeftHandRotation { get; private set; }
    public Quaternion RightHandRotation { get; private set; }

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
        // LateUpdate so we read AFTER the source animator has posed the rig this frame.
        ChestRotation = chest.rotation;
        LeftHandRotation = leftHand.rotation;
        RightHandRotation = rightHand.rotation;
    }
}