using UnityEngine;

/// <summary>
/// 마법사 시전 관리 — 입력 / 마나 / 쿨다운 / 사거리를 판정하고 커서 위치에 마법을 소환한다.
///
/// 폭발 자체의 피해·반경·연출은 소환되는 프리팹의 SpellAoE 가 스스로 담당한다.
/// (시전 조건 = 여기, 마법의 효과 = 프리팹 → 마법 종류를 늘려도 이 스크립트는 그대로)
///
/// 붙이는 곳: Player_Mage 루트 (PlayerStatus 와 같은 오브젝트).
/// </summary>
public class SpellCaster : MonoBehaviour
{
    [Header("참조 (비우면 자동)")]
    [SerializeField] Camera cam;
    [SerializeField] PlayerStatus status;

    [Header("마법")]
    [Tooltip("커서 위치에 소환할 폭발 프리팹 (SpellAoE 부착).")]
    [SerializeField] GameObject spellPrefab;

    [Header("비용 / 쿨다운")]
    [SerializeField] float manaCost = 10f;
    [Tooltip("재시전까지 걸리는 시간(초).")]
    [SerializeField] float cooldown = 0.5f;

    [Header("사거리")]
    [Tooltip("플레이어 기준 최대 시전 거리. 0 이하면 무제한.")]
    [SerializeField] float maxCastRange = 8f;
    [Tooltip("켜면 사거리 밖을 클릭해도 사거리 끝에 시전. 끄면 시전 실패.")]
    [SerializeField] bool clampToRange = true;

    float lastCastTime = float.NegativeInfinity;
    SpellAoE cachedSpell;   // 프리팹의 SpellAoE (반경 조회용, 인스턴스 아님)

    // ── 조준점(AimReticle)·UI 가 참조하는 읽기 전용 상태 ──
    public bool IsOnCooldown => Time.time - lastCastTime < cooldown;
    public bool HasEnoughMana => status == null || status.CurrentMp >= manaCost;
    public bool CanCast => !IsOnCooldown && HasEnoughMana;
    public float MaxCastRange => maxCastRange;
    /// <summary>현재 장착된 마법의 폭발 반경 (조준점의 범위 원 표시용).</summary>
    public float CurrentSpellRadius => cachedSpell != null ? cachedSpell.Radius : 0f;

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (status == null) status = GetComponent<PlayerStatus>();

        // 프리팹 에셋에서 컴포넌트만 읽음 (소환되는 것 아님)
        if (spellPrefab != null) cachedSpell = spellPrefab.GetComponent<SpellAoE>();
    }

    /// <summary>
    /// 마우스 월드 좌표 → 실제로 마법이 터질 지점.
    /// 조준점과 시전이 "같은 함수"를 쓰므로 표시와 실제 착탄이 어긋날 수 없다.
    /// </summary>
    public Vector2 ResolveTarget(Vector2 mouseWorld, out bool withinRange)
    {
        withinRange = true;
        if (maxCastRange <= 0f) return mouseWorld;

        Vector2 toTarget = mouseWorld - (Vector2)transform.position;
        if (toTarget.magnitude <= maxCastRange) return mouseWorld;

        withinRange = false;

        // 사거리 밖: 클램프 모드면 사거리 끝으로 당기고, 아니면 원래 위치(= 시전 실패 예정)
        return clampToRange
            ? (Vector2)transform.position + toTarget.normalized * maxCastRange
            : mouseWorld;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryCast();
    }

    void TryCast()
    {
        if (cam == null || spellPrefab == null) return;
        if (IsOnCooldown) return;

        // 커서의 월드 좌표 = 마법이 터질 자리
        Vector3 ms = Input.mousePosition;
        ms.z = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector2 mouseWorld = cam.ScreenToWorldPoint(ms);

        // 사거리 판정 (마나를 소모하기 "전"에 처리해야 실패 시 마나가 낭비되지 않음)
        Vector2 target = ResolveTarget(mouseWorld, out bool withinRange);
        if (!withinRange && !clampToRange) return;

        // 마나 소모. 부족하면 ConsumeMp 가 false 를 반환하고 시전 취소.
        if (status != null && !status.ConsumeMp(manaCost)) return;

        Instantiate(spellPrefab, target, Quaternion.identity);
        lastCastTime = Time.time;
    }

    // 씬 뷰에서 사거리를 눈으로 확인
    void OnDrawGizmosSelected()
    {
        if (maxCastRange <= 0f) return;
        Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, maxCastRange);
    }
}
