using UnityEngine;

public class CopyHandMovement : MonoBehaviour
{
    [Header("Character 1 Hands")]
    public Transform sourceLeftHand;
    public Transform sourceRightHand;

    [Header("Character 1 Elbows (mock UWB tag position)")]
    public Transform sourceLeftElbow;
    public Transform sourceRightElbow;

    [Header("Character 1 Forearms (mock forearm rotation)")]
    public Transform sourceLeftForearm;
    public Transform sourceRightForearm;

    [Header("Character 2 Hands")]
    public Transform targetLeftHand;
    public Transform targetRightHand;

    [Header("Character 2 Elbow Hint Targets (Two Bone IK hints)")]
    public Transform targetLeftElbowHint;
    public Transform targetRightElbowHint;

    [Header("Character 2 Forearm Bones")]
    public Transform targetLeftForearm;
    public Transform targetRightForearm;

    void LateUpdate()
    {
        // Copy left hand
        targetLeftHand.position = sourceLeftHand.position;
        targetLeftHand.rotation = sourceLeftHand.rotation;

        // Copy right hand
        targetRightHand.position = sourceRightHand.position;
        targetRightHand.rotation = sourceRightHand.rotation;

        // Drive elbow hint targets from source elbow position (simulates UWB tag near elbow)
        if (sourceLeftElbow != null && targetLeftElbowHint != null)
            targetLeftElbowHint.position = sourceLeftElbow.position;

        if (sourceRightElbow != null && targetRightElbowHint != null)
            targetRightElbowHint.position = sourceRightElbow.position;

        // Apply forearm rotation from source (simulates IMU quaternion on elbow tag)
        if (sourceLeftForearm != null && targetLeftForearm != null)
            targetLeftForearm.rotation = sourceLeftForearm.rotation;

        if (sourceRightForearm != null && targetRightForearm != null)
            targetRightForearm.rotation = sourceRightForearm.rotation;
    }
}