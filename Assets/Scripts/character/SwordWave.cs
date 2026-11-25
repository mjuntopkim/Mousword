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

    private float speed;
    private float damage;
    private Rigidbody2D rb;

    private SpriteRenderer spriteRenderer;
    private Collider2D hitCollider;
    private Sprite[] frames;
    private float age;
    private float fadeStart = -1f;

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

        // 생성 당시 설정된 회전 각도의 오른쪽(transform.right) 방향으로 날아감
        if (rb != null)
        {
            rb.linearVelocity = transform.right * speed;
        }

        if (pixelWorldSize > 0f)
        {
            SetupPixelFrames(pixelWorldSize);
        }

        // 시간 지나면 검기 자동 파괴
        Destroy(gameObject, Lifetime);
    }

    private void SetupPixelFrames(float pixelWorldSize)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        float scale = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
        float localPixel = pixelWorldSize / scale;
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

    private void Update()
    {
        if (frames == null) return;
        age += Time.deltaTime;

        if (fadeStart < 0f && age >= Lifetime - FadeFrameTime * FadeFrames) BeginFade();

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
            monster.TakeDamage((int)damage);

            // 픽셀 검기는 사라지는 프레임을 보여준 뒤 삭제
            if (frames != null) BeginFade();
            else Destroy(gameObject);
        }
    }
}
