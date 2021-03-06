using System;
using System.Collections;
using System.Numerics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class Player : MonoBehaviour
{
    public int cp;
    [Header("Player Input")]
    [SerializeField]
    private Vector2 dir;
    private IEnumerator _grabbing = null;
    private int _stamina = 100;
    private IEnumerator _flashing = null;

    [Header("Movement")]
    public float runSpeed = 10.0f;
    public float maxSpeed = 7.0f;
    public float jumpForce = 10.0f;
    public float climbSpeed = 3.0f;
    public float drag = 4.0f;
    public float jumpingGravity = 1.0f;
    public float fallMultiplier = 2.5f;
    // WallSlide
    public bool wallSlide;
    public float slideSpeed = 3;

    // dash
    public float dashSpeed = 10;
    private float _dashTime;
    public float startDashTime = 0.1f;
    public bool hasDashed = false;
    private bool _dashing = false;

    // buffered jumping
    private bool _jumping = false;
    public float jumpDelay = 0.25f;
    private float _lastJumped;

    // coyote time
    private float _lastValidGroundTouch;
    public float groundTouchedValidTil = 0.25f;
    
    //death
    private bool _isDead = false;
    
    //pause
    public GameObject pauseMenuObject;
    private bool _isPaused = false;

    [Header("Components")]
    public GameObject spriteHolder;
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    private Rigidbody2D _rb;
    private BoxCollider2D _bc;
    public LayerMask groundLayer;

    [Header("Collision")]
    [SerializeField]
    public bool isGrounded = false;
    [SerializeField]
    private bool isGroundedLeft = false;
    [SerializeField]
    private bool isGroundedRight = false;
    [SerializeField]
    private bool isOnWall = false;
    [SerializeField]
    private bool isOnLeftWall = false;
    [SerializeField]
    private bool isOnRightWall = false;

    [SerializeField] private bool isBangingHeadLeft = false;
    [SerializeField] private bool isBangingHeadRight = false;
    [SerializeField] private bool isBangingHeadBoth = false;

    public Vector3 raycastOffsetLeft;
    public Vector3 raycastOffsetRight;
    public float lengthToGround = 0.83f;
    public float lengthToWall = 0.4f;
    public Vector3 cornerCorrectionOffsetLeft;
    public Vector3 cornerCorrectionOffsetRight;
    public Vector3 cornerCorrectionInnerOffset;
    public float cornerCorrectionLength = 0.25f;
    
    // climb ledge
    public Vector3 climbLedgeOffset;
    public bool feetTouchingWall = false;
    private IEnumerator _climbingLedge = null;

    [Header("Particles")]
    public ParticleSystem dust;

    private static readonly int IsJumping = Animator.StringToHash("isJumping");
    private static readonly int IsRunning = Animator.StringToHash("isRunning");
    private static readonly int IsFalling = Animator.StringToHash("isFalling");
    private static readonly int IsGrabbing = Animator.StringToHash("isGrabbing");
    private static readonly int IsSliding = Animator.StringToHash("isSliding");
    private static readonly int IsClimbing = Animator.StringToHash("isClimbing");

    // Start is called before the first frame update
    void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        _bc = GetComponent<BoxCollider2D>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 pos = transform.position;
        
        bool wasGrounded = isGrounded;
        isGroundedLeft = Physics2D.Raycast(pos - raycastOffsetLeft, Vector2.down, lengthToGround, groundLayer);
        isGroundedRight = Physics2D.Raycast(pos + raycastOffsetRight, Vector2.down, lengthToGround, groundLayer);
        isGrounded = isGroundedLeft || isGroundedRight;

        isOnLeftWall = Physics2D.Raycast(pos, Vector2.left, lengthToWall, groundLayer);
        isOnRightWall = Physics2D.Raycast(pos, Vector2.right, lengthToWall, groundLayer);
        isOnWall = isOnLeftWall || isOnRightWall;
        
        isBangingHeadLeft = Physics2D.Raycast(pos - cornerCorrectionOffsetLeft, Vector2.up, cornerCorrectionLength, groundLayer) && !Physics2D.Raycast(pos - cornerCorrectionOffsetLeft + cornerCorrectionInnerOffset, Vector2.up, cornerCorrectionLength, groundLayer);
        isBangingHeadRight = Physics2D.Raycast(pos + cornerCorrectionOffsetRight, Vector2.up, cornerCorrectionLength, groundLayer) && !Physics2D.Raycast(pos + cornerCorrectionOffsetRight - cornerCorrectionInnerOffset, Vector2.up, cornerCorrectionLength, groundLayer);
        isBangingHeadBoth = isBangingHeadLeft && isBangingHeadRight;
        
        bool feetOnLeftWall = Physics2D.Raycast(pos + climbLedgeOffset, Vector2.left, lengthToWall, groundLayer);
        bool feetOnRightWall = Physics2D.Raycast(pos + climbLedgeOffset, Vector2.right, lengthToWall, groundLayer);
        feetTouchingWall = feetOnLeftWall || feetOnRightWall;


        if(!wasGrounded && isGrounded)
        {
            StartCoroutine(SqueezeSprites(new Vector2(1.25f, 0.8f), 0.05f));
        }

        if (isGrounded || isOnWall)
        {
            _lastValidGroundTouch = Time.time + groundTouchedValidTil;
            wallSlide = false;
        }

        if(isGrounded && _grabbing == null)
        {
            _stamina = 100;
            spriteRenderer.color = Color.white;
        }
        
        if (_stamina == 0)
        {
            spriteRenderer.color = Color.red;
        }

        if (isOnWall && _grabbing == null && !isGrounded && _rb.velocity.y <= 0)
        {   
            wallSlide = true;
            spriteRenderer.flipX = !isOnLeftWall;
            WallSlide();
        }
    }

    private void FixedUpdate()
    {
        if (_isPaused || _isDead)
        {
            return;
        }
        Vector2 currentDirection = dir;

        UpdateDirection(currentDirection);
        UpdateAnimator(currentDirection);

        MoveCharacter(currentDirection);

        if(_lastJumped > Time.time && _lastValidGroundTouch > Time.time)
        {
            _lastValidGroundTouch = 0;
            Jump();
        }

        UpdatePhysics(currentDirection);
    }

    /**
     * Tinkers with the physics of our character depending on the current player state
     */
    private void UpdatePhysics(Vector2 input)
    {
        if ((isBangingHeadLeft || isBangingHeadRight) && !isBangingHeadBoth)
        {
            _bc.enabled = false;

            Vector2 correctionVelocity = isBangingHeadLeft ? Vector2.right : Vector2.left;
            correctionVelocity.y = _rb.velocity.y;

            _rb.velocity = correctionVelocity;
        }
        else
        {
            if (!_bc.enabled)
            {
                _bc.enabled = true;
                _rb.velocity = new Vector2(0, _rb.velocity.y);
            }
        }

        if (_dashing)
        {
            return;
        }

        if(_grabbing != null && isOnWall)
        {
            _rb.gravityScale = 0;
            return;
        }

        if (isGrounded)
        {
            // var velocity = _rb.velocity;
            // var changingDirection = input.x < 0 && velocity.x > 0 || input.x > 0 && velocity.x < 0;
            // rb.drag = Mathf.Abs(input.x) == 0 || changingDirection ? drag : 0;

            _rb.gravityScale = 0;
            hasDashed = false;
        }
        else
        {
            _rb.gravityScale = jumpingGravity;
            // rb.drag = drag * 0.15f;
            if (_rb.velocity.y < 0)
            {
                _rb.gravityScale *= fallMultiplier;
            }
            else if (_rb.velocity.y > 0 && !_jumping)
            {
                _rb.gravityScale *= fallMultiplier / 2;
            }
        }
    }

    /**
     * Move character in X&Y direction
     */
    private void MoveCharacter(Vector2 input)
    {
        //climbing up.
        if(_grabbing != null)
        {
            // still on wall, we can climb
            if (isOnWall)
            {
                _rb.velocity = new Vector2(0, input.y * climbSpeed);
                return;
            }
            
            // only feet left on wall, we have to climb the ledge
            if(feetTouchingWall)
            {
                if (_climbingLedge != null)
                {
                    return;
                }
                _climbingLedge = ClimbLedge();
                StartCoroutine(_climbingLedge);
                return;
            }
        }

        // stop climbing the ledge if user input steers in other direction
        if (_climbingLedge != null && Mathf.Abs(input.x) > 0f)
        {
            StopCoroutine(_climbingLedge);
            _climbingLedge = null;
        }

        _rb.velocity += Vector2.right * (input.x * runSpeed * Time.deltaTime);
        if(Mathf.Abs(_rb.velocity.x) > maxSpeed && !_dashing)
        {
            _rb.velocity = new Vector2(Mathf.Sign(_rb.velocity.x) * maxSpeed, _rb.velocity.y);
        }
    }

    /**
     * Coroutine that makes our player climb the ledge
     * Gets executed when in the climbing process, our players head doesn't touch the wall anymore.
     * This is the indicator that we have to climb the ledge.
     */
    private IEnumerator ClimbLedge()
    {
        // boost character up till our feet aren't touching the wall anymore
        while (feetTouchingWall)
        {
            _rb.AddForce(Vector2.up, ForceMode2D.Impulse);
            yield return new WaitForFixedUpdate();
        }
        
        spriteRenderer.flipX = !spriteRenderer.flipX;
        //_rb.velocity = Vector2.zero;
        
        // disable collider and move our character over the ledge
        _bc.enabled = false;
        
        while (!isGroundedLeft || !isGroundedRight)
        {
            Vector2 velocity = spriteRenderer.flipX ? Vector2.left : Vector2.right;
            velocity *= runSpeed * Time.deltaTime;
            _rb.velocity += velocity;
            yield return new WaitForFixedUpdate();
        }

        //stop velocity once we touch the ground after successfully climbing the ledge
        _rb.velocity = Vector2.zero;
        _bc.enabled = true;
        
        // we aren't holding on to anything, stop the coroutine.
        if (_grabbing != null)
        {
            StopCoroutine(_grabbing);
            _grabbing = null;

        }
        _climbingLedge = null;
    }
    
    /**
     * Makes the player jump by...
     * adding force to rigidbody
     * trigger animator isJumping value
     * drain stamina when on wall
     * play graphic/sound effects
     */
    public void Jump()
    {
        Vector2 jumpDirection = Vector2.up;
        // walljump
        SoundManagerScript.PlaySound("Jump");

        // drain stamina when jumping on wall
        if (_grabbing != null && _stamina > 0)
        {
            _stamina -= _stamina > 25 ? 25 : 0;
        }


        bool changeDirection = false;
        if (isOnWall && !isGrounded)
        {
            jumpDirection += isOnLeftWall ? Vector2.right : Vector2.left;
            changeDirection = true;
        }
        animator.SetTrigger(IsJumping);
        _rb.velocity = new Vector2(_rb.velocity.x, 0);
        _rb.AddForce(jumpDirection * jumpForce, ForceMode2D.Impulse);
        _lastJumped = 0;

        dust.Play();
        StartCoroutine(SqueezeSprites(new Vector2(0.8f, 1.25f), 0.05f));

        if (changeDirection)
        {
            UpdateDirection(jumpDirection);
        }
    }
    
    /**
     * Updates the facing direction of the player according to the user input
     */
    private void UpdateDirection(Vector2 input, bool ignoreWall = false)
    {
        if((_grabbing != null || wallSlide) && !ignoreWall)
        {
            if(isOnLeftWall)
            {
                spriteRenderer.flipX = false;
            } else if(isOnRightWall)
            {
                spriteRenderer.flipX = true;
            }
            return;
        }
        if(input.x == 0.0f)
        {
            return;
        }

        bool oldDirection = spriteRenderer.flipX;
        spriteRenderer.flipX = input.x < 0.0f;

        if (oldDirection != spriteRenderer.flipX)
        {
            dust.Play();
        }
    }

    /**
     * Updates animator values dependant on player state
     */
    private void UpdateAnimator(Vector2 input)
    {
        animator.SetBool(IsRunning, Mathf.Abs(_rb.velocity.x) > 0.1f);
        animator.SetBool(IsFalling, !isOnWall && _rb.velocity.y < -0.001f);
        animator.SetBool(IsGrabbing, _grabbing != null && isOnWall);
        animator.SetBool(IsSliding, isOnWall && !isGrounded && _grabbing == null && wallSlide);
        animator.SetBool(IsClimbing, _grabbing != null && (isOnWall || feetTouchingWall) && Mathf.Abs(_rb.velocity.y) > 0.1f);
    }

    /**
     * Draws out Gizmos of our RayCasts in Editor/Game
     */
    private void OnDrawGizmos()
    {
        var pos = transform.position;
        
        Gizmos.color = Color.red;
        Gizmos.DrawLine(pos + raycastOffsetRight, pos + raycastOffsetRight + Vector3.down * lengthToGround);
        Gizmos.DrawLine(pos - raycastOffsetLeft, pos - raycastOffsetLeft + Vector3.down * lengthToGround);
        
        //climbing (hands)
        Gizmos.DrawLine(pos, pos + Vector3.left * lengthToWall);
        Gizmos.DrawLine(pos, pos + Vector3.right * lengthToWall);
        
        //climbing (ledge)
        Gizmos.DrawLine(pos + climbLedgeOffset, pos + climbLedgeOffset + Vector3.left * lengthToWall);
        Gizmos.DrawLine(pos + climbLedgeOffset, pos + climbLedgeOffset + Vector3.right * lengthToWall);
        
        Gizmos.DrawLine(pos + cornerCorrectionOffsetRight - cornerCorrectionInnerOffset, pos + cornerCorrectionOffsetRight - cornerCorrectionInnerOffset + Vector3.up * cornerCorrectionLength);
        Gizmos.DrawLine(pos - cornerCorrectionOffsetLeft + cornerCorrectionInnerOffset, pos - cornerCorrectionOffsetLeft + cornerCorrectionInnerOffset + Vector3.up * cornerCorrectionLength);

        Gizmos.DrawLine(pos + cornerCorrectionOffsetRight, pos + cornerCorrectionOffsetRight + Vector3.up * cornerCorrectionLength);
        Gizmos.DrawLine(pos - cornerCorrectionOffsetLeft, pos - cornerCorrectionOffsetLeft + Vector3.up * cornerCorrectionLength);
    }

    /**
     * Adds a squeeze effect to the character sprites.
     * Used at jump start and landing
     */
    IEnumerator SqueezeSprites(Vector2 squeeze, float animTime)
    {
        Vector3 squeezedScale = squeeze;
        squeezedScale.z = 1.0f;

        float timePassed = 0;

        while(timePassed <= 1.0f)
        {
            timePassed += Time.deltaTime / animTime;
            spriteHolder.transform.localScale = Vector3.Lerp(Vector3.one, squeezedScale, timePassed);
            yield return null;
        }
        timePassed = 0;
        while(timePassed <= 1.0f)
        {
            timePassed += Time.deltaTime / animTime;
            spriteHolder.transform.localScale = Vector3.Lerp(squeezedScale, Vector3.one, timePassed);
            yield return null;
        }
    }
    
    /**
     * Unity Input system Event Handler for Move
     */
    public void OnMove(InputAction.CallbackContext ctx)
    {
        if (_isDead)
        {
            return;
        }
        dir = ctx.ReadValue<Vector2>();
    }

    /**
     * Unity Input system Event Handler for Jumping
     */
    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (_isDead)
        {
            return;
        }
        
        switch (ctx.phase)
        {
            case InputActionPhase.Started:
                _lastJumped = Time.time + jumpDelay;
                break;
            case InputActionPhase.Performed:
                _jumping = true;
                break;
            default:
                _jumping = false;
                break;
        }
    }

    /**
     * Unity Input system Event Handler for Grabbing
     */
    public void OnGrab(InputAction.CallbackContext ctx)
    {
        if (_isDead)
        {
            return;
        }
        switch (ctx.phase)
        {
            case InputActionPhase.Started:
                if (!isOnWall || _stamina <= 0)
                {
                    return;
                }
                _grabbing = Grab();
                StartCoroutine(_grabbing);
                break;
            case InputActionPhase.Performed:
                if (!isOnWall)
                {
                    if (_grabbing != null)
                    {
                        StopCoroutine(_grabbing);
                        _grabbing = null;
                    }
                }
                else
                {
                    if (_grabbing == null && _stamina > 0)
                    {
                        _grabbing = Grab();
                        StartCoroutine(_grabbing);
                    }
                }
                break;
            default:
                if (_grabbing != null)
                {
                    StopCoroutine(_grabbing);
                    _grabbing = null;
                }
                break;
        }
    }

    /**
     * Unity Input system Event Handler for Grabbing/Climbing
     */
    private IEnumerator Grab()
    {
        int stage = 0;
        while (_stamina > 0)
        {
            IEnumerator flashing = null;

            //base stamina removal
            _stamina -= 1;
            
            // moving on the wall, drain more stamina
            if (Mathf.Abs(_rb.velocity.y) > 0.1f)
            {
                _stamina -= 1;
            }
            
            if (stage == 0 && _stamina <= 50)
            {
                stage = 1;
                flashing = FlashCharacter(Color.red, 2);
            }
            else if (stage == 1 && _stamina <= 30)
            {
                stage = 2;
                flashing = FlashCharacter(Color.red, 4);
            }
            else if (stage == 2 && _stamina <= 10)
            {
                stage = 3;
                flashing = FlashCharacter(Color.red, 6);
            }

            if (flashing != null)
            {
                if (_flashing != null)
                {
                    StopCoroutine(_flashing);
                }
                _flashing = flashing;
                StartCoroutine(_flashing);
            }
            
            yield return new WaitForSeconds(0.1f);
        }

        if (_flashing == null)
        {
            StopCoroutine(_flashing);
            _flashing = null;
        }
        _grabbing = null;
    }
    
    /**
     * Flashes the character by changing the color used by our Shader Graph (Universal Render Pipeline)
     */
    private IEnumerator FlashCharacter(Color color, int flashAmountInOneSecond)
    {
        float endTime = Time.fixedTime + 1;
        while (endTime > Time.fixedTime)
        {
            spriteRenderer.color = color;
            yield return new WaitForSeconds(1.0f / flashAmountInOneSecond / 2);
            spriteRenderer.color = Color.white;
            yield return new WaitForSeconds(1.0f / flashAmountInOneSecond / 2);
        }
    }

    /**
     * Unity Input system Event Handler for Dash
     */
    public void OnDash(InputAction.CallbackContext ctx)
    {
        if (_isDead)
        {
            return;
        }
        switch (ctx.phase)
        {
            case InputActionPhase.Started:
            case InputActionPhase.Performed:
                StartCoroutine(Dash());
                break;
        }
    }

    /**
     * Coroutine executed when dashing
     */
    private IEnumerator Dash()
    {
        if (hasDashed)
            yield break;
        
        _rb.velocity = Vector2.zero;
        dust.Play();
        SoundManagerScript.PlaySound("Dash");
        CameraShake.Instance.ShakeCamera(2f, 0.2f);
        FindObjectOfType<GhostTrail>().ShowGhost();

        float disableGravityForSecs = 0.075f;
        if (dir.Equals(Vector2.zero) || dir.Equals(Vector2.left) || dir.Equals(Vector2.right))
        {
            Vector2 dashingDirection = spriteRenderer.flipX ? Vector2.left : Vector2.right;
            dashingDirection += new Vector2(0, 0.1f);
            _rb.velocity += dashingDirection * dashSpeed;
            disableGravityForSecs *= 1.5f;
        }
        else
        {
            _rb.velocity += dir.normalized * dashSpeed;
        }
        hasDashed = true;
        _dashing = true;
        float gravityScale = _rb.gravityScale;
        _rb.gravityScale = 0;
        yield return new WaitForSeconds(disableGravityForSecs);
        _rb.gravityScale = gravityScale;
        _dashing = false;
    }

    /**
     * Handles entering the collider of other game objects
     * CP, Death, Dash-Orbs, ...
     */
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Coin"))
        {
            cp++;
            Destroy(other.gameObject);
            SoundManagerScript.PlaySound("CoinPickup");
        }

        if (other.gameObject.CompareTag("Death"))
        {
            StartCoroutine(Die());
            SoundManagerScript.PlaySound("Death");
        }

        if (other.gameObject.CompareTag("Orb"))
        {
            SoundManagerScript.PlaySound("DashOrb");
            Destroy(other.gameObject);
            hasDashed = false;
            _stamina = 100;
        }
    }

    /**
     * Coroutine exected once the character dies, delays death for 200ms so it feels better
     * Spawns player at latest checkpoint
     */
    private IEnumerator Die()
    {
        _isDead = true;
        yield return new WaitForSeconds(0.2f);

        if (!Checkpoint.CurrentCheckpoint)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            yield break;
        }
        
        transform.position = Checkpoint.CurrentCheckpoint.transform.position;
        yield return new WaitForFixedUpdate();
        _isDead = false;
    }
    
    /**
     * Wall Sliding logic when character is on wall but not grabbing onto it or mana equal 0
     */
    private void WallSlide()
    {
        Vector2 currentVelocity = _rb.velocity;
        bool pushingWall = currentVelocity.x > 0 && isOnRightWall 
                           || currentVelocity.x < 0 && isOnLeftWall;
        float push = pushingWall ? 0 : currentVelocity.x;

        _rb.velocity = new Vector2(push, -slideSpeed);
    }
    
    /**
     * Unity Input system Event Handler for Interactions
     */
    public void OnInteract(InputAction.CallbackContext ctx)
    {
        switch (ctx.phase)
        {
            case InputActionPhase.Started: 
                SignManager.InteractWithCurrentSign();
                break;
            default:
                break;
        }
    }

    /**
     * Unity Input system Event Handler for ESC/pause
     */
    public void OnPause(InputAction.CallbackContext ctx)
    {
        switch(ctx.phase)
        {
            case InputActionPhase.Started: 
                Pause();
                break;
            default:
                break;
        }
    }

    /**
     * Disables pause menu and switches to gameplay action map
     */
    public void Resume()
    {
        pauseMenuObject.SetActive(false);
        GetComponent<PlayerInput>().SwitchCurrentActionMap("Gameplay");
        _isPaused = false;
    }

    /**
     * Enables pause menu and switches to menu action map
     */
    private void Pause()
    {
        pauseMenuObject.SetActive(true);
        GetComponent<PlayerInput>().SwitchCurrentActionMap("Menu");
        _isPaused = true;
        StartCoroutine(StartPause());
    }

    /**
     * Pause Coroutine saves important rigidbody info for the duration of the pause
     */
    private IEnumerator StartPause()
    {
        var gravityScale = _rb.gravityScale;
        var velocity = _rb.velocity;

        _rb.gravityScale = 0;
        _rb.velocity = Vector2.zero;

        yield return new WaitUntil(() => _isPaused == false);

        _rb.gravityScale = gravityScale;
        _rb.velocity = velocity;
    }
}
