using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerSkillController : MonoBehaviour
{
    public float DashCooldownRemaining => Mathf.Max(0f, currentCoolTime);
    public float SpinCooldownRemaining => Mathf.Max(0f, spinCoolTime);
    public float SwordWaveCooldownRemaining => Mathf.Max(0f, swordWaveCoolTime);
    public float UltimateCooldownRemaining => Mathf.Max(0f, ultimateCoolTime);

    public SkillData DashSkillData => dashSlashSkill;
    public SkillData SpinSkillData => spinSkill;
    public SkillData SwordWaveSkillData => swordWaveSkill;
    public SkillData UltimateSkillData => ultimateSkill;

    [SerializeField] private SkillData dashSlashSkill; // 대시 스킬 데이터를 등록
    [SerializeField] private SkillData swordWaveSkill; // 검기 스킬 데이터 등록 
    [SerializeField] private SkillData ultimateSkill;  // 궁극기 스킬 데이터 등록
    [SerializeField] private SkillData spinSkill;

    [SerializeField] private Transform weaponPivot;    // 360도 무기 회전축
    [SerializeField] private SpriteRenderer bodySpriteRenderer; // idle 상태일때 캐릭터가 바라보고있는 방향 참조
    [SerializeField] private LayerMask enemyLayer;      // 궁극기·회전 스킬 타격 대상 몬스터 레이어

    // 컴포넌트 할당할 변수
    private Rigidbody2D rigid;              
    private PlayerStatus playerStatus;

    private float currentCoolTime = 0f;     // 실시간으로 감소할 현재 남아있는 스킬 쿨타임
    private float swordWaveCoolTime = 0f;     // 검기 스킬 쿨타임
    private float ultimateCoolTime = 0f;   // 궁극기 스킬 쿨타임

    private PlayerDashVisuals dashVisuals;
    private PlayerMove dashMovement;
    private bool dashMovementWasEnabled;
    private float dashOriginalGravity;
    private Coroutine dashRoutine;

    private bool isDashing = false;             // 캐릭터가 현재 대시를 수행 중인지 여부를 저장

    // 대시 속도 곡선: v = 평균속도 × (DashStartSpeed - DashEaseAmount + DashEaseAmount × (1-u)²)
    // (1-u)² 의 평균이 1/3 이므로 DashEaseAmount = (DashStartSpeed - 1) × 1.5 일 때 평균 배율이 정확히 1
    private const float DashStartSpeed = 1.8f;
    private const float DashEaseAmount = (DashStartSpeed - 1f) * 1.5f;

    [Header("대시 연출")]
    [Tooltip("출발 순간 화면이 진행 반대쪽으로 밀리는 폭 (화면 높이 대비)")]
    [SerializeField] private float dashCameraKick = 0.003f;
    [SerializeField] private float dashCameraKickTime = 0.2f;

    [Header("검기 연출")]
    [Tooltip("발사 순간 화면이 조준 방향으로 밀리는 폭 (화면 높이 대비)")]
    [SerializeField] private float swordWaveCameraKick = 0.003f;
    [SerializeField] private float swordWaveCameraKickTime = 0.2f;
    [Tooltip("공중 시전: 발사 후 멈춘 채로 유지하는 시간 (초)")]
    [SerializeField] private float swordWaveAirHold = 0.08f;
    [Tooltip("공중 시전: 발사 반동으로 조준 반대쪽으로 밀리는 거리 (월드 단위, 위로는 밀리지 않음)")]
    [SerializeField] private float swordWaveRecoil = 0.3f;

    // 공중 검기 시전 중 허공에 멈춤 (에어 스톨)
    private bool airStalling;
    private PlayerMove airStallMove;
    private bool airStallMoveWasEnabled;
    private float airStallGravity;
    private float airStallRecoilTime = -1f;     // 발사 후 지난 시간 (발사 전 < 0)
    private Vector2 airStallRecoilDir;
    private bool isExecutingUltimate = false;   // 궁극기 연출이 끝날 때까지 (재사용 방지)
    private bool isCastingUltimate = false;     // 궁극기로 플레이어가 고정된 동안 (다른 스킬 사용 불가)
    private bool ultimateLocked = false;
    private Coroutine ultimateRoutine;
    private PlayerUltimateVisuals ultimateVisuals;
    private PlayerMove ultimateMove;
    private bool ultimateMoveWasEnabled;
    private RigidbodyConstraints2D ultimateConstraints;

    private PlayerSwordWaveVisuals swordWaveVisuals; // 검기 시전 모션 연출
    private Coroutine swordWaveRoutine;
    private bool isCastingSwordWave = false;        // 검기 시전 모션 중 여부

    private float spinCoolTime = 0f;            // 회전 스킬 쿨타임
    private bool isSpinning = false;            // 회전 상태
    private Coroutine spinRoutine;
    private PlayerSpinVisuals spinVisuals;      // 회전 스킬 연출

    private WeaponAim weaponAim;                // 회전시킬 검 오브젝트의 컴포넌트
    private Rigidbody2D weaponRigid;            // 회전시킬 검 오브젝트의 컴포넌트

    // 회전 반경 안에 들어온 Collider들을 저장
    private readonly List<Collider2D> spinOverlapResults = new List<Collider2D>();
    // 이번 회전 스킬에서 이미 피해를 받은 몬스터 저장
    // (몇 번 맞았는지, 마지막으로 맞을 때까지 검이 돈 누적 각도) — 바퀴마다 1회씩 타격하기 위해 기록
    private readonly Dictionary<Monster, Vector2Int> spinHitEnemies = new Dictionary<Monster, Vector2Int>();
    private const int SpinRehitAngle = 300;     // 같은 적을 다시 때리려면 검이 최소 이만큼 더 돌아야 함
    private int spinMaxHits = 1;                // 적 하나가 이번 스킬에서 맞을 수 있는 최대 횟수 (= 바퀴 수)
    private float spinTravel;                   // 판정 구간에서 검이 지금까지 돈 누적 각도
    private int spinStoppedLap;                 // 히트스톱을 이미 넣은 마지막 바퀴 (바퀴당 한 번만)

    [Header("회전베기 타격감")]
    [Tooltip("1·2바퀴 적중: 히트스톱 (초, 바퀴당 한 번)")]
    [SerializeField] private float spinHitStop = 0.03f;
    [Tooltip("1·2바퀴 적중: 화면 흔들림 세기 (화면 높이 대비)")]
    [SerializeField] private float spinShakeStrength = 0.004f;
    [SerializeField] private float spinShakeTime = 0.08f;
    [Tooltip("마지막 바퀴 적중(피니시): 히트스톱 (초)")]
    [SerializeField] private float spinFinishHitStop = 0.06f;
    [Tooltip("마지막 바퀴 적중(피니시): 화면 흔들림 세기 (화면 높이 대비)")]
    [SerializeField] private float spinFinishShakeStrength = 0.008f;
    [SerializeField] private float spinFinishShakeTime = 0.14f;
    [Tooltip("적이 하얗게 번쩍이는 시간 (초)")]
    [SerializeField] private float spinFlashTime = 0.06f;
    [Tooltip("처치 타격일 때 흔들림 세기·히트스톱에 곱하는 값")]
    [SerializeField] private float spinKillMultiplier = 1.5f;
    private RigidbodyInterpolation2D weaponInterpolation; // 회전 중 잠깐 바꾼 보간 설정 복구용

    void Start()
    {   
        // 컴포넌트 할당
        rigid = GetComponent<Rigidbody2D>();
        playerStatus = GetComponent<PlayerStatus>();

        if (bodySpriteRenderer == null)
        {
            bodySpriteRenderer = GetComponent<SpriteRenderer>();
        }

        // weaponPivot을 기준으로 WeaponAim 찾기
        if (weaponPivot != null)
        {
            weaponAim = weaponPivot.GetComponent<WeaponAim>();

            // weaponPivot 자체에 없다면 자식에서도 검색
            if (weaponAim == null)
            {
                weaponAim = weaponPivot.GetComponentInChildren<WeaponAim>();
            }

            // WeaponAim이 붙어있는 오브젝트의 Rigidbody2D 가져오기
            if (weaponAim != null)
            {
                weaponRigid = weaponAim.GetComponent<Rigidbody2D>();
            }
        }
    }

    void Update()
    {
        // 쿨타임 타이머 차감(쿨타임이 남아있으면 매 프레임 흐른 시간만큼 차감)
        if (currentCoolTime > 0) currentCoolTime -= Time.deltaTime;     // 대시 스킬 쿨타임 계산
        if (swordWaveCoolTime > 0) swordWaveCoolTime -= Time.deltaTime; // 검기 스킬 쿨타임 계산
        if (ultimateCoolTime > 0) ultimateCoolTime -= Time.deltaTime;   // 궁극기 쿨타임 계산
        if (spinCoolTime > 0) spinCoolTime -= Time.deltaTime;

        // 좌측 Shift 키를 누르고, 쿨타임이 끝났으며, 현재 대시 중이 아닐 때 실행
        if (Input.GetKeyDown(KeyCode.LeftShift) && currentCoolTime <= 0 && !isDashing && !isCastingSwordWave && !isSpinning && !isCastingUltimate)
        {
            // 대시 시도
            TryExecuteDash(dashSlashSkill);
        }

        // E 키 또는 마우스 우클릭: 검기 발사 스킬 실행
        if ((Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(1)) && swordWaveCoolTime <= 0 && !isCastingSwordWave && !isDashing && !isSpinning && !isCastingUltimate)
        {
            TryExecuteSwordWave(swordWaveSkill);
        }

        // [R 키] 궁극기: 화면 전체 일괄 타격
        // (검기 포즈를 함께 쓰므로 대시·검기·회전 중에는 쓰지 않음)
        if (Input.GetKeyDown(KeyCode.R) && ultimateCoolTime <= 0 && !isExecutingUltimate && !isDashing && !isCastingSwordWave && !isSpinning)
        {
            TryExecuteUltimate(ultimateSkill);
        }

        // [Q 키] 360도 회전베기 (대시·검기와는 몸 연출이 겹치므로 동시에 쓰지 않음)
        if (Input.GetKeyDown(KeyCode.Q) && spinCoolTime <= 0f && !isSpinning && !isCastingSwordWave && !isDashing && !isCastingUltimate)
        {
            TryExecuteSpin(spinSkill);
        }
    }

    #region 1. 대시 스킬 로직
    private void TryExecuteDash(SkillData skill)
    {
        // 예외 처리(에러 방지)
        if (skill == null) return;

        // PlayerStatus가 제대로 붙어있는지 확인
        if (playerStatus == null)
        {
            Debug.LogError("오류: Player 1 오브젝트에 'PlayerStatus' 스크립트가 없습니다!");
            return;
        }

        // PlayerStatus의 마나를 확인하고 차감 (마나가 충분한지 체크)
        if (playerStatus.CurrentMp >= skill.manaCost)
        {
            playerStatus.ConsumeMp(skill.manaCost); // 마나 소모 함수

            Debug.Log($"[대시 사용] 소모 마나: {skill.manaCost} | 남은 마나: {playerStatus.CurrentMp}/{playerStatus.MaxMp}");

            currentCoolTime = skill.coolTime;       // 쿨타임 적용

            // 대시 물리 연산 코루틴 시작
            dashRoutine = StartCoroutine(DashCoroutine(skill));
        }
        else
        {
            Debug.Log("마나가 부족하여 대시를 사용할 수 없습니다!");
        }
    }

    private IEnumerator DashCoroutine(SkillData skill)
    {
        isDashing = true;
        dashMovement = GetComponent<PlayerMove>();
        dashMovementWasEnabled = dashMovement != null && dashMovement.enabled;
        if (dashMovement != null) dashMovement.enabled = false;
        dashOriginalGravity = rigid.gravityScale;
        rigid.gravityScale = 0f;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float facing = horizontal != 0f ? Mathf.Sign(horizontal) :
            (bodySpriteRenderer != null ? (bodySpriteRenderer.flipX ? -1f : 1f) : Mathf.Sign(transform.localScale.x));
        if (bodySpriteRenderer != null) bodySpriteRenderer.flipX = facing < 0f;
        if (dashVisuals == null)
            dashVisuals = GetComponent<PlayerDashVisuals>() ?? gameObject.AddComponent<PlayerDashVisuals>();

        try
        {
            dashVisuals.Begin(bodySpriteRenderer, facing, skill.dashDuration, IsDashGrounded());
            if (skill.effectPrefab != null)
            {
                var effect = Instantiate(skill.effectPrefab, transform.position, Quaternion.identity);
                var scale = effect.transform.localScale;
                scale.x *= facing;
                effect.transform.localScale = scale;
            }

            // 출발 순간 화면이 진행 반대쪽으로 한 번 살짝 밀림 (떨림 없음)
            HitFeedback.Kick(Vector2.left * facing, dashCameraKick, dashCameraKickTime);

            // 속도 곡선: 출발 순간 가장 빠르게 튀어나간 뒤 감속. 평균 속도 = dashForce 라서 이동 거리는 이전과 같음
            int steps = DashPhysicsSteps(skill.dashDuration);
            for (int i = 0; i < steps; i++)
            {
                // 각 물리 스텝 구간의 중간 시점 속도 사용 → 총 이동 거리가 곡선 적분값(= 평균속도 × 시간)과 거의 같음
                float u = (i + 0.5f) / steps;
                rigid.linearVelocity = Vector2.right * facing * skill.dashForce * DashSpeedCurve(u);
                yield return new WaitForFixedUpdate();
            }
        }
        finally
        {
            FinishDash();
        }
    }

    // 대시가 이어지는 물리 스텝 수 (0.18초 / 0.02초 = 9스텝)
    public static int DashPhysicsSteps(float dashDuration)
    {
        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.01f, dashDuration) / Time.fixedDeltaTime));
    }

    // 평균 속도 대비 배율. 1.8배로 출발해 0.6배까지 감속 (0~1 구간 평균 = 1)
    private static float DashSpeedCurve(float u)
    {
        float r = 1f - Mathf.Clamp01(u);
        return DashStartSpeed - DashEaseAmount + DashEaseAmount * r * r;
    }

    private bool IsDashGrounded() => IsGroundedFor(dashMovement);

    private static bool IsGroundedFor(PlayerMove move)
    {
        return move != null && move.groundCheck != null &&
            Physics2D.OverlapCircle(move.groundCheck.position, move.checkRadius, move.isLayer) != null;
    }

    private void FinishDash()
    {
        if (!isDashing) return;
        isDashing = false;
        if (rigid != null)
        {
            rigid.linearVelocity = Vector2.zero;
            rigid.gravityScale = dashOriginalGravity;
        }
        if (dashMovement != null) dashMovement.enabled = dashMovementWasEnabled;
        if (dashVisuals != null) dashVisuals.End(IsDashGrounded());
        dashRoutine = null;
    }

    private void OnDisable()
    {
        if (dashRoutine != null) StopCoroutine(dashRoutine);
        FinishDash();
        if (dashVisuals != null) dashVisuals.Cancel();
        if (swordWaveRoutine != null) StopCoroutine(swordWaveRoutine);
        FinishSwordWave();
        if (spinRoutine != null) StopCoroutine(spinRoutine);
        FinishSpin();
        if (ultimateRoutine != null) StopCoroutine(ultimateRoutine);
        ultimateRoutine = null;
        if (ultimateVisuals != null) ultimateVisuals.Cancel();
        UnlockForUltimate();
        isExecutingUltimate = false;
    }
    #endregion

    #region 2. 검기 스킬 로직
    private void TryExecuteSwordWave(SkillData skill)
    {
        if (skill == null || skill.projectilePrefab == null) return;

        if (playerStatus == null)
        {
            Debug.LogError("오류: Player 1 오브젝트에 'PlayerStatus' 스크립트가 없습니다!");
            return;
        }

        // 마나 검증 및 차감
        if (playerStatus.CurrentMp >= skill.manaCost)
        {
            playerStatus.ConsumeMp(skill.manaCost);
            Debug.Log($"[검기 발사] 소모 마나: {skill.manaCost} | 남은 마나: {playerStatus.CurrentMp}/{playerStatus.MaxMp}");

            swordWaveCoolTime = skill.coolTime; // 쿨타임 적용
            swordWaveRoutine = StartCoroutine(SwordWaveCoroutine(skill)); // 내려베기 모션 후 임팩트 프레임에서 발사
        }
        else
        {
            Debug.Log("마나가 부족하여 검기를 발사할 수 없습니다!");
        }
    }

    // 검기 시전: 조준 방향으로 몸을 돌리고 내려베기 모션을 재생한 뒤, 임팩트 프레임에서 검기 발사
    private IEnumerator SwordWaveCoroutine(SkillData skill)
    {
        isCastingSwordWave = true;
        try
        {
            if (weaponPivot == null)
            {
                Debug.LogWarning("Weapon Pivot이 인스펙터에 연결되지 않았습니다!");
                yield break;
            }

            // 키를 누른 순간 마우스를 향한 weaponPivot 방향으로 발사 방향 고정
            Quaternion aim = weaponPivot.rotation;
            float facing = (aim * Vector3.right).x < 0f ? -1f : 1f;
            if (bodySpriteRenderer != null) bodySpriteRenderer.flipX = facing < 0f;

            if (swordWaveVisuals == null)
                swordWaveVisuals = GetComponent<PlayerSwordWaveVisuals>() ?? gameObject.AddComponent<PlayerSwordWaveVisuals>();
            swordWaveVisuals.Begin(bodySpriteRenderer, weaponPivot, facing);
            BeginAirStall(); // 공중이면 시전 동안 허공에 멈춤 (땅에서는 아무 일 없음)

            yield return new WaitForSeconds(PlayerSwordWaveVisuals.ReleaseTime);
            ShootSwordWave(skill, aim);
            StartAirStallRecoil(aim * Vector3.right);
            yield return new WaitForSeconds(PlayerSwordWaveVisuals.Duration - PlayerSwordWaveVisuals.ReleaseTime);
        }
        finally
        {
            FinishSwordWave();
        }
    }

    private void FinishSwordWave()
    {
        isCastingSwordWave = false;
        swordWaveRoutine = null;
        EndAirStall();
        if (swordWaveVisuals != null) swordWaveVisuals.Cancel();
    }

    // 공중 시전: 낙하·점프·좌우 이동을 멈추고 그 자리에 고정
    private void BeginAirStall()
    {
        if (airStalling || rigid == null) return;
        PlayerMove move = GetComponent<PlayerMove>();
        if (move == null || IsGroundedFor(move)) return;

        airStalling = true;
        airStallMove = move;
        airStallMoveWasEnabled = move.enabled;
        move.enabled = false;
        airStallGravity = rigid.gravityScale;
        rigid.gravityScale = 0f;
        rigid.linearVelocity = Vector2.zero;
        airStallRecoilTime = -1f;
    }

    // 발사 순간: 조준 반대쪽으로 반동 (위로는 밀리지 않음). 이후 FixedUpdate 가 감속시키고 swordWaveAirHold 뒤 해제
    private void StartAirStallRecoil(Vector2 aimDirection)
    {
        if (!airStalling) return;
        Vector2 back = -aimDirection.normalized;
        if (back.y > 0f) back.y = 0f;
        airStallRecoilDir = back;
        airStallRecoilTime = 0f;
        ApplyAirStallRecoil();
        if (swordWaveAirHold <= 0f) EndAirStall();
    }

    // 반동 속도: 0.08초 동안 선형 감속 → 이동 거리 = 초기속도 × 유지시간 / 2 = swordWaveRecoil
    private void ApplyAirStallRecoil()
    {
        float hold = Mathf.Max(0.0001f, swordWaveAirHold);
        float v0 = swordWaveRecoil * 2f / hold;
        float k = 1f - Mathf.Clamp01((airStallRecoilTime + Time.fixedDeltaTime * 0.5f) / hold);
        rigid.linearVelocity = airStallRecoilDir * v0 * k;
    }

    private void EndAirStall()
    {
        if (!airStalling) return;
        airStalling = false;
        airStallRecoilTime = -1f;
        if (rigid != null)
        {
            rigid.gravityScale = airStallGravity;
            rigid.linearVelocity = Vector2.zero;
        }
        if (airStallMove != null) airStallMove.enabled = airStallMoveWasEnabled;
        airStallMove = null;
    }

    void FixedUpdate()
    {
        if (!airStalling) return;
        if (airStallRecoilTime < 0f)
        {
            rigid.linearVelocity = Vector2.zero; // 발사 전: 허공에 고정
            return;
        }
        // 이번 물리 스텝에 쓸 속도를 넣고 나서 시간을 진행 (물리 계산은 FixedUpdate 직후에 일어남)
        if (airStallRecoilTime >= swordWaveAirHold - 1e-4f) { EndAirStall(); return; }
        ApplyAirStallRecoil();
        airStallRecoilTime += Time.fixedDeltaTime;
    }

    private void ShootSwordWave(SkillData skill, Quaternion aim)
    {
        // 1. weaponPivot 위치에서 조준 방향(aim)으로 검기 생성
        GameObject waveObj = Instantiate(skill.projectilePrefab, weaponPivot.position, aim);

        // 2. 배경 소품에 가리지 않도록 플레이어와 같은 정렬 레이어의 앞쪽에 그림
        SpriteRenderer waveRenderer = waveObj.GetComponent<SpriteRenderer>();
        if (waveRenderer != null && bodySpriteRenderer != null)
        {
            waveRenderer.sortingLayerID = bodySpriteRenderer.sortingLayerID;
            waveRenderer.sortingOrder = bodySpriteRenderer.sortingOrder + 5;
        }

        // 3. 검기 스크립트에 속도, 데미지, 캐릭터 픽셀 크기 전달
        SwordWave swordWave = waveObj.GetComponent<SwordWave>();
        if (swordWave != null)
        {
            float pixel = swordWaveVisuals != null ? swordWaveVisuals.PixelWorldSize : 0f;
            swordWave.Initialize(skill.projectileSpeed, skill.damage, pixel);
        }

        // 4. 발사 순간 화면이 조준 방향으로 한 번 살짝 밀림 (떨림 없음)
        HitFeedback.Kick(aim * Vector3.right, swordWaveCameraKick, swordWaveCameraKickTime);
    }
    #endregion

    #region 3. 궁극기(화면 전체 일괄 타격) 로직
    private void TryExecuteUltimate(SkillData skill)
    {
        if (skill == null) return;

        if (playerStatus == null)
        {
            Debug.LogError("오류: Player 1 오브젝트에 'PlayerStatus' 스크립트가 없습니다!");
            return;
        }

        if (playerStatus.CurrentMp >= skill.manaCost)
        {
            playerStatus.ConsumeMp(skill.manaCost);
            Debug.Log($"[궁극기 발동] 소모 마나: {skill.manaCost} | 남은 마나: {playerStatus.CurrentMp}/{playerStatus.MaxMp}");

            ultimateCoolTime = skill.coolTime;

            ultimateRoutine = StartCoroutine(UltimateCoroutine());
        }
        else
        {
            Debug.Log("마나가 부족하여 궁극기를 사용할 수 없습니다!");
        }
    }

    // 궁극기 시전 흐름 (연출 타이밍은 PlayerUltimateVisuals)
    // ① 시전·② 예고·③ 낙하: 플레이어 제자리 고정 + 무적
    // ④ 충돌: 화면 안의 모든 몬스터 일괄 타격
    // 내려베기 동작이 끝나면 조작 복구, ⑤ 여파 연출은 계속 재생
    private IEnumerator UltimateCoroutine()
    {
        isExecutingUltimate = true;
        isCastingUltimate = true;
        LockForUltimate();
        try
        {
            if (ultimateVisuals == null)
            {
                ultimateVisuals = GetComponent<PlayerUltimateVisuals>();
                if (ultimateVisuals == null) ultimateVisuals = gameObject.AddComponent<PlayerUltimateVisuals>();
            }

            bool impacted = false;
            bool started = ultimateVisuals.Begin(bodySpriteRenderer, weaponPivot, Camera.main, GetGroundMask(), () =>
            {
                impacted = true;
                return ApplyUltimateDamage();
            });

            // 연출을 시작할 수 없으면(카메라 없음 등) 타격만 바로 적용
            if (!started)
            {
                ApplyUltimateDamage();
                yield break;
            }

            while (!impacted && ultimateVisuals.IsPlaying) yield return null;

            // 내려베기 동작 마무리까지 고정 (히트스톱 동안에는 게임 시간이 멈춰 있으므로 그만큼 늦게 풀림)
            yield return new WaitForSeconds(PlayerSwordWaveVisuals.Duration - PlayerSwordWaveVisuals.ReleaseTime);
            UnlockForUltimate();

            while (ultimateVisuals.IsPlaying) yield return null;
        }
        finally
        {
            UnlockForUltimate();
            isExecutingUltimate = false;
            ultimateRoutine = null;
        }
    }

    private void LockForUltimate()
    {
        if (ultimateLocked) return;
        ultimateLocked = true;
        ultimateMove = GetComponent<PlayerMove>();
        ultimateMoveWasEnabled = ultimateMove != null && ultimateMove.enabled;
        if (ultimateMove != null) ultimateMove.enabled = false;
        if (rigid != null)
        {
            ultimateConstraints = rigid.constraints;
            rigid.linearVelocity = Vector2.zero;
            rigid.constraints = RigidbodyConstraints2D.FreezeAll; // 공중에서 써도 그 자리에 멈춰 있음
        }
        if (playerStatus != null) playerStatus.SkillInvincible = true;
    }

    private void UnlockForUltimate()
    {
        isCastingUltimate = false;
        if (!ultimateLocked) return;
        ultimateLocked = false;
        if (rigid != null)
        {
            rigid.constraints = ultimateConstraints;
            // FreezeAll 동안 물리 바디가 잠들어서, 풀어줘도 깨우지 않으면 공중에 떠 있는 채로 남음
            rigid.WakeUp();
        }
        if (ultimateMove != null) ultimateMove.enabled = ultimateMoveWasEnabled;
        if (playerStatus != null) playerStatus.SkillInvincible = false;
    }

    // 거대한 검이 꽂힐 땅 = 플레이어가 밟는 땅 레이어
    private LayerMask GetGroundMask()
    {
        PlayerMove move = GetComponent<PlayerMove>();
        if (move != null && move.isLayer.value != 0) return move.isLayer;
        return ~(enemyLayer.value | (1 << gameObject.layer));
    }

    // 검이 땅에 꽂히는 순간 호출: 화면 안의 모든 몬스터에게 동시에 한 번씩 피해
    // 맞은 몬스터들의 위치(적중 이펙트용)를 돌려줌
    public List<Vector3> ApplyUltimateDamage()
    {
        List<Vector3> hitPositions = new List<Vector3>();
        if (ultimateSkill == null) return hitPositions;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("Main Camera를 찾을 수 없습니다!");
            return hitPositions;
        }

        // 현재 화면(메인 카메라) 영역 안의 enemyLayer 콜라이더 수집
        Vector2 center = mainCam.transform.position;
        Vector2 size = new Vector2(mainCam.orthographicSize * 2 * mainCam.aspect, mainCam.orthographicSize * 2);
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, enemyLayer);

        // 콜라이더가 여러 개 달린 몬스터도 한 번만 맞도록 몬스터 단위로 중복 제거
        Dictionary<Monster, Vector3> targets = new Dictionary<Monster, Vector3>();
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            Monster monster = hit.GetComponentInParent<Monster>();
            if (monster != null && !targets.ContainsKey(monster)) targets.Add(monster, hit.bounds.center);
        }

        foreach (KeyValuePair<Monster, Vector3> target in targets)
        {
            target.Key.TakeDamage(ultimateSkill.damage);
            hitPositions.Add(target.Value);

            if (ultimateSkill.ultimateHitEffectPrefab != null)
                Instantiate(ultimateSkill.ultimateHitEffectPrefab, target.Value, Quaternion.identity);
        }

        Debug.Log($"[궁극기 일괄 타격] 적중 {targets.Count}마리 | 데미지: {ultimateSkill.damage}");
        return hitPositions;
    }

    private void OnDrawGizmosSelected()
    {
        // 카메라 감지 영역 Gizmo 표시
        if (Camera.main != null)
        {
            Gizmos.color = Color.red;
            Camera mainCam = Camera.main;
            Vector3 center = mainCam.transform.position;
            Vector3 size = new Vector3(mainCam.orthographicSize * 2 * mainCam.aspect, mainCam.orthographicSize * 2, 1);
            Gizmos.DrawWireCube(center, size);
        }

        // 회전 스킬 판정 반경 표시
        if (weaponPivot != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(SpinCenter, GetSpinRadius(spinSkill));
        }
    }
    #endregion

    #region 4. 360도 회전 스킬

    private void TryExecuteSpin(SkillData skill)
    {
        // 스킬 데이터가 없으면 실행 안 함
        if (skill == null)
        {
            Debug.LogWarning("Spin Skill 데이터가 없습니다.");
            return;
        }

        // PlayerStatus 확인
        if (playerStatus == null)
        {
            Debug.LogWarning("PlayerStatus가 없습니다.");
            return;
        }

        // 검 확인
        if (weaponAim == null || weaponRigid == null)
        {
            Debug.LogWarning("WeaponAim 또는 무기의 Rigidbody2D를 찾지 못했습니다.");
            return;
        }

        // 마나가 부족하면 실행 안 함
        if (playerStatus.CurrentMp < skill.manaCost)
        {
            Debug.Log("마나가 부족해서 회전 스킬을 사용할 수 없습니다.");
            return;
        }

        // 마나 소모
        playerStatus.ConsumeMp(skill.manaCost);

        // 쿨타임 적용
        spinCoolTime = skill.coolTime;

        // 실제 검 회전 시작
        spinRoutine = StartCoroutine(SpinSwordCoroutine(skill));
    }


    // 회전 중심 = 무기 회전축 위치
    private Vector2 SpinCenter =>
        weaponRigid != null ? weaponRigid.position :
        (weaponAim != null ? (Vector2)weaponAim.transform.position : (Vector2)weaponPivot.position);

    // ① 준비 → ② 회전(판정) → ③ 마무리
    private IEnumerator SpinSwordCoroutine(SkillData skill)
    {
        isSpinning = true;

        // 이전 회전 스킬의 피격 기록 제거
        spinHitEnemies.Clear();
        spinStoppedLap = 0;

        // 기존 평타의 누적 회전각 초기화 후 마우스 방향 추적 중지
        weaponAim.ConsumeSwing();
        weaponAim.enabled = false;

        // 현재 조준 각도에서 시작
        // 조준이 오른쪽이면 시계 방향(-), 왼쪽이면 반시계 방향(+)으로 돌아 앞쪽부터 벤다
        float startAngle = weaponRigid.rotation;
        float facing = Mathf.Cos(startAngle * Mathf.Deg2Rad) < 0f ? -1f : 1f;
        float spinDirection = -facing;
        float radius = GetSpinRadius(skill);

        // 당긴 만큼 더 돌아서 원래 조준 각도에서 끝남 (예: 1080° = 3바퀴 + 준비 각도)
        float sweep = Mathf.Abs(skill.spinDegrees) + PlayerSpinVisuals.WindupAngle;
        float spinSpeed = sweep / Mathf.Max(skill.spinDuration, 0.01f);

        // 바퀴 수만큼 같은 적을 여러 번 때릴 수 있음 (360° = 1회, 1080° = 3회)
        spinMaxHits = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(skill.spinDegrees) / 360f - 0.01f));
        spinTravel = 0f;

        // 아주 빠르게 돌 때 물리 프레임 사이에서 검이 뚝뚝 끊겨 보이지 않도록 보간 사용
        weaponInterpolation = weaponRigid.interpolation;
        weaponRigid.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (spinVisuals == null)
            spinVisuals = GetComponent<PlayerSpinVisuals>() ?? gameObject.AddComponent<PlayerSpinVisuals>();
        spinVisuals.Begin(bodySpriteRenderer, weaponAim.transform, radius, facing, spinDirection, spinSpeed);

        try
        {
            // ① 준비: 진행 반대 방향으로 검을 살짝 당김 (판정 없음)
            float windupAngle = startAngle - spinDirection * PlayerSpinVisuals.WindupAngle;
            yield return RotateWeapon(startAngle, windupAngle, PlayerSpinVisuals.WindupTime, null, radius);

            // ② 회전
            spinVisuals.BeginSpin();
            CheckSpinDamage(skill, windupAngle, windupAngle, radius); // 검이 출발하는 위치도 판정
            yield return RotateWeapon(windupAngle, windupAngle + spinDirection * sweep, skill.spinDuration, skill, radius);

            // ③ 마무리: 조준은 바로 돌려주고, 연출이 끝날 때까지만 다른 스킬과 겹치지 않게 대기
            ReleaseWeapon();
            spinVisuals.EndSpin();
            yield return new WaitForSeconds(PlayerSpinVisuals.RecoverTime);
        }
        finally
        {
            FinishSpin();
        }
    }

    // 물리 프레임마다 검을 from → to 로 일정한 속도로 돌림. damageSkill이 있으면 지나간 구간을 판정
    private IEnumerator RotateWeapon(float from, float to, float duration, SkillData damageSkill, float radius)
    {
        float speed = Mathf.Abs(to - from) / Mathf.Max(duration, 0.01f);
        float angle = from;

        while (angle != to)
        {
            float next = Mathf.MoveTowards(angle, to, speed * Time.fixedDeltaTime);
            weaponRigid.MoveRotation(next);

            // 다음 물리 프레임까지 기다림
            yield return new WaitForFixedUpdate();

            if (damageSkill != null)
            {
                spinTravel += Mathf.Abs(next - angle);
                CheckSpinDamage(damageSkill, angle, next, radius);
            }

            angle = next;
        }
    }

    // 마우스 조준 복구
    private void ReleaseWeapon()
    {
        if (weaponRigid != null) weaponRigid.interpolation = weaponInterpolation;
        if (weaponAim == null) return;
        weaponAim.ConsumeSwing();
        weaponAim.enabled = true;
    }

    private void FinishSpin()
    {
        if (!isSpinning) return;
        ReleaseWeapon();
        if (spinVisuals != null) spinVisuals.Cancel();
        spinHitEnemies.Clear();
        isSpinning = false;
        spinRoutine = null;
    }

    // 판정 반경 = 회전축에서 검 스프라이트의 가장 먼 꼭짓점까지 거리 + 보정값
    private float GetSpinRadius(SkillData skill)
    {
        float bonus = skill != null ? skill.spinRadiusBonus : 0f;
        if (weaponPivot == null) return Mathf.Max(0f, bonus);

        Vector2 center = SpinCenter;
        float reach = 0f;
        foreach (SpriteRenderer r in weaponPivot.GetComponentsInChildren<SpriteRenderer>())
        {
            if (r.sprite == null) continue;
            Bounds b = r.sprite.bounds;
            for (int i = 0; i < 4; i++)
            {
                Vector3 corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i < 2 ? b.min.y : b.max.y, 0f);
                reach = Mathf.Max(reach, Vector2.Distance(r.transform.TransformPoint(corner), center));
            }
        }
        return Mathf.Max(0f, reach + bonus);
    }

    // 회전 스킬의 몬스터 공격 판정
    // 반경 안의 적 중에서 이번 물리 프레임에 검이 지나간 각도 구간 [fromAngle, toAngle] 안에 있는 적을 타격
    // 같은 적은 검이 한 바퀴 가까이(SpinRehitAngle) 더 돌아 다시 지나갈 때만, 바퀴 수만큼까지 다시 맞음
    private void CheckSpinDamage(SkillData skill, float fromAngle, float toAngle, float radius)
    {
        Vector2 center = SpinCenter;

        // 이전 검사 결과 초기화
        spinOverlapResults.Clear();

        // 몬스터 레이어만 검사
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(enemyLayer);
        filter.useTriggers = true;

        // 회전 반경 안의 Collider 검색
        Physics2D.OverlapCircle(center, radius, filter, spinOverlapResults);

        float sweep = Mathf.Abs(toAngle - fromAngle);
        float direction = toAngle >= fromAngle ? 1f : -1f;

        foreach (Collider2D hit in spinOverlapResults)
        {
            if (hit == null)
                continue;

            // Collider의 부모에서 몬스터 검색
            Monster monster = hit.GetComponentInParent<Monster>();

            if (monster == null)
                continue;

            // 이미 바퀴 수만큼 맞았거나, 이번 바퀴에 이미 맞은 몬스터면 무시
            int travel = Mathf.RoundToInt(spinTravel);
            bool hitBefore = spinHitEnemies.TryGetValue(monster, out Vector2Int record);
            if (hitBefore && (record.x >= spinMaxHits || travel - record.y < SpinRehitAngle))
                continue;

            // 검이 아직 지나가지 않은 위치면 무시
            if (!IsInSweep(hit.bounds, center, fromAngle, direction, sweep))
                continue;

            int lap = hitBefore ? record.x + 1 : 1; // 이 적이 몇 번째로 맞는지 = 몇 바퀴째 적중인지
            spinHitEnemies[monster] = new Vector2Int(lap, travel);

            bool wasAlive = monster.currentHP > 0f;
            monster.TakeDamage((int)skill.damage);

            if (spinVisuals != null)
                spinVisuals.Spark(hit.bounds.center);

            if (wasAlive) // 이미 쓰러진 몬스터를 다시 친 경우엔 연출 없음
            {
                // 화면은 검이 도는 방향(적 위치에서의 접선)으로 밀림
                Vector2 offset = (Vector2)hit.bounds.center - center;
                Vector2 tangent = new Vector2(-offset.y, offset.x) * direction;
                PlaySpinHitFeedback(monster, tangent, lap, monster.currentHP <= 0f);
            }

            Debug.Log($"[회전 스킬 적중] {monster.name} 데미지: {skill.damage}");
        }
    }

    // 회전베기 타격감: 흰 플래시는 적중마다, 히트스톱은 바퀴당 한 번, 마지막 바퀴는 피니시로 더 강하게
    // (같은 프레임에 여러 마리가 맞아도 HitFeedback 이 더 센 흔들림·더 긴 히트스톱 하나로 합침)
    private void PlaySpinHitFeedback(Monster monster, Vector2 swingDir, int lap, bool killed)
    {
        bool finisher = lap >= spinMaxHits;
        float mul = killed ? spinKillMultiplier : 1f;

        HitFlash.Play(monster, spinFlashTime);

        if (lap > spinStoppedLap)
        {
            spinStoppedLap = lap;
            HitFeedback.HitStop((finisher ? spinFinishHitStop : spinHitStop) * mul);
        }

        if (finisher)
            HitFeedback.Shake(swingDir, spinFinishShakeStrength * mul, spinFinishShakeTime);
        else
            HitFeedback.Shake(swingDir, spinShakeStrength * mul, spinShakeTime);
    }

    // 적이 이번에 검이 쓸고 지나간 부채꼴 안에 있는지 (적의 크기만큼 각도 여유를 줌)
    private static bool IsInSweep(Bounds bounds, Vector2 center, float fromAngle, float direction, float sweep)
    {
        Vector2 offset = (Vector2)bounds.center - center;
        float distance = offset.magnitude;
        float extent = ((Vector2)bounds.extents).magnitude;

        // 회전축이 적 몸 안에 있으면 무조건 적중
        if (distance <= extent)
            return true;

        float halfWidth = Mathf.Asin(extent / distance) * Mathf.Rad2Deg;
        float enemyAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

        // 회전 방향 기준으로 시작 각도에서 적까지 몇 도 떨어져 있는지
        float along = Mathf.Repeat((enemyAngle - fromAngle) * direction, 360f);
        return along <= sweep + halfWidth || along >= 360f - halfWidth;
    }

    #endregion
}



