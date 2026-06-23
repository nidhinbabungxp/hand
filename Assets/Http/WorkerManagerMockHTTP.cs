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
//[System.Serializable]
//public class SensorRaw
//{
//    public float x, y, z;
//    public string tag;
//}

//[System.Serializable]
//public class SensorReading
//{
//    public string timestamp;
//    public float distance;
//    public SensorRaw raw;
//}

//// ──────────────────────────────────────────────
//// Maps sensor tag "T1" → Unity worker ID "W01"
//// Fill this in Inspector
//// ──────────────────────────────────────────────
//[System.Serializable]
//public class TagMapping
//{
//    public string sensorTag;   // e.g. "T1"
//    public string workerId;    // e.g. "W01"
//}

//[System.Serializable]
//public class WorkerEntryHTTP
//{
//    public string id;
//    public GameObject obj;
//    [HideInInspector] public Vector3 targetPosition;
//    [HideInInspector] public Vector3 lastPosition;
//    [HideInInspector] public Animator anim;
//    [HideInInspector] public float currentSpeed;
//}

public class WorkerManagerMockHTTP : MonoBehaviour
{
    [Header("Mock Mode (no server needed)")]
    public bool useMock = false;
    public MockSensorServer mockServer; // drag MockSensorServer GameObject here

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
    public bool useRealSensorData = false;

    private Dictionary<string, WorkerEntryHTTP> entryMap = new();
    private Dictionary<string, string> tagToWorker = new();

    string statusMsg = "Waiting...";
    int msgCount = 0;

    [Header("OnGUI")]
    public float guiWidth;
    public float guiHeight;
    public float guiMarginX;
    public float guiMarginY;

    [Header("Dig Control")]
    public DigController digController;  // drag the character here

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

        if (useMock)
            StartCoroutine(MockLoop());
        else
            StartCoroutine(PollHTTP());
    }

    // ──────────────────────────────────────────────
    // MOCK LOOP — reads from MockSensorServer directly
    // ──────────────────────────────────────────────
    IEnumerator MockLoop()
    {
        statusMsg = "Mock mode active";
        Debug.Log("[MOCK] Running with MockSensorServer");

        while (true)
        {
            if (mockServer == null)
            {
                Debug.LogError("[MOCK] MockSensorServer not assigned in Inspector");
                yield break;
            }

            string json = mockServer.LatestJSON;

            if (!string.IsNullOrEmpty(json))
                ProcessJSON(json, "MOCK");

            yield return new WaitForSeconds(pollInterval);
        }
    }

    // ──────────────────────────────────────────────
    // HTTP LOOP — reads from real server
    // ──────────────────────────────────────────────
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
                ProcessJSON(req.downloadHandler.text, "HTTP");
            else
            {
                statusMsg = $"HTTP ERROR: {req.error}";
                Debug.LogError($"[HTTP] {req.error}");
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }

    // ──────────────────────────────────────────────
    // SHARED — same parsing for both mock and real
    // ──────────────────────────────────────────────
    void ProcessJSON(string json, string source)
    {

        // ✅ don't update position while worker is digging
        if (digController != null && digController.isDigging)
        {
            Debug.Log($"[{source}] Skipping — worker is digging");
            return;
        }

        try
        {
            SensorReading reading = JsonUtility.FromJson<SensorReading>(json);

            if (reading.raw == null)
            {
                Debug.Log($"[{source}] Skipping — no raw block: {json}");
                return;
            }

            string sensorTag = reading.raw?.tag ?? "";

            if (!tagToWorker.TryGetValue(sensorTag, out string workerId))
            {
                statusMsg = $"Unknown tag: '{sensorTag}'";
                Debug.LogWarning($"[{source}] No mapping for tag '{sensorTag}' — add in Inspector");
            }
            else if (entryMap.TryGetValue(workerId, out WorkerEntryHTTP entry))
            {
                if (!useRealSensorData)
                    entry.targetPosition = SensorToUnity(reading.raw.x, reading.raw.y, reading.raw.z);
                else
                    entry.targetPosition = RealSensorData(reading.raw.x, reading.raw.y, reading.raw.z);

                msgCount++;
                statusMsg = $"OK #{msgCount} | {workerId} | dist:{reading.distance:F2}m [{source}]";
            }
            else
            {
                statusMsg = $"Worker '{workerId}' not in list";
                Debug.LogWarning($"[{source}] Worker '{workerId}' not found in workerList");
            }
        }
        catch (System.Exception e)
        {
            statusMsg = "PARSE FAIL";
            Debug.LogError($"[{source}] Parse failed: {e.Message}");
        }
    }

    Vector3 SensorToUnity(float sx, float sy, float sz)
    {
        return new Vector3(
            sy * tunnelScale,
            floorY,
            (flipZ ? -sx : sx) * tunnelScale
        );
    }

    Vector3 RealSensorData(float sx, float sy, float sz)
    {
        return new Vector3(sx, sy, sz);
    }

    void Update()
    {

        if(digController == null || !digController.isDigging)
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
        else
        {
            print("Digging in progress — skipping position updates");
        }
       
    }

    void UpdateAnimation(WorkerEntryHTTP entry)
    {
        if (entry.anim == null) return;

        float moved = Vector3.Distance(entry.obj.transform.position, entry.lastPosition);
        float speed = 0f;

        // 2. SAFETY CHECK: Only divide if deltaTime is not zero
        if (Time.deltaTime > 0)
        {
            speed = moved / Time.deltaTime;
        }
        else
        {
            speed = 0f; // Default to 0 if the frame hasn't progressed
        }

        // 3. NAN GUARD: Double check that speed itself isn't NaN before setting the animator
        if (float.IsNaN(speed))
        {
            speed = 0f;
        }

        entry.lastPosition = entry.obj.transform.position;

        print("curSpeed " + speed);
        float maxSpeed = 3.0f;
        float normalizedSpeed = Mathf.Clamp01(speed / maxSpeed);

        entry.currentSpeed = Mathf.Lerp(
            entry.currentSpeed,
            normalizedSpeed,
            Time.deltaTime * 8f
        );

        entry.anim.SetFloat("speed", entry.currentSpeed);
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(guiMarginX, guiMarginY, guiWidth, guiHeight));

        GUI.color = statusMsg.StartsWith("OK") || statusMsg.StartsWith("Mock") ? Color.green : Color.red;
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