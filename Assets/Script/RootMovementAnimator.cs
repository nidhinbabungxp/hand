using UnityEngine;

public class RootMovementAnimator : MonoBehaviour
{
    [Header("References")]
    public Transform rootObject;
    public Animator animator;

    [Header("Animator Parameter")]
    public string walkingBoolName = "Walking";

    [Header("Movement Detection")]
    public float moveThreshold = 0.001f;
    public bool ignoreY = true;

    [Header("Manual Test")]
    public bool manualTest = false;
    public float manualMoveSpeed = 2f;

    private Vector3 lastPosition;

    private void Start()
    {
        if (rootObject == null)
            rootObject = transform;

        lastPosition = rootObject.position;
    }

    private void Update()
    {
        if (rootObject == null || animator == null)
            return;

        if (manualTest)
            MoveManually();

        UpdateWalkingAnimation();
    }

    private void MoveManually()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(horizontal, 0f, vertical).normalized;

        rootObject.position += move * manualMoveSpeed * Time.deltaTime;
    }

    private void UpdateWalkingAnimation()
    {
        Vector3 currentPosition = rootObject.position;
        Vector3 moveDelta = currentPosition - lastPosition;

        if (ignoreY)
            moveDelta.y = 0f;

        bool isWalking = moveDelta.magnitude > moveThreshold;

        animator.SetBool(walkingBoolName, isWalking);

        lastPosition = currentPosition;
    }
}