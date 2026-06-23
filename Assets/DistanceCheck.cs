using UnityEngine;

public class DistanceCheck : MonoBehaviour
{
    public Transform objectA;
    public Transform objectB;

    void Update()
    {
        // Get the positions
        Vector3 posA = objectA.position;
        Vector3 posB = objectB.position;

        // Calculate distance in meters
        float distanceInMeters = Vector3.Distance(posA, posB);

        Debug.Log("Distance: " + distanceInMeters + " meters");
    }
}