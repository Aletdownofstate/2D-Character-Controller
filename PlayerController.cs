using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Control Settings")]
    public bool isControlEnabled = true;
    public bool canDoubleJump = true;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public bool isRunning;
    public float runSpeed = 5f;

    [Header("Jump Settings")]
    public bool isJumping = false;
    public float jumpForce = 10f;
    public float jumpCutMultiplier = 0.5f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.2f;

    [Header("Gravity Settings")]
    public float fallMultiplier = 3f;
    public float lowFallMultiplier = 2f;

    [Header("Ground Check Settings")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.6f;
    public bool isGrounded;

    [Header("Wall Check Settings")]
    public LayerMask wallLayer;
    public float wallCheckRadius = 0.02f;

    private Rigidbody2D rb;
    private InputSystem_Actions controls;
    private Vector2 moveHorizontal;
    private float currentMovementSpeed;
    private Vector2 facingDirection = Vector2.right;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool hasDoubleJumped = false;

    private void OnEnable()
    {
        if (controls == null) return;

        controls.Player.Jump.performed += OnJump;
        controls.Player.Jump.canceled += OnJumpRelease;
        controls.Player.Sprint.performed += OnSprint;
        controls.Player.Sprint.canceled += OnSprintRelease;

        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Jump.performed -= OnJump;
        controls.Player.Jump.canceled -= OnJumpRelease;
        controls.Player.Sprint.performed -= OnSprint;
        controls.Player.Sprint.canceled -= OnSprintRelease;

        controls.Disable();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();            
            rb.angularDamping = 0.1f;
            rb.linearDamping = 1.0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }        

        controls = new InputSystem_Actions();
    }

    private void Update()
    {
        if (!isControlEnabled) return;

        isGrounded = IsGrounded();

        if (isGrounded)
        {
            currentMovementSpeed = isRunning ? runSpeed : moveSpeed;
            hasDoubleJumped = false;
        }

        CoyoteTime();

        if (jumpBufferCounter > 0f) jumpBufferCounter -= Time.deltaTime;

        moveHorizontal = controls.Player.Move.ReadValue<Vector2>();
        FlipPlayerDirection();
        Movement();

        isJumping = IsJumping();
        JumpBuffer();
    }

    private void FixedUpdate()
    {
        ApplyFallGravity();
    }

    private void OnJump(InputAction.CallbackContext context) => jumpBufferCounter = jumpBufferTime;    

    private void OnJumpRelease(InputAction.CallbackContext context)
    {
        if (rb.linearVelocity.y > 0)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);
        }
    }

    private void OnSprint(InputAction.CallbackContext context) => isRunning = true;    

    private void OnSprintRelease(InputAction.CallbackContext context) => isRunning = false;

    private void Movement()
    {
        float movementSpeed = currentMovementSpeed;
        float moveDirection = moveHorizontal.x;

        if (!isGrounded)
        {
            if ((IsTouchingWallLeft() && moveDirection < 0f) || 
                (IsTouchingWallRight() && moveDirection > 0f))
            {
                moveDirection = 0f;
            }
        }        

        float xVelocity = moveDirection * movementSpeed;
        rb.linearVelocity = new Vector2(xVelocity, rb.linearVelocity.y);
    }

    private void FlipPlayerDirection()
    {
        float xVel = rb.linearVelocity.x;

        if (Mathf.Abs(xVel) > 0.01f)
        {
            float newScaleX = Mathf.Sign(xVel) * Mathf.Abs(transform.localScale.x);
            transform.localScale = new Vector3(newScaleX, transform.localScale.y, transform.localScale.z);
            facingDirection = (xVel > 0) ? Vector2.right : Vector2.left;
        }
    }

    private void Jump()
    {
        if (isControlEnabled && coyoteTimeCounter > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            coyoteTimeCounter = 0f;
            hasDoubleJumped = false;
        }
        else if(canDoubleJump && !hasDoubleJumped)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            hasDoubleJumped = true;
        }
    }

    private void CoyoteTime()
    {
        if (IsGrounded())
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;

        }
    }

    private void JumpBuffer()
    {
        if (jumpBufferCounter > 0f)
        {
            if (coyoteTimeCounter > 0f || (canDoubleJump && !hasDoubleJumped))
            {
                Jump();
                jumpBufferCounter = 0f;
            }
        }
    }

    private void ApplyFallGravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !controls.Player.Jump.IsPressed())
        {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowFallMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    private bool IsJumping()
    {
        return rb.linearVelocity.y != 0 && !IsGrounded();
    }

    private bool IsGrounded()
    {        
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private bool IsTouchingWallLeft()
    {
        Vector2 boxSize = new Vector2(1f, 1f);
        Vector2 castDirection = Vector2.left;

        RaycastHit2D hit = Physics2D.BoxCast(transform.position, boxSize, 0f, castDirection, wallCheckRadius, wallLayer);
        return hit.collider != null;
    }

    private bool IsTouchingWallRight()
    {
        Vector2 boxSize = new Vector2(1f, 1f);
        Vector2 castDirection = Vector2.right;

        RaycastHit2D hit = Physics2D.BoxCast(transform.position, boxSize, 0f, castDirection, wallCheckRadius, wallLayer);
        return hit.collider != null;
    }
}
