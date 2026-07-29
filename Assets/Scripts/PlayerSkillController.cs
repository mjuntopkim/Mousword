using UnityEngine;
using System.Collections;

public class PlayerSkillController : MonoBehaviour
{
    [SerializeField] private SkillData dashSlashSkill; // 인스펙터에서 대시 스킬 데이터를 등록

    [SerializeField] private SkillData swordWaveSkill; // 검기 스킬 데이터 등록 (Sword Wave Skill Data)

    [SerializeField] private Transform weaponPivot;    // 360도 무기 회전축
    [SerializeField] private SpriteRenderer bodySpriteRenderer; // idle 상태일때 캐릭터가 바라보고있는 방향 참조

    // 컴포넌트 할당할 변수
    private Rigidbody2D rigid;              
    private PlayerStatus playerStatus;

    private float currentCoolTime = 0f;     // 실시간으로 감소할 현재 남아있는 스킬 쿨타임
    private float swordWaveCoolTime = 0f;     // 검기 스킬 쿨타임
    private bool isDashing = false;         // 캐릭터가 현재 대시를 수행 중인지 여부를 저장

    void Start()
    {   
        // 컴포넌트 할당
        rigid = GetComponent<Rigidbody2D>();
        playerStatus = GetComponent<PlayerStatus>();
    }

    void Update()
    {
        // 대시 스킬 쿨타임 계산
        if (currentCoolTime > 0)
        {
            // 쿨타임이 남아있으면 매 프레임 흐른 시간만큼 차감
            currentCoolTime -= Time.deltaTime;
        }

        // 검기 스킬 쿨타임 계산
        if (swordWaveCoolTime > 0)
        {
            swordWaveCoolTime -= Time.deltaTime;
        }

        // 좌측 Shift 키를 누르고, 쿨타임이 끝났으며, 현재 대시 중이 아닐 때 실행합니다.
        if (Input.GetKeyDown(KeyCode.LeftShift) && currentCoolTime <= 0 && !isDashing)
        {
            // 대시 시도
            TryExecuteDash(dashSlashSkill);
        }

        // E 키 또는 마우스 우클릭: 검기 발사 스킬 실행
        if ((Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(1)) && swordWaveCoolTime <= 0)
        {
            TryExecuteSwordWave(swordWaveSkill);
        }
    }

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
            StartCoroutine(DashCoroutine(skill));
        }
        else
        {
            Debug.Log("마나가 부족하여 대시를 사용할 수 없습니다!");
        }
    }

    private IEnumerator DashCoroutine(SkillData skill)
    {
        // 대시 상태 활성화
        isDashing = true;

        // 1. 대시 중 플레이어 조작(PlayerMove)이 물리 연산을 방해하지 못하게 비활성화
        PlayerMove playerMove = GetComponent<PlayerMove>();
        if (playerMove != null) playerMove.enabled = false;

        // 2. 대시 중 중력 때문에 밑으로 쳐지는 현상 방지
        float originalGravity = rigid.gravityScale;
        rigid.gravityScale = 0f;

        // 대시 방향 결정
        Vector2 dashDirection = Vector2.right;

        // 키보드 AD나 방향키 입력을 직접 감지 (GetAxisRaw 사용으로 미끄러짐 방지)
        float horizontalInput = Input.GetAxisRaw("Horizontal");

        if (horizontalInput != 0)
        {
            // 키 입력이 있다면 누르고 있는 좌/우 방향으로 대시(-1, 1)
            dashDirection = new Vector2(horizontalInput, 0f).normalized;
        }
        else
        {
            // 2.정지 상태일 때는 직접 연결한 몸통 스프라이트의 flipX 값을 읽어옴
            if (bodySpriteRenderer != null)
            {
                float facingDirection = bodySpriteRenderer.flipX ? -1f : 1f;
                dashDirection = new Vector2(facingDirection, 0f).normalized;
            }
            else
            {
                Debug.LogWarning("방향 추적용 스프라이트가 인스펙터에 연결되지 않았습니다!");
                dashDirection = new Vector2(transform.localScale.x, 0f).normalized;
            }
        }
        // ====================================================================

        // 4. 계산된 대시 방향 벡터에 대시 속도를 곱해 일직선으로 뻗어가도록 이동
        rigid.linearVelocity = dashDirection * skill.dashForce;

        // 5. 대시 이펙트 프리팹 생성 (있을 경우에만)
        if (skill.effectPrefab != null)
        {
            Instantiate(skill.effectPrefab, transform.position, transform.rotation);
        }

        // 6. 스킬 데이터에 정의된 대시 지속 시간만큼 대기
        yield return new WaitForSeconds(skill.dashDuration);

        // 7. 대시 종료 후 정지 및 중력/스크립트 상태 원상복구
        rigid.linearVelocity = Vector2.zero;    // 대기 지속 시간이 지나면 밀리는 관성을 지우기 휘애 속도를 즉시 0으로 만듦
        rigid.gravityScale = originalGravity;   // 대시 시작 전에 백업해 두었던 원래 중력 값으로 원복

        if (playerMove != null) playerMove.enabled = true; // 이동 제어 스크립트 복구

        isDashing = false;
    }

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
            ShootSwordWave(skill);             // 실제 투사체 생성
        }
        else
        {
            Debug.Log("마나가 부족하여 검기를 발사할 수 없습니다!");
        }
    }

    private void ShootSwordWave(SkillData skill)
    {
        if (weaponPivot == null)
        {
            Debug.LogWarning("Weapon Pivot이 인스펙터에 연결되지 않았습니다!");
            return;
        }

        // 1. 마우스를 바라보고 있는 weaponPivot의 위치와 회전값(rotation)으로 검기 생성
        GameObject waveObj = Instantiate(skill.projectilePrefab, weaponPivot.position, weaponPivot.rotation);

        // 2. 검기 스크립트에 속도 및 데미지 전달
        SwordWave swordWave = waveObj.GetComponent<SwordWave>();
        if (swordWave != null)
        {
            swordWave.Initialize(skill.projectileSpeed, skill.damage);
        }
    }
}



