using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 사망 연출과 '계속하기' 부활 처리.
/// 사망: 조작·스킬 정지 → 하얀 섬광 + 히트스톱 → (공중이면 착지까지 대기) → 슬로모션으로 뒤로 쓰러짐
///      → 바닥 충돌 흔들림 → 눈빛이 꺼질 즈음 게임 오버 화면(GameOverUI).
/// 계속하기: 쓰러진 그 자리에서 체력·마나 회복 → 눈빛이 다시 켜지며 일어남 → 조작 복구 + 잠깐 무적.
/// PlayerStatus 가 Awake 에서 자동으로 붙인다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerDeathSequence : MonoBehaviour
{
    [Header("쓰러지는 연출")]
    [Tooltip("죽는 순간 하얗게 번쩍이며 멈추는 시간 (실제 시간, 초)")]
    [SerializeField] private float hitStopTime = 0.08f;
    [Tooltip("쓰러지는 동안의 게임 속도 (1 = 정상)")]
    [SerializeField, Range(0.1f, 1f)] private float slowMotionScale = 0.5f;
    [Tooltip("바닥에 부딪힐 때 화면 흔들림 세기 (화면 높이 대비)")]
    [SerializeField] private float impactShakeStrength = 0.006f;
    [SerializeField] private float impactShakeTime = 0.2f;
    [Tooltip("공중에서 죽었을 때 착지를 기다리는 최대 시간 (초)")]
    [SerializeField] private float maxAirWait = 3f;

    [Header("게임 오버 화면")]
    [Tooltip("사망 애니메이션이 이 시점(초)에 도달하면 화면이 어두워지기 시작 (눈빛이 흐려지는 순간)")]
    [SerializeField] private float overlayStartTime = 0.95f;

    private const float RecoilFrameTime = 0.05f;   // 공중에서 떨어지는 동안 멈춰 둘 프레임 (뒤로 젖혀진 자세)
    private const float ImpactFrameTime = 0.3166f; // Player_Die 에서 바닥에 부딪히는 프레임 시각

    private static readonly int DieState = Animator.StringToHash("Player_Die");
    private static readonly int GetUpState = Animator.StringToHash("Player_GetUp");
    private static readonly int IdleState = Animator.StringToHash("Player_Idle");

    private PlayerStatus status;
    private PlayerMove move;
    private PlayerSkillController skills;
    private Rigidbody2D rigid;
    private Animator anim;

    // 쓰러질 때 숨긴 무기(자식 오브젝트)의 렌더러·콜라이더와 원래 켜짐 상태
    private readonly List<KeyValuePair<Renderer, bool>> hiddenRenderers = new List<KeyValuePair<Renderer, bool>>();
    private readonly List<KeyValuePair<Collider2D, bool>> hiddenColliders = new List<KeyValuePair<Collider2D, bool>>();

    private Coroutine routine;
    private bool holdX;                     // 쓰러진 동안 좌우로 미끄러지지 않게 고정

    /// <summary>쓰러져 있거나 일어나는 중 (조작 불가)</summary>
    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        status = GetComponent<PlayerStatus>();
        move = GetComponent<PlayerMove>();
        skills = GetComponent<PlayerSkillController>();
        rigid = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    private void FixedUpdate()
    {
        if (holdX && rigid != null)
            rigid.linearVelocity = new Vector2(0f, rigid.linearVelocity.y);
    }

    public void Play()
    {
        if (IsPlaying) return;
        routine = StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        IsPlaying = true;

        // 1. 조작·스킬 정지
        //    스킬 컴포넌트를 먼저 끈다: OnDisable 이 대시 중력 등을 복구하면서 PlayerMove 를 다시 켜기 때문
        if (skills != null) skills.enabled = false;
        if (move != null) move.enabled = false;
        HideWeapon();
        holdX = true;
        if (rigid != null)
            rigid.linearVelocity = new Vector2(0f, Mathf.Min(rigid.linearVelocity.y, 0f));

        // 2. 하얀 섬광 프레임 + 히트스톱
        anim.speed = 1f;
        anim.Play(DieState, 0, 0f);
        anim.Update(0f);
        HitFeedback.HitStop(hitStopTime);
        yield return new WaitForSecondsRealtime(hitStopTime);
        while (HitFeedback.IsHitStopped) yield return null;

        // 3. 공중이면 뒤로 젖혀진 자세로 떨어진 뒤 쓰러진다
        if (!IsGrounded())
        {
            SetDieClipTime(RecoilFrameTime);
            anim.speed = 0f;
            float waited = 0f;
            while (!IsGrounded() && waited < maxAirWait)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            anim.speed = 1f;
            SetDieClipTime(RecoilFrameTime);
        }

        // 4. 슬로모션으로 쓰러짐 → 바닥에 닿는 순간 정상 속도 + 흔들림
        Time.timeScale = slowMotionScale;
        while (ClipTime(DieState) < ImpactFrameTime) yield return null;
        Time.timeScale = 1f;
        HitFeedback.Shake(Vector2.down, impactShakeStrength, impactShakeTime);

        // 5. 눈빛이 흐려지기 시작하면 게임 오버 화면
        while (ClipTime(DieState) < overlayStartTime) yield return null;
        GameOverUI.Show(this);
        routine = null;
    }

    /// <summary>
    /// '계속하기': 쓰러진 그 자리에서 체력·마나를 채우고 일어난다. 다 일어서면 onStood 호출.
    /// </summary>
    public void Revive(Action onStood = null)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(GetUpRoutine(onStood));
    }

    private IEnumerator GetUpRoutine(Action onStood)
    {
        IsPlaying = true;
        if (status != null) status.RestoreFull();   // HUD 게이지가 일어나는 동안 차오름

        // 눈빛이 다시 켜지고 → 몸을 일으켜 → 기본 자세
        anim.speed = 1f;
        anim.Play(GetUpState, 0, 0f);
        anim.Update(0f);
        yield return null;
        while (true)
        {
            AnimatorStateInfo s = anim.GetCurrentAnimatorStateInfo(0);
            if (s.shortNameHash != GetUpState || s.normalizedTime >= 1f) break;
            yield return null;
        }

        // 조작 복구
        anim.Play(IdleState, 0, 0f);
        RestoreWeapon();
        holdX = false;
        if (move != null)
        {
            move.IsMovementLocked = false;
            move.enabled = true;
        }
        if (skills != null) skills.enabled = true;

        IsPlaying = false;
        routine = null;
        if (status != null) status.Revive();         // 사망 해제 + 잠깐 무적
        onStood?.Invoke();
    }

    private bool IsGrounded()
    {
        if (move == null || move.groundCheck == null) return true;
        return Physics2D.OverlapCircle(move.groundCheck.position, move.checkRadius, move.isLayer) != null;
    }

    private float ClipTime(int state)
    {
        AnimatorStateInfo s = anim.GetCurrentAnimatorStateInfo(0);
        return s.shortNameHash == state ? s.normalizedTime * s.length : 0f;
    }

    private void SetDieClipTime(float seconds)
    {
        AnimatorStateInfo s = anim.GetCurrentAnimatorStateInfo(0);
        float length = s.shortNameHash == DieState && s.length > 0f ? s.length : 1.4f;
        anim.Play(DieState, 0, seconds / length);
        anim.Update(0f);
    }

    // 쓰러지는 몸만 보이도록 자식(무기 등)의 렌더러·콜라이더를 끈다
    private void HideWeapon()
    {
        hiddenRenderers.Clear();
        hiddenColliders.Clear();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject == gameObject) continue;
            hiddenRenderers.Add(new KeyValuePair<Renderer, bool>(r, r.enabled));
            r.enabled = false;
        }
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>(true))
        {
            if (c.gameObject == gameObject) continue;
            hiddenColliders.Add(new KeyValuePair<Collider2D, bool>(c, c.enabled));
            c.enabled = false;
        }
    }

    private void RestoreWeapon()
    {
        foreach (var pair in hiddenRenderers) if (pair.Key != null) pair.Key.enabled = pair.Value;
        foreach (var pair in hiddenColliders) if (pair.Key != null) pair.Key.enabled = pair.Value;
        hiddenRenderers.Clear();
        hiddenColliders.Clear();
    }
}
