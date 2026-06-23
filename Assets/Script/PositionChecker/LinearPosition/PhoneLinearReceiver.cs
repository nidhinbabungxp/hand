using System.Text;
using UnityEngine;
using M2MqttUnity;
using uPLibrary.Networking.M2Mqtt.Messages;

public class PhoneLinearReceiver : M2MqttUnityClient
{
    [Header("Topic")]
    public string sensorTopic = "phone/linear";   // new topic

    [Header("Target")]
    public GameObject targetObject;

    [Header("Movement Settings")]
    public float moveSpeed = 1f;
    public float deadzone = 0.3f;     // ignore tiny vibrations
    public float damping = 0.95f;

    [Header("Debug")]
    public Vector3 currentAccel;      // visible in Inspector live

    private Vector3 pendingAccel = Vector3.zero;
    private Vector3 velocity = Vector3.zero;
    private Vector3 objectPosition = Vector3.zero;
    public bool resetPosition = false;

    protected override void SubscribeTopics()
    {
        client.Subscribe(
            new string[] { sensorTopic },
            new byte[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE }
        );
        Debug.Log("Linear subscribed to: " + sensorTopic);
    }

    protected override void UnsubscribeTopics()
    {
        client.Unsubscribe(new string[] { sensorTopic });
    }

    protected override void DecodeMessage(string topic, byte[] message)
    {
        string payload = Encoding.UTF8.GetString(message);

        // Only process linear acceleration — ignore everything else
        if (!payload.Contains("linear_acceleration")) return;

        try
        {
            int start = payload.IndexOf("[") + 1;
            int end   = payload.IndexOf("]");
            string[] parts = payload.Substring(start, end - start).Split(',');

            if (parts.Length < 3) return;

            float ax = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            float ay = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            float az = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

            // Apply deadzone
            if (Mathf.Abs(ax) < deadzone) ax = 0f;
            if (Mathf.Abs(ay) < deadzone) ay = 0f;
            if (Mathf.Abs(az) < deadzone) az = 0f;

            pendingAccel = new Vector3(ax, 0f, ay);
            currentAccel = pendingAccel; // show in Inspector

            // Hard stop when phone is still
            if (ax == 0f && ay == 0f)
                velocity = Vector3.zero;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Linear parse error: " + e.Message);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (targetObject == null) return;

        if (resetPosition)
        {
            velocity = Vector3.zero;
            objectPosition = Vector3.zero;
            targetObject.transform.position = Vector3.zero;
            resetPosition = false;
            Debug.Log("Position reset");
        }

        // Apply movement
        velocity += pendingAccel * moveSpeed * Time.deltaTime;

        if (pendingAccel == Vector3.zero)
            velocity = Vector3.Lerp(velocity, Vector3.zero, Time.deltaTime * 10f);
        else
            velocity *= damping;

        objectPosition += velocity * Time.deltaTime;
        targetObject.transform.position = objectPosition;
    }
}