using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float moveSpeed = 5f;    
    public float jumpForce = 7f;

    public float fallMultiplier = 2.5f; // 떨어질 때 추가로 받을 중력 배수
    public float lowJumpMultiplier = 2f;    //점프기를 짧게 눌렀을 때 받는 중력

    public bool isGrounded;            // 현재 바닥인지 확인
    public Transform groundCheck;      // 발밑 위치를 정할 오브젝트
    public float checkRadius = 0.2f;   // 감지 범위
    public LayerMask isLayer;          // 바닥으로 인식할 레이어

    private Rigidbody2D rigid;
    private float moveInput;

    private Animator anim;                 // 애니메이션 조종기
    private SpriteRenderer spriteRenderer; // 좌우 방향 뒤집기용

    void Start()
    {
        rigid = GetComponent<Rigidbody2D>();

        anim = GetComponent<Animator>();                 
        spriteRenderer = GetComponent<SpriteRenderer>(); 
    }


    void Update()
    {
        // 핵심: 발밑에 가상의 원을 그려서 'Ground' 레이어와 닿아있는지 체크합니다.
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, checkRadius, isLayer);

        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            rigid.linearVelocity = new Vector2(rigid.linearVelocity.x, jumpForce);
        }

        // 4. 떨어질 때 묵직하게
        if (rigid.linearVelocity.y < 0)
        {
            rigid.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.deltaTime;
        }
        // 5. 짧은 점프 처리 
        // 캐릭터가 위로 올라가는 중(y > 0) 인데, 점프 버튼(Jump)에서 손을 뗐다면(!Input.GetButton)
        else if (rigid.linearVelocity.y > 0 && !Input.GetButton("Jump"))
        {
            // 중력을 강하게 줘서 상승을 확 꺾어버린다.
            rigid.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.deltaTime;
        }

        // 캐릭터 좌우 뒤집기
        if (moveInput > 0)
        {
            spriteRenderer.flipX = false; // 오른쪽을 볼 때 (기본)
        }
        else if (moveInput < 0)
        {
            spriteRenderer.flipX = true;  // 왼쪽을 볼 때 (반전)
        }

        // 애니메이터에게 현재 속도(절댓값) 넘겨주기
        anim.SetFloat("Speed", Mathf.Abs(moveInput));

        anim.SetBool("isGrounded", isGrounded);
    }

    void FixedUpdate()
    {
        rigid.linearVelocity = new Vector2(moveInput * moveSpeed, rigid.linearVelocity.y);
    }
}
