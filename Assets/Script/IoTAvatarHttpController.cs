using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class IoTAvatarHttpController : MonoBehaviour
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

    [Header("Receiver Mode")]
    public ReceiverMode receiverMode = ReceiverMode.FullBody;

    [Header("API")]
    public string apiUrl = "http://10.57.221.56:8080/api/all/latest";
    public float refreshRate = 0.05f;

    [Header("Avatar Parts")]
    public Transform root;
    public Transform chest;
    public Transform leftHand;
    public Transform rightHand;

    [Header("Smoothing")]
    public float positionSmooth = 10f;
    public float rotationSmooth = 10f;

    [Header("Position Settings")]
    public float scale = 1f;
    public Vector3 positionOffset;

    private Vector3 rootTargetPos;
    private Quaternion chestTargetRot;

    private Vector3 leftHandTargetPos;
    private Quaternion leftHandTargetRot;

    private Vector3 rightHandTargetPos;
    private Quaternion rightHandTargetRot;

    void Start()
    {
        if (root != null)
            rootTargetPos = root.position;

        if (chest != null)
            chestTargetRot = chest.rotation;

        if (leftHand != null)
        {
            leftHandTargetPos = leftHand.position;
            leftHandTargetRot = leftHand.rotation;
        }

        if (rightHand != null)
        {
            rightHandTargetPos = rightHand.position;
            rightHandTargetRot = rightHand.rotation;
        }

        StartCoroutine(FetchLoop());
    }

    void Update()
    {
        if (root != null)
        {
            root.position = Vector3.Lerp(
                root.position,
                rootTargetPos,
                Time.deltaTime * positionSmooth
            );
        }

        if (chest != null)
        {
            chest.rotation = Quaternion.Slerp(
                chest.rotation,
                chestTargetRot,
                Time.deltaTime * rotationSmooth
            );
        }

        if (leftHand != null)
        {
            leftHand.position = Vector3.Lerp(
                leftHand.position,
                leftHandTargetPos,
                Time.deltaTime * positionSmooth
            );

            leftHand.rotation = Quaternion.Slerp(
                leftHand.rotation,
                leftHandTargetRot,
                Time.deltaTime * rotationSmooth
            );
        }

        if (rightHand != null)
        {
            rightHand.position = Vector3.Lerp(
                rightHand.position,
                rightHandTargetPos,
                Time.deltaTime * positionSmooth
            );

            rightHand.rotation = Quaternion.Slerp(
                rightHand.rotation,
                rightHandTargetRot,
                Time.deltaTime * rotationSmooth
            );
        }
    }

    IEnumerator FetchLoop()
    {
        while (true)
        {
            yield return StartCoroutine(GetLatestData());
            yield return new WaitForSeconds(refreshRate);
        }
    }

    IEnumerator GetLatestData()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            request.timeout = 2;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("HTTP Error: " + request.error);
                yield break;
            }

            string json = request.downloadHandler.text;

            try
            {
                IoTData data = JsonUtility.FromJson<IoTData>(json);
                ApplyData(data);
            }
            catch (Exception e)
            {
                Debug.LogError("JSON Parse Error: " + e.Message);
                Debug.LogError("Received JSON: " + json);
            }
        }
    }

    void ApplyData(IoTData data)
    {
        if (data == null) return;

        switch (receiverMode)
        {
            case ReceiverMode.RightOnly:
                ApplyRightHand(data);
                break;

            case ReceiverMode.LeftOnly:
                ApplyLeftHand(data);
                break;

            case ReceiverMode.LeftAndRightOnly:
                ApplyLeftHand(data);
                ApplyRightHand(data);
                break;

            case ReceiverMode.ChestRotationOnly:
                ApplyChestRotation(data);
                break;

            case ReceiverMode.ChestPositionToRoot:
                ApplyChestPositionToRoot(data);
                break;

            case ReceiverMode.FullBody:
                ApplyChestPositionToRoot(data);
                ApplyChestRotation(data);
                ApplyLeftHand(data);
                ApplyRightHand(data);
                break;
        }
    }

    void ApplyChestPositionToRoot(IoTData data)
    {
        if (data.chest == null || data.chest.position == null) return;
        rootTargetPos = ToUnityPosition(data.chest.position);
    }

    void ApplyChestRotation(IoTData data)
    {
        if (data.chest == null || data.chest.rotation == null) return;
        chestTargetRot = ToUnityRotation(data.chest.rotation);
    }

    void ApplyLeftHand(IoTData data)
    {
        if (data.leftHand == null) return;

        if (data.leftHand.position != null)
            leftHandTargetPos = ToUnityPosition(data.leftHand.position);

        if (data.leftHand.rotation != null)
            leftHandTargetRot = ToUnityRotation(data.leftHand.rotation);
    }

    void ApplyRightHand(IoTData data)
    {
        if (data.rightHand == null) return;

        if (data.rightHand.position != null)
            rightHandTargetPos = ToUnityPosition(data.rightHand.position);

        if (data.rightHand.rotation != null)
            rightHandTargetRot = ToUnityRotation(data.rightHand.rotation);
    }

    Vector3 ToUnityPosition(PositionData p)
    {
        return new Vector3(
            p.x * scale,
            p.y * scale,
            p.z * scale
        ) + positionOffset;
    }

    Quaternion ToUnityRotation(RotationData r)
    {
        return Quaternion.Euler(
            r.pitch,
            r.yaw,
            r.roll
        );
    }
}

[Serializable]
public class IoTData
{
    public BodyPartData chest;
    public BodyPartData leftHand;
    public BodyPartData rightHand;
}

[Serializable]
public class BodyPartData
{
    public PositionData position;
    public RotationData rotation;
}

[Serializable]
public class PositionData
{
    public float x;
    public float y;
    public float z;
}

[Serializable]
public class RotationData
{
    public float pitch;
    public float yaw;
    public float roll;
}