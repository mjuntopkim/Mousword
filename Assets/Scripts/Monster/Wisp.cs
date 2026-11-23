using UnityEngine;

public class Wisp : Monster
{
    [Header("활공 (제자리 ↔ 옆 칸을 천천히 오가기)")]
    [Tooltip("좌우로 오가는 거리 (타일 칸 수). 1 = 자기 칸과 옆 칸, 즉 그리드 2칸 사용")]
    public float glideCells = 1f;
    [Tooltip("타일 한 칸의 크기 (월드 단위). 현재 맵의 Grid Cell Size 는 1")]
    public float cellSize = 1f;
    [Tooltip("한쪽 끝에서 반대쪽 끝까지 가는 데 걸리는 시간(초). 클수록 느리게 활공")]
    public float glideTime = 3f;
    [Tooltip("켜면 스폰 위치를 두 칸의 가운데로 보고 양옆으로 반 칸씩 오감 (2칸짜리 타일 묶음으로 스폰할 때).\n" +
             "끄면 스폰 위치(자기 칸)에서 오른쪽 옆 칸까지 오감.")]
    public bool spawnIsCenter = true;
    [Tooltip("활공하면서 위아래로 살짝 떠오르는 폭")]
    public float bobAmplitude = 0.08f;
    [Tooltip("위아래로 떠오르는 주기(초)")]
    public float bobTime = 2.2f;

    [SerializeField] private float recoveryHPAmount = 20f; // HP 회복량
    [SerializeField] private float recoveryManaAmount = 20f; // 마나 회복량

    [HideInInspector] public Vector2 anchorPosition;   // 활공 구간의 가운데 지점
    [HideInInspector] public Vector2 targetOffset;     // 가운데 기준, 지금 있어야 할 위치 (WispState 가 계산)

    protected Rigidbody2D rigid;             // 2D 물리 연산을 다루기 위한 컴포넌트 변수
    private PlayerStatus playerStatus; // 플레이어 상태 참조
    private bool isDead = false; // 위습이 죽었는지 여부

    /// <summary>가운데에서 양 끝까지의 거리 (월드 단위).</summary>
    public float HalfGlideDistance => glideCells * cellSize * 0.5f;

    protected override void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            if (player == null) player = playerObj.transform;
            playerStatus = playerObj.GetComponent<PlayerStatus>(); // 플레이어 상태 참조
        }
        else
        {
            Debug.LogWarning("Wisp: 'Player' 태그를 가진 오브젝트를 찾지 못했습니다.");
        }

        base.Start();
        rigid = GetComponent<Rigidbody2D>();
        rigid.gravityScale = 0f;                     // 공중에 떠 있어야 하므로 중력 제거

        constMoveSpeed = moveSpeed;                  //위습의 이동속도 상수화

        // 활공 구간의 가운데 지점 계산
        anchorPosition = transform.position;
        if (!spawnIsCenter)
        {
            anchorPosition += Vector2.right * HalfGlideDistance; // 자기 칸 ~ 오른쪽 옆 칸의 가운데
        }

        fsm = new FSM(new WispState.IdleState(this));
    }

    protected override void Update()
    {
        if (isDead) {
            return; // 위습이 죽었으면 Update를 실행하지 않음
        }

        base.Update();
        if (anim != null) anim.SetBool("Wait", isWait); //대기 애니메이션 상태 전환
        fsm.stateUpdate();
    }

    void FixedUpdate()
    {
        if (isDead) {
            rigid.linearVelocity = Vector2.zero; // 사망 연출 중에는 제자리
            return;
        }

        // 위아래로 천천히 떠오르기
        float bob = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(bobTime, 0.01f)) * bobAmplitude;
        Vector2 desiredPosition = anchorPosition + targetOffset + Vector2.up * bob;

        // 위치를 직접 옮기지 않고 속도로 이동시켜 충돌은 그대로 유지
        rigid.linearVelocity = (desiredPosition - rigid.position) / Time.fixedDeltaTime;
    }

    public override void changeState(state newState)
    {
        if (isDead) {
            return; // 위습이 죽었으면 상태 전환을 하지 않음
        }

        switch (newState)
        {
            case state.idle:
                fsm.changeState(new WispState.IdleState(this));
                break;
            case state.die:
                isDead = true; // 위습이 죽었음을 표시
                fsm.changeState(new CommonState.DieState(this));
                Debug.Log("상태 변환됨");

                if (playerStatus != null) {
                    Debug.Log("회복 호출됨");
                    playerStatus.RecoverHp(recoveryHPAmount); // HP 회복
                    playerStatus.RecoverMp(recoveryManaAmount); // 마나 회복
                }
                else {
                    Debug.LogWarning("Wisp: PlayerStatus를 찾을 수 없습니다. HP와 마나 회복이 적용되지 않습니다.");
                }

                break;
        }
    }
}
