using UnityEngine;

/// Drives a Humanoid IK Pass character from any IMotionSource.
/// Attach to the receiver character. Drag any GameObject with IMotionSource into sourceObject.
[RequireComponent(typeof(Animator))]
public class IKReceiverNew : MonoBehaviour
{
    [Header("Drag the GameObject that has TargetDataSource or DataSource")]
    public MonoBehaviour sourceObject;  // Must implement IMotionSource

    [Header("Hand rotation correction (try 90,0,0 if hands face wrong way)")]
    public Vector3 handCorrectionEuler = new Vector3(90f, 0f, 0f);

    IMotionSource source;
    Animator animator;
    Transform chestBone;
    Quaternion handCorrection;

    void Start()
    {
        animator = GetComponent<Animator>();
        chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);

        source = sourceObject as IMotionSource;
        if (source == null)
            Debug.LogError("IKReceiver: sourceObject does not implement IMotionSource!", this);

        handCorrection = Quaternion.Euler(handCorrectionEuler);
    }

    void LateUpdate()
    {
        if (source == null) return;
        chestBone.rotation = source.ChestRotation;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (source == null) return;

        // Positions come from source directly now (already world-space target positions).
        // Convert to local offset relative to source chest, then apply to our chest.
        Vector3 srcChestPos = source.ChestPosition;
        Quaternion srcChestRot = source.ChestRotation;

        // Source-chest-local offsets
        Vector3 leftOffset = Quaternion.Inverse(srcChestRot) * (source.LeftHandPosition - srcChestPos);
        Vector3 rightOffset = Quaternion.Inverse(srcChestRot) * (source.RightHandPosition - srcChestPos);

        // Apply relative to THIS character's chest
        Vector3 localLeftPos = chestBone.position + chestBone.rotation * leftOffset;
        Vector3 localRightPos = chestBone.position + chestBone.rotation * rightOffset;

        // Left hand
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, localLeftPos);
        
        Quaternion currentLeftRotLocal = Quaternion.Inverse(srcChestRot) * source.LeftHandRotation;
        animator.SetIKRotation(AvatarIKGoal.LeftHand, chestBone.rotation * currentLeftRotLocal * handCorrection);

        // Right hand
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
        animator.SetIKPosition(AvatarIKGoal.RightHand, localRightPos);
        
        Quaternion currentRightRotLocal = Quaternion.Inverse(srcChestRot) * source.RightHandRotation;
        animator.SetIKRotation(AvatarIKGoal.RightHand, chestBone.rotation * currentRightRotLocal * handCorrection);
    }
}