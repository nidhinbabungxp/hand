using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class TunnelPlayerController : MonoBehaviour
{
    [Header("References")]
    public MultiPhoneController phoneController;  // drag MultiPhoneController GameObject here
    public int phoneIndex = 0;                    // 0 = phone1, 1 = phone2

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float gravity = -9.81f;
    public float groundCheckDistance = 0.2f;
    public LayerMask groundLayer;

    [Header("Smoothing")]
    public float positionSmoothSpeed = 10f;
    public float rotationSmoothSpeed = 5f;

    private CharacterController cc;
    private Vector3 verticalVelocity = Vector3.zero;
    private bool isGrounded;

    void Start()
    {
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (phoneController == null) return;
        if (phoneIndex >= phoneController.phones.Count) return;

        MultiPhoneController.PhoneBinding phone = phoneController.phones[phoneIndex];

        
        if (phone.newRotationReceived)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                phone.targetRotation,
                Time.deltaTime * rotationSmoothSpeed
            );
        }

       
        
        Vector3 moveDirection = Vector3.zero;

        if (phone.pendingAccel != Vector3.zero)
        {
            
            moveDirection = transform.TransformDirection(
                new Vector3(phone.pendingAccel.x, 0f, phone.pendingAccel.z)
            );
            moveDirection = moveDirection.normalized * moveSpeed;
        }

       
        isGrounded = cc.isGrounded;

        if (isGrounded && verticalVelocity.y < 0)
            verticalVelocity.y = -2f;

        verticalVelocity.y += gravity * Time.deltaTime;
        moveDirection.y = verticalVelocity.y;

      
        cc.Move(moveDirection * Time.deltaTime);
    }
}