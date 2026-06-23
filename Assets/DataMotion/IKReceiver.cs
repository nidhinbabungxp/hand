using UnityEngine;

/// Drives Character B's upper body from 3 source quaternions
/// (simulating chest + 2 wrist IMUs).
[RequireComponent(typeof(Animator))]
public class IKReceiver : MonoBehaviour
{
    public DataSource source;   
    
    // drag Character A here in // Correction for hand bind-pose mismatch. Adjust this value if it's not exactly 90 around X.
    public Quaternion handCorrection = Quaternion.Euler(90f, 0f, 0f);
    Animator animator;
    Transform chestBone;

    // Source references (cached)
    Animator srcAnimator;
    Transform srcChest, srcLeftHand, srcRightHand;

    void Start()
    {
        animator = GetComponent<Animator>();
        chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);

        if (source != null)
        {
            srcAnimator = source.GetComponent<Animator>();
            // fixed: actually get the bone transforms from the source animator
            srcChest = srcAnimator.GetBoneTransform(HumanBodyBones.Chest);
            srcLeftHand = srcAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            srcRightHand = srcAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        }
    }

    // Chest is driven directly (no IK target for spine in Unity's built-in IK).
    void LateUpdate()
    {
        if (source == null) return;
        chestBone.rotation = source.ChestRotation;
    }

    // Wrists are driven via IK so shoulder + elbow auto-solve.
    void OnAnimatorIK(int layerIndex)
    {
        if (source == null) return;

        // Get wrist offsets RELATIVE to source's chest
        Vector3 srcLeftOffset = srcChest.InverseTransformPoint(srcLeftHand.position);
        Vector3 srcRightOffset = srcChest.InverseTransformPoint(srcRightHand.position);

        // Apply those offsets relative to THIS character's chest
        Vector3 localLeftPos = chestBone.TransformPoint(srcLeftOffset);
        Vector3 localRightPos = chestBone.TransformPoint(srcRightOffset);

        // Left hand
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1f);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, localLeftPos);
        
        Quaternion currentLeftRotLocal = Quaternion.Inverse(srcChest.rotation) * source.LeftHandRotation;
        animator.SetIKRotation(AvatarIKGoal.LeftHand, chestBone.rotation * currentLeftRotLocal * handCorrection);

        // Right hand
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1f);
        animator.SetIKPosition(AvatarIKGoal.RightHand, localRightPos);
        
        Quaternion currentRightRotLocal = Quaternion.Inverse(srcChest.rotation) * source.RightHandRotation;
        animator.SetIKRotation(AvatarIKGoal.RightHand, chestBone.rotation * currentRightRotLocal * handCorrection);

    }
}