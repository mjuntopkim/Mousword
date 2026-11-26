using UnityEngine;

public class Goblin : Monster
{
    protected Rigidbody2D rigid;              // 2D 물리 연산을 다루기 위한 컴포넌트 변수

    public float maxIdleMoveDistance = 3f;         //고블린이 플레이어를 인식하지 않을 때 이동할 수 있는 최대 거리
    [HideInInspector] public Vector2 startPosition;                                   //고블린의 처음 위치
    [HideInInspector] public float curMoveDistance = 0f;                              //고블린이 이동한 거리
    public float noticeMoveDistance = 1f;                                             //고블린이 플레이어를 인식했을때 매번 이동하는 거리
    [HideInInspector] public int moveDirection = 1;                                   //고블린이 이동할 방향(1: 오른쪽, -1: 왼쪽)

    [HideInInspector] public bool isGround;                   //고블린이 이동할 때 바로앞의 땅을 감지하는 변수
    private float checkRadius = 0.2f;       //고블린이 이동할 때 바로앞의 땅을 감지하는 반지름

    public GameObject Knife;
    public Transform knifeSpawnPoint;
    [HideInInspector] public bool isAttack = false; //고블린 검이 화면에 남아있음

    private Vector2 groundCheckOffset;                        //고블린 기준 GroundCheck의 월드 오프셋 (x는 절댓값)
    public float directionDeadZone = 0.1f;                    //플레이어와 x 차이가 이 값보다 작으면 방향을 바꾸지 않음
    public float kiteStartOffset = 1.5f;                      //거리 < attackRadius - kiteStartOffset 이면 카이팅 시작
    public float kiteStopOffset = 0.5f;                       //거리 > attackRadius - kiteStopOffset 이면 카이팅 종료
    [HideInInspector] public bool isKiting = false;           //카이팅(뒤로 도망) 중인지

    // ===== [원본 코드 - 문제 있음] =====
    // GroundCheck의 기준 위치를 저장하지 않아서, 방향 전환 시 땅 감지 위치를 맞출 수 없었음
    //protected override void Start()
    //{
    //    base.Start();
    //    rigid = GetComponent<Rigidbody2D>();
    //
    //    constMoveSpeed = moveSpeed;                  //고블린의 이동속도 상수화
    //    startPosition = transform.position;          //고블린의 처음 위치 저장
    //
    //    fsm = new FSM(new GoblinState.IdleState(this));
    //}
    // ===== [원본 코드 끝] =====

    protected override void Start()
    {
        base.Start();
        rigid = GetComponent<Rigidbody2D>();

        constMoveSpeed = moveSpeed;                  //고블린의 이동속도 상수화
        startPosition = transform.position;          //고블린의 처음 위치 저장

        //GroundCheck의 월드 오프셋 저장 (루트 scale이 -1이어도 영향 없도록 월드 좌표 기준, x는 절댓값)
        Vector2 offset = groundCheck.position - transform.position;
        groundCheckOffset = new Vector2(Mathf.Abs(offset.x), offset.y);
        updateGroundCheckPosition(moveDirection);

        fsm = new FSM(new GoblinState.IdleState(this));
    }

    //GroundCheck를 direction 방향 앞쪽으로 이동
    public void updateGroundCheckPosition(int direction)
    {
        groundCheck.position = new Vector2(transform.position.x + direction * groundCheckOffset.x,
                                           transform.position.y + groundCheckOffset.y);
    }

    //direction 방향 앞쪽에 땅이 있는지 확인 (GroundCheck를 옮기지 않고 검사만 함)
    public bool checkGroundAt(int direction)
    {
        Vector2 checkPos = new Vector2(transform.position.x + direction * groundCheckOffset.x,
                                       transform.position.y + groundCheckOffset.y);
        return Physics2D.OverlapCircle(checkPos, checkRadius, LayerMask.GetMask("Ground"));
    }

    //플레이어 방향 반환 (x 차이가 너무 작으면 fallback 방향 유지 → 제자리 좌우 떨림 방지)
    public int getDirectionToPlayer(int fallback)
    {
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) < directionDeadZone) return fallback;
        return dx > 0 ? 1 : -1;
    }

    //이동 방향 설정 + 스프라이트 반전 + GroundCheck 위치/감지 즉시 갱신
    public void setDirection(int direction)
    {
        moveDirection = direction;
        flip(direction);
        updateGroundCheckPosition(direction);
        isGround = groundChecker(checkRadius);
    }

    protected override void Update()
    {
        base.Update();
        if (!isGround) //앞에 땅이 없으면
        {
            isWait = true; //대기상태 갱신
        }

        anim.SetBool("Idle", isWait); //대기 애니메이션 상태 전환
        fsm.stateUpdate();
    }

    // ===== [원본 코드 - 문제 있음] =====
    // GroundCheck가 Body가 아닌 루트의 자식이라 flip()으로 따라가지 않음 → 이동 방향과 다른 쪽 땅을 감지
    //void FixedUpdate()
    //{
    //    //가상의 원을 그려서 'Ground' 레이어와 닿아있는지 체크
    //    //고블린이 이동할 방향의 바로 앞에 땅이 있는지 확인
    //    isGround = groundChecker(checkRadius);
    //
    //    //고블린이 이동한 거리 계산
    //    curMoveDistance = Mathf.Abs(transform.position.x - startPosition.x);
    //
    //    //고블린이 대기 상태일 때
    //    if (isWait)
    //    {
    //        moveSpeed = 0; //이동속도 0으로 설정
    //    }
    //
    //    rigid.linearVelocity = new Vector2(moveDirection * moveSpeed, rigid.linearVelocity.y);
    //}
    // ===== [원본 코드 끝] =====

    void FixedUpdate()
    {
        //GroundCheck를 현재 이동 방향 앞쪽으로 이동
        updateGroundCheckPosition(moveDirection);

        //가상의 원을 그려서 'Ground' 레이어와 닿아있는지 체크
        //고블린이 이동할 방향의 바로 앞에 땅이 있는지 확인
        isGround = groundChecker(checkRadius);

        //고블린이 이동한 거리 계산
        curMoveDistance = Mathf.Abs(transform.position.x - startPosition.x);

        //고블린이 대기 상태일 때
        if (isWait)
        {
            moveSpeed = 0; //이동속도 0으로 설정
        }

        rigid.linearVelocity = new Vector2(moveDirection * moveSpeed, rigid.linearVelocity.y);
    }

    public override void changeState(state newState)
    {
        switch (newState)
        {
            case state.idle:
                fsm.changeState(new GoblinState.IdleState(this));
                break;
            case state.notice:
                fsm.changeState(new GoblinState.NoticeState(this));
                break;
            case state.attack:
                fsm.changeState(new GoblinState.AttackState(this));
                break;
            case state.die:
                fsm.changeState(new CommonState.DieState(this));
                break;
        }
    }

    public void Attack(float speed)
    {
        if (Knife != null && knifeSpawnPoint != null)
        {
            GameObject knife = Instantiate(Knife, knifeSpawnPoint.position, Quaternion.identity);

            MonsterHitBox hitBox = knife.GetComponentInChildren<MonsterHitBox>();
            if (hitBox != null)
            {
                hitBox.SetMonster(this);
            }
            knife.GetComponent<Knife>().setSpeed(speed);
            knife.GetComponent<Knife>().setDirection(moveDirection);
            knife.GetComponent<Knife>().setGoblin(this);
        }
    }
}
