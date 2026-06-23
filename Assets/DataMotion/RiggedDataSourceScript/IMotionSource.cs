using UnityEngine;

/// Common interface for any motion data source (bone-reading, target-reading, IMU, etc.).
/// Receivers depend on this, not on a concrete class.
public interface IMotionSource
{
    Quaternion ChestRotation { get; }
    Quaternion LeftHandRotation { get; }
    Quaternion RightHandRotation { get; }

    Vector3 ChestPosition { get; }
    Vector3 LeftHandPosition { get; }
    Vector3 RightHandPosition { get; }
}