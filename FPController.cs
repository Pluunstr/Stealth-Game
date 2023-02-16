using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPController : MonoBehaviour
{
    public bool CanMove { get; private set; } = true;
    private bool isSprinting => canSprint && Input.GetKey(sprintKey);
    private bool ShouldJump => Input.GetKeyDown(jumpKey) && CharacterController.isGrounded && !isSliding;
    private bool shouldCrouch => Input.GetKeyDown(crouchKey) && !DuringCrouchAnimation && CharacterController.isGrounded;

    [Header("functional options")]
    [SerializeField] private bool canSprint = true;
    [SerializeField] private bool canJump = true;
    [SerializeField] private bool canCrouch = true;
    [SerializeField] private bool canBob = true;
    [SerializeField] private bool WillSlideOnSlopes = true;
    [SerializeField] private bool canZoom = true;
    [SerializeField] private bool canInteract = true;
    [SerializeField] private bool EnableMagic = true;

    [Header("Controls")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;
    [SerializeField] private KeyCode crouchKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode ZoomKey = KeyCode.Mouse1;
    [SerializeField] private KeyCode InteractKey = KeyCode.Mouse0;
    [Header("Movement Parameters")]
    [SerializeField] private float WalkSpeed = 3.0f;
    [SerializeField] private float SprintSpeed = 6.0f;
    [SerializeField] private float CrouchSpeed = 1.5f;
    [SerializeField] private float SlopeSpeed = 8f;
    
    [Header("Look Parameter")]
    [SerializeField, Range(1, 10)] private float LookSpeedX = 2.0f;
    [SerializeField, Range(1, 10)] private float LookSpeedY = 2.0f;
    [SerializeField, Range(1, 180)] private float UpperLookLimit = 80.0f;
    [SerializeField, Range(1, 180)] private float LowerLookLimit = 80.0f;

    [Header("Jumping Parameters")]
    [SerializeField] private float jumpForce = 8.0f;
    [SerializeField] private float Gravity = 30.0f;

    [Header("Crouch Parameters")]
    [SerializeField] private float CrouchHeight = 0.5f;
    [SerializeField] private float StandingHeight = 2.0f;
    [SerializeField] private float TimetoCrouch = 0.25f;
    [SerializeField] private Vector3 CrouchingCenter = new Vector3(0, 0.5f, 0);
    [SerializeField] private Vector3 StandingCenter = new Vector3(0, 0, 0);
    private bool isCrouching;
    private bool DuringCrouchAnimation;


    [Header("Headbob Perameters")]
    [SerializeField] private float walkBobSpeed = 7f;
    [SerializeField] private float WalkBobAmount = 0.05f;
    [SerializeField] private float CrouchBobSpeed = 7f;
    [SerializeField] private float CrouchBobAmount = 0.01f;
    [SerializeField] private float SprintBobSpeed = 12f;
    [SerializeField] private float SprintBobAmount = 0.1f;
    private float defaultYPos = 0;
    private float Timer;

    [Header("Zoom Parameters")]
    [SerializeField] private float timeToZoom = 0.3f;
    [SerializeField] private float zoomFOV = 30.0f;
    private float defaultFOV;
    private Coroutine zoomRoutine;

    [Header("Magic")]
    private int BlinkCharge = 3;

    //Sliding Properties
    private Vector3 hitPointNormal;
    private bool isSliding
    {
        get
        {
            if (CharacterController.isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 2f))
            {
                hitPointNormal = slopeHit.normal;
                return Vector3.Angle(hitPointNormal, Vector3.up) > CharacterController.slopeLimit;
            }
             else
            {
                return false;
            }
        }
    }

    [Header("Interaction")]
    [SerializeField] private Vector3 interactionRayPoint = default;
    [SerializeField] private float interactionDistance = default;
    [SerializeField] private LayerMask interactionLayer = default;
    private Interactable CurrentInteractable;

    private Camera PlayerCamera;
    private CharacterController CharacterController;

    private Vector3 MoveDirection;
    private Vector2 CurrentInput;

    private float rotationX = 0;
    void Awake()
    {
        PlayerCamera = GetComponentInChildren<Camera>();
        CharacterController = GetComponent<CharacterController>();
        defaultYPos = PlayerCamera.transform.localPosition.y;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        defaultFOV = PlayerCamera.fieldOfView;
    }

    void Update()
    {
        if (CanMove)
        {
            HandleMovementInput();
            HandleMouseLook();
            if (canJump)
            {
                handleJump();
            }
            if (canCrouch)
            {
                handleCrouch();
            }
            if (canBob)
            {
                HandleHeadBob();
            }
            if (canZoom)
            {
                handleZoom();
            }
            if (canInteract)
            {
                HandleInteractionCheck();
                HandleInteractionInput();
            }
            if (EnableMagic)
            {
                HandleMagic();
                StartCoroutine(Blink_Recharge());
            }
            HandleFinalMovement();
        }
    }
    private void HandleMovementInput()
    {
        CurrentInput = new Vector2((isCrouching ? CrouchSpeed : isSprinting ? SprintSpeed : WalkSpeed) * Input.GetAxisRaw("Vertical"), (isCrouching ? CrouchSpeed : isSprinting ? SprintSpeed : WalkSpeed) * Input.GetAxisRaw("Horizontal"));
        float MoveDirectionY = MoveDirection.y;
        MoveDirection = (transform.TransformDirection(Vector3.forward) * CurrentInput.x) + (transform.TransformDirection(Vector3.right) * CurrentInput.y);
        MoveDirection.y = MoveDirectionY;
    }
    private void HandleMouseLook()
    {
        rotationX -= Input.GetAxis("Mouse Y") * LookSpeedY;
        rotationX = Mathf.Clamp(rotationX, -UpperLookLimit, LowerLookLimit);
        PlayerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * LookSpeedX, 0);
    }
    private void handleJump()
    {
        if (ShouldJump)
        {
            MoveDirection.y = jumpForce;
        }
    }
    private void handleCrouch()
    {
        if (shouldCrouch)
        {
            StartCoroutine(CrouchStand());
        }
    }
    private void handleZoom()
    {
        if (Input.GetKeyDown(ZoomKey))
        {
            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
                zoomRoutine = null;
            }
            zoomRoutine = StartCoroutine(ToggleZoom(true));
        }
        if (Input.GetKeyUp(ZoomKey))
        {
            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
                zoomRoutine = null;
            }
            zoomRoutine = StartCoroutine(ToggleZoom(false));
        }
    }
    private void HandleMagic()
    {

        if (BlinkCharge != 0)
        {
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {

                Vector3 ray = transform.TransformDirection(Vector3.forward);
                RaycastHit hit;
                if (Physics.Raycast(PlayerCamera.transform.position, ray, out hit, 10))
                {
                    PlayerCamera.transform.position = hit.point -= this.transform.forward * 1;
                }
                else
                {
                    PlayerCamera.transform.position += this.transform.forward * 10;
                }
                BlinkCharge -= 1;
            }
        }
    }
    IEnumerator Blink_Recharge()
    {
        yield return new WaitForSeconds(3);
        if (BlinkCharge < 3)
        {
            BlinkCharge += 1;
        }
        StartCoroutine("Blink_Recharge");
    }

    private void HandleInteractionCheck()
    {
        if (Physics.Raycast(PlayerCamera.ViewportPointToRay(interactionRayPoint), out RaycastHit hit, interactionDistance))
        {
            if (hit.collider.gameObject.layer == 6 && (CurrentInteractable == null || hit.collider.gameObject.GetInstanceID() != CurrentInteractable.GetInstanceID()))
            {
                hit.collider.TryGetComponent(out CurrentInteractable);

                if (CurrentInteractable)
                {
                    CurrentInteractable.OnFocus(); 
                }
            }
        }
        else if (CurrentInteractable)
        {
            CurrentInteractable.OnLoseFocus();
            CurrentInteractable = null;
        }
    }
    private void HandleInteractionInput()
    {
        if (Input.GetKeyDown(InteractKey) && CurrentInteractable != null && Physics.Raycast(PlayerCamera.ViewportPointToRay(interactionRayPoint), out RaycastHit hit, interactionDistance, interactionLayer))
        {
            CurrentInteractable.OnInteract();
        }
    }
    private void HandleFinalMovement()
    {
        if (!CharacterController.isGrounded)
        {
            MoveDirection.y -= Gravity * Time.deltaTime;
        }
        if (WillSlideOnSlopes && isSliding)
        {
            MoveDirection += new Vector3(hitPointNormal.x, -hitPointNormal.y, hitPointNormal.z) * SlopeSpeed;
        }
        CharacterController.Move(MoveDirection * Time.deltaTime);
    }
    private IEnumerator CrouchStand()
    {
        if (isCrouching && Physics.Raycast(PlayerCamera.transform.position, Vector3.up, 1f))
        {
            yield break;
        }

        DuringCrouchAnimation = true;
        float timeElapsed = 0;
        float targetHeight = isCrouching ? StandingHeight : CrouchHeight;
        float currentHeight = CharacterController.height;
        Vector3 targetCenter = isCrouching ? StandingCenter : CrouchingCenter;
        Vector3 currentCenter = CharacterController.center;

        while (timeElapsed < TimetoCrouch)
        {
            CharacterController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / TimetoCrouch);
            CharacterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / TimetoCrouch);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        CharacterController.height = targetHeight;
        CharacterController.center = targetCenter;

        isCrouching = !isCrouching;

        DuringCrouchAnimation = false;
    }
    private void HandleHeadBob()
    {
        if (!CharacterController.isGrounded)
        {
            return;
        }
        if (Mathf.Abs(MoveDirection.x) > 0.1f || Mathf.Abs(MoveDirection.z) > 0.1f)
        {
            Timer += Time.deltaTime * (isCrouching ? CrouchBobSpeed : isSprinting ? SprintBobSpeed : walkBobSpeed);
            PlayerCamera.transform.localPosition = new Vector3(PlayerCamera.transform.localPosition.x, defaultYPos + Mathf.Sin(Timer) * (isCrouching ? CrouchBobAmount : isSprinting ? SprintBobAmount : WalkBobAmount), PlayerCamera.transform.localPosition.z);
        }
    }
    private IEnumerator ToggleZoom(bool isEnter)
    {
        float targetFOV = isEnter ? zoomFOV : defaultFOV;
        float startingFOV = PlayerCamera.fieldOfView;
        float timeElapsed = 0;
        while (timeElapsed < timeToZoom)
        {
            PlayerCamera.fieldOfView = Mathf.Lerp(startingFOV, targetFOV, timeElapsed / timeToZoom);
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        PlayerCamera.fieldOfView = targetFOV;
        zoomRoutine = null;
    }
}
