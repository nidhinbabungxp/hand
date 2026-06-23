//using MessagePack;
//using NativeWebSocket;
//using System.Collections;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//using UnityEngine;
//using UnityEngine.Networking;
//using static UnityEngine.EventSystems.EventTrigger;


//// ──────────────────────────────────────────────
//// WorkerData — used by BOTH msgpack and JSON
//// JsonUtility needs [System.Serializable]
//// MessagePack needs [MessagePackObject] + [Key]
//// Both attributes are compatible on the same class
//// ──────────────────────────────────────────────
//[MessagePackObject]
//[System.Serializable]
//public class WorkerData
//{
//    [Key("id")] public string id;
//    [Key("x")] public float x;
//    [Key("y")] public float y;
//    [Key("z")] public float z;
//    [Key("s")] public byte s;

//    // Uncomment when IMU rotation data is ready
//    // [Key("rx")] public float rx;
//    // [Key("ry")] public float ry;
//    // [Key("rz")] public float rz;
//}

//// Wrapper needed because JsonUtility cannot
//// deserialize a root-level array directly
//// Server must send: {"workers": [...]}

//[System.Serializable]
//public class WorkerBatch
//{
//    public List<WorkerData> workers;
//}

//// ──────────────────────────────────────────────────────────────────
//// RealSensorRaw — the "raw" block inside the real sensor JSON
//// {
////   "timestamp": "2026-04-25T14:27:55.520785",
////   "distance": 7.73,
////   "raw": {
////     "distance": 7.73,
////     "x": 5.07,   ← position already solved server-side
////     "y": 3.68,
////     "z": -4.53,
////     "d1-d4": ..., "rssi1-4": ...,
////     "tag": "T1"
////   }
//// }
//// ──────────────────────────────────────────────────────────────────
//[System.Serializable]
//public class RealSensorRaw
//{
//    public float distance;
//    public float x;
//    public float y;
//    public float z;
//    public float d1, d2, d3, d4;        // per-anchor distances (ignore for now)
//    public int rssi1, rssi2, rssi3, rssi4; // signal strength (ignore for now)
//    public string tag;                    // "T1", "T2" etc
//}

//[System.Serializable]
//public class RealSensorReading
//{
//    public string timestamp;
//    public float distance;         // top-level filtered distance
//    public RealSensorRaw raw;
//}


//[System.Serializable]
//public class WorkerEntry
//{
//    public string id;
//    public GameObject obj;
//    [HideInInspector] public Vector3 targetPosition;
//    [HideInInspector] public Vector3 targetRotation;
//    [HideInInspector] public Vector3 lastPosition; // ✅ for speed calculation
//    [HideInInspector] public Animator anim;
//    [HideInInspector] public float currentSpeed = 0;  // ✅ for smooth blend
//    [HideInInspector] public byte targetStatus;
//}

//public class WorkerManager : MonoBehaviour
//{
//    public bool useWorkerManagerForReceivingData = false; // toggle this to switch between receiving data via WebSocket/HTTP vs direct method calls from UWBSensorBridge

//    [Header("HTTP Polling (Real Sensor)")]
//    public bool useHTTP = false;
//    public string httpURL = "http://192.168.1.100/api/workers/latest";
//    public float pollInterval = 0.1f;  // 10Hz

//    // ── Tag ID mapping
//    // Real sensor sends tag as "T1", "T2" etc
//    // Map those to your Unity worker IDs "W01", "W02" etc
//    // Add entries here to match however many tags you have
//    [Header("Tag ID Mapping  (sensor tag → Unity worker ID)")]
//    public List<TagMapping> tagMappings = new List<TagMapping>();

//    [Header("Unity Coordinate Mapping")]
//    // Their server gives x,y,z in sensor/real-world space
//    // Adjust these to match where your Unity tunnel starts
//    public float floorY = -5.19972324f;
//    public float tunnelScale = 1.0f;
//    public bool flipZ = true;   // sensor +x → Unity -z (tunnel goes -z)

//    WebSocket ws;

//    [Header("Worker Setup")]
//    public List<WorkerEntry> workerList = new List<WorkerEntry>();
//    public float smoothSpeed = 8f;

//    // ✅ Toggle this in Inspector:
//    // true  = server sends MessagePack binary  (current Python server)
//    // false = server sends plain JSON text
//    [Header("Data Format")]
//    public bool useMsgPack = true;
//    public bool useAnimViaUnity = true;
//    public bool updatePosOnly = true;


//    private Dictionary<string, WorkerEntry> entryMap = new();
//    private Dictionary<string, string> tagToWorker = new();  // "T1" → "W01"

//    // For OnGUI display only
//    List<WorkerData> latestWorkers = new();
//    string statusMsg = "Connecting...";
//    int msgCount = 0;

//    // ─────────────────────────────────────────────────────────
//    // PUBLIC — called by UWBSensorBridge to push real positions
//    // UWBSensorBridge handles distance → position conversion
//    // This just receives the final Unity position
//    // ─────────────────────────────────────────────────────────
//    public void SetTargetPosition(string workerId, Vector3 position)
//    {
//        if (entryMap.TryGetValue(workerId, out WorkerEntry entry))
//        {
//            entry.targetPosition = position;
//            statusMsg = $"UWB live — {workerId}";
//        }
//        else
//        {
//            Debug.LogWarning($"[WM] Unknown worker ID from UWB bridge: '{workerId}'");
//        }
//    }

//    async void Start()
//    {
//        foreach (var entry in workerList)
//        {
//            entryMap[entry.id] = entry;
//            entry.targetPosition = entry.obj.transform.position;
//            entry.targetRotation = entry.obj.transform.eulerAngles;
//            entry.lastPosition = entry.obj.transform.position;
//            if (entry.obj.TryGetComponent(out Animator anim))
//                entry.anim = anim; // ✅ cache once
//        }

//        if (!useWorkerManagerForReceivingData)
//        {
//            Debug.LogWarning("WorkerManager is set to receive data directly via method calls. WebSocket/HTTP will be disabled.");
//            return;
//        }

//        if (useHTTP)
//            StartCoroutine(PollHTTP());
//        else
//            await ConnectWebSocket();


//    }

//    // ──────────────────────────────────────────────────────────────
//    // COORDINATE MAPPING
//    // Converts real sensor x,y,z → Unity position
//    // Their x = along tunnel → Unity -z
//    // Their y = width        → Unity x
//    // Their z = height       → ignored, use floorY
//    // ──────────────────────────────────────────────────────────────
//    Vector3 SensorToUnity(float sx, float sy, float sz)
//    {
//        float ux = sy * tunnelScale;                    // width  → Unity x
//        float uy = floorY;                               // fixed floor
//        float uz = (flipZ ? -sx : sx) * tunnelScale;    // along tunnel → Unity ±z

//        return new Vector3(ux, uy, uz);
//    }

//    // ──────────────────────────────────────────────────────────────
//    // HTTP POLLING — real sensor data
//    // Parses RealSensorReading and pushes to entryMap
//    // ──────────────────────────────────────────────────────────────
//    IEnumerator PollHTTP()
//    {
//        while (true)
//        {
//            UnityWebRequest req = UnityWebRequest.Get(httpURL);
//            yield return req.SendWebRequest();

//            bool shouldWait = true;

//            if (req.result == UnityWebRequest.Result.Success)
//            {
//                try
//                {
//                    string raw = req.downloadHandler.text;
//                    Debug.Log($"[HTTP] Raw JSON: {raw}");

//                    RealSensorReading reading = JsonUtility.FromJson<RealSensorReading>(raw);

//                    // resolve tag ID → worker ID
//                    // "T1" → "W01" using tagMappings, fallback to raw tag string
//                    string sensorTag = reading.raw?.tag ?? "T1";
//                    if (!tagToWorker.TryGetValue(sensorTag, out string workerId))
//                    {
//                        Debug.LogWarning($"[HTTP] No mapping for tag '{sensorTag}' — add it to Tag Mappings in Inspector");
//                        shouldWait = false;
//                    }
//                    else if (entryMap.TryGetValue(workerId, out WorkerEntry entry))
//                    {
//                        // position already solved by their server — just map coords
//                        Vector3 pos = SensorToUnity(reading.raw.x, reading.raw.y, reading.raw.z);
//                        entry.targetPosition = pos;

//                        msgCount++;
//                        statusMsg = $"OK — msg #{msgCount} | tag:{sensorTag} dist:{reading.distance:F2}m";

//                        Debug.Log($"[HTTP] {workerId} → sensor({reading.raw.x:F2},{reading.raw.y:F2},{reading.raw.z:F2}) → unity{pos}");
//                    }
//                    else
//                    {
//                        Debug.LogWarning($"[HTTP] Worker '{workerId}' not in workerList");
//                    }
//                }
//                catch (System.Exception e)
//                {
//                    statusMsg = "PARSE FAIL";
//                    Debug.LogError($"[HTTP] Parse failed: {e.Message}");
//                }
//            }
//            else
//            {
//                statusMsg = $"HTTP ERROR: {req.error}";
//                Debug.LogError($"[HTTP] {req.error}");
//            }

//            if (shouldWait)
//                yield return new WaitForSeconds(pollInterval);
//        }
//    }


//    async Task ConnectWebSocket()
//    {
//        ws = new WebSocket("ws://localhost:8765");

//        ws.OnOpen += () =>
//        {
//            statusMsg = "Connected ✓";
//            Debug.Log("[WS] Connected!");
//        };

//        ws.OnError += (e) =>
//        {
//            statusMsg = $"ERROR: {e}";
//            Debug.LogError("[WS] Error: " + e);
//        };

//        ws.OnClose += (e) =>
//        {
//            statusMsg = $"Closed: {e}";
//            Debug.Log("[WS] Disconnected");
//        };

//        ws.OnMessage += (bytes) =>
//        {
//            Debug.Log($"[WS] Raw bytes: {bytes.Length}");

//            try
//            {
//                List<WorkerData> data;

//                if (useMsgPack)
//                {
//                    // ── MSGPACK PATH ──────────────────────────────────────
//                    // Current Python server sends binary MessagePack frames
//                    data = MessagePackSerializer.Deserialize<List<WorkerData>>(bytes);
//                }
//                else
//                {
//                    // ── JSON PATH ─────────────────────────────────────────
//                    // For servers that send plain JSON text over WebSocket
//                    // Expected format:
//                    // {"workers":[{"id":"W00","x":1.0,"y":0.0,"z":2.0,"s":1}]}
//                    string json = System.Text.Encoding.UTF8.GetString(bytes);
//                    Debug.Log($"[WS] JSON text: {json}");
//                    WorkerBatch batch = JsonUtility.FromJson<WorkerBatch>(json);
//                    data = batch.workers;
//                }

//                latestWorkers = data;
//                msgCount++;
//                statusMsg = $"OK — msg #{msgCount} ({(useMsgPack ? "MsgPack" : "JSON")})";
//                Debug.Log($"[WS] Deserialized {data.Count} workers");

//                foreach (var w in data)
//                {
//                    if (entryMap.TryGetValue(w.id, out WorkerEntry entry))
//                    {
//                        entry.targetPosition = new Vector3(w.x, w.y, w.z);
//                        entry.targetStatus = w.s; // store status for Update() to use
//                        // Uncomment when rotation fields are added to WorkerData
//                        // entry.targetRotation = new Vector3(w.rx, w.ry, w.rz);
//                    }
//                    else
//                    {
//                        Debug.LogWarning($"[WS] Unknown worker ID: '{w.id}' — check Inspector IDs match server");
//                    }
//                }
//            }
//            catch (System.Exception e)
//            {
//                statusMsg = "DESERIALIZE FAIL";
//                Debug.LogError($"[WS] Deserialize FAILED: {e.Message}");
//            }
//        };

//        await ws.Connect();
//    }

//    //IEnumerator PollHTTP()
//    //{
//    //    while (true)
//    //    {
//    //        UnityWebRequest req = UnityWebRequest.Get(httpURL);
//    //        yield return req.SendWebRequest();

//    //        if (req.result == UnityWebRequest.Result.Success)
//    //        {
//    //            try
//    //            {
//    //                // reuses your existing JSON path
//    //                WorkerBatch batch = JsonUtility.FromJson<WorkerBatch>(req.downloadHandler.text);
//    //                latestWorkers = batch.workers;
//    //                msgCount++;
//    //                statusMsg = $"OK — msg #{msgCount} (HTTP)";

//    //                foreach (var w in batch.workers)
//    //                {
//    //                    if (entryMap.TryGetValue(w.id, out WorkerEntry entry))
//    //                    {
//    //                        entry.targetPosition = new Vector3(w.x, w.y, w.z);
//    //                        entry.targetStatus = w.s;
//    //                    }
//    //                    else
//    //                    {
//    //                        Debug.LogWarning($"[HTTP] Unknown ID: '{w.id}'");
//    //                    }
//    //                }
//    //            }
//    //            catch (System.Exception e)
//    //            {
//    //                statusMsg = "PARSE FAIL";
//    //                Debug.LogError($"[HTTP] Parse failed: {e.Message}");
//    //            }
//    //        }
//    //        else
//    //        {
//    //            statusMsg = $"HTTP ERROR: {req.error}";
//    //            Debug.LogError($"[HTTP] {req.error}");
//    //        }

//    //        yield return new WaitForSeconds(pollInterval);
//    //    }

//    //}

//    void Update()
//    {

//#if !UNITY_WEBGL || UNITY_EDITOR
//        if (useWorkerManagerForReceivingData)
//            ws.DispatchMessageQueue();
//#endif
//        if (updatePosOnly)
//        {
//            // Flat loop — no dictionary lookup per frame, just pure math
//            for (int i = 0; i < workerList.Count; i++)
//            {
//                WorkerEntry entry = workerList[i];

//                entry.obj.transform.position = Vector3.Lerp(
//                    entry.obj.transform.position,
//                    entry.targetPosition,
//                    Time.deltaTime * smoothSpeed
//                );

//                if (entry.anim == null) continue;

//                float targetSpeed = AnimationVia(entry);

//                // smooth the speed value so blend tree transitions feel natural
//                entry.currentSpeed = Mathf.Lerp(
//                    entry.currentSpeed,
//                    targetSpeed,
//                    Time.deltaTime * 8f
//                );

//                // push to blend tree — one SetFloat per worker per frame
//                entry.anim.SetFloat("speed", entry.currentSpeed < 0.1 ? 0 : entry.currentSpeed);

//                // Uncomment when rotation is ready
//                // entry.obj.transform.rotation = Quaternion.Lerp(
//                //     entry.obj.transform.rotation,
//                //     Quaternion.Euler(entry.targetRotation),
//                //     Time.deltaTime * smoothSpeed
//                // );
//            }
//        }
//        else
//        {
//            for (int i = 0; i < workerList.Count; i++)
//            {
//                WorkerEntry entry = workerList[i];

//                // ── POSITION ─────────────────────────────────
//                Vector3 newPosition = Vector3.Lerp(
//                    entry.obj.transform.position,
//                    entry.targetPosition,
//                    Time.deltaTime * smoothSpeed
//                );

//                // ── ROTATION ─────────────────────────────────
//                float movedDistance = Vector3.Distance(newPosition, entry.lastPosition);
//                float speed = movedDistance / Time.deltaTime;

//                Quaternion newRotation = entry.obj.transform.rotation; // default keep current

//                // only rotate if actually moving — avoids snapping to zero when idle
//                if (movedDistance > 0.01f)
//                {
//                    Vector3 moveDir = (newPosition - entry.lastPosition).normalized;
//                    // only rotate on XZ plane — ignore any Y drift
//                    moveDir.y = 0;

//                    if (moveDir != Vector3.zero)
//                    {
//                        Quaternion targetRot = Quaternion.LookRotation(moveDir);
//                        newRotation = Quaternion.Slerp(
//                            entry.obj.transform.rotation,
//                            targetRot,
//                            Time.deltaTime * smoothSpeed
//                        );
//                    }
//                }



//                if (entry.anim == null) continue;

//                float targetSpeed = AnimationVia(entry);

//                // smooth the speed value so blend tree transitions feel natural
//                entry.currentSpeed = Mathf.Lerp(
//                    entry.currentSpeed,
//                    targetSpeed,
//                    Time.deltaTime * 8f
//                );

//                // ── APPLY BOTH AT ONCE ────────────────────────
//                entry.obj.transform.SetPositionAndRotation(newPosition, newRotation);

//                // ── STORE FOR NEXT FRAME ──────────────────────
//                entry.lastPosition = newPosition;

//                // push to blend tree — one SetFloat per worker per frame
//                entry.anim.SetFloat("speed", entry.currentSpeed < 0.1 ? 0 : entry.currentSpeed);

//            }
//        }

//    }

//    public float AnimationVia(WorkerEntry entry)
//    {
//        float targetSpeed = 0;

//        if (useAnimViaUnity)
//        {
//            // ✅ how far did we actually move this frame
//            float movedDistance = Vector3.Distance(
//                entry.obj.transform.position,
//                entry.lastPosition
//            );
//            // convert to units/sec so threshold is frame-rate independent
//            float speed = movedDistance / Time.deltaTime;
//            print(speed);
//            // drive blend tree from actual movement — no server status needed
//            targetSpeed = speed < 0.05f ? 0f      // idle
//                             : speed < 3.0f ? 0.5f    // walking
//                             : 1.0f;                    // running

//            entry.lastPosition = entry.obj.transform.position;
//        }
//        else
//        {
//            // ✅ convert status byte to target speed float
//            // s: 0=idle → 0.0, 1=walking → 0.5, 2=running → 1.0
//            targetSpeed = entry.targetStatus == 0 ? 0f
//                             : entry.targetStatus == 1 ? 0.5f
//                             : 1.0f;
//        }

//        return targetSpeed;

//    }

//    void OnGUI()
//    {
//        GUILayout.BeginArea(new Rect(10, 10, 340, 420));

//        GUI.color = statusMsg.StartsWith("OK") || statusMsg.StartsWith("Connected") /*|| statusMsg.StartsWith("UWB")*/
//            ? Color.green : Color.red;
//       // GUILayout.Label($"WebSocket: {statusMsg}");
       

//         GUILayout.Label($"Mode: {(useHTTP ? "HTTP polling " + statusMsg : "WebSocket " + statusMsg)}  |  Format: {(useMsgPack ? "MsgPack" : "JSON")}");
//        GUI.color = Color.white;
//        GUILayout.Label($"Workers: {latestWorkers.Count}");
//        GUILayout.Space(6);

//        foreach (var w in latestWorkers)
//        {
//            string state = w.s == 0 ? "idle" : w.s == 1 ? "walking" : "working";
//            GUILayout.Label($"{w.id}  x:{w.x:F1}  z:{w.z:F1}  [{state}]");
//        }

//        GUILayout.EndArea();
//    }

//    private async Task CloseWebSocketIfNeeded()
//    {
//        var socket = ws; // capture local reference
//        if (socket == null)
//            return;

//        try
//        {
//            // optional: only close if still open/connecting
//            if (socket.State == WebSocketState.Open || socket.State == WebSocketState.Connecting)
//                await socket.Close();
//        }
//        catch (System.Exception ex)
//        {
//            Debug.LogWarning($"[WS] Close failed: {ex}");
//        }
//    }

//    // ──────────────────────────────────────────────────────────────
//    // CLEANUP
//    // ──────────────────────────────────────────────────────────────

//    async void OnDestroy() => await CloseWebSocketIfNeeded();
//    async void OnApplicationQuit() => await CloseWebSocketIfNeeded();

//    // ──────────────────────────────────────────────────────────────────
//    // TagMapping — shown in Inspector
//    // Maps sensor tag string → Unity worker ID
//    // e.g. "T1" → "W01"
//    // ──────────────────────────────────────────────────────────────────
//    [System.Serializable]
//    public class TagMapping
//    {
//        public string sensorTag;   // what comes in JSON:  "T1"
//        public string workerId;    // your Unity worker ID: "W01"
//    }
//}