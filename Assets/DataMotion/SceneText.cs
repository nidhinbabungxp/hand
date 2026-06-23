using UnityEngine;
using UnityEditor;

public class SceneText : MonoBehaviour
{
    public string text;
    private void OnDrawGizmos()
    {
        // Define the position in 3D space where the text should appear
        //Vector3 textPosition = transform.position + Vector3.up * 2f;

        // Set the color for the text
        GUI.color = Color.white;

        // Display the text in the editor scene
        Handles.Label(transform.position, text + transform.position);
    }
}