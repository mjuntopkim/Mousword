using UnityEngine;

public class SwordHitbox : MonoBehaviour
{
    public int damage = 10;

    [Header("타격감")]
    [Tooltip("화면 흔들림 세기 (화면 높이 대비 비율)")]
    public float shakeStrength = 0.006f;
    [Tooltip("화면 흔들림 길이 (초, 히트스톱이 끝난 뒤 시작)")]
    public float shakeTime = 0.12f;
    [Tooltip("히트스톱 길이 (초, 실제 시간)")]
    public float hitStopTime = 0.05f;
    [Tooltip("적이 하얗게 번쩍이는 시간 (초, 실제 시간)")]
    public float flashTime = 0.06f;
    [Tooltip("처치 타격일 때 흔들림 세기·히트스톱에 곱하는 값")]
    public float killMultiplier = 1.5f;

    WeaponAim weaponAim;

    void Start()
    {
        weaponAim = GetComponentInParent<WeaponAim>();
    }

    void OnTriggerStay2D(Collider2D other)
    {
        //Demo_Monster monster = other.GetComponentInParent<Demo_Monster>();
        Monster monster = other.GetComponentInParent<Monster>();

        if (monster == null)
            return;

        if (weaponAim == null)
            return;

        if (!weaponAim.CanAttack)
            return;

        bool wasAlive = monster.currentHP > 0f;
        Vector2 swingDir = weaponAim.SwingTangent; // ConsumeSwing 전에 읽어야 함

        monster.TakeDamage(damage);
        weaponAim.ConsumeSwing();

        if (wasAlive) // 이미 쓰러진 몬스터를 다시 친 경우엔 연출 없음
            PlayHitFeedback(monster, swingDir, monster.currentHP <= 0f);
    }

    void PlayHitFeedback(Monster monster, Vector2 swingDir, bool killed)
    {
        float mul = killed ? killMultiplier : 1f;
        HitFlash.Play(monster, flashTime);
        HitFeedback.HitStop(hitStopTime * mul);
        HitFeedback.Shake(swingDir, shakeStrength * mul, shakeTime);
    }
}
