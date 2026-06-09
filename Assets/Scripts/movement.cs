using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.InputSystem;

public class movement : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float gravity = -9.81f;
    public float jumpForce = 1f;
    private Vector3 velocity;

    [Header("Look")]
    public float sensitivity = 2f;
    public Transform cameraTransform;

    [Header("Sprint")]
    public float sprintMulti = 1.6f;
    public float maxStamina = 5f;
    public float staminaDrain = 1.5f;
    public float staminaRegen = 1f;
    public float regenDelay;
    public float regenDelayTime = 1f;

    public float stamina;
    public bool isSprinting;

    private CharacterController controller;
    private InputSystem_Actions input;

    private Vector2 move;
    private Vector2 look;

    private float xRotation;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        input = new InputSystem_Actions();

        stamina = maxStamina;
        isSprinting = false;
    }

    private void OnEnable()
    {
        input.Enable();

        input.Player.Move.performed += context => move = context.ReadValue<Vector2>();
        input.Player.Move.canceled += context => move = Vector2.zero;

        input.Player.Look.performed += context => look = context.ReadValue<Vector2>();
        input.Player.Look.canceled += context => look = Vector2.zero;

        input.Player.Sprint.performed += _ => isSprinting = true;
        input.Player.Sprint.canceled += _ => isSprinting = false;

        input.Player.Jump.performed += _ => HandleJump();

    }

    private void OnDisable()
    {
        input.Disable();
    }

    void Update()
    {
        HandleLook();
        HandleMove();
        HandleSprint();
    }

    void HandleLook()
    {

        float mouseX = look.x * sensitivity;
        float mouseY = look.y * sensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation , -85f , 85f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMove()
    {
        Vector3 inputDir = transform.right * move.x + transform.forward * move.y;

        float currentSpeed = speed;

        if (isSprinting && stamina > 0f && move.magnitude > 0.1f)
            currentSpeed *= sprintMulti;

        if (controller.isGrounded)
        {
            velocity.x = inputDir.x * currentSpeed;
            velocity.z = inputDir.z * currentSpeed;

            if (velocity.y < 0)
                velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;

        controller.Move(velocity * Time.deltaTime);
    }

    void HandleJump()
    {
        if (!controller.isGrounded) return;

        Vector3 inputDir = transform.right * move.x + transform.forward * move.y;

        velocity.x = inputDir.x * speed * .2f;
        velocity.z = inputDir.z * speed * .2f;

        velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
    }

    void HandleSprint()
    {

        if (isSprinting && move.magnitude > 0.1f && stamina > 0f)
        {
            stamina -= staminaDrain * Time.deltaTime;
            regenDelay = regenDelayTime;
        }
        else
        {
            if (regenDelay > 0)
            {
                regenDelay -= Time.deltaTime;
            }
            else
            {
                stamina += staminaRegen * Time.deltaTime;
            }
        }

        if (stamina <= 0f)
            isSprinting = false;

        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
    }
}
