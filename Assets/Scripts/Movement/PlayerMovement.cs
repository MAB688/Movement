using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Bugs:
// Crouching while standing still on a slope
// Sliding from flat ground up a slope
// Crouching is detected as going up sometimes
// Can't jump on slopes

// Ideas:
// Use Co-Pilot to refactor some code (PlayerInput)

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    // How fast the player moves, how much drag, jump height, etc.
    private float moveSpeed;
    public float groundDrag, walkSpeed, sprintSpeed;
    
    [Header("Jumping")]
    public float jumpForce, jumpCooldown, airMult;
    private bool readyToJump;

    [Header("Crouching")]
    public float crouchSpeed, crouchHeight;
    private float startHeight;
    
    [Header("Ground Check")]
    public float playerHeight;
    public LayerMask whatIsGround;
    private bool grounded;

    [Header("Slope Handling")]
    public float maxSlopeAngle;
    private float angle;
    private RaycastHit slopeHit;
    private bool jumping;

    [Header("Slide")]
    public float maxSlideTime, slideForce, slideCooldown;
    private float slideTimer, lastY;
    private bool sliding, readyToSlide;

    [Header("Keybinds")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.C;
    
    // The direction the player is facing
    public Transform orientation;
    // Detect if the player presses "a" or "d" and "w" or "s"
    private float horiInput, vertInput;
    // The direction to move the player in
    Vector3 moveDirection;
    
    // The player's rigidbody
    Rigidbody rb;
    CapsuleCollider body;

    // An enum is a special class that represents a group of unchangeable constants
    public MovementState state;
    public enum MovementState
    {
        walking,
        sprinting,
        sliding,
        couching,
        idle,
        air
    }

    void Start()
    {
        // Get the player's initial height
        body = GetComponentInChildren<CapsuleCollider>();
        startHeight = body.height;
        
        // Get the player's rigidbody
        rb = GetComponent<Rigidbody>();
        // Freeze the rotation so that the player will not fall over
        rb.freezeRotation = true;

        // Enable jumping
        ResetJump();
        ResetSlide();
    }

    void Update()
    {     
        // Ground Check (Origin, direction of ray, max length of ray, ground layer)
        grounded = Physics.SphereCast(transform.position, body.radius * 0.9f, Vector3.down, out slopeHit, playerHeight * 0.5f + 0.1f, whatIsGround);
        
        // Dynamically keep track of player's height
        playerHeight = body.height;

        // Get the players keyboard input
        PlayerInput();
        
        // Limit player speed if needed
        SpeedLimit();

        // Check which movement state the player is in
        StateHandler();
    }

    void FixedUpdate()
    {
        // Check for slide
        if (sliding)
            Slide();
            
        // Calculate movement direction and move 
        else
            MovePlayer();
        
        // Apply drag when the player is on the ground
        if(grounded)
            rb.drag = groundDrag;
        else
            rb.drag = 0;
    }

    void PlayerInput()
    {
        // Detect if the player is pressing the movement keys
        horiInput = Input.GetAxisRaw("Horizontal");
        vertInput = Input.GetAxisRaw("Vertical");

        // Must be on ground
        if(grounded)
        {   
            // Detect when the player jumps
            if(Input.GetKey(jumpKey) && readyToJump && state != MovementState.couching && state != MovementState.sliding)
            {
                readyToJump = false;
                Jump();
                jumping = false;
                // Adds a jump delay
                Invoke(nameof(ResetJump), jumpCooldown);
            }

            // Detect if player slides
            if(CheckSlide())
            {
                readyToSlide = false;
                body.height = crouchHeight;
                slideTimer = maxSlideTime;
                sliding = true;
                Invoke(nameof(ResetSlide), slideTimer + slideCooldown); 
            }
            // Detect when the player crouches and alter the player's height (originally changed player's y scale)
            else if(Input.GetKey(crouchKey) || state == MovementState.sliding)
            {
                body.height = crouchHeight;
            }        
            else if(!ForceCrouch())
                body.height = startHeight;
        }
    }

    private void StateHandler()
    {   
        // On ground
        if (grounded)
        {
            // Mode - Sliding
            if(sliding)
                state = MovementState.sliding;
                
            // Mode - Crouching
            else if(Input.GetKey(crouchKey) || body.height == crouchHeight)
            {
                state = MovementState.couching;
                moveSpeed = crouchSpeed;
            }
            // Mode - Sprinting
            else if(state != MovementState.couching && Input.GetKey(sprintKey) && (horiInput != 0 || vertInput != 0) && Input.GetKey("w"))
            {
                state = MovementState.sprinting;
                moveSpeed = sprintSpeed;
            }
            // Mode - Walking
            else if(horiInput != 0 || vertInput != 0)
            {
                state = MovementState.walking;
                moveSpeed = walkSpeed;
            }
            // Mode - Idle
            else
            {
                state = MovementState.idle;
            }
        }
        // In air
        else
            state = MovementState.air;
    }

    void MovePlayer()
    {
        // Move the direction the player is looking if the corresponding keys are pressed
        moveDirection = orientation.forward * vertInput + orientation.right * horiInput;

        // On slope
        if(OnSlope() && !jumping)
        {
            // Add force at the slope angle for smooth movement
            rb.AddForce(GetSlopeMoveDir() * moveSpeed * 20f, ForceMode.Force);
            
            // Add downward force to keep the player flat on slope
            if(rb.velocity.y > 0)
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
        }
        // On ground
        else if(grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);

        // In air - slowed movement
        else if (!grounded)
            rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMult, ForceMode.Force);

        // Turn gravity off while on slope
        rb.useGravity = !OnSlope();
    }

    void SpeedLimit()
    {
        // Limit speed on slope when not jumping
        if (OnSlope() && !jumping)
        {
            if(rb.velocity.magnitude > moveSpeed)
                rb.velocity = rb.velocity.normalized * moveSpeed;
        }
        
        else
        {// Get the player's speed
        Vector3 flatVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        // Limit the player's velocity if needed
        if (flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.velocity = new Vector3(limitedVel.x, rb.velocity.y, limitedVel.z);
            }
        }
    }

    void Jump() // Could try to make jumping feel better, specifically when going up slopes,
    {
        jumping = true;
        rb.velocity = new Vector3(rb.velocity.x, jumpForce, rb.velocity.z);
    }

    void ResetJump()
    {
        readyToJump = true;
    }

    bool OnSlope()
    {   // Spherecasts are more accurate on slopes
        if(grounded)
        {
            // Gets the angle of the slope we are on
            angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            // Returns true if the angle is smaller than the max slope angle
            return angle < maxSlopeAngle && angle != 0;
        }
        // If the raycast does not hit a slope return false
        return false;
    }

    Vector3 GetSlopeMoveDir()
    {
        // Since this is a direction we normalize it
        return Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
    }

    // Forces the player to crouch when there is not enough room to stand up
    bool ForceCrouch()
    {
        if(Physics.SphereCast(transform.position, body.radius, Vector3.up, out RaycastHit crouchHit, startHeight * 0.5f))
                return true;
            
        return false;
    }

    void Slide() // Add slide delay (like jump delay) to prevent spam, should not be able to slide up a slope
    {
        // If not on slope or not moving upward
        if(!OnSlope() || rb.velocity.y > -0.1f)
        {
            rb.AddForce(moveDirection.normalized * slideForce, ForceMode.Force);
            slideTimer -= Time.deltaTime;
        }
        // Sliding up slope *NOT working
        else if (GoingUp())
        {
            rb.AddForce(moveDirection.normalized * slideForce * 0.4f, ForceMode.Force);
            slideTimer -= Time.deltaTime;
        }
        // Sliding on slope
        else
        {
            rb.AddForce(GetSlopeMoveDir().normalized * slideForce, ForceMode.Force);
            //(slideForce * (angle * 0.2f))
        }

        if(slideTimer <= 0)
            sliding = false;
            
    }

    void ResetSlide()
    {
        readyToSlide = true;
    }

    bool CheckSlide()
    {
        if(!GoingUp() && Input.GetKey(sprintKey) && Input.GetKeyDown(crouchKey) && Input.GetKey("w") && state != MovementState.couching && readyToSlide)
            return true;
        else
            return false;
    }

    // Used to prevent sliding up slopes (coming from flat ground)
    bool GoingUp()
    {
        float currY = transform.position.y;

        if (currY <= lastY)   // Negative number means you are going down
        {
            lastY = currY;
            return false;
        }

        lastY = currY;
        return true;
    }
}