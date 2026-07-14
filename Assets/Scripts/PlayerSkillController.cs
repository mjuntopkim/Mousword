using UnityEngine;
using System.Collections;

public class PlayerSkillController : MonoBehaviour
{
    [Header("스킬 데이터 에셋")]
    [SerializeField] private SkillData dashSlashSkill; // 인스펙터에서 대시 스킬 데이터를 등록합니다.
    [SerializeField] private Transform weaponPivot;    // 360도 무기 회전축
    [SerializeField] private SpriteRenderer bodySpriteRenderer;

    private Rigidbody2D rigid;
    private PlayerStatus playerStatus;

    

    private float currentCoolTime = 0f;
    private bool isDashing = false;

    void Start()
    {
        rigid = GetComponent<Rigidbody2D>();
        playerStatus = GetComponent<PlayerStatus>();
    }

    void Update()
    {
        // 쿨타임 계산
        if (currentCoolTime > 0)
        {
            currentCoolTime -= Time.deltaTime;
        }

        // 좌측 Shift 키를 누르고, 쿨타임이 끝났으며, 현재 대시 중이 아닐 때 실행합니다.
        if (Input.GetKeyDown(KeyCode.LeftShift) && currentCoolTime <= 0 && !isDashing)
        {
            TryExecuteDash(dashSlashSkill);
        }
    }

    private void TryExecuteDash(SkillData skill)
    {
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
        isDashing = true;

        // 1. 대시 중 플레이어 조작(PlayerMove)이 물리 연산을 방해하지 못하게 비활성화
        PlayerMove playerMove = GetComponent<PlayerMove>();
        if (playerMove != null) playerMove.enabled = false;

        // 2. 대시 중 중력 때문에 밑으로 쳐지는 현상 방지
        float originalGravity = rigid.gravityScale;
        rigid.gravityScale = 0f;

        // ================= [ 수정된 부분: 대시 방향 결정 ] =================
        Vector2 dashDirection = Vector2.right;

        // 키보드 AD나 방향키 입력을 직접 감지 (GetAxisRaw 사용으로 미끄러짐 방지)
        float horizontalInput = Input.GetAxisRaw("Horizontal");

        if (horizontalInput != 0)
        {
            // 키 입력이 있다면 누르고 있는 좌/우 방향으로 대시
            dashDirection = new Vector2(horizontalInput, 0f).normalized;
        }
        else
        {
            // 2.정지 상태일 때는 직접 연결한 몸통 스프라이트의 flipX 값을 읽어옵니다!
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

        // 4. 리지드바디 속도 변경 (최신 유니티 linearVelocity 활용)
        rigid.linearVelocity = dashDirection * skill.dashForce;

        // 5. 대시 이펙트 프리팹 생성 (있을 경우에만)
        if (skill.effectPrefab != null)
        {
            Instantiate(skill.effectPrefab, transform.position, transform.rotation);
        }

        // 6. 스킬 데이터에 정의된 대시 지속 시간만큼 대기
        yield return new WaitForSeconds(skill.dashDuration);

        // 7. 대시 종료 후 정지 및 중력/스크립트 상태 원상복구
        rigid.linearVelocity = Vector2.zero;
        rigid.gravityScale = originalGravity;

        if (playerMove != null) playerMove.enabled = true; // 이동 제어 스크립트 복구

        isDashing = false;
    }
}
