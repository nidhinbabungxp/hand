using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// Polls HTTP for IMU sensor data (CHEST, LEFT, RIGHT).
public class IMUDataSource : MonoBehaviour, IMotionSource
{
    public enum EndpointMode { Combined, Separate, Rian }

    public Quaternion rianRotation = Quaternion.identity;

    [Header("Endpoint Mode")]
    public EndpointMode endpointMode = EndpointMode.Combined;

    [Header("Combined Mode")]
    public string combinedUrl = "http://192.168.1.100:8080/sensors";

    [Header("Separate Mode")]
    public string chestUrl = "http://192.168.1.100:8080/sensor/chest";
    public string leftHandUrl = "http://192.168.1.100:8080/sensor/left";
    public string rightHandUrl = "http://192.168.1.100:8080/sensor/right";

    [Header("Rian Mode")]
    public string rianUrl = "http://192.168.1.100:8080/sensors";

    [Header("Polling")]
    public float pollInterval = 0.05f;

    [Header("Sensor Location Names")]
    public string chestName = "CHEST";
    public string leftName = "LEFT_HAND";
    public string rightName = "RIGHT_HAND";

    [Header("Euler Convention")]
    public Vector3 chestEulerOffset = Vector3.zero;
    public Vector3 leftHandEulerOffset = Vector3.zero;
    public Vector3 rightHandEulerOffset = Vector3.zero;

    [Header("Position Estimation")]
    public float armLength = 0.55f;

    [Header("Debug")]
    public bool logData = false;

    [Tooltip("Print received chest, left hand, and right hand data.")]
    public bool printReceivedMotionData = true;

    public Quaternion ChestRotation { get; private set; } = Quaternion.identity;
    public Quaternion LeftHandRotation { get; private set; } = Quaternion.identity;
    public Quaternion RightHandRotation { get; private set; } = Quaternion.identity;

    public Vector3 ChestPosition { get; private set; } = Vector3.zero;
    public Vector3 LeftHandPosition { get; private set; } = Vector3.zero;
    public Vector3 RightHandPosition { get; private set; } = Vector3.zero;

    public Transform cube3D;

    public float changeSignX = 1f;
    public float changeSignY = 1f;
    public float changeSignZ = 1f;

    public static bool firstTime;

    private void Start()
    {
        StartCoroutine(PollLoop());
    }

    private IEnumerator PollLoop()
    {
        while (true)
        {
            if (endpointMode == EndpointMode.Combined)
            {
                yield return StartCoroutine(FetchUrl(combinedUrl));
            }
            else if (endpointMode == EndpointMode.Separate)
            {
                Coroutine c1 = StartCoroutine(FetchUrl(chestUrl));
                Coroutine c2 = StartCoroutine(FetchUrl(leftHandUrl));
                Coroutine c3 = StartCoroutine(FetchUrl(rightHandUrl));

                yield return c1;
                yield return c2;
                yield return c3;
            }
            else
            {
                yield return StartCoroutine(FetchUrl(rianUrl));
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }

    private IEnumerator FetchUrl(string url)
    {
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 2;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                if (logData)
                {
                    Debug.LogWarning($"IMUDataSource: {url} -> {req.error}");
                }

                yield break;
            }

            string json = req.downloadHandler.text;

            if (logData)
            {
                Debug.Log("Received raw IMU JSON: " + json);
            }

            if (endpointMode != EndpointMode.Rian)
            {
                ParseResponse(json);
            }
            else
            {
                IMUSensorData data = JsonUtility.FromJson<IMUSensorData>(json);

                if (data == null || !data.IsValid())
                {
                    Debug.LogWarning("Rian data is invalid.");
                    yield break;
                }

                rianRotation = new Quaternion(data.qx, data.qy, data.qz, data.qw);

                if (printReceivedMotionData)
                {
                    Debug.Log(
                        $"[RIAN RECEIVED] " +
                        $"Location={data.location} | " +
                        $"State={data.state} | " +
                        $"Quat=({data.qx:F4}, {data.qy:F4}, {data.qz:F4}, {data.qw:F4})"
                    );
                }

                ApplyReading();
            }
        }
    }

    private void ParseResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        string trimmed = json.Trim();

        if (trimmed.StartsWith("["))
        {
            SensorReading[] readings = JsonArrayHelper.FromJson<SensorReading>(trimmed);

            foreach (SensorReading r in readings)
            {
                ApplyReading(r);
            }
        }
        else if (trimmed.StartsWith("{"))
        {
            ActivityResponse resp = JsonUtility.FromJson<ActivityResponse>(trimmed);

            if (resp != null && resp.activity != null && resp.activity.chest != null)
            {
                ApplyReading(resp.activity.chest, chestName);
                ApplyReading(resp.activity.left_hand, leftName);
                ApplyReading(resp.activity.right_hand, rightName);
            }
            else
            {
                SensorReading r = JsonUtility.FromJson<SensorReading>(trimmed);
                ApplyReading(r);
            }
        }
    }

    private void ApplyReading(SensorReading r, string forcedLocation = null)
    {
        if (r == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(forcedLocation))
        {
            r.location = forcedLocation;
        }

        if (string.Equals(r.location, chestName, StringComparison.OrdinalIgnoreCase))
        {
            Quaternion quaternion = new Quaternion(
                r.rotation.qx,
                -r.rotation.qy,
                r.rotation.qz,
                r.rotation.qw
            );

            ChestRotation = quaternion;

            if (printReceivedMotionData)
            {
                Debug.Log(
                    $"[CHEST RECEIVED] " +
                    $"Rotation Quaternion=({r.rotation.qx:F4}, {r.rotation.qy:F4}, {r.rotation.qz:F4}, {r.rotation.qw:F4}) | " +
                    $"Euler={ChestRotation.eulerAngles}"
                );
            }
        }
        else if (string.Equals(r.location, leftName, StringComparison.OrdinalIgnoreCase))
        {
            Quaternion quaternion = new Quaternion(
                r.rotation.qx,
                r.rotation.qy,
                r.rotation.qz,
                r.rotation.qw
            );

            LeftHandRotation = quaternion;

            Vector3 pos = new Vector3(
                changeSignX * r.position_abs.x,
                changeSignY * r.position_abs.z,
                changeSignZ * r.position_abs.y
            );

            pos.y = Mathf.Clamp(pos.y, 0.27f, 1.14f);

            LeftHandPosition = pos;

            if (printReceivedMotionData)
            {
                Debug.Log(
                    $"[LEFT HAND RECEIVED] " +
                    $"RawAbsPosition=({r.position_abs.x:F3}, {r.position_abs.y:F3}, {r.position_abs.z:F3}) | " +
                    $"AppliedPosition={LeftHandPosition} | " +
                    $"Rotation Quaternion=({r.rotation.qx:F4}, {r.rotation.qy:F4}, {r.rotation.qz:F4}, {r.rotation.qw:F4}) | " +
                    $"Euler={LeftHandRotation.eulerAngles}"
                );
            }
        }
        else if (string.Equals(r.location, rightName, StringComparison.OrdinalIgnoreCase))
        {
            Quaternion quaternion = new Quaternion(
                r.rotation.qx,
                r.rotation.qy,
                r.rotation.qz,
                r.rotation.qw
            );

            if (endpointMode == EndpointMode.Rian)
            {
                RightHandRotation = rianRotation;
            }
            else
            {
                RightHandRotation = quaternion;
            }

            RightHandPosition = new Vector3(
                changeSignX * r.position_abs.x,
                changeSignY * r.position_abs.z,
                changeSignZ * r.position_abs.y
            );

            if (cube3D != null)
            {
                cube3D.position = RightHandPosition;
            }

            firstTime = true;

            if (printReceivedMotionData)
            {
                Debug.Log(
                    $"[RIGHT HAND RECEIVED] " +
                    $"RawAbsPosition=({r.position_abs.x:F3}, {r.position_abs.y:F3}, {r.position_abs.z:F3}) | " +
                    $"AppliedPosition={RightHandPosition} | " +
                    $"Rotation Quaternion=({r.rotation.qx:F4}, {r.rotation.qy:F4}, {r.rotation.qz:F4}, {r.rotation.qw:F4}) | " +
                    $"Euler={RightHandRotation.eulerAngles}"
                );
            }
        }
        else
        {
            if (printReceivedMotionData)
            {
                Debug.LogWarning($"[UNKNOWN IMU RECEIVED] Location={r.location}");
            }
        }
    }

    private void ApplyReading()
    {
        RightHandRotation = rianRotation;

        if (printReceivedMotionData)
        {
            Debug.Log($"[RIAN RIGHT HAND RECEIVED] Rotation={RightHandRotation.eulerAngles}");
        }
    }

    [Serializable]
    public class ActivityResponse
    {
        public ActivityData activity;
    }

    [Serializable]
    public class ActivityData
    {
        public SensorReading chest;
        public SensorReading left_hand;
        public SensorReading right_hand;
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
        public float qw;
        public float qx;
        public float qy;
        public float qz;
    }

    [Serializable]
    public class AbsRotationData
    {
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public class SensorReading
    {
        public string location;
        public PositionData position;
        public AbsRotationData position_abs;
        public RotationData rotation;
    }

    [Serializable]
    public class IMUSensorData
    {
        public float qw;
        public float qx;
        public float qy;
        public float qz;

        public string state;
        public string location;

        public float ax = 0;
        public float ay = 0;
        public float az = 0;

        public string gesture = "NONE";

        public bool IsValid()
        {
            return !float.IsNaN(qw) && !string.IsNullOrEmpty(state);
        }
    }

    public static class JsonArrayHelper
    {
        public static T[] FromJson<T>(string json)
        {
            string wrapped = "{\"items\":" + json + "}";
            Wrapper<T> w = JsonUtility.FromJson<Wrapper<T>>(wrapped);
            return w.items;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }
    }
}