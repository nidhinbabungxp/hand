using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class ApiTest : MonoBehaviour
{
    public Transform chestObject;

    private string apiUrl = "http://10.57.221.56:8080/api/all/latest";

    void Start()
    {
        StartCoroutine(GetDataLoop());
    }

    IEnumerator GetDataLoop()
    {
        while (true)
        {
            yield return GetData();
            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator GetData()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                RootData data = JsonUtility.FromJson<RootData>(json);

                Chest chest = data.activity.chest;

                chestObject.localPosition = new Vector3(
                    chest.position.x,
                    chest.position.y,
                    chest.position.z
                );

                Quaternion chestRotation = new Quaternion(
                    chest.rotation.qx,
                    chest.rotation.qy,
                    chest.rotation.qz,
                    chest.rotation.qw
                );

                chestObject.localRotation = chestRotation;

                Debug.Log("Chest updated");

                Debug.Log($"Raw Rotation -> qw:{chest.rotation.qw}, qx:{chest.rotation.qx}, qy:{chest.rotation.qy}, qz:{chest.rotation.qz}");

                Debug.Log("Unity Quaternion: " + chestRotation);

                Debug.Log("Euler Angles: " + chestRotation.eulerAngles);
            }

            else
            {
                Debug.LogError("API Error: " + request.error);
            }
        }
    }
}

[System.Serializable]
public class RootData
{
    public Activity activity;
}

[System.Serializable]
public class Activity
{
    public Chest chest;
}

[System.Serializable]
public class Chest
{
    public Position position;
    public Position position_abs;
    public Rotation rotation;
}

[System.Serializable]
public class Position
{
    public float x;
    public float y;
    public float z;
}

[System.Serializable]
public class Rotation
{
    public float qw;
    public float qx;
    public float qy;
    public float qz;
}