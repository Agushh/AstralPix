using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
public class PlayerScript : MonoBehaviour
{
    // Components
    [Header("Components :")]
    CapsuleCollider2D cc2D;
    Animator anim;
    Camera mainCam;
    Rigidbody2D rb2D;
    [SerializeField] WorldManager worldManager;
    [SerializeField] TileConfig blocks;
    [SerializeField] InputSystem_Actions controls;
    InputAction interactAction;
    InputAction leftClick;
    InputAction rightClick;

    #region Animation
    [Header("Animation : ")]

    [SerializeField] int IdleIndex = 0;
    
    int actualIdleAnimationIndex, state, currentState;

    static readonly int Idle1_anim = Animator.StringToHash("Idle1");
    static readonly int Idle2_anim = Animator.StringToHash("Idle2");

    static readonly int Walk_anim = Animator.StringToHash("Walk");
    static readonly int RWalk_anim = Animator.StringToHash("R-Walk");

    static readonly int CrouchIdle_anim = Animator.StringToHash("IdleCrouch");
    static readonly int CrouchWalk_anim = Animator.StringToHash("CrouchWalk");
    static readonly int RCrouchWalk_anim = Animator.StringToHash("R-CrouchWalk");

    static readonly int JumpAscend_anim = Animator.StringToHash("JumpAscend");
    static readonly int JumpUp_anim = Animator.StringToHash("JumpUp");
    static readonly int JumpDescend_anim = Animator.StringToHash("JumpDescend");
    static readonly int JumpDown_anim = Animator.StringToHash("JumpDown");
    
    Dictionary<int, float> clipDurations = new();

    #endregion

    #region Inputs

    [Header("Inputs : ")]

    [SerializeField] float angle;
    [SerializeField] Vector2 mousePosition;
    [SerializeField] Vector2Int blockCursor, chunkCursor, blockRelativeToChunk;
    public Vector2Int BlockCursor => blockCursor;
    public Vector2Int ChunkCursor => chunkCursor;

    public Vector2Int BlockRelativeToChunk => blockRelativeToChunk;

    int selectedBlockIndex = 0;
    public int SelectedBlockIndex => selectedBlockIndex;
    int maxBlockTypes; 

    [SerializeField] float mouseDistance;
    [SerializeField] Vector2 moveInput;
    bool isCrouching = false;

    #endregion

    #region Arms, Head and items

    [Serializable]
    struct arm
    {
        public ItemData equippedItem;
        public int itemId;
        public SpriteRenderer itemRenderer;

        public Vector2 displacement;

        public Transform handPivot, hand;
        public Transform pivot, crouchPivot;
        public GameObject staticArm, dynamicArm;

        public Vector2 startPosition;
    }


    [Header("Arms (pivots and items): ")]
    [SerializeField] arm leftArm;
    
    [SerializeField] arm rightArm;
    
    bool isHandOnCrouchState = false;
    bool twoHanded = false;

    [Header("Head Pivots: ")]
    [SerializeField] GameObject head;
    [SerializeField] GameObject anglePivot, headPivot, crouchHeadPivot;
    Vector2 headStartPosition;


    #endregion

    #region Movement


    [Header("Horizontal Movement : ")]

    [SerializeField] float acceletarion;
    [SerializeField] float decceleration, maxSpeed, crouchScale;

    [Header("JumpingRayCast : ")]
    [SerializeField] LayerMask jumpableLayerMask;
    [SerializeField] float extraHeight = 0.1f;
    
    [Header("Jump : ")]
    [SerializeField] float jumpForce = 7f;
    [SerializeField] float jumpEndEarlyFactor = 0.5f;
    [SerializeField] float maxFallSpeed = 10f;
    [SerializeField] float gravityScale = 1f;
    [SerializeField] float jumpBufferTime = 0.2f;
    [SerializeField] float apexModifier = 2f, apexThreshold = 0.5f;
    
    bool grounded = false;
    
    bool jumping = false;
    
    bool jumpActivator = false;
    
    bool jumpButton;
    
    float jumpBufferCounter = 0;

    #endregion
    private void Awake()
    {
        controls = new();

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Player.Jump.started += ctx => jumpButton = true;
        controls.Player.Jump.canceled += ctx => jumpButton = false;

        controls.Player.Crouch.started += ctx => isCrouching = true;
        controls.Player.Crouch.canceled += ctx => isCrouching = false;

        interactAction = controls.FindAction("interact");
        leftClick = controls.FindAction("LeftMouseButton");
        rightClick = controls.FindAction("RightMouseButton");

        cc2D = GetComponent<CapsuleCollider2D>();
        rb2D = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;
        anim = GetComponent<Animator>();

        clipDurations = anim.runtimeAnimatorController.animationClips
            .ToDictionary(c => Animator.StringToHash(c.name), c => c.length);
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();



    void Start()
    {
        leftArm.startPosition = leftArm.staticArm.transform.localPosition;
        rightArm.startPosition = rightArm.staticArm.transform.localPosition;
        headStartPosition = head.transform.localPosition;

        leftArm.displacement = leftArm.pivot.localPosition - leftArm.handPivot.localPosition;
        rightArm.displacement = rightArm.pivot.localPosition - rightArm.handPivot.localPosition;
        maxBlockTypes = blocks.Tiles.Length;
    }



    void Update()
    {

        bool rightItem = rightArm.equippedItem != null;
        bool leftItem = leftArm.equippedItem != null;
        twoHanded = rightItem && rightArm.equippedItem.TwoHanded;

        //seteo de items en manos
        if ((!rightItem && rightArm.itemId != 0) || (rightItem && rightArm.itemId != rightArm.equippedItem.itemID))
        {
            ChangeHandItem(rightArm.itemRenderer, rightArm.equippedItem, out rightArm.itemId, rightArm.hand);
        }

        if ((!leftItem && leftArm.itemId != 0) || (leftItem && leftArm.itemId != leftArm.equippedItem.itemID))
        {
            if(twoHanded) leftArm.equippedItem = null;            
            else ChangeHandItem(leftArm.itemRenderer, leftArm.equippedItem, out leftArm.itemId, leftArm.hand);
        }


        //input

        MouseInput();
        mainCam.transform.position =new(transform.position.x, transform.position.y,-10);

        if (Mouse.current.scroll.ReadValue().y != 0)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;

            if (scroll > 0)
                selectedBlockIndex++;
            else if (scroll < 0)
                selectedBlockIndex--;

            // Mantener el índice dentro de rango
            if (selectedBlockIndex < 1) selectedBlockIndex = maxBlockTypes - 1;
            if (selectedBlockIndex >= maxBlockTypes) selectedBlockIndex = 1;

        }
        bool isFront = false;
        if (interactAction.IsPressed())
        {
            isFront = true;
        }
        if (rightClick.IsPressed())
        {
            worldManager.PlaceBlock(chunkCursor, blockRelativeToChunk, selectedBlockIndex, isFront);
        }
        if(leftClick.IsPressed())
        {
            worldManager.PlaceBlock(chunkCursor, blockRelativeToChunk, 0, isFront);
        }

        //---------------

        //cambio de brazos estaticos a dinamicos.
        ChangeStaticDynamicArms(leftArm, leftItem || twoHanded);
        ChangeStaticDynamicArms(rightArm, rightItem);

        //movimiento de brazos y cabeza respecto al apuntado.
        RotationLogic(leftItem, rightItem);

        //change hand position in crouching and standing
        if (isCrouching != isHandOnCrouchState)
        {
            if (isCrouching)
            {
                leftArm.handPivot.localPosition = (Vector2)leftArm.crouchPivot.transform.localPosition - leftArm.displacement;
                rightArm.handPivot.localPosition = (Vector2)rightArm.crouchPivot.transform.localPosition - rightArm.displacement;
            }
            else
            {
                leftArm.handPivot.localPosition = (Vector2)leftArm.pivot.localPosition - leftArm.displacement;
                rightArm.handPivot.localPosition = (Vector2)rightArm.pivot.localPosition - rightArm.displacement;
            }
            isHandOnCrouchState = isCrouching;
        }

        //Animations

        switch (IdleIndex)
        {
            case 1: actualIdleAnimationIndex = Idle1_anim; break;
            case 2: actualIdleAnimationIndex = Idle2_anim; break;
        }

        state = GetState();
        if (state != currentState)
        {
            if(state == JumpUp_anim)
            {
                StartCoroutine(PlayAnimationEnumerator(JumpAscend_anim, JumpUp_anim));
                currentState = state;
            }
            else if(state == JumpDown_anim)
            {
                StartCoroutine(PlayAnimationEnumerator(JumpDescend_anim, JumpDown_anim));
                currentState = state;
            }
            else
            {
                anim.CrossFade(state, 0, 0, 0, 0);
                currentState = state;
            }
            
        }
    }


    private void FixedUpdate()
    {
        GroundDetector();
        
        walk();
        
        jump();
    }

    void walk()
    {
        float speed = 0;
        if (moveInput.x != 0)
        {
            speed = Mathf.Lerp(rb2D.linearVelocityX, moveInput.x * maxSpeed * (isCrouching ? crouchScale : 1), acceletarion);
            rb2D.linearVelocityX = speed;
        }
        else
        {
            speed = Mathf.Lerp(rb2D.linearVelocityX, 0, decceleration);
            rb2D.linearVelocityX = speed;
        }
    }

    void jump()
    {
        if (jumpButton) jumpBufferCounter = jumpBufferTime;
        jumpBufferCounter -= Time.fixedDeltaTime;
        jumpActivator = jumpBufferCounter > 0;
        
        if (jumpActivator && grounded)
        {
            jumpBufferCounter = jumpBufferTime;
            jumping = true;
            rb2D.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
        else if (!jumpButton && jumping) 
        {
            jumping = false;
            if (rb2D.linearVelocityY > 0)
            {
                rb2D.linearVelocityY = rb2D.linearVelocityY * jumpEndEarlyFactor;
            }
        }
        
        else if(grounded)
        {
            jumping = false;
        }

        if(rb2D.linearVelocityY < -maxFallSpeed) rb2D.linearVelocityY = -maxFallSpeed;
        if(rb2D.linearVelocityY < 0 && rb2D.linearVelocityY > -apexThreshold)
        {
            rb2D.gravityScale = gravityScale / apexModifier;
        }
        else if ( rb2D.linearVelocityY < -apexThreshold ) rb2D.gravityScale = gravityScale;
    }

    void GroundDetector()
    {
        //Visualize the Raycast on the editor
        Vector2 rayOrigin = new(transform.position.x, cc2D.bounds.min.y); Debug.DrawRay(rayOrigin, Vector2.down * extraHeight, Color.red);
        RaycastHit2D raycast = Physics2D.Raycast(rayOrigin, Vector2.down, extraHeight, jumpableLayerMask);
        grounded =  raycast.collider != null;
    }

    void ChangeStaticDynamicArms(arm Arm, bool armState)
    {
        Arm.staticArm.SetActive(armState);
        Arm.dynamicArm.SetActive(!armState);
    }
    IEnumerator PlayAnimationEnumerator(int anim1, int anim2)
    {
        anim.CrossFade(anim1, 0, 0, 0);

        yield return new WaitForSeconds(clipDurations[anim1]);

        if(!grounded)anim.CrossFade(anim2, 0, 0, 0);
    }


    int GetState()
    {
        if(rb2D.linearVelocityY > 0.3 && !grounded) return JumpUp_anim;
        if(rb2D.linearVelocityY < -0.3 && !grounded) return JumpDown_anim;
        if(!grounded) return JumpUp_anim;
        if (isCrouching)
        {
            if (leftArm.itemId != 0 || rightArm.itemId != 0)
            {
                if (rb2D.linearVelocityX > 0)
                {
                    if (angle < -90 || angle > 90) return RCrouchWalk_anim;
                    else return CrouchWalk_anim;
                }
                else if (rb2D.linearVelocityX < 0)
                {
                    if (angle < -90 || angle > 90) return CrouchWalk_anim;
                    else return RCrouchWalk_anim;
                }
                else return CrouchIdle_anim;
            }
            else if (rb2D.linearVelocityX > 0) return CrouchWalk_anim;
            else return CrouchIdle_anim;
        }
        else
        {
            if (leftArm.itemId != 0 || rightArm.itemId != 0)
            {
                if (rb2D.linearVelocityX > 0)
                {
                    if (angle < -90 || angle > 90) return RWalk_anim;
                    else return Walk_anim;
                }
                else if (rb2D.linearVelocityX < 0)
                {
                    if (angle < -90 || angle > 90) return Walk_anim;
                    else return RWalk_anim;
                }
                else return actualIdleAnimationIndex;
            }
            else if (rb2D.linearVelocityX != 0) return Walk_anim;
            else return actualIdleAnimationIndex;
        }
    }

    void ChangeHandItem(SpriteRenderer itemRenderer, ItemData equippedItem, out int itemIdHeldInHand, Transform hand)
    {
        if (equippedItem == null)
        {
            itemRenderer.sprite = null;
            itemIdHeldInHand = 0;
            return;
        }
        itemRenderer.sprite = equippedItem.itemSprite;
        itemIdHeldInHand = equippedItem.itemID;
        hand.localRotation = Quaternion.Euler(0, 0, equippedItem.itemAngle);
    }

    void MouseInput()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();

        mousePosition = mainCam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCam.nearClipPlane));
        
        blockCursor = new(Mathf.FloorToInt(mousePosition.x), Mathf.FloorToInt(mousePosition.y));
        chunkCursor = new(Mathf.FloorToInt((float)blockCursor.x/ ChunkManager.chunkSize), Mathf.FloorToInt((float)blockCursor.y / ChunkManager.chunkSize));
        blockRelativeToChunk = blockCursor - chunkCursor * ChunkManager.chunkSize;


        Vector2 direction = mousePosition - (Vector2)anglePivot.transform.position;

        mouseDistance = direction.magnitude;

        angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
    }

    void RotationLogic(bool moveLeft, bool moveRight)
    {
        if (twoHanded)
        {
            RotateAroundAxis(rightArm.staticArm.transform, rightArm.startPosition, isCrouching ? rightArm.crouchPivot : rightArm.pivot, angle, 1, rightArm.equippedItem.angleOffset);
            float tempAngle = angle;
            if (tempAngle > 90)
            {
                float a = 180 - tempAngle;
                a *= 1.5f;
                tempAngle = 180 - a;
            }
            else if (tempAngle < -90)
            {
                float a = - 180 - tempAngle;
                a *= 1.5f;
                tempAngle = -180 - a;
            }
            else tempAngle *= 1.5f;
            RotateAroundAxis(leftArm.staticArm.transform, leftArm.startPosition, isCrouching ? leftArm.crouchPivot : leftArm.pivot, tempAngle, 1, rightArm.equippedItem.angleOffset);
        }
        else
        {
            if (moveLeft)
                RotateAroundAxis(leftArm.staticArm.transform, leftArm.startPosition, isCrouching ? leftArm.crouchPivot : leftArm.pivot, angle, 1, leftArm.equippedItem.angleOffset);
            else
                leftArm.staticArm.transform.SetLocalPositionAndRotation(leftArm.startPosition, Quaternion.Euler(0f, 0f, 0f));

            if (moveRight)
                RotateAroundAxis(rightArm.staticArm.transform, rightArm.startPosition, isCrouching ? rightArm.crouchPivot : rightArm.pivot, angle, 1, rightArm.equippedItem.angleOffset);
            else
                rightArm.staticArm.transform.SetLocalPositionAndRotation(rightArm.startPosition, Quaternion.Euler(0f, 0f, 0f));
        }

        if (moveLeft || moveRight)
            RotateAroundAxis(head.transform, headStartPosition, isCrouching ? crouchHeadPivot.transform : headPivot.transform, angle, 2f, 0);
        else
            head.transform.SetLocalPositionAndRotation(headStartPosition, Quaternion.Euler(0f, 0f, 0f));

        if (moveLeft || moveRight)
        {
            if (angle < -90 || angle > 90)
            {
                // Invertir verticalmente el sprite
                gameObject.transform.localScale = new(-1, transform.localScale.y);
            }
            else
            {
                gameObject.transform.localScale = new(1, transform.localScale.y);
            }
        }
        else
        {
            //aca se detecta el cambio de direccion del personaje segun su movimiento, ya que no apunta al mouse
            if (rb2D.linearVelocityX > 0)
            {
                gameObject.transform.localScale = new(1, transform.localScale.y);
            }
            else if(rb2D.linearVelocityX < 0)
            {
                gameObject.transform.localScale = new(-1, transform.localScale.y);
            }
        }
    }

    void RotateAroundAxis(Transform obj, Vector2 StartPosition, Transform axis, float angle, float factorOfRotation = 1, float rotationOffset = 0)
    {
        float correctedAngle = (transform.localScale.x < 0) ? 180f - angle : angle;

        if (correctedAngle >= 180) correctedAngle -= 360;
        if (correctedAngle <= -180) correctedAngle += 360;

        correctedAngle /= factorOfRotation;

        correctedAngle += rotationOffset;


        Vector2 Offset = StartPosition - (Vector2)axis.localPosition;


        Vector2 Displacement = new(
            Offset.x * Mathf.Cos(Mathf.Deg2Rad * correctedAngle) - Offset.y * Mathf.Sin(Mathf.Deg2Rad * correctedAngle),
            Offset.x * Mathf.Sin(Mathf.Deg2Rad * correctedAngle) + Offset.y * Mathf.Cos(Mathf.Deg2Rad * correctedAngle)
            );

        obj.SetLocalPositionAndRotation((Vector2)axis.localPosition + Displacement, Quaternion.Euler(0f, 0f, correctedAngle));
    }
    void OnDestroy()
    {
        StopAllCoroutines();
    }
}
