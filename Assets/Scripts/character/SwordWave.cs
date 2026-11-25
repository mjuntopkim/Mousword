using UnityEngine;

public class SwordWave : MonoBehaviour
{
    private const float Lifetime = 3f;
    private const int SpawnFrames = 2, LoopFrames = 4, FadeFrames = 3;
    private const float SpawnFrameTime = 0.04f; // 펼쳐지며 생겨나는 2프레임
    private const float LoopFrameTime = 0.05f;  // 심선이 일렁이는 4프레임 반복
    private const float FadeFrameTime = 0.06f;  // 조각으로 흩어지는 3프레임
    private const int FrameWidth = 56, FrameHeight = 80;

    // 검기 픽셀 프레임은 픽셀 크기(PPU)별로 한 번만 만들어 재사용
    private static Sprite[] cachedFrames;
    private static float cachedPpu;
    private static Sprite cachedDisc, cachedRing; // 발사 섬광 (지름 13px 원 / 고리)

    // ── 발사·비행 연출 ──
    private const float LaunchBoost = 2f, BoostTime = 0.15f;     // 2배 속도로 출발해 0.15초 동안 기본 속도로 감속
    private const float PopScale = 1.3f, PopTime = 0.08f;        // 1.3배 크기로 튀어나왔다가 원래 크기로
    private const float GhostInterval = 0.03f, GhostLifetime = 0.06f, GhostAlpha = 0.45f; // 잔상 2개 정도가 따라옴
    private const float DustInterval = 0.03f;
    private const int DustPerEmit = 2, LaunchShards = 10, FlashDiameter = 13;
    private static readonly Color32 FxWhite = new Color32(235, 250, 255, 255);
    private static readonly Color32 FxLight = new Color32(150, 220, 255, 255);
    private static readonly Color32 FxBlue = new Color32(70, 150, 255, 255);
    private static readonly Color32 FxDeep = new Color32(40, 80, 220, 255);
    private static readonly Color32[] FxPalette = { FxWhite, FxLight, FxBlue, FxDeep };

    [Header("타격감")]
    [Tooltip("히트스톱 길이 (초, 실제 시간)")]
    [SerializeField] private float hitStopTime = 0.04f;
    [Tooltip("화면 흔들림 세기 (화면 높이 대비 비율)")]
    [SerializeField] private float shakeStrength = 0.005f;
    [Tooltip("화면 흔들림 길이 (초, 히트스톱이 끝난 뒤 시작)")]
    [SerializeField] private float shakeTime = 0.1f;
    [Tooltip("적이 하얗게 번쩍이는 시간 (초, 실제 시간)")]
    [SerializeField] private float flashTime = 0.06f;
    [Tooltip("처치 타격일 때 흔들림 세기·히트스톱에 곱하는 값")]
    [SerializeField] private float killMultiplier = 1.5f;

    private float speed;
    private float damage;
    private Rigidbody2D rb;

    private SpriteRenderer spriteRenderer;
    private Collider2D hitCollider;
    private Sprite[] frames;
    private float age;
    private float fadeStart = -1f;

    private Vector3 baseScale = Vector3.one;
    private float currentPop = 1f;      // 지금 적용된 등장 크기 배율
    private float pixelWorld, localPixel, flightTime, ghostTimer, dustTimer;

    public void Initialize(float moveSpeed, float skillDamage)
    {
        Initialize(moveSpeed, skillDamage, 0f);
    }

    // pixelWorldSize: 캐릭터 시트 1픽셀의 월드 크기. 0보다 크면 픽셀 검기 애니메이션을 같은 픽셀 크기로 재생
    public void Initialize(float moveSpeed, float skillDamage, float pixelWorldSize)
    {
        speed = moveSpeed;
        damage = skillDamage;
        rb = GetComponent<Rigidbody2D>();
        hitCollider = GetComponent<Collider2D>();
        baseScale = transform.localScale;

        // 생성 당시 설정된 회전 각도의 오른쪽(transform.right) 방향으로 날아감 (출발 순간이 가장 빠름)
        flightTime = 0f;
        if (rb != null)
        {
            rb.linearVelocity = transform.right * speed * LaunchSpeed(0f);
        }

        if (pixelWorldSize > 0f)
        {
            SetupPixelFrames(pixelWorldSize);
            if (frames != null)
            {
                pixelWorld = pixelWorldSize;
                EmitLaunch(); // 원래 크기 기준 위치에서 섬광이 터지도록 크기를 키우기 전에
                SetPop(PopScale);
            }
        }

        // 3초 뒤 메모리 관리를 위해 자동 파괴
        Destroy(gameObject, Lifetime);
    }

    private void SetupPixelFrames(float pixelWorldSize)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        localPixel = pixelWorldSize / scale;
        float ppu = 1f / localPixel;
        if (cachedFrames == null || !Mathf.Approximately(cachedPpu, ppu))
        {
            var sheet = Resources.Load<Texture2D>("SwordWave/SwordWaveProjectile");
            if (sheet == null)
            {
                Debug.LogWarning("검기 스프라이트가 없습니다: Resources/SwordWave/SwordWaveProjectile", this);
                return;
            }
            cachedFrames = new Sprite[SpawnFrames + LoopFrames + FadeFrames];
            for (int i = 0; i < cachedFrames.Length; i++)
            {
                cachedFrames[i] = Sprite.Create(sheet, new Rect(i * FrameWidth, 0, FrameWidth, FrameHeight), new Vector2(0.5f, 0.5f), ppu);
                cachedFrames[i].name = "SwordWaveProjectile_" + i;
            }
            cachedDisc = CreateCircleSprite(ppu, false);
            cachedRing = CreateCircleSprite(ppu, true);
            cachedPpu = ppu;
        }
        frames = cachedFrames;
        spriteRenderer.sprite = frames[0];
        // 왼쪽으로 날아갈 때도 갈고리 끝이 위쪽에 오도록 상하 반전
        spriteRenderer.flipY = transform.right.x < 0f;

        // 충돌 범위를 초승달 몸통(가로 13px, 세로 64px)에 맞춤
        var box = hitCollider as BoxCollider2D;
        if (box != null)
        {
            box.size = new Vector2(13f, 64f) * localPixel;
            box.offset = new Vector2(15f, 0f) * localPixel;
        }
    }

    // 출발 속도 배율: LaunchBoost → 1 로 감속 (제곱 곡선). 늘어나는 사거리 = 기본속도 × BoostTime / 3 (약 0.75칸)
    private static float LaunchSpeed(float t)
    {
        if (t >= BoostTime) return 1f;
        float r = 1f - t / BoostTime;
        return 1f + (LaunchBoost - 1f) * r * r;
    }

    private void FixedUpdate()
    {
        if (rb == null || speed <= 0f || flightTime >= BoostTime || fadeStart >= 0f) return;
        flightTime += Time.fixedDeltaTime;
        rb.linearVelocity = transform.right * speed * LaunchSpeed(flightTime);
    }

    private void Update()
    {
        if (frames == null) return;
        float dt = Time.deltaTime;
        age += dt;

        if (fadeStart < 0f && age >= Lifetime - FadeFrameTime * FadeFrames) BeginFade();

        // 등장 크기: 1.3배 → 1배 (ease-out)
        if (currentPop != 1f)
        {
            float k = Mathf.Clamp01(age / PopTime);
            SetPop(Mathf.Lerp(PopScale, 1f, 1f - (1f - k) * (1f - k)));
        }

        // 날아가는 동안 잔상과 픽셀 가루를 남김
        if (fadeStart < 0f && dt > 0f)
        {
            ghostTimer += dt;
            if (ghostTimer >= GhostInterval) { ghostTimer -= GhostInterval; EmitGhost(); }
            dustTimer += dt;
            if (dustTimer >= DustInterval) { dustTimer -= DustInterval; EmitDust(); }
        }

        if (fadeStart >= 0f)
        {
            float t = age - fadeStart;
            if (t >= FadeFrameTime * FadeFrames) { Destroy(gameObject); return; }
            spriteRenderer.sprite = frames[SpawnFrames + LoopFrames + (int)(t / FadeFrameTime)];
        }
        else if (age < SpawnFrameTime * SpawnFrames)
        {
            spriteRenderer.sprite = frames[(int)(age / SpawnFrameTime)];
        }
        else
        {
            spriteRenderer.sprite = frames[SpawnFrames + (int)((age - SpawnFrameTime * SpawnFrames) / LoopFrameTime) % LoopFrames];
        }
    }

    #region 발사·비행 픽셀 연출

    private void SetPop(float pop)
    {
        currentPop = pop;
        transform.localScale = baseScale * pop;
    }

    // 초승달 몸통 위의 한 점 (스프라이트 로컬 픽셀 좌표 → 월드)
    private Vector3 CrescentPoint(float px, float py)
    {
        if (spriteRenderer != null && spriteRenderer.flipY) py = -py;
        return transform.TransformPoint(new Vector3(px, py, 0f) * localPixel);
    }

    private static Color FxColor(int i, float alpha)
    {
        Color c = FxPalette[i % FxPalette.Length];
        c.a = alpha;
        return c;
    }

    // 발사 순간: 섬광(원 + 퍼지는 고리)과 조준 방향으로 튀는 파편
    private void EmitLaunch()
    {
        Vector3 origin = CrescentPoint(15f, 0f);
        Vector3 flashScale = Vector3.one * (pixelWorld / localPixel); // 섬광 스프라이트도 캐릭터 픽셀 크기로
        PixelFxPool.Spawn(cachedDisc, origin, Quaternion.identity, flashScale, FxWhite, 0.05f, Vector3.zero, spriteRenderer, 2, 0f, 1.2f);
        PixelFxPool.Spawn(cachedRing, origin, Quaternion.identity, flashScale, new Color(0.6f, 0.9f, 1f, 0.9f), 0.1f, Vector3.zero, spriteRenderer, 2, 0f, 2f);

        Vector2 forward = transform.right;
        for (int i = 0; i < LaunchShards; i++)
        {
            Vector2 dir = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f)) * forward;
            float size = pixelWorld * (i % 3 == 0 ? 2f : 1f);
            PixelFxPool.Spawn(PixelFxPool.Pixel, origin, Quaternion.identity, new Vector3(size, size, 1f), FxColor(i, 0.95f),
                Random.Range(0.15f, 0.28f), dir * Random.Range(40f, 90f) * pixelWorld, spriteRenderer, 1, 6f);
        }
    }

    // 지금 모습 그대로 반투명 잔상을 남김 (빠를수록 간격이 벌어져 길게 늘어짐)
    private void EmitGhost()
    {
        SpriteRenderer ghost = PixelFxPool.Spawn(spriteRenderer.sprite, transform.position, transform.rotation, transform.lossyScale,
            new Color(1f, 1f, 1f, GhostAlpha), GhostLifetime, Vector3.zero, spriteRenderer, -1);
        if (ghost != null) ghost.flipY = spriteRenderer.flipY;
    }

    // 초승달 몸통에서 뒤로 흩날리는 파란 픽셀 가루
    private void EmitDust()
    {
        Vector2 back = -transform.right;
        Vector2 side = transform.up;
        for (int i = 0; i < DustPerEmit; i++)
        {
            Vector3 p = CrescentPoint(Random.Range(10f, 20f), Random.Range(-30f, 30f));
            Vector2 v = (back * Random.Range(8f, 20f) + side * Random.Range(-6f, 6f)) * pixelWorld;
            PixelFxPool.Spawn(PixelFxPool.Pixel, p, Quaternion.identity, new Vector3(pixelWorld, pixelWorld, 1f),
                FxColor(Random.Range(1, FxPalette.Length), 0.85f), Random.Range(0.2f, 0.35f), v, spriteRenderer, -1, 2f);
        }
    }

    // 지름 13px 픽셀 원 (ring = 테두리만)
    private static Sprite CreateCircleSprite(float ppu, bool ring)
    {
        int n = FlashDiameter;
        var tex = new Texture2D(n, n) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        float c = (n - 1) * 0.5f, outer = n * 0.5f, inner = outer - 1.5f;
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            bool on = d <= outer - 0.25f && (!ring || d >= inner);
            tex.SetPixel(x, y, on ? Color.white : Color.clear);
        }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), ppu);
        sprite.name = ring ? "SwordWaveFlashRing" : "SwordWaveFlashDisc";
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }

    #endregion

    private void BeginFade()
    {
        fadeStart = age;
        if (hitCollider != null) hitCollider.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fadeStart >= 0f) return;

        // 몬스터와 충돌 시 데미지 부여 및 검기 삭제
        Monster monster = other.GetComponentInParent<Monster>();
        if (monster != null)
        {
            bool wasAlive = monster.currentHP > 0f;
            monster.TakeDamage((int)damage);

            if (wasAlive) // 이미 쓰러진 몬스터를 다시 친 경우엔 연출 없음
                PlayHitFeedback(monster, other.bounds.center, monster.currentHP <= 0f);

            // 픽셀 검기는 사라지는 프레임을 보여준 뒤 삭제
            if (frames != null) BeginFade();
            else Destroy(gameObject);
        }
    }

    // 플래시는 항상, 히트스톱·흔들림은 적중 지점이 화면 안일 때만 (화면 밖에서 화면이 흔들리면 어색함)
    private void PlayHitFeedback(Monster monster, Vector3 hitPoint, bool killed)
    {
        HitFlash.Play(monster, flashTime);

        if (!IsOnScreen(hitPoint)) return;

        float mul = killed ? killMultiplier : 1f;
        // 화면은 검기가 날아가던 방향으로 밀림
        Vector2 dir = rb != null && rb.linearVelocity.sqrMagnitude > 1e-4f ? rb.linearVelocity : (Vector2)transform.right;
        HitFeedback.HitStop(hitStopTime * mul);
        HitFeedback.Shake(dir, shakeStrength * mul, shakeTime);
    }

    private static bool IsOnScreen(Vector3 worldPoint)
    {
        Camera cam = Camera.main;
        if (cam == null) return false;
        Vector3 v = cam.WorldToViewportPoint(worldPoint);
        return v.z >= 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
    }
}
