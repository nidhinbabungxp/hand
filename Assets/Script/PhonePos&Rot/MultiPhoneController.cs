using System.Text;
using System.Collections.Generic;
using UnityEngine;
using M2MqttUnity;
using uPLibrary.Networking.M2Mqtt.Messages;

public class MultiPhoneController : M2MqttUnityClient
{
    [System.Serializable]
    public class PhoneBinding
    {
        public string topic = "phone1/sensors";   // unique topic per phone
        public GameObject character;              // unique character per phone
        [HideInInspector] public Quaternion targetRotation = Quaternion.identity;
        [HideInInspector] public Vector3 pendingAccel = Vector3.zero;
        [HideInInspector] public Vector3 velocity = Vector3.zero;
        [HideInInspector] public Vector3 characterPosition = Vector3.zero;
        [HideInInspector] public bool newRotationReceived = false;
    }

    [Header("Phone Bindings")]
    public List<PhoneBinding> phones = new List<PhoneBinding>();

    [Header("Rotation Settings")]
    public float rotationSmoothSpeed = 5f;

    [Header("Position Settings")]
    public float moveSpeed = 1f;
    public float deadzone = 1f;
    public float damping = 0.95f;

    // -------------------------------------------------------
    // MQTT
    // -------------------------------------------------------

    protected override void SubscribeTopics()
    {
        // Build topic and QoS arrays from phone list
        string[] topics = new string[phones.Count];
        byte[] qosLevels = new byte[phones.Count];

        for (int i = 0; i < phones.Count; i++)
        {
            topics[i] = phones[i].topic;
            qosLevels[i] = MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE;
            Debug.Log("Subscribed to: " + phones[i].topic);
        }

        client.Subscribe(topics, qosLevels);
    }

    protected override void UnsubscribeTopics()
    {
        string[] topics = new string[phones.Count];
        for (int i = 0; i < phones.Count; i++)
            topics[i] = phones[i].topic;

        client.Unsubscribe(topics);
    }

    protected override void DecodeMessage(string topic, byte[] message)
    {
        string payload = Encoding.UTF8.GetString(message);

        // Find which phone this topic belongs to
        PhoneBinding phone = phones.Find(p => p.topic == topic);
        if (phone == null) return;

        if (payload.Contains("rotation_vector"))
            DecodeRotation(phone, payload);
        else if (payload.Contains("accelerometer"))
            DecodePosition(phone, payload);
    }

    // -------------------------------------------------------
    // Rotation decoder
    // -------------------------------------------------------

    private void DecodeRotation(PhoneBinding phone, string payload)
    {
        try
        {
            int start = payload.IndexOf("[") + 1;
            int end = payload.IndexOf("]");
            string[] parts = payload.Substring(start, end - start).Split(',');

            if (parts.Length < 4) return;

            float x = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            float y = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            float z = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            float w = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);

            //float yaw = new Quaternion(x, y, z, w).eulerAngles.y;
            float yaw = new Quaternion(x, z, y, w).eulerAngles.y;
            phone.targetRotation = Quaternion.Euler(0f, yaw, 0f);
            phone.newRotationReceived = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Rotation parse error: " + e.Message);
        }
    }

    // -------------------------------------------------------
    // Position decoder
    // -------------------------------------------------------

    private void DecodePosition(PhoneBinding phone, string payload)
    {
        try
        {
            int start = payload.IndexOf("[") + 1;
            int end = payload.IndexOf("]");
            string[] parts = payload.Substring(start, end - start).Split(',');

            if (parts.Length < 3) return;

            float ax = float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            float ay = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            float az = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

            az -= 9.8f;

            if (Mathf.Abs(ax) < deadzone) ax = 0f;
            if (Mathf.Abs(ay) < deadzone) ay = 0f;
            if (Mathf.Abs(az) < deadzone) az = 0f;

            phone.pendingAccel = new Vector3(ax, 0f, ay);

            // Hard stop when phone is still
            if (ax == 0f && ay == 0f)
                phone.velocity = Vector3.zero;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Position parse error: " + e.Message);
        }
    }

    // -------------------------------------------------------
    // Update — apply rotation and position for ALL phones
    // -------------------------------------------------------

    protected override void Update()
    {
        base.Update();

        foreach (PhoneBinding phone in phones)
        {
            if (phone.character == null) continue;

            // Apply rotation
            if (phone.newRotationReceived)
            {
                phone.character.transform.rotation = Quaternion.Slerp(
                    phone.character.transform.rotation,
                    phone.targetRotation,
                    Time.deltaTime * rotationSmoothSpeed
                );
            }

            // Apply position
            phone.velocity += phone.pendingAccel * moveSpeed * Time.deltaTime;

            if (phone.pendingAccel == Vector3.zero)
                phone.velocity = Vector3.Lerp(phone.velocity, Vector3.zero, Time.deltaTime * 10f);
            else
                phone.velocity *= damping;

            //phone.characterPosition += phone.velocity * Time.deltaTime;
            //phone.character.transform.position = phone.characterPosition;
        }
    }
}