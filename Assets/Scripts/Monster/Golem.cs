using UnityEngine;

public class Golem : Monster
{
    [HideInInspector] public Rigidbody2D rigid;              // 2D 물리 연산을 다루기 위한 컴포넌트 변수

    public float traceMoveDistance = 1f;          //골렘이 플레이어를 인식했을때 매번 이동하는 거리
    [HideInInspector] public Vector2 startPosition;                                   //골렘의 처음 위치
    [HideInInspector] public float curMoveDistance = 0f;                              //골렘이 이동한 거리
    [HideInInspector] public int moveDirection = 1;                                   //골렘이 이동할 방향(1: 오른쪽, -1: 왼쪽)

    [HideInInspector] public bool isGround;                   //골렘이 이동할 때 바로앞의 땅을 감지하는 변수

    [HideInInspector] public int skillCount;                   //골렘이 한스킬을 몇번 연속으로 갈길지 결정하는 변수
    [HideInInspector] public bool isJumping = false; //점프하는 중
    private float checkRadius = 0.2f;       //골렘이 이동할 때 바로앞의 땅을 감지하는 반지름
    [SerializeField] private int rockSpeed = 5;                                      //골렘이 바위를 던질 때의 속도
    public GameObject Rock;
    public Transform rockSpawnPoint;

    public GameObject MiniGolem;
    public Transform SummonSpawnPoint1;
    public Transform SummonSpawnPoint2;

    private bool isPase2 = false;

    protected override void Start()
    {
        base.Start();
        rigid = GetComponent<Rigidbody2D>();

        constMoveSpeed = moveSpeed;                  //골렘의 이동속도 상수화
        startPosition = transform.position;          //골렘의 처음 위치 저장

        fsm = new FSM(new GolemState.IdleState(this));
    }

    protected override void setState()
    {
        if (curState == state.die || !isAnimeEnd) return;
        if (curState == state.throwRock || curState == state.jump || curState == state.smash || curState == state.notice || curState == state.summon) //공격 스킬이 방금 끝났다면 Idle로 복귀하고 후딜레이 타이머 시작
        {
            curWaitTime = 0f;
            curState = state.idle;
            changeState(state.idle);
            return;
        }
        if ((curState == state.idle || curState == state.notice) && curWaitTime < maxWaitTime) //후딜레이 대기
        {
            curWaitTime += Time.deltaTime;
            return;
        }
        if (currentHP>maxHP/2) //1페이지
        {
            if (!isRecognzed && !isJumping) //플레이어가 멀리 있을 때
            {
                float randnum = Random.Range(0, 1f);
                curState = randnum > 0.3f ? state.throwRock : state.jump;
            }
            else if (distanceToPlayer <= attackRadius && !isJumping) //현재 일반 공격 범위에 들었을때
            {
                curState = state.smash;
            }
            else
            {
                curState = state.notice;
            }
        }
        else                   //2페이지
        {
            if (!isPase2) { isPase2 = true; maxWaitTime /= 1.5f; rockSpeed +=2; }
            if (distanceToPlayer > attackRadius && !isJumping) //플레이어가 멀리 있을 때
            {
                float randnum = Random.Range(0, 1f);
                if(randnum > 0.5f) { curState = state.throwRock; }
                else if (randnum > 0.2f) { curState = state.jump; }
                else { curState = state.summon; }
            }
            else if (distanceToPlayer <= attackRadius && !isJumping) //현재 일반 공격 범위에 들었을때
            {
                curState = state.smash;
            }
            else
            {
                curState = state.notice;
            }
        }
        changeState(curState);
    }

    protected override void Update()
    {
        base.Update();
        if (!isGround && !isJumping) //앞에 땅이 없으면
        {
            isWait = true; //대기상태 갱신
        }

        //anim.SetBool("Idle", isWait); //대기 애니메이션 상태 전환
        fsm.stateUpdate();
    }

    //물리 이동 부분
    //이동 로직: 현재 자기 위치 저장 -> 플레이어 인식 여부에 따라 이동 방향 결정 -> 특정 거리만큼 이동 -> 자기 위치 저장
    void FixedUpdate()
    {
        //가상의 원을 그려서 'Ground' 레이어와 닿아있는지 체크
        //골렘이 이동할 방향의 바로 앞에 땅이 있는지 확인
        isGround = groundChecker(checkRadius);

        //골렘이 이동한 거리 계산
        curMoveDistance = Mathf.Abs(transform.position.x - startPosition.x);

        //골렘이 대기 상태일 때
        if (isWait)
        {
            moveSpeed = 0; //이동속도 0으로 설정
        }
        if (!isJumping) //골렘 이동
        {
            if (isWait) moveSpeed = 0;
            rigid.linearVelocity = new Vector2(moveDirection * moveSpeed, rigid.linearVelocity.y);
        }  
    }

    public override void changeState(state newState)
    {
        switch (newState)
        {
            case state.idle:
                fsm.changeState(new GolemState.IdleState(this));
                break;
            case state.notice:
                fsm.changeState(new GolemState.TraceState(this));
                break;
            case state.jump:
                fsm.changeState(new GolemState.JumpState(this));
                break;
            case state.throwRock:
                fsm.changeState(new GolemState.ThrowRockState(this));
                break;
            case state.smash:
                fsm.changeState(new GolemState.SmashState(this));
                break;
            case state.summon:
                fsm.changeState(new GolemState.SummonState(this));
                break;
            case state.die:
                fsm.changeState(new CommonState.DieState(this));
                break;
        }
    }

    public void Throw(int on)
    {
        if (on == 0) { return; }

        if (Rock != null && rockSpawnPoint != null)
        {
            GameObject rock = Instantiate(Rock, rockSpawnPoint.position, Quaternion.identity);

            MonsterHitBox hitBox = rock.GetComponentInChildren<MonsterHitBox>();
            if (hitBox != null)
            {
                hitBox.SetMonster(this);
            }

            Vector2 RockVector = (player.position - transform.position).normalized; //플레이어와 스켈레톤 사이의 방향 벡터 계산, 단위 벡터로 정규화
            float RockDirection = Mathf.Atan2(RockVector.y, RockVector.x) * Mathf.Rad2Deg; //플레이어를 바라보도록 각도 계산


            rock.GetComponent<Rock>().setSpeed(rockSpeed);
            rock.GetComponent<Rock>().setDirection(RockDirection, RockVector);
            rock.GetComponent<Rock>().setGolem(this);
        }
    }

    public void Summon(int on)
    {
        if (on == 0) { return; }

        if (MiniGolem != null && SummonSpawnPoint1 != null && SummonSpawnPoint2 != null)
        {
            SpawnMiniGolem(SummonSpawnPoint1.position);
            SpawnMiniGolem(SummonSpawnPoint2.position);
        }
    }

    private void SpawnMiniGolem(Vector3 spawnPos)
    {
        GameObject obj = Instantiate(MiniGolem, spawnPos, Quaternion.identity);
        MiniGolem mini = obj.GetComponent<MiniGolem>();
        if (mini != null)
        {
            mini.player = this.player; // 소환된 미니골렘에 플레이어 참조 전달
        }
    }
}
