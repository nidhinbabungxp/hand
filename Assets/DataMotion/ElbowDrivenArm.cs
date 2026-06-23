using UnityEngine;

/// Drives shoulder rotation and predicts wrist position from a single elbow sensor.
/// Inputs:  elbowTarget  (position + rotation from UWB + IMU tag near elbow)
/// Outputs: shoulder bone rotation (partial copy), wrist target position (computed)
public class ElbowDrivenArm : MonoBehaviour
{
    [Header("Elbow sensor transform (position = UWB, rotation = IMU)")]
    public Transform elbowTarget;

    [Header("Bones to drive")]
    public Transform shoulderBone;
    public Transform forearmBone;       // used only to get forearm length at Start

    [Header("Wrist target (IK hand target to drive)")]
    public Transform wristTarget;

    [Header("Tuning")]
    [Range(0f, 1f)]
    [Tooltip("How much the shoulder follows elbow rotation. 0 = no influence, 1 = full copy.")]
    public float shoulderInfluence = 0.5f;

    float _forearmLength;
    Quaternion _shoulderBindRotation;   // shoulder rest rotation at Start

    void Start()
    {
        // Auto-calculate forearm length from rig
        if (forearmBone != null && wristTarget != null)
            _forearmLength = Vector3.Distance(forearmBone.position, wristTarget.position);
        else
            Debug.LogWarning("ElbowDrivenArm: assign forearmBone and wristTarget to auto-calculate forearm length.");

        // Cache shoulder's rest rotation so we slerp FROM it
        if (shoulderBone != null)
            _shoulderBindRotation = shoulderBone.rotation;
    }

    void LateUpdate()
    {
        if (elbowTarget == null) return;

        // --- Shoulder: partial copy of elbow rotation ---
        if (shoulderBone != null)
            shoulderBone.rotation = Quaternion.Slerp(_shoulderBindRotation, elbowTarget.rotation, shoulderInfluence);

        // --- Wrist: elbow position + forearm length along elbow's forward ---
        if (wristTarget != null)
            wristTarget.position = elbowTarget.position + elbowTarget.forward * _forearmLength;
    }
}