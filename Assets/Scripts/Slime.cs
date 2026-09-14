using UnityEngine;

public class Slime : Monster
{
    protected Rigidbody2D rigid;              // 2D 물리 연산을 다루기 위한 컴포넌트 변수

    private float constMoveSpeed = 0f;                        //슬라임의 이동속도 상수화
    [SerializeField] private float maxIdleMoveDistance = 3f;         //슬라임이 플레이어를 인식하지 않을 때 이동할 수 있는 최대 거리
    [SerializeField] private float noticeMoveDistance = 1f;          //슬라임이 플레이어를 인식했을때 매번 이동하는 거리
    private Vector2 startPosition;                                   //슬라임의 처음 위치
    private float curMoveDistance = 0f;                              //슬라임이 이동한 거리
    private int moveDirection = 1;                                   //슬라임이 이동할 방향(1: 오른쪽, -1: 왼쪽)

    private bool isGround;           //슬라임이 이동할 때 바로앞의 땅을 감지하는 변수
    private float checkRadius = 0.2f;       //슬라임이 이동할 때 바로앞의 땅을 감지하는 반지름

    private int isPause = 0;              //슬라임이 일시정지 상태인지 나타내는 변수

    protected override void Start()
    {
        base.Start();
        rigid = GetComponent<Rigidbody2D>();

        constMoveSpeed = moveSpeed;                  //슬라임의 이동속도 상수화
        startPosition = transform.position;          //슬라임의 처음 위치 저장
    }

    protected override void Update()
    {
        base.Update();

        if (!isGround) //앞에 땅이 없으면
        {
            isWait = true; //대기상태 갱신
        }

        anim.SetBool("Wait", isWait); //대기 애니메이션 상태 전환
    }

    //물리 이동 부분
    //이동 로직: 현재 자기 위치 저장 -> 플레이어 인식 여부에 따라 이동 방향 결정 -> 특정 거리만큼 이동 -> 자기 위치 저장
    void FixedUpdate()
    {
        //가상의 원을 그려서 'Ground' 레이어와 닿아있는지 체크
        //슬라임이 이동할 방향의 바로 앞에 땅이 있는지 확인
        isGround = groundChecker(checkRadius);

        //슬라임이 이동한 거리 계산
        curMoveDistance = Mathf.Abs(transform.position.x - startPosition.x);

        stateUpdate();

        //슬라임이 대기 상태일 때
        if (isWait)
        {
            moveSpeed = 0; //이동속도 0으로 설정
        }

        if (isPause == 1)
        {
            moveSpeed = 0;
        }

        rigid.linearVelocity = new Vector2(moveDirection * moveSpeed, rigid.linearVelocity.y);  //슬라임 이동
    }
    private void pauseTemporary(float speed)
    {
        if (speed <= 0)
        {
            isPause = 1;
        }
        else
        {
            isPause = 0;
        }
    }

    public override void stateEnter()               //상태 진입시 한번만
    {
        switch (curState)
        {
            case state.idle:                // Idle 상태
                break;
            case state.notice:              // Notice 상태
                flip(player.position.x > transform.position.x ? 1 : -1);      //플레이어가 인식 범위에 들어선 순간에 플레이어를 바라봄
                break;
            case state.attack:              // Attack 상태
                break;
        }
        return;
    }          
    public override void stateUpdate()              //상태 유지시 매 프레임마다
    {
        switch (curState)
        {
            case state.idle:                // Idle 상태

                moveSpeed = constMoveSpeed;

                if (curMoveDistance > maxIdleMoveDistance)
                {
                    isWait = true; //대기상태 갱신
                    curMoveDistance = 0f; //이동 거리 초기화

                    transform.position = new Vector2(startPosition.x + moveDirection * maxIdleMoveDistance, transform.position.y);

                    startPosition = transform.position; //슬라임의 현재 위치를 startPosition으로 갱신
                }

                if (isWait)
                {
                    if (curWaitTime < maxWaitTime)
                    {
                        isWait = true;
                        curWaitTime += Time.deltaTime;
                    }
                    else if (curWaitTime >= maxWaitTime || !isGround)
                    {
                        moveDirection = (int)(startPosition.x) <= (int)(transform.position.x) ? -1 : 1; //이동 방향 반전
                        flip(moveDirection);                                             //좌우 반전
                        isWait = false;
                        curWaitTime = 0f;
                    }
                }

                break;

            case state.notice:              // Notice 상태    

                moveSpeed = constMoveSpeed*1.2f;

                if (curMoveDistance > noticeMoveDistance)
                {
                    isWait = true; //대기상태 갱신
                    curMoveDistance = 0f; // 이동 거리 초기화

                    transform.position = new Vector2(startPosition.x + moveDirection * noticeMoveDistance, transform.position.y);
                }

                // 플레이어 방향으로 이동
                //아주 잠깐 멈췄다가 플레이어를 향해 일정 거리를 전진하는 방식
                if (isWait)
                {
                    moveDirection = player.position.x > transform.position.x ? 1 : -1; //플레이어 방향으로 이동
                    startPosition = transform.position;
                    flip(moveDirection);
                    if (isGround) { isWait = false; }
                }

                break;

            case state.attack:              // Attack 상태

                moveSpeed = 0f;

                if (anim != null)
                {
                    anim.SetBool("Attack", true);       //에니메이션에 히트박스 이벤트 설정이 되어있어서 별도로 히트박스 이벤트 작성할 필요 없음
                }

                break;
        }

        return;
    }         
    public override void stateEnd()                 //상태 종료시 한번만
    {
        switch (curState)
        {
            case state.idle:                // Idle 상태
                break;
            case state.notice:              // Notice 상태
                break;
            case state.attack:              // Attack 상태
                if (anim != null)
                {
                    anim.SetBool("Attack", false);      //공격 애니메이션 끔
                }
                break;
        }
        return;
    }           
}
