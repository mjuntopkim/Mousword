using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;
public class Slime : Monster
{
    protected Rigidbody2D rigid;              // 2D 물리 연산을 다루기 위한 컴포넌트 변수

    [SerializeField] private float maxNormalMoveDistance = 3f;      //슬라임이 플레이어를 인식하지 않을 때 이동할 수 있는 최대 거리
    [SerializeField] private float attackMoveDistance = 1f;          //슬라임이 플레이어를 인식했을때 매번 이동하는 거리
    private Vector2 startPosition;                                   //슬라임의 처음 위치
    private float curMoveDistance = 0f;                              //슬라임이 이동한 거리
    private int moveDirection = 1;                                   //슬라임이 이동할 방향(1: 오른쪽, -1: 왼쪽)

    private bool isGround;           //슬라임이 이동할 때 바로앞의 땅을 감지하는 변수
    private float checkRadius = 0.5f;       //슬라임이 이동할 때 바로앞의 땅을 감지하는 반지름


    //플레이어를 인식하지 않을 때 행동
    private void normalAction()
    {
        if (isWait)
        {
            if (curWaitTime < maxWaitTime)
            {
                isWait = true;
                curWaitTime += Time.fixedDeltaTime;
            }
            else if(curWaitTime >= maxWaitTime || !isGround)
            {
                moveDirection = startPosition.x < transform.position.x ? -1 : 1; //이동 방향 반전
                flip(moveDirection);                                             //좌우 반전
                isWait = false;
                curWaitTime = 0f;
            }
        }
    }

    private void attackAction()
    {
        // 플레이어 방향으로 이동
        //아주 잠깐 멈췄다가 플레이어를 향해 일정 거리를 전진하는 방식
        if (isWait)
        {
            if (player.position.x > transform.position.x) //플레이어가 왼쪽에 있을 때
            {
                moveDirection = 1;
            }
            else
            {
                moveDirection = -1;
            }
            startPosition = transform.position;
            flip(moveDirection);
            if (isGround) { isWait = false; }
        }
    }

    protected override void Start()
    {
        base.Start();
        rigid = GetComponent<Rigidbody2D>();

        startPosition = transform.position;          //슬라임의 처음 위치 저장
    }

    protected override void Update()
    {
        base.Update();

        //플레이어가 인식 범위에 나가는 순간에 startPosition을 현재 위치로 초기화
        if (isRecognzed == false && wasRecognized == true)
        {
            startPosition = transform.position;
        }
        wasRecognized = isRecognzed;                //이전 프레임의 인식 상태 저장


        
        Debug.DrawRay(new Vector2(transform.position.x + moveDirection * transform.localScale.x, transform.position.y - transform.localScale.y), Vector2.down * checkRadius, Color.red);
        Debug.Log($"isGround: {isGround}, moveDirection: {moveDirection}, curWaitTime: {curWaitTime}");
        if (!isGround) //앞에 땅이 없으면
        {
            isWait = true; //대기상태 갱신
        }

        //플레이어 인식 여부에 따라 상태 전환
        if (isRecognzed)
        {
            curState = state.attack;
            attackAction();
        }
        else
        {
            curState = state.normal;
            normalAction();
        }
    }

    //물리 이동 부분
    //이동 로직: 현재 자기 위치 저장 -> 플레이어 인식 여부에 따라 이동 방향 결정 -> 특정 거리만큼 이동 -> 자기 위치 저장
    void FixedUpdate()
    {
        //가상의 원을 그려서 'Ground' 레이어와 닿아있는지 체크
        //아래가 땅인지 (위치: 슬라임 위치 + 이동 방향 * 가로 길이, 슬라임 위치 - 세로 길이)
        isGround = groundChecker(new Vector2(transform.position.x + moveDirection * transform.localScale.x, transform.position.y - transform.localScale.y),
            checkRadius);

        //슬라임이 이동한 거리 계산
        curMoveDistance = Mathf.Abs(transform.position.x - startPosition.x);

        //슬라임이 대기 상태일 때
        if (isWait)
        {
            rigid.linearVelocity = new Vector2(0f, rigid.linearVelocity.y);
        }

        //슬라임이 공격 상태일 때 
        else if (curState == state.attack)
        {
            // X축 속도를 moveDirection * moveSpeed로 직접 지정하여 좌우로 정확히 이동하도록 제어
            // Y축 속도는 Update에서 계산된 rigid.linearVelocity.y를 그대로 유지
            rigid.linearVelocity = new Vector2(moveDirection * moveSpeed * 1.5f, rigid.linearVelocity.y);

            if(curMoveDistance > attackMoveDistance)
            {
                isWait = true; //대기상태 갱신
                curMoveDistance = 0f; // 이동 거리 초기화

                if (moveDirection == 1)
                {
                    transform.position = new Vector2(startPosition.x + attackMoveDistance, transform.position.y);
                }
                else if (moveDirection == -1)
                {
                    transform.position = new Vector2(startPosition.x - attackMoveDistance, transform.position.y);
                }
            }
            
        }

        //슬라임이 플레이어를 인식하지 않을 때
        else if (curState == state.normal)
        {
            rigid.linearVelocity = new Vector2(moveDirection * moveSpeed, rigid.linearVelocity.y);

            if (curMoveDistance > maxNormalMoveDistance)
            {
                isWait = true; //대기상태 갱신
                curMoveDistance = 0f; //이동 거리 초기화
                
                if (moveDirection == 1)
                {
                    transform.position = new Vector2(startPosition.x + maxNormalMoveDistance, transform.position.y);
                }
                else if (moveDirection == -1)
                {
                    transform.position = new Vector2(startPosition.x - maxNormalMoveDistance, transform.position.y);
                }

                startPosition = transform.position; //슬라임의 현재 위치를 startPosition으로 갱신
            }
        }
    }
}
