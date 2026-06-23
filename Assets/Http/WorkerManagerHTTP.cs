using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// ──────────────────────────────────────────────
// Matches the real sensor JSON:
// {
//   "timestamp": "...",
//   "distance": 7.73,
//   "raw": {
//     "x": 5.07, "y": 3.68, "z": -4.53,
//     "tag": "T1", ...
//   }
// }
// ──────────────────────────────────────────────
[System.Serializable]
public class SensorRaw
{
    public float x, y, z;
    public string tag;
}

[System.Serializable]
public class SensorReading
{
    public string timestamp;
    public float distance;
    public SensorRaw raw;
}

// ──────────────────────────────────────────────
// Maps sensor tag "T1" → Unity worker ID "W01"
// Fill this in Inspector
// ──────────────────────────────────────────────
[System.Serializable]
public class TagMapping
{
    public string sensorTag;   // e.g. "T1"
    public string workerId;    // e.g. "W01"
}

[System.Serializable]
public class WorkerEntryHTTP
{
    public string id;
    public GameObject obj;
    [HideInInspector] public Vector3 targetPosition;
    [HideInInspector] public Vector3 lastPosition;
    [HideInInspector] public Animator anim;
    [HideInInspector] public float currentSpeed;
}

public class WorkerManagerHTTP : MonoBehaviour
{
    [Header("HTTP")]
    public string httpURL = "http://192.168.1.100/api/latest";
    public float pollInterval = 0.125f;   // 8 Hz

    [Header("Tag Mapping  (sensor tag → worker ID)")]
    public List<TagMapping> tagMappings = new List<TagMapping>();

    [Header("Workers")]
    public List<WorkerEntryHTTP> workerList = new List<WorkerEntryHTTP>();
    public float smoothSpeed = 8f;
    public bool updatePosAndRot = false;

    [Header("Coordinate Mapping")]
    public float floorY = -5.19972324f;
    public float tunnelScale = 1.0f;
    public bool flipZ = true;

    private Dictionary<string, WorkerEntryHTTP> entryMap = new();
    private Dictionary<string, string> tagToWorker = new();

    string statusMsg = "Waiting...";
    int msgCount = 0;

    void Start()
    {
        foreach (var entry in workerList)
        {
            entryMap[entry.id] = entry;
            entry.targetPosition = entry.obj.transform.position;
            entry.lastPosition = entry.obj.transform.position;
            if (entry.obj.TryGetComponent(out Animator anim))
                entry.anim = anim;
        }

        foreach (var m in tagMappings)
            tagToWorker[m.sensorTag] = m.workerId;

        StartCoroutine(PollHTTP());
    }

    IEnumerator PollHTTP()
    {
        while (true)
        {
            UnityWebRequest req = UnityWebRequest.Get(httpURL);
            yield return req.SendWebRequest();

            Debug.Log($"[HTTP] Response code: {req.responseCode}");
            Debug.Log($"[HTTP] Result: {req.result}");
            Debug.Log($"[HTTP] Error: {req.error}");
            Debug.Log($"[HTTP] Body: {req.downloadHandler.text}");

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    SensorReading reading = JsonUtility.FromJson<SensorReading>(req.downloadHandler.text);

                    string sensorTag = reading.raw?.tag ?? "";

                    if (!tagToWorker.TryGetValue(sensorTag, out string workerId))
                    {
                        statusMsg = $"Unknown tag: '{sensorTag}'";
                        Debug.LogWarning($"[HTTP] No mapping for tag '{sensorTag}' — add in Inspector");
                    }
                    else if (entryMap.TryGetValue(workerId, out WorkerEntryHTTP entry))
                    {
                        entry.targetPosition = SensorToUnity(reading.raw.x, reading.raw.y, reading.raw.z);
                        msgCount++;
                        statusMsg = $"OK #{msgCount} | {workerId} | dist:{reading.distance:F2}m";
                    }
                    else
                    {
                        statusMsg = $"Worker '{workerId}' not in list";
                        Debug.LogWarning($"[HTTP] Worker '{workerId}' not found in workerList");
                    }
                }
                catch (System.Exception e)
                {
                    statusMsg = "PARSE FAIL";
                    Debug.LogError($"[HTTP] Parse failed: {e.Message}");
                }
            }
            else
            {
                statusMsg = $"HTTP ERROR: {req.error}";
                Debug.LogError($"[HTTP] {req.error}");
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }

    Vector3 SensorToUnity(float sx, float sy, float sz)
    {
        return new Vector3(
            sx,                      // sensor y (width)  → Unity x
            sy,                                 // fixed floor
            sz//(flipZ ? -sx : sx) * tunnelScale        // sensor x (length) → Unity ±z
        );
    }

    void Update()
    {
        if (!updatePosAndRot)
        {
            for (int i = 0; i < workerList.Count; i++)
            {
                WorkerEntryHTTP entry = workerList[i];

                entry.obj.transform.position = Vector3.Lerp(
                    entry.obj.transform.position,
                    entry.targetPosition,
                    Time.deltaTime * smoothSpeed
                );

                UpdateAnimation(entry);
            }
        }
        else
        {
            for (int i = 0; i < workerList.Count; i++)
            {
                WorkerEntryHTTP entry = workerList[i];

                Vector3 newPos = Vector3.Lerp(
                    entry.obj.transform.position,
                    entry.targetPosition,
                    Time.deltaTime * smoothSpeed
                );

                Quaternion newRot = entry.obj.transform.rotation;
                float moved = Vector3.Distance(newPos, entry.lastPosition);

                if (moved > 0.01f)
                {
                    Vector3 dir = (newPos - entry.lastPosition).normalized;
                    dir.y = 0;
                    if (dir != Vector3.zero)
                    {
                        newRot = Quaternion.Slerp(
                            entry.obj.transform.rotation,
                            Quaternion.LookRotation(dir),
                            Time.deltaTime * smoothSpeed
                        );
                    }
                }

                entry.obj.transform.SetPositionAndRotation(newPos, newRot);
                entry.lastPosition = newPos;

                UpdateAnimation(entry);
            }
        }
    }

    void UpdateAnimation(WorkerEntryHTTP entry)
    {
        if (entry.anim == null) return;

        float moved = Vector3.Distance(entry.obj.transform.position, entry.lastPosition);
        float speed = moved / Time.deltaTime;
        entry.lastPosition = entry.obj.transform.position;

        float target = speed < 0.05f ? 0f : speed < 3.0f ? 0.5f : 1.0f;
        entry.currentSpeed = Mathf.Lerp(entry.currentSpeed, target, Time.deltaTime * 8f);
        entry.anim.SetFloat("speed", entry.currentSpeed < 0.05f ? 0f : entry.currentSpeed);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 360, 400));

        GUI.color = statusMsg.StartsWith("OK") ? Color.green : Color.red;
        GUILayout.Label($"Status: {statusMsg}");
        GUI.color = Color.white;

        GUILayout.Label($"URL: {httpURL}");
        GUILayout.Space(6);

        foreach (var entry in workerList)
        {
            Vector3 p = entry.targetPosition;
            GUILayout.Label($"{entry.id}  →  x:{p.x:F2}  y:{p.y:F2}  z:{p.z:F2}");
        }

        GUILayout.EndArea();
    }
}