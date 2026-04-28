using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    public float moveSpeed = 5f;    
    public float jumpForce = 7f;    

    public bool isGrounded;            // 현재 바닥인지 확인
    public Transform groundCheck;      // 발밑 위치를 정할 오브젝트
    public float checkRadius = 0.2f;   // 감지 범위
    public LayerMask isLayer;          // 바닥으로 인식할 레이어

    private Rigidbody2D rigid;
    private float moveInput;

    void Start()
    {
        rigid = GetComponent<Rigidbody2D>();
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
    }

    void FixedUpdate()
    {
        rigid.linearVelocity = new Vector2(moveInput * moveSpeed, rigid.linearVelocity.y);
    }
}
