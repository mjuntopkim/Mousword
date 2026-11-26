using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("이동")]
    public float moveSpeed = 5f;

    [Header("점프")]
    public float jumpForce = 7f;
    [SerializeField] float coyoteTime = 0.1f;           // 바닥을 떠난 뒤 점프를 허용할 시간
    [SerializeField] float jumpBufferTime = 0.15f;      // 점프 입력을 기억해 둘 시간
    [SerializeField] float jumpGroundIgnoreTime = 0.1f; // 점프 직후 바닥 판정을 무시할 시간 (2단 점프 방지)
    [SerializeField] float groundGraceTime = 0.06f;   // 접촉이 끊겨도 바닥으로 유지할 시간

    [Header("점프 중력 보정")]
    public float fallMultiplier = 2.5f;     // 떨어질 때 추가로 받을 중력 배수
    public float lowJumpMultiplier = 2f;    // 점프키를 짧게 눌렀을 때 받는 중력

    [Header("바닥 감지")]
    public bool isGrounded;                 // 현재 바닥인지 (접촉면 기준)
    [SerializeField] float maxSlopeAngle = 50f;   // 바닥으로 인정할 최대 경사 (이보다 가파르면 벽)
    public Transform groundCheck;           // 전사 스킬이 사용 (지우지 말 것)
    public float checkRadius = 0.2f;        // 전사 스킬이 사용 (지우지 말 것)
    public LayerMask isLayer;               // 바닥으로 인식할 레이어
    [SerializeField] float groundStickSpeed = 2f;

    // 켜면 좌우 이동과 점프 입력을 무시 (마법사 캐스팅, 포털, 보스방 연출 등). 낙하는 그대로 진행된다.
    public bool IsMovementLocked { get; set; }

    float lastGround = float.NegativeInfinity;      // 마지막으로 바닥에 있던 시각
    float lastJumpButton = float.NegativeInfinity;  // 마지막으로 점프 버튼을 누른 시각
    float lastJumpTime = float.NegativeInfinity;    // 마지막으로 점프한 시각
    float lastContactTime = float.NegativeInfinity; // 마지막으로 바닥과 접촉한 시각

    ContactFilter2D groundFilter;                           // 위를 향한 면만 걸러내는 필터
    readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    Vector2 groundNormal = Vector2.up;                      // 밟고 있는 면의 방향
    float defaultGravity;                                   // 원래 중력값

    private Rigidbody2D rigid;
    private float moveInput;
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    bool JustJumped => Time.time - lastJumpTime < jumpGroundIgnoreTime;


    // 씬이 변경되어도 플레이어 게임 오브젝트는 유지
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        rigid = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        defaultGravity = rigid.gravityScale;

        // 법선이 위쪽(90°) 기준 ±maxSlopeAngle 안에 있는 면만 바닥으로 인정
        groundFilter.SetLayerMask(isLayer);
        groundFilter.SetNormalAngle(90f - maxSlopeAngle, 90f + maxSlopeAngle);
    }

    // 전사 스킬이 이 스크립트를 끄고 중력을 저장할 때, 0이 아닌 원래 값이 저장되도록 되돌려 둠
    void OnDisable()
    {
        if (rigid != null) rigid.gravityScale = defaultGravity;
    }

    void Update()
    {
        if (PauseMenuUI.IsPaused) return; // 일시정지 중 입력 무시 (점프 입력이 쌓여 재개 직후 뛰는 것 방지)

        UpdateGround();

        moveInput = IsMovementLocked ? 0f : Input.GetAxisRaw("Horizontal");
        if (Input.GetButtonDown("Jump")) lastJumpButton = Time.time;

        HandleJump();
        ApplyJumpGravity();
        UpdateFacing();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (isGrounded && moveInput == 0f)
        {
            // 땅 위에 가만히 서 있음 → 중력 끄고 정지 (경사에서 안 미끄러짐)
            rigid.gravityScale = 0f;
            rigid.linearVelocity = Vector2.zero;
        }
        else if (isGrounded)
        {
            // 땅 위에서 이동 → 면을 따라 이동. 세로 속도가 남지 않아 방향을 틀어도 안 뜸
            rigid.gravityScale = defaultGravity;   // 중력이 매 스텝 살짝 눌러 줘서 바닥에 붙어 있음
            Vector2 alongGround = new Vector2(groundNormal.y, -groundNormal.x);
            // 면을 따라가는 속도 + 바닥 쪽(법선의 반대 방향)으로 살짝 누르는 속도
            rigid.linearVelocity = alongGround * moveInput * moveSpeed
                                 - groundNormal * groundStickSpeed;
        }
        else
        {
            // 공중 → 좌우만 제어하고 세로 속도는 유지
            rigid.gravityScale = defaultGravity;
            rigid.linearVelocity = new Vector2(moveInput * moveSpeed, rigid.linearVelocity.y);
        }
    }

    // ───────────── 바닥 감지 ─────────────

    // 실제로 닿아 있는 면 중 위를 향한 면이 있으면 바닥. 속도는 보지 않으므로 오르막에서도 정상 판정
    void UpdateGround()
    {
        int count = rigid.GetContacts(groundFilter, contacts);

        // 실제로 몸을 받쳐 주는 접촉만 바닥으로 인정
        // (한쪽 방향 발판을 아래에서 통과하는 중이면 접촉 반응이 꺼져 있음 → 제외)
        int supportCount = 0;
        float bestY = -1f;
        for (int i = 0; i < count; i++)
        {
            if (!contacts[i].enabled) continue;

            supportCount++;
            if (contacts[i].normal.y > bestY)
            {
                bestY = contacts[i].normal.y;
                groundNormal = contacts[i].normal;
            }
        }

        if (supportCount > 0)
        {
            lastContactTime = Time.time;
        }
        else if (JustJumped || Time.time - lastContactTime > groundGraceTime)
        {
            groundNormal = Vector2.up;   // 확실히 공중일 때만 초기화
        }

        bool recentlyTouched = Time.time - lastContactTime <= groundGraceTime;
        isGrounded = recentlyTouched && !JustJumped;

        if (isGrounded) lastGround = Time.time;
    }

    // ───────────── 점프 ─────────────

    void HandleJump() // 코요테 타임 + 점프 버퍼링
    {
        bool jumpBuffered = Time.time - lastJumpButton <= jumpBufferTime;
        bool coyoteValid = Time.time - lastGround <= coyoteTime;

        if (!IsMovementLocked && jumpBuffered && coyoteValid)
        {
            Jump();
            lastJumpButton = float.NegativeInfinity;   // 입력 소비
            lastGround = float.NegativeInfinity;       // 같은 유예 시간 안의 재점프 방지
        }
    }

    void Jump()
    {
        rigid.gravityScale = defaultGravity;           // 서 있다가 점프하면 중력이 0일 수 있으므로 복구
        rigid.linearVelocity = new Vector2(rigid.linearVelocity.x, jumpForce);
        lastJumpTime = Time.time;
        isGrounded = false;
    }

    void ApplyJumpGravity() // 공중에서만: 떨어질 때 묵직하게 + 짧은 점프
    {
        if (isGrounded) return;

        if (rigid.linearVelocity.y < 0)
            rigid.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        else if (rigid.linearVelocity.y > 0 && !Input.GetButton("Jump"))
            rigid.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
    }

    // ───────────── 표시 ─────────────

    void UpdateFacing()
    {
        if (moveInput > 0) spriteRenderer.flipX = false;
        else if (moveInput < 0) spriteRenderer.flipX = true;
    }

    void UpdateAnimator()
    {
        anim.SetFloat("Speed", Mathf.Abs(moveInput));
        anim.SetBool("isGrounded", isGrounded);
    }
}