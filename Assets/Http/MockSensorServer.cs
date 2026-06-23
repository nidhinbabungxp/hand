using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ──────────────────────────────────────────────────────────────────
// MockSensorServer
//
// Imitates the real sensor HTTP server locally inside Unity.
// Simulates a worker moving back and forth in the tunnel,
// producing the same JSON format as the real server:
//
// {
//   "timestamp": "2026-04-27T15:46:08.968409",
//   "distance": 0.71,
//   "raw": {
//     "distance": 0.71,
//     "x": 0.48, "y": 0.41, "z": 0.32,
//     "d1":0.3, "d2":0.37, "d3":0.22, "d4":0.27,
//     "rssi1":-52,"rssi2":-54,"rssi3":-49,"rssi4":-51,
//     "tag": "T1"
//   }
// }
//
// HOW TO USE:
// 1. Add this script to any GameObject in your scene
// 2. In WorkerManagerHTTP Inspector, tick "Use Mock" and
//    it will call GetLatestJSON() instead of HTTP
// ──────────────────────────────────────────────────────────────────

public class MockSensorServer : MonoBehaviour
{
    [Header("Tunnel Config")]
    public float tunnelLength = 100.0f;   // how far the worker walks (meters)
    public float tunnelWidth = 6.0f;
    public float workerHeight = 1.7f;

    [Header("Worker Movement")]
    public float walkSpeed = 1.2f;    // meters per second
    public float pauseChance = 0.02f;   // chance to pause each update
    public float pauseDuration = 2.0f;   // how long to pause

    [Header("Noise")]
    public float positionNoise = 0.05f;  // gaussian noise on x,y,z
    public float distanceNoise = 0.03f;  // noise on distance reading

    [Header("Simulation")]
    [Tooltip("Seconds between simulation updates (8 Hz => 0.125)")]
    public float updateInterval = 0.125f; // seconds per tick (was hardcoded 0.125f)

    // If you want pauseChance to be interpreted as probability per second, set this to true
    public bool pauseChanceIsPerSecond = false;

    // current simulated position
    float _x = 0.5f;
    float _y = 3.0f;   // center of tunnel width
    float _z = 1.7f;   // worker height
    int _direction = 1;
    float _pauseTimer = 0f;
    bool _isPaused = false;

    // latest JSON string — WorkerManagerHTTP reads this
    public string LatestJSON { get; private set; } = "";

    void Start()
    {
        _x = 0.5f;
        StartCoroutine(SimulationLoop());
    }

    IEnumerator SimulationLoop()
    {
        while (true)
        {
            UpdatePosition();
            LatestJSON = BuildJSON();
            yield return new WaitForSeconds(updateInterval);   // use named interval
        }
    }

    void UpdatePosition()
    {
        if (_isPaused)
        {
            _pauseTimer -= updateInterval; // use named interval
            if (_pauseTimer <= 0f)
                _isPaused = false;
            return;
        }

        // random pause: if pauseChanceIsPerSecond==true convert to per-tick probability
        float effectivePauseChance = pauseChance;
        if (pauseChanceIsPerSecond && updateInterval > 0f)
        {
            // per-tick probability = 1 - (1 - p_perSecond)^(tickSeconds)
            effectivePauseChance = 1f - Mathf.Pow(1f - pauseChance, updateInterval);
        }

        if (Random.value < effectivePauseChance)
        {
            _isPaused = true;
            _pauseTimer = pauseDuration;
            return;
        }

        // move along tunnel: distance = speed (m/s) * seconds per tick
        float step = walkSpeed * updateInterval;
        _x += step * _direction;

        // small lateral drift
        _y += Random.Range(-0.05f, 0.05f);
        _y = Mathf.Clamp(_y, 0.5f, tunnelWidth - 0.5f);

        // bounce at tunnel ends
        if (_x >= tunnelLength) { _x = tunnelLength; _direction = -1; }
        if (_x <= 0f) { _x = 0f; _direction = 1; }
    }

    string BuildJSON()
    {
        // add noise
        float nx = _x + GaussianNoise(positionNoise);
        float ny = _y + GaussianNoise(positionNoise);
        float nz = _z + GaussianNoise(positionNoise * 0.5f);

        // distance = straight line from origin anchor (0,0,0) to worker
        float dist = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
        dist += GaussianNoise(distanceNoise);
        dist = Mathf.Max(0f, dist);

        // fake per-anchor distances (d1-d4) — just noise around real distance
        float d1 = Mathf.Max(0f, dist + GaussianNoise(0.1f));
        float d2 = Mathf.Max(0f, dist + GaussianNoise(0.1f));
        float d3 = Mathf.Max(0f, dist + GaussianNoise(0.1f));
        float d4 = Mathf.Max(0f, dist + GaussianNoise(0.1f));

        string ts = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.ffffff");

        // build JSON manually — no extra dependencies needed
        string json =
            "{" +
                $"\"timestamp\":\"{ts}\"," +
                $"\"distance\":{dist:F2}," +
                "\"raw\":{" +
                    $"\"distance\":{dist:F2}," +
                    $"\"x\":{nx:F2}," +
                    $"\"y\":{ny:F2}," +
                    $"\"z\":{nz:F2}," +
                    $"\"d1\":{d1:F2}," +
                    $"\"d2\":{d2:F2}," +
                    $"\"d3\":{d3:F2}," +
                    $"\"d4\":{d4:F2}," +
                    "\"rssi1\":-52,\"rssi2\":-54,\"rssi3\":-49,\"rssi4\":-51," +
                    "\"tag\":\"T1\"" +
                "}" +
            "}";

        return json;
    }

    float GaussianNoise(float std)
    {
        // Box-Muller transform
        float u1 = 1f - Random.value;
        float u2 = 1f - Random.value;
        return std * Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
    }

    // Optional: show current state in Inspector at runtime
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 200, 300, 100));
        GUI.color = Color.cyan;
        GUILayout.Label($"[MOCK] x:{_x:F2} y:{_y:F2} dir:{(_direction > 0 ? "→" : "←")} paused:{_isPaused}");
        GUI.color = Color.white;
        GUILayout.EndArea();
    }
}