using UnityEngine;
using System.Collections;

public class PlayerStatus : MonoBehaviour
{
    // 생존 스텟
    [SerializeField] private float maxHp = 100f;       // 최대 체력
    [SerializeField] private float currentHp;          //현재 체력

    [SerializeField] private float maxMp = 50f;        // 최대 마나
    [SerializeField] private float currentMp;          // 현재 마나

    //방어 스텟
    [SerializeField] private float defense = 5f;       // 방어력(데미지 감소)
    [SerializeField] private float invincibleTime = 0.5f; // 피격 무적 시간

    [SerializeField] private float reviveInvincibleTime = 1.5f; // 계속하기로 부활한 직후 무적 시간

    private bool isInvincible = false;                 // 현재 무적 상태 여부
    private SpriteRenderer spriteRenderer;
    private Coroutine invincibleRoutine;               // 진행 중인 피격 무적(깜빡임) 코루틴
    private PlayerDeathSequence deathSequence;         // 쓰러지는 연출 + 게임 오버 화면

    // 다른 스크립트에서 스텟을 안전하게 읽을 수 있도록 프로퍼티(Property) 제공
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float CurrentMp => currentMp;
    public float MaxMp => maxMp;
    public bool IsDead { get; private set; } = false;   // 사망 여부
    public bool SkillInvincible { get; set; }           // 스킬 시전 중 무적 (궁극기 등)

    void Awake()
    {
        // 사망 연출 컴포넌트가 프리팹에 없으면 자동으로 붙인다 (스테이지 시작 위치 기록을 위해 미리 붙여 둠)
        if (!TryGetComponent(out deathSequence))
            deathSequence = gameObject.AddComponent<PlayerDeathSequence>();
    }

    void Start()
    {
        // 게임 시작 시 체력과 마나를 최대치로 초기화
        currentHp = maxHp;
        currentMp = maxMp;

        // 피격 무적 연출을 위한 스프라이트 렌더러 컴포넌트
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // 테스트용: F4 키로 즉시 사망 (에디터·개발 빌드 전용)
    void Update()
    {
        if (!IsDead && Input.GetKeyDown(KeyCode.F4))
        {
            isInvincible = false;
            SkillInvincible = false;
            TakeDamage(currentHp + defense + 1f);
        }
    }
#endif

    // 플레이어가 데미지를 입을 때 호출하는 메서드
    public void TakeDamage(float damage)
    {
        // 사망했거나 무적 상태라면 데미지를 받지 않음
        if (IsDead || isInvincible || SkillInvincible) return;

        // 방어력을 계산한 최종 데미지 (최소 1의 데미지는 입도록 설정)
        float finalDamage = Mathf.Max(damage - defense, 1f);

        currentHp -= finalDamage;
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp); // 체력이 0 미만, 최대치 초과되지 않게 방지

        Debug.Log($"플레이어가 {finalDamage}의 데미지를 입었습니다. 남은 체력: {currentHp}");

        if (currentHp <= 0)
        {
            Die();
        }
        else
        {
            // 무적 시간 및 피격 연출 연출 시작
            invincibleRoutine = StartCoroutine(InvincibleCooldown(invincibleTime));
        }
    }

    // 피격 시 일정 시간 무적 상태로 만들고 깜빡이는 연출
    private IEnumerator InvincibleCooldown(float duration)
    {
        // 무적 상태로 전환
        isInvincible = true;

        // 무적 시간 동안 캐릭터를 깜빡거리게 만듦 (피격 시각 효과)
        float timer = 0f;
        while (timer < duration)
        {
            // 알파값을 0.2와 1.0 사이로 변환
            float alpha = spriteRenderer.color.a == 1f ? 0.2f : 1f;
            spriteRenderer.color = new Color(1f, 1f, 1f, alpha);

            yield return new WaitForSeconds(0.1f);
            timer += 0.1f;
        }

        // 무적 종료 후 원래 투명도로 복귀
        spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        isInvincible = false;       // 무적 상태 해제
        invincibleRoutine = null;
    }

    // 깜빡임 중이면 멈추고 원래 색으로 되돌림
    private void StopInvincibleFlicker()
    {
        if (invincibleRoutine != null) StopCoroutine(invincibleRoutine);
        invincibleRoutine = null;
        isInvincible = false;
        if (spriteRenderer != null) spriteRenderer.color = Color.white;
    }

    // 마나 / 스태미나 소비 메서드
    public bool ConsumeMp(float amount)
    {
        if (currentMp >= amount)
        {
            currentMp -= amount;
            return true; // 소비 성공
        }

        Debug.Log("마나/스태미나가 부족합니다.");
        return false; // 소비 실패
    }

    // 마나  회복 메서드 (포션이나 자연 회복용)
    public void RecoverMp(float amount)
    {
        currentMp += amount;
        currentMp = Mathf.Clamp(currentMp, 0f, maxMp);
    }

    // 체력 회복 메서드 (흡혈이나 포션용)
    public void RecoverHp(float amount)
    {
        if (IsDead) return;

        currentHp += amount;
        currentHp = Mathf.Clamp(currentHp, 0f, maxHp);
    }

    // 플레이어 사망 처리
    private void Die()
    {
        IsDead = true;
        Debug.Log("플레이어가 사망했습니다.");

        // 깜빡이던 중에 죽었으면 반투명 상태로 쓰러지지 않도록 정리
        StopInvincibleFlicker();

        // 조작 정지 → 쓰러지는 애니메이션 → 게임 오버 화면
        deathSequence.Play();
    }

    // 게임 오버 화면의 '계속하기' 1단계: 쓰러진 채로 체력·마나를 가득 채움 (아직 사망 상태라 피해를 받지 않음)
    public void RestoreFull()
    {
        currentHp = maxHp;
        currentMp = maxMp;
    }

    // '계속하기' 2단계: 다 일어선 뒤 다시 살아나고 잠깐 무적 (깜빡임)
    public void Revive()
    {
        StopInvincibleFlicker();
        RestoreFull();
        IsDead = false;
        if (reviveInvincibleTime > 0f)
            invincibleRoutine = StartCoroutine(InvincibleCooldown(reviveInvincibleTime));
    }
}
