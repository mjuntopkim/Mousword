using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// 궁극기 연출: 화면 중앙에 거대한 검이 떨어져 화면 전체의 지면을 부순다.
/// ① 시전  플레이어가 검을 치켜듦(검기 포즈 재사용), 화면이 어두워짐
/// ② 예고  낙하 지점에 빛기둥·바닥 표식, 하늘에 반짝임
/// ③ 낙하  플레이어 검을 크게 키운 거대한 검이 잔상을 남기며 내리꽂힘
/// ④ 충돌  onImpact 호출(판정은 PlayerSkillController) + 히트스톱·화면 플래시·강한 흔들림
/// ⑤ 여파  충돌 지점에서 화면 끝까지 퍼지는 지면 파괴 파동:
///         화면에 보이는 모든 지면을 따라 빛나는 균열·솟구치는 바위·파편·흙먼지,
///         하늘로 퍼지는 충격파 고리와 빛줄기, 천장·화면 위에서 떨어지는 돌가루,
///         검은 빛 조각으로 부서지고 화면이 돌아옴
/// 타임라인은 실제 시간(unscaled)으로 흐르며, 히트스톱 동안에는 멈춘다.
/// 모든 효과는 맨 위 정렬 레이어에 그려서 어두워진 화면 위로 보이게 한다.
/// 이펙트의 픽셀 1칸 = 거대한 검 스프라이트의 픽셀 1칸 크기.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerUltimateVisuals : MonoBehaviour
{
    // ── 타임라인 (초) ──
    public const float TelegraphStart = 0.25f; // ② 예고 시작
    public const float DropStart = 0.6f;       // ③ 낙하 시작
    public const float ImpactTime = 0.68f;     // ④ 충돌
    public const float HitStopTime = 0.1f;     // 충돌 순간 정지 시간 (실제 시간)

    private const float DarkFadeIn = 0.25f, DarkAlpha = 0.55f, DropDarkAlpha = 0.7f;
    private const float DarkFadeOutStart = ImpactTime + 0.85f, DarkFadeOutTime = 0.5f;
    private const float BeamGrowTime = 0.1f;
    private const float MarkerGrowTime = 0.3f;
    private const float FlashHold = 0.04f, FlashFade = 0.18f, FlashAlpha = 0.95f;
    private const float ShakeTime = 0.55f, ShakeStrength = 0.035f;   // 흔들림 폭 = 화면 높이 × 비율
    private const float RumbleTime = 0.9f, RumbleStrength = 0.006f;  // 파동이 퍼지는 동안의 잔진동
    private const float ColumnTime = 0.25f;
    private const float WaveTime = 0.32f;          // 파괴 파동이 화면 끝까지 퍼지는 시간
    private const float SurfaceGlowTime = 0.55f;   // 지면 표면이 빛났다 식는 시간
    private const float CrackGrowTime = 0.1f, CrackLife = 1.4f;
    private const float RaysTime = 0.3f, RingTime = 0.4f;
    private const float SpikeRiseTime = 0.07f;
    private const float FallingStart = 0.03f, FallingEnd = 0.9f, FallingRate = 70f; // 위에서 떨어지는 돌가루 (개/초)
    private const float ShatterDelay = 0.55f, ShatterWarn = 0.15f; // 충돌 후 검이 부서지기까지, 부서지기 전 깜빡임
    private const float EndTime = ImpactTime + 1.85f; // 가장 늦게 생긴 균열이 식어 사라질 때까지
    private const float SinkRatio = 0.08f;  // 검 길이 중 땅에 박히는 비율
    private const float MaxStep = 0.05f;    // 프레임이 끊겨도 연출이 한 번에 건너뛰지 않도록
    private const int RayCount = 12, MaxSpikes = 28, MarginTexels = 4;

    // ── 정렬 순서 (맨 위 정렬 레이어 안에서) ──
    private const int DarkOrder = 32000, BeamOrder = 32010, MarkerOrder = 32014, GlintOrder = 32015,
        PlayerOrder = 32020, GroundFxOrder = 32027, SpikeOrder = 32028, AuraOrder = 32030, GhostOrder = 32031,
        SwordOrder = 32033, ColumnOrder = 32036, ParticleOrder = 32040, SparkOrder = 32045, FlashOrder = 32100;

    // ── 금빛·흰빛 팔레트 ──
    private static readonly Color32 White = new Color32(255, 250, 228, 255);
    private static readonly Color32 GoldLight = new Color32(255, 222, 120, 255);
    private static readonly Color32 Gold = new Color32(255, 186, 64, 255);
    private static readonly Color32 GoldDeep = new Color32(236, 140, 44, 255);
    private static readonly Color32 Ember = new Color32(110, 50, 25, 255);
    private static readonly Color[] EarthColors =
    {
        new Color32(112, 82, 58, 255), new Color32(74, 54, 42, 255), new Color32(150, 140, 125, 255),
    };
    private static readonly Color[] ShardColors =
    {
        new Color32(255, 250, 228, 255), new Color32(255, 222, 120, 255), new Color32(175, 220, 232, 255),
    };
    private static readonly Color DustColor = new Color(0.8f, 0.74f, 0.65f, 0.65f);
    private static readonly float[] GhostAlphas = { 0.45f, 0.28f, 0.14f };

    // '@' 흰색, 'o' 밝은 금색, '#' 진한 금색. 위에서 아래 순서.
    private static readonly string[][] Patterns =
    {
        new[] // 0: 하늘 반짝임 (작게)
        {
            ".........",
            "....#....",
            "....o....",
            "...o@o...",
            ".#o@@@o#.",
            "...o@o...",
            "....o....",
            "....#....",
            ".........",
        },
        new[] // 1: 하늘 반짝임 (크게)
        {
            "....#....",
            "....o....",
            "..#.o.#..",
            "...o@o...",
            "#oo@@@oo#",
            "...o@o...",
            "..#.o.#..",
            "....o....",
            "....#....",
        },
        new[] // 2: 몬스터 적중 (터짐)
        {
            "#...#...#",
            ".#..o..#.",
            "..o.o.o..",
            "...o@o...",
            "#oo@@@oo#",
            "...o@o...",
            "..o.o.o..",
            ".#..o..#.",
            "#...#...#",
        },
        new[] // 3: 몬스터 적중 (흩어짐)
        {
            "#.......#",
            "..#...#..",
            ".........",
            ".o.....o.",
            ".........",
            ".o.....o.",
            ".........",
            "..#...#..",
            "#.......#",
        },
    };

    private sealed class Bit
    {
        public SpriteRenderer r;
        public Vector2 pos, vel;
        public float gravity, drag, age, life;
        public Color color;
    }

    private sealed class Spark
    {
        public SpriteRenderer r;
        public float age;
    }

    // 화면 안의 지면 표면 한 칸(2텍셀 폭). 파동이 닿는 시각 trig에 파괴 효과가 터짐
    private struct Surface
    {
        public float x, y, trig;
        public int ix, iy; // 화면 캔버스에서 표면 바로 아래(땅 속) 텍셀
    }

    private sealed class Crack
    {
        public readonly List<Vector2Int> points = new List<Vector2Int>();
        public float trig;
    }

    private sealed class Spike
    {
        public SpriteRenderer r;
        public PixelCanvas canvas;
        public Vector3 position;
        public float trig, crumble, dir;
        public int w, h;
        public bool risen, crumbled;
    }

    /// <summary>코드로 그리는 픽셀 텍스처. 1텍셀 = 1/ppu 월드 단위.</summary>
    private sealed class PixelCanvas
    {
        public readonly int w, h;
        public readonly float ppu;
        public readonly Vector2 pivot;
        public readonly Sprite sprite;
        private readonly Texture2D tex;
        private readonly Color32[] buf;

        public PixelCanvas(int w, int h, Vector2 pivot, float ppu, string name)
        {
            this.w = w; this.h = h; this.ppu = ppu; this.pivot = pivot;
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name
            };
            buf = new Color32[w * h];
            Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
            sprite.name = name;
        }

        public bool Matches(int w2, int h2, Vector2 pivot2, float ppu2) =>
            w == w2 && h == h2 && pivot == pivot2 && Mathf.Approximately(ppu, ppu2);

        public void Clear() => Array.Clear(buf, 0, buf.Length);

        public void Set(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < w && y < h) buf[y * w + x] = c;
        }

        public void SetIfEmpty(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < w && y < h && buf[y * w + x].a == 0) buf[y * w + x] = c;
        }

        public void Apply()
        {
            tex.SetPixels32(buf);
            tex.Apply(false);
        }

        public void Release()
        {
            ReleaseAsset(sprite);
            ReleaseAsset(tex);
        }
    }

    private const int BitPoolSize = 512, SparkPoolSize = 24;
    private readonly Bit[] bits = new Bit[BitPoolSize];
    private readonly Spark[] sparks = new Spark[SparkPoolSize];
    private int nextBit, nextSpark;

    private readonly List<SpriteRenderer> allRenderers = new List<SpriteRenderer>();
    private Transform root, swordRoot;
    private SpriteRenderer dark, flash, beamOuter, beamMid, beamCore, marker, glint, aura, sword;
    private SpriteRenderer column, columnOuter, groundFx;
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[3];
    private readonly Transform[] ghostRoots = new Transform[3];
    private readonly List<Spike> spikes = new List<Spike>();
    private int spikeCount;

    private Sprite pixel;
    private Sprite[] patternSprites;
    private float patternPpu = -1f;
    private PixelCanvas markerCanvas, auraCanvas, screenCanvas;

    private PlayerSwordWaveVisuals pose;
    private Func<List<Vector3>> impactCallback;
    private Camera cam;
    private SpriteRenderer source;
    private Material material;
    private int layerId, objectLayer;

    private bool active, impacted, shattered, poseHolding, hitStopActive, hasGround;
    private float t, freeze, sinceImpact, fallingAccum;
    private float P, H, W, impactX, groundY, screenTop, screenBottom, screenLeft, swordLength, swordWidth, z;
    private float originX, originY; // 화면 캔버스 (0,0) 텍셀의 월드 좌표
    private int markerRx, drawnMarkerRx = -1, impactIx, impactIy;
    private bool screenCanvasDirty;

    private readonly List<Surface> surfaces = new List<Surface>();
    private readonly List<Vector2> ceilings = new List<Vector2>();
    private readonly List<Crack> crackList = new List<Crack>();
    private readonly float[] rayAngles = new float[RayCount];
    private int nextSurface;

    private ContactFilter2D solidFilter;
    private readonly RaycastHit2D[] rayHits = new RaycastHit2D[1];
    private readonly Collider2D[] overlapHits = new Collider2D[1];

    private Vector3 shakeOffset;
    private bool hooked;

    public bool IsPlaying => active;
    public bool HasImpacted => impacted;

    /// <summary>
    /// 궁극기 연출 시작. onImpact는 검이 땅에 꽂히는 순간 한 번 호출되며, 맞은 몬스터 위치를 돌려주면 그 자리에 적중 스파크를 그린다.
    /// 시작하지 못하면(카메라 없음 등) false.
    /// </summary>
    public bool Begin(SpriteRenderer body, Transform weaponRoot, Camera camera, LayerMask groundMask, Func<List<Vector3>> onImpact)
    {
        Cancel();
        if (body == null || camera == null || !camera.orthographic) return false;

        source = body;
        cam = camera;
        impactCallback = onImpact;
        material = body.sharedMaterial;
        var layers = SortingLayer.layers;
        layerId = layers.Length > 0 ? layers[layers.Length - 1].id : body.sortingLayerID;
        objectLayer = body.gameObject.layer;
        z = body.transform.position.z;

        solidFilter = new ContactFilter2D();
        solidFilter.SetLayerMask(groundMask);
        solidFilter.useTriggers = false;

        // 화면 크기와 낙하 지점(화면 중앙 아래의 땅)
        H = camera.orthographicSize * 2f;
        W = H * camera.aspect;
        Vector3 c = camera.transform.position;
        screenTop = c.y + H * 0.5f;
        screenBottom = c.y - H * 0.5f;
        screenLeft = c.x - W * 0.5f;
        impactX = c.x;
        hasGround = FindGround(impactX, c.y, out groundY);
        if (!hasGround) groundY = screenBottom + H * 0.2f;

        // 거대한 검 = 플레이어 검 스프라이트. 손잡이가 화면 위쪽 끝 바로 아래에 오도록 길이를 맞춤
        SpriteRenderer weaponSword = FindSword(weaponRoot);
        int texLong = 72, texShort = 10;
        if (weaponSword != null)
        {
            Rect rect = weaponSword.sprite.rect;
            Bounds b = weaponSword.sprite.bounds;
            bool vertical = b.size.y > b.size.x;
            texLong = Mathf.Max(8, Mathf.RoundToInt(vertical ? rect.height : rect.width));
            texShort = Mathf.Max(2, Mathf.RoundToInt(vertical ? rect.width : rect.height));
        }
        swordLength = Mathf.Clamp((screenTop - H * 0.06f - groundY) / (1f - SinkRatio), H * 0.45f, H * 1.2f);
        P = swordLength / texLong;
        swordWidth = texShort * P;

        EnsureObjects();
        PreparePatterns();
        ConfigureSword(sword, weaponSword);
        for (int i = 0; i < ghosts.Length; i++) ConfigureSword(ghosts[i], weaponSword);
        BuildAura(texLong, texShort);

        markerRx = Mathf.Clamp(Mathf.RoundToInt(W * 0.08f / P), 6, 60);
        int markerRy = Mathf.Max(1, Mathf.RoundToInt(markerRx * 0.25f));
        EnsureCanvas(ref markerCanvas, 2 * (markerRx + 1) + 3, 2 * (markerRy + 1) + 3, new Vector2(0.5f, 0.5f), "Ultimate Marker");
        marker.sprite = markerCanvas.sprite;

        // 화면 전체를 덮는 픽셀 캔버스 (지면 균열·표면 빛·충격파 고리·빛줄기)
        Vector3 origin = Snap(screenLeft - MarginTexels * P, screenBottom - MarginTexels * P);
        originX = origin.x;
        originY = origin.y;
        int cw = Mathf.CeilToInt(W / P) + MarginTexels * 2 + 2;
        int ch = Mathf.CeilToInt(H / P) + MarginTexels * 2 + 2;
        EnsureCanvas(ref screenCanvas, cw, ch, Vector2.zero, "Ultimate Ground FX");
        groundFx.sprite = screenCanvas.sprite;
        impactIx = TexelX(impactX);
        impactIy = TexelY(groundY);

        ScanTerrain();
        BuildCracks();
        BuildSpikes();
        for (int i = 0; i < RayCount; i++)
            rayAngles[i] = Mathf.Lerp(12f, 168f, (i + Random.Range(0.2f, 0.8f)) / RayCount) * Mathf.Deg2Rad;

        HideAll();
        t = freeze = sinceImpact = fallingAccum = 0f;
        impacted = shattered = false;
        drawnMarkerRx = -1;
        nextSurface = 0;
        screenCanvasDirty = false;

        // ① 시전 포즈: 낙하 지점을 바라보며 검을 치켜들고 충돌 직전까지 유지
        float dx = impactX - body.transform.position.x;
        float facing = Mathf.Abs(dx) > 0.3f ? Mathf.Sign(dx) : (body.flipX ? -1f : 1f);
        body.flipX = facing < 0f;
        pose = GetComponent<PlayerSwordWaveVisuals>();
        if (pose == null) pose = gameObject.AddComponent<PlayerSwordWaveVisuals>();
        pose.Begin(body, weaponRoot, facing, true);
        pose.SetSorting(layerId, PlayerOrder);
        poseHolding = pose.IsPlaying;

        Hook();
        active = true;
        Tick(0f);
        return true;
    }

    public void Cancel()
    {
        EndHitStop();
        Unhook();
        if (poseHolding && pose != null) pose.Cancel();
        poseHolding = false;
        active = false;
        HideAll();
    }

    private void LateUpdate()
    {
        if (active) Tick(Mathf.Min(Time.unscaledDeltaTime, MaxStep));
    }

    private void Tick(float dt)
    {
        if (source == null || cam == null) { Cancel(); return; }

        if (freeze > 0f)
        {
            freeze -= dt;
            if (freeze <= 0f) EndHitStop();
        }
        else t += dt;
        if (impacted) sinceImpact += dt;

        KeepRootInWorld();

        // 플레이어의 내려베기 임팩트 프레임이 거대한 검의 충돌과 겹치도록 미리 풀어줌
        if (poseHolding && t >= ImpactTime - PlayerSwordWaveVisuals.ReleaseLead)
        {
            poseHolding = false;
            if (pose != null) pose.ReleaseHold();
        }
        if (!impacted && t >= ImpactTime) DoImpact();
        if (impacted && !shattered && t >= ImpactTime + ShatterDelay) Shatter();

        float sim = freeze > 0f ? 0f : dt;
        float e = t - ImpactTime;
        if (impacted)
        {
            FireSurfaces(e);
            UpdateSpikes(e);
            SpawnFalling(e, sim);
        }

        UpdateScreen();
        UpdateTelegraph();
        UpdateSword();
        UpdateImpactColumn(e);
        DrawGroundFx(e);
        UpdateBits(sim);
        UpdateSparks(sim);
        UpdateShake(e);

        if (t >= EndTime && !AnyAlive()) Finish();
    }

    #region 단계별 갱신

    // 화면 어둡게 / 충돌 플래시
    private void UpdateScreen()
    {
        Vector2 c = cam.transform.position;
        float a;
        if (t < DarkFadeIn) a = DarkAlpha * t / DarkFadeIn;
        else if (t < DropStart) a = DarkAlpha;
        else if (t < DarkFadeOutStart) a = Mathf.Lerp(DarkAlpha, DropDarkAlpha, Mathf.Clamp01((t - DropStart) / (ImpactTime - DropStart)));
        else a = DropDarkAlpha * (1f - Mathf.Clamp01((t - DarkFadeOutStart) / DarkFadeOutTime));
        SetQuad(dark, c, W * 1.4f, H * 1.4f, new Color(0.02f, 0.01f, 0.05f, a));

        float f = 0f;
        if (impacted)
            f = sinceImpact < FlashHold ? FlashAlpha : FlashAlpha * (1f - Mathf.Clamp01((sinceImpact - FlashHold) / FlashFade));
        SetQuad(flash, c, W * 1.4f, H * 1.4f, new Color(1f, 0.98f, 0.9f, f));
    }

    // ② 예고: 빛기둥, 바닥 표식, 하늘 반짝임
    private void UpdateTelegraph()
    {
        if (t < TelegraphStart || impacted)
        {
            beamCore.enabled = beamMid.enabled = beamOuter.enabled = marker.enabled = glint.enabled = false;
            return;
        }

        bool dropping = t >= DropStart;
        float pulse = 0.8f + 0.2f * Mathf.Sin(t * 45f);
        float k = Mathf.Clamp01((t - TelegraphStart) / BeamGrowTime);
        float beamTop = screenTop + H * 0.05f;
        float beamBottom = Mathf.Lerp(beamTop, groundY, 1f - (1f - k) * (1f - k));
        float len = beamTop - beamBottom;
        Vector2 mid = new Vector2(impactX, (beamTop + beamBottom) * 0.5f);
        float core = dropping ? 2f * P : P;
        SetQuad(beamCore, mid, core, len, WithAlpha(White, pulse));
        SetQuad(beamMid, mid, core + 2f * P, len, WithAlpha(GoldLight, 0.5f * pulse));
        SetQuad(beamOuter, mid, core + 6f * P, len, WithAlpha(Gold, (dropping ? 0.32f : 0.2f) * pulse));

        float km = Mathf.Clamp01((t - TelegraphStart) / MarkerGrowTime);
        DrawMarker(Mathf.RoundToInt(Mathf.Lerp(1f, markerRx, 1f - Mathf.Pow(1f - km, 3f))));
        marker.transform.position = Snap(impactX, groundY);
        marker.color = new Color(1f, 1f, 1f, 0.7f + 0.3f * pulse);
        marker.enabled = true;

        bool glinting = t >= TelegraphStart + 0.05f && !dropping;
        glint.enabled = glinting;
        if (glinting)
        {
            glint.sprite = patternSprites[(int)(t / 0.06f) % 2];
            glint.transform.position = Snap(impactX, screenTop - 6f * P);
        }
    }

    // ③ 낙하 + 꽂혀 있는 검
    private void UpdateSword()
    {
        bool hasSprite = sword.sprite != null;
        if (t < DropStart || shattered)
        {
            sword.enabled = aura.enabled = false;
            foreach (var g in ghosts) g.enabled = false;
            return;
        }

        float endY = groundY - SinkRatio * swordLength;
        float y = endY;
        if (!impacted)
        {
            float k = Mathf.Clamp01((t - DropStart) / (ImpactTime - DropStart));
            y = Mathf.Lerp(screenTop + H * 0.02f, endY, k * k);
        }
        swordRoot.position = Snap(impactX, y);
        sword.enabled = hasSprite;
        aura.enabled = true;

        float auraAlpha = 0.9f;
        if (impacted)
        {
            float e = t - ImpactTime;
            auraAlpha = e > ShatterDelay - ShatterWarn
                ? ((int)(e / 0.03f) % 2 == 0 ? 1f : 0.35f)  // 부서지기 직전 깜빡임
                : 0.75f + 0.25f * Mathf.Sin(e * 30f);
        }
        aura.color = new Color(1f, 1f, 1f, auraAlpha);

        for (int i = 0; i < ghosts.Length; i++)
        {
            bool show = hasSprite && !impacted && t > DropStart + 0.01f;
            ghosts[i].enabled = show;
            if (!show) continue;
            ghostRoots[i].position = Snap(impactX, y + (i + 1) * swordLength * 0.14f);
            ghosts[i].color = new Color(1f, 0.92f, 0.65f, GhostAlphas[i]);
        }
    }

    // ④ 충돌 지점에서 위로 솟는 빛기둥
    private void UpdateImpactColumn(float e)
    {
        float kc = impacted ? Mathf.Clamp01(e / ColumnTime) : 1f;
        if (kc >= 1f)
        {
            column.enabled = columnOuter.enabled = false;
            return;
        }
        float w = Mathf.Max(P, Mathf.Round(Mathf.Lerp(14f, 2f, kc)) * P);
        float top = screenTop + H * 0.05f;
        Vector2 mid = new Vector2(impactX, (top + groundY) * 0.5f);
        SetQuad(column, mid, w, top - groundY, WithAlpha(White, 1f - kc));
        SetQuad(columnOuter, mid, w * 2.4f, top - groundY, WithAlpha(Gold, 0.45f * (1f - kc)));
    }

    #endregion

    #region 화면 전체 지면 파괴

    // 화면 안의 모든 지면 표면(위가 비고 아래가 땅인 곳)과 천장(아래가 빈 곳)을 2텍셀 간격으로 찾음
    private void ScanTerrain()
    {
        surfaces.Clear();
        ceilings.Clear();
        float bottom = screenBottom - P;
        float maxDist = W * 0.5f + MarginTexels * P;
        float speed = maxDist / WaveTime;
        for (int ix = 0; ix + 1 < screenCanvas.w; ix += 2)
        {
            float x = originX + (ix + 1) * P;
            float y = screenTop + P;
            if (IsSolid(x, y))
            {
                y = SkipSolid(x, y, bottom);
                if (y > bottom) ceilings.Add(new Vector2(x, y));
            }
            for (int guard = 0; guard < 8 && y > bottom; guard++)
            {
                int n = Physics2D.Raycast(new Vector2(x, y), Vector2.down, solidFilter, rayHits, y - bottom);
                if (n == 0) break;
                float sy = rayHits[0].point.y;
                if (rayHits[0].distance > 0f && sy <= screenTop)
                {
                    float dist = Mathf.Abs(x - impactX) + Mathf.Abs(sy - groundY) * 0.5f;
                    surfaces.Add(new Surface
                    {
                        x = x, y = sy, ix = ix, iy = TexelY(sy) - 1,
                        trig = dist / speed + Random.Range(0f, 0.025f),
                    });
                }
                float below = SkipSolid(x, sy - P * 0.5f, bottom);
                if (below <= bottom) break;
                ceilings.Add(new Vector2(x, below));
                y = below;
            }
        }
        surfaces.Sort((a, b) => a.trig.CompareTo(b.trig));
    }

    // 균열: 충돌 지점의 큰 균열 + 표면마다 땅 속으로 짧게 (가운데일수록 많고 길게)
    private void BuildCracks()
    {
        crackList.Clear();
        int budget = 3500; // 땅 판정 횟수 상한
        if (hasGround)
        {
            int len = Mathf.Clamp(Mathf.RoundToInt(W * 0.14f / P), 10, 90);
            for (int i = 0; i < 6; i++)
            {
                float dir = i % 2 == 0 ? 1f : -1f;
                float angle = i < 2 ? Random.Range(6f, 18f) : i < 4 ? Random.Range(25f, 45f) : Random.Range(55f, 75f);
                // 표면 바로 아래 줄(땅 속)에서 시작
                AddCrack(new Vector2(impactIx, impactIy - 1), dir, angle, Mathf.RoundToInt(len * Random.Range(0.6f, 1f)), 0f, ref budget, true);
            }
        }
        foreach (Surface s in surfaces)
        {
            float d = Mathf.Clamp01(Mathf.Abs(s.x - impactX) / (W * 0.5f));
            if (Random.value > Mathf.Lerp(0.75f, 0.3f, d)) continue;
            float dir = s.x >= impactX ? 1f : -1f;
            int len = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(12f, 4f, d) * Random.Range(0.7f, 1.3f)));
            AddCrack(new Vector2(s.ix + Random.Range(0, 2), s.iy), dir, Random.Range(40f, 80f), len, s.trig, ref budget, false);
            if (budget <= 0) break;
        }
    }

    private void AddCrack(Vector2 start, float dir, float angleDown, int length, float trig, ref int budget, bool branch)
    {
        var crack = new Crack { trig = trig };
        Vector2 p = start;
        float a = angleDown;
        var last = new Vector2Int(int.MinValue, int.MinValue);
        for (int s = 0; s < length && budget > 0; s++)
        {
            a = Mathf.Clamp(a + Random.Range(-18f, 18f), 0f, 85f);
            p += new Vector2(dir * Mathf.Cos(a * Mathf.Deg2Rad), -Mathf.Sin(a * Mathf.Deg2Rad));
            var q = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            if (q == last) continue;
            budget--;
            if (!IsSolid(TexelCenterX(q.x), TexelCenterY(q.y))) break; // 땅 밖으로 나가면 멈춤
            crack.points.Add(q);
            last = q;
        }
        if (crack.points.Count == 0) return;
        crackList.Add(crack);
        if (branch && crack.points.Count > 8)
        {
            Vector2Int from = crack.points[crack.points.Count * 2 / 5];
            AddCrack(from, dir, angleDown + 25f, length / 3, trig + 0.04f, ref budget, false);
        }
    }

    // 솟구치는 바위: 표면을 따라 드문드문, 가운데일수록 크고 촘촘하게
    private void BuildSpikes()
    {
        spikeCount = 0;
        float lastX = float.NegativeInfinity;
        var ordered = new List<Surface>(surfaces);
        ordered.Sort((a, b) => a.x.CompareTo(b.x));
        foreach (Surface s in ordered)
        {
            if (spikeCount >= MaxSpikes) break;
            float dx = s.x - impactX;
            if (Mathf.Abs(dx) < swordWidth) continue; // 검이 꽂힌 자리는 비움
            float d = Mathf.Clamp01(Mathf.Abs(dx) / (W * 0.5f));
            if (Mathf.Abs(s.x - lastX) < Mathf.Lerp(3f, 7f, d) * P) continue;
            if (Random.value > Mathf.Lerp(0.85f, 0.35f, d)) continue;
            lastX = s.x;

            int h = Mathf.Max(4, Mathf.RoundToInt(Mathf.Lerp(18f, 6f, d) * Random.Range(0.8f, 1.2f)));
            int w = Mathf.Max(3, Mathf.RoundToInt(h * Random.Range(0.45f, 0.7f)));
            float dir = dx >= 0f ? 1f : -1f;

            Spike spike;
            if (spikeCount < spikes.Count) spike = spikes[spikeCount];
            else
            {
                SpriteRenderer r = null;
                spike = new Spike { r = Ensure(ref r, "Rock Spike " + spikeCount, root, SpikeOrder) };
                spikes.Add(spike);
            }
            spikeCount++;
            spike.w = w; spike.h = h; spike.dir = dir;
            DrawSpike(spike, Random.Range(0.2f, 0.6f) * dir);
            spike.position = Snap(s.x, s.y);
            spike.trig = s.trig + 0.02f;
            spike.crumble = spike.trig + Random.Range(0.35f, 0.7f);
            spike.risen = spike.crumbled = false;
        }
    }

    private void DrawSpike(Spike spike, float lean)
    {
        int w = spike.w + 2, h = spike.h + 3;
        const int embed = 2; // 땅 속에 묻히는 줄 수
        if (spike.canvas == null || !spike.canvas.Matches(w, h, new Vector2(0.5f, (float)embed / h), 1f / P))
        {
            spike.canvas?.Release();
            spike.canvas = new PixelCanvas(w, h, new Vector2(0.5f, (float)embed / h), 1f / P, "Ultimate Rock Spike");
        }
        PixelCanvas cv = spike.canvas;
        cv.Clear();
        Color32 outline = new Color32(52, 38, 30, 255), lit = new Color32(176, 140, 96, 255),
            mid = new Color32(134, 104, 76, 255), shade = new Color32(92, 70, 52, 255);
        float jitter = 0f;
        for (int y = 0; y < h; y++)
        {
            float p = Mathf.Clamp01((y - embed) / (float)(h - embed));
            if (y % 2 == 0) jitter = Random.Range(-0.6f, 0.6f);
            float half = (spike.w * 0.5f) * Mathf.Pow(1f - p, 0.9f) + (p < 0.95f ? jitter : 0f);
            float cx = w * 0.5f + lean * p * spike.w * 0.6f;
            int left = Mathf.RoundToInt(cx - half), right = Mathf.RoundToInt(cx + half) - 1;
            if (right < left) right = left;
            for (int x = left; x <= right; x++)
            {
                Color32 c;
                if (x == left || x == right || y == h - 1) c = outline;
                else
                {
                    // 충돌 지점을 향한 면이 밝음
                    float side = (x - cx) / Mathf.Max(0.5f, half) * -spike.dir;
                    c = side > 0.25f ? lit : side < -0.35f ? shade : mid;
                    if (Random.value < 0.08f) c = shade;
                }
                if (y < embed + 1) c = y < embed ? GoldDeep : Gold; // 밑동은 갈라진 틈의 빛
                cv.Set(x, y, c);
            }
        }
        cv.Apply();
        spike.r.sprite = cv.sprite;
    }

    private void FireSurfaces(float e)
    {
        while (nextSurface < surfaces.Count && surfaces[nextSurface].trig <= e)
        {
            Surface s = surfaces[nextSurface++];
            float d = Mathf.Clamp01(Mathf.Abs(s.x - impactX) / (W * 0.5f));
            float power = Mathf.Lerp(1f, 0.5f, d);
            float dir = s.x >= impactX ? 1f : -1f;
            Vector2 at = new Vector2(s.x, s.y + P);

            int chunks = Random.value < 0.6f ? 1 : 2;
            for (int i = 0; i < chunks; i++)
                SpawnBit(at, new Vector2(dir * Random.Range(4f, 25f), Random.Range(20f, 60f) * power) * P,
                    (Random.value < 0.25f ? 2f : 1f) * P, EarthColors[Random.Range(0, EarthColors.Length)],
                    Random.Range(0.5f, 0.85f), 200f * P, 0f);
            if (Random.value < 0.55f)
                SpawnBit(at, new Vector2(dir * Random.Range(5f, 18f), Random.Range(3f, 10f)) * P,
                    Random.Range(2, 4) * P, DustColor, Random.Range(0.6f, 0.95f), -4f * P, 2.5f);
            if (Random.value < 0.3f)
                SpawnBit(at, new Vector2(Random.Range(-10f, 10f), Random.Range(30f, 70f) * power) * P,
                    P, Random.value < 0.5f ? (Color)White : GoldLight, Random.Range(0.25f, 0.4f), 150f * P, 0.5f);
        }
    }

    private void UpdateSpikes(float e)
    {
        for (int i = 0; i < spikeCount; i++)
        {
            Spike s = spikes[i];
            if (s.crumbled || e < s.trig) continue;
            if (e >= s.crumble)
            {
                // 바위가 무너지며 파편으로
                s.crumbled = true;
                s.r.enabled = false;
                for (int j = 0; j < 8; j++)
                {
                    float hy = Random.Range(0f, s.h) * P;
                    Vector2 p = new Vector2(s.position.x + Random.Range(-0.4f, 0.4f) * s.w * P * (1f - hy / (s.h * P)), s.position.y + hy);
                    SpawnBit(p, new Vector2(Random.Range(-15f, 15f) + s.dir * 8f, Random.Range(5f, 30f)) * P,
                        (Random.value < 0.35f ? 2f : 1f) * P, EarthColors[Random.Range(0, EarthColors.Length)],
                        Random.Range(0.45f, 0.75f), 200f * P, 0f);
                }
                SpawnBit(s.position + Vector3.up * P, new Vector2(Random.Range(-8f, 8f), 6f) * P, 3f * P, DustColor, 0.8f, -4f * P, 2.5f);
                continue;
            }
            float k = Mathf.Clamp01((e - s.trig) / SpikeRiseTime);
            s.r.transform.position = s.position;
            s.r.transform.localScale = new Vector3(1f, k < 1f ? 1f - (1f - k) * (1f - k) : 1f, 1f); // 땅에서 솟구침
            s.r.color = Color.white;
            s.r.enabled = true;
        }
    }

    // 천장 아래(없으면 화면 위)에서 돌가루가 우수수 떨어짐
    private void SpawnFalling(float e, float sim)
    {
        if (e < FallingStart || e > FallingEnd || sim <= 0f) return;
        fallingAccum += sim * FallingRate * Mathf.Lerp(1.6f, 0.4f, (e - FallingStart) / (FallingEnd - FallingStart));
        while (fallingAccum >= 1f)
        {
            fallingAccum -= 1f;
            Vector2 p = ceilings.Count > 0 && Random.value < 0.7f
                ? ceilings[Random.Range(0, ceilings.Count)] + new Vector2(Random.Range(-1f, 1f) * P, -P * 0.5f)
                : new Vector2(screenLeft + Random.value * W, screenTop + P);
            if (Random.value < 0.8f)
                SpawnBit(p, new Vector2(Random.Range(-4f, 4f), -Random.Range(0f, 15f)) * P,
                    (Random.value < 0.25f ? 2f : 1f) * P, EarthColors[Random.Range(0, EarthColors.Length)],
                    Random.Range(1f, 1.4f), 180f * P, 0f);
            else
                SpawnBit(p, new Vector2(Random.Range(-3f, 3f), -2f) * P, 2f * P, WithAlpha(DustColor, 0.45f),
                    Random.Range(0.7f, 1f), 20f * P, 2f);
        }
    }

    // 화면 캔버스: 빛줄기 → 충격파 고리 → 균열 빛 → 표면 빛 → 균열
    private void DrawGroundFx(float e)
    {
        if (!impacted)
        {
            groundFx.enabled = false;
            return;
        }
        float end = Mathf.Max(CrackLife + 0.5f, Mathf.Max(RingTime, RaysTime));
        if (e > end)
        {
            if (screenCanvasDirty) { screenCanvas.Clear(); screenCanvas.Apply(); screenCanvasDirty = false; }
            groundFx.enabled = false;
            return;
        }

        PixelCanvas cv = screenCanvas;
        cv.Clear();
        int cx = impactIx, cy = impactIy + 1;

        // 하늘로 뻗는 빛줄기
        float kr = e / RaysTime;
        if (kr < 1f)
        {
            float maxR = Mathf.Sqrt(cv.w * cv.w + cv.h * cv.h);
            foreach (float a in rayAngles)
            {
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (float r = 4f; r < maxR; r += 1f)
                {
                    float alpha = (1f - kr) * (1f - r / maxR) * 210f;
                    if (alpha < 8f) break;
                    cv.Set(cx + Mathf.RoundToInt(ca * r), cy + Mathf.RoundToInt(sa * r), WithAlpha32(GoldLight, (byte)alpha));
                }
            }
        }

        // 화면 끝까지 퍼지는 충격파 고리
        float kg = e / RingTime;
        if (kg < 1f)
        {
            float maxR = Mathf.Sqrt(cv.w * cv.w + cv.h * cv.h) * 0.75f;
            float radius = Mathf.Lerp(2f, maxR, 1f - (1f - kg) * (1f - kg));
            byte alpha = (byte)(255f * (kg < 0.5f ? 1f : 1f - (kg - 0.5f) / 0.5f));
            Circle(cv, cx, cy, radius + 1f, WithAlpha32(Gold, (byte)(alpha * 0.6f)));
            Circle(cv, cx, cy, radius, WithAlpha32(White, alpha));
            Circle(cv, cx, cy, radius - 1f, WithAlpha32(GoldLight, (byte)(alpha * 0.8f)));
        }

        // 균열 주변의 빛 (갓 생긴 균열만)
        foreach (Crack c in crackList)
        {
            float age = e - c.trig;
            if (age < 0f || age > 0.5f) continue;
            byte ga = (byte)(170f * (1f - age / 0.5f));
            int n = Mathf.CeilToInt(c.points.Count * Mathf.Clamp01(age / CrackGrowTime));
            for (int i = 0; i < n; i++)
            {
                Vector2Int q = c.points[i];
                Color32 g = new Color32(240, 140, 40, ga);
                cv.Set(q.x + 1, q.y, g); cv.Set(q.x - 1, q.y, g);
                cv.Set(q.x, q.y + 1, g); cv.Set(q.x, q.y - 1, g);
            }
        }

        // 파동이 지나간 지면 표면이 하얗게 → 금빛으로 빛났다 식음
        foreach (Surface s in surfaces)
        {
            float k = (e - s.trig) / SurfaceGlowTime;
            if (k < 0f || k >= 1f) continue;
            Color32 c = k < 0.12f ? White : Color32.Lerp(GoldLight, GoldDeep, (k - 0.12f) / 0.88f);
            byte a = (byte)(255f * (1f - k));
            Color32 line = WithAlpha32(c, a), halo = WithAlpha32(c, (byte)(a * 0.45f));
            for (int dx = 0; dx < 2; dx++)
            {
                cv.Set(s.ix + dx, s.iy, line);
                cv.Set(s.ix + dx, s.iy + 1, halo);
                if (k < 0.3f) cv.Set(s.ix + dx, s.iy + 2, WithAlpha32(c, (byte)(a * 0.2f)));
            }
        }

        // 균열: 흰빛 → 금빛 → 식은 불씨색, 마지막에 사라짐
        foreach (Crack c in crackList)
        {
            float age = e - c.trig;
            if (age < 0f) continue;
            float life = age / CrackLife;
            if (life >= 1f) continue;
            Color32 core = life < 0.06f ? White : Color32.Lerp(GoldLight, Ember, Mathf.Clamp01((life - 0.06f) / 0.6f));
            core.a = (byte)(255f * (life < 0.7f ? 1f : 1f - (life - 0.7f) / 0.3f));
            int n = Mathf.CeilToInt(c.points.Count * Mathf.Clamp01(age / CrackGrowTime));
            for (int i = 0; i < n; i++) cv.Set(c.points[i].x, c.points[i].y, core);
        }

        cv.Apply();
        screenCanvasDirty = true;
        groundFx.transform.position = new Vector3(originX, originY, z);
        groundFx.color = Color.white;
        groundFx.enabled = true;
    }

    private static void Circle(PixelCanvas cv, int cx, int cy, float r, Color32 c)
    {
        if (r <= 0f) return;
        int steps = Mathf.Max(8, Mathf.CeilToInt(2f * Mathf.PI * r * 1.3f));
        for (int i = 0; i < steps; i++)
        {
            float a = i * 2f * Mathf.PI / steps;
            cv.Set(cx + Mathf.RoundToInt(Mathf.Cos(a) * r), cy + Mathf.RoundToInt(Mathf.Sin(a) * r), c);
        }
    }

    #endregion

    #region 충돌 · 파쇄

    private void DoImpact()
    {
        impacted = true;
        sinceImpact = 0f;

        // 히트스톱: 게임 전체를 잠깐 멈춤 (연출은 실제 시간으로 계속). timeScale 은 공용 HitFeedback 이 관리
        freeze = HitStopTime;
        hitStopActive = true;
        HitFeedback.HitStop(HitStopTime);

        List<Vector3> hits = null;
        try { hits = impactCallback?.Invoke(); }
        catch (Exception e) { Debug.LogException(e, this); }
        if (hits != null) foreach (Vector3 h in hits) SpawnSpark(h);

        // 검이 꽂힌 자리의 큰 폭발
        Vector2 g = new Vector2(impactX, groundY);
        if (hasGround)
        {
            for (int i = 0; i < 40; i++) // 흙·돌 파편
            {
                float dir = i % 2 == 0 ? 1f : -1f;
                SpawnBit(g + new Vector2(dir * Random.Range(0, 5) * P, P),
                    new Vector2(dir * Random.Range(15f, 80f), Random.Range(30f, 110f)) * P,
                    Random.Range(1, 4) * P, EarthColors[Random.Range(0, EarthColors.Length)],
                    Random.Range(0.55f, 0.9f), 220f * P, 0f);
            }
            for (int i = 0; i < 20; i++) // 흙먼지
            {
                float dir = i % 2 == 0 ? 1f : -1f;
                SpawnBit(g + new Vector2(dir * Random.Range(0, 8) * P, P),
                    new Vector2(dir * Random.Range(12f, 50f), Random.Range(2f, 16f)) * P,
                    Random.Range(2, 5) * P, DustColor, Random.Range(0.6f, 0.95f), -6f * P, 3f);
            }
        }
        for (int i = 0; i < 30; i++) // 빛 불꽃
        {
            SpawnBit(g + new Vector2(0f, 2f * P), new Vector2(Random.Range(-70f, 70f), Random.Range(30f, 130f)) * P,
                P, i % 2 == 0 ? (Color)White : GoldLight, Random.Range(0.3f, 0.55f), 160f * P, 0.5f);
        }
    }

    // 검이 빛 조각으로 부서짐
    private void Shatter()
    {
        shattered = true;
        sword.enabled = aura.enabled = false;
        float tipY = groundY - SinkRatio * swordLength;
        for (int i = 0; i < 56; i++)
        {
            float along = Random.Range(SinkRatio, 1f);
            Vector2 p = new Vector2(impactX + Random.Range(-0.5f, 0.5f) * swordWidth, tipY + along * swordLength);
            SpawnBit(p, new Vector2(Random.Range(-50f, 50f), Random.Range(-5f, 35f)) * P,
                (Random.value < 0.3f ? 2f : 1f) * P, ShardColors[Random.Range(0, ShardColors.Length)],
                Random.Range(0.4f, 0.7f), 90f * P, 1f);
        }
    }

    private void EndHitStop()
    {
        if (!hitStopActive) return;
        hitStopActive = false;
        freeze = 0f; // timeScale 복구는 HitFeedback 이 실제 시간으로 알아서 처리
    }

    private void Finish()
    {
        Unhook();
        EndHitStop();
        active = false;
        HideAll();
    }

    #endregion

    #region 파티클 · 스파크

    private void SpawnBit(Vector2 pos, Vector2 vel, float size, Color color, float life, float gravity, float drag)
    {
        int slot = nextBit;
        nextBit = (nextBit + 1) % BitPoolSize;
        Bit b = bits[slot];
        if (b == null)
        {
            SpriteRenderer r = null;
            bits[slot] = b = new Bit { r = Ensure(ref r, "Bit " + slot, root, ParticleOrder) };
        }
        b.r.sprite = pixel;
        b.pos = pos; b.vel = vel; b.color = color;
        b.life = life; b.gravity = gravity; b.drag = drag; b.age = 0f;
        b.r.transform.localScale = new Vector3(size, size, 1f);
        b.r.transform.position = Snap(pos.x, pos.y);
        b.r.color = color;
        b.r.enabled = true;
    }

    private void UpdateBits(float dt)
    {
        foreach (Bit b in bits)
        {
            if (b == null || !b.r.enabled) continue;
            b.age += dt;
            if (b.age >= b.life) { b.r.enabled = false; continue; }
            b.vel.y -= b.gravity * dt;
            b.vel /= 1f + b.drag * dt;
            b.pos += b.vel * dt;
            float k = b.age / b.life;
            b.r.color = WithAlpha(b.color, b.color.a * (k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f));
            b.r.transform.position = Snap(b.pos.x, b.pos.y);
        }
    }

    private void SpawnSpark(Vector3 position)
    {
        int slot = nextSpark;
        nextSpark = (nextSpark + 1) % SparkPoolSize;
        Spark s = sparks[slot];
        if (s == null)
        {
            SpriteRenderer r = null;
            sparks[slot] = s = new Spark { r = Ensure(ref r, "Spark " + slot, root, SparkOrder) };
        }
        s.age = 0f;
        s.r.sprite = patternSprites[2];
        s.r.transform.position = Snap(position.x, position.y);
        s.r.color = Color.white;
        s.r.enabled = true;
    }

    private void UpdateSparks(float dt)
    {
        foreach (Spark s in sparks)
        {
            if (s == null || !s.r.enabled) continue;
            s.age += dt;
            if (s.age >= 0.16f) { s.r.enabled = false; continue; }
            s.r.sprite = patternSprites[s.age < 0.07f ? 2 : 3];
        }
    }

    private bool AnyAlive()
    {
        foreach (Bit b in bits) if (b != null && b.r.enabled) return true;
        foreach (Spark s in sparks) if (s != null && s.r.enabled) return true;
        for (int i = 0; i < spikeCount; i++) if (!spikes[i].crumbled) return true;
        return false;
    }

    #endregion

    #region 화면 흔들림 (카메라 적용은 공용 HitFeedback 이 담당 — 기본 공격 흔들림과 겹쳐도 안전하게 합산)

    private void UpdateShake(float e)
    {
        shakeOffset = Vector3.zero;
        if (impacted)
        {
            float amp = 0f;
            if (sinceImpact < ShakeTime)
            {
                float k = 1f - sinceImpact / ShakeTime;
                amp += ShakeStrength * H * k * k;
            }
            if (e < RumbleTime) amp += RumbleStrength * H * (1f - Mathf.Max(0f, e) / RumbleTime);
            if (amp > 0f)
            {
                Vector2 r = Random.insideUnitCircle * amp;
                float step = P * 0.5f;
                shakeOffset = new Vector3(Mathf.Round(r.x / step) * step, Mathf.Round(r.y / step) * step, 0f);
            }
        }
        if (hooked) HitFeedback.SetShakeOffset(this, shakeOffset);
    }

    private void Hook()
    {
        hooked = true;
    }

    private void Unhook()
    {
        if (hooked) HitFeedback.ClearShakeOffset(this);
        hooked = false;
        shakeOffset = Vector3.zero;
    }

    #endregion

    #region 픽셀 그리기

    private void DrawMarker(int rx)
    {
        if (rx == drawnMarkerRx) return;
        drawnMarkerRx = rx;
        PixelCanvas cv = markerCanvas;
        cv.Clear();
        int cx = cv.w / 2, cy = cv.h / 2;
        int ry = Mathf.Max(1, Mathf.RoundToInt(rx * 0.25f));
        Ellipse(cv, cx, cy, rx + 1, ry + 1, WithAlpha32(Gold, 150));
        Ellipse(cv, cx, cy, rx, ry, White);
        if (rx > 4) Ellipse(cv, cx, cy, rx / 2, Mathf.Max(1, ry / 2), WithAlpha32(GoldLight, 170));
        cv.Apply();
    }

    private static void Ellipse(PixelCanvas cv, int cx, int cy, int rx, int ry, Color32 c)
    {
        if (rx <= 0) { cv.Set(cx, cy, c); return; }
        int steps = Mathf.Max(8, Mathf.CeilToInt(Mathf.PI * Mathf.Max(rx, ry) * 3f));
        for (int i = 0; i < steps; i++)
        {
            float a = i * 2f * Mathf.PI / steps;
            cv.Set(cx + Mathf.RoundToInt(Mathf.Cos(a) * rx), cy + Mathf.RoundToInt(Mathf.Sin(a) * ry), c);
        }
    }

    // 검 뒤의 금빛 후광: 검 실루엣보다 1~2칸 넓은 기둥, 칼끝은 가늘게 (칼끝이 아래, 피벗 = 칼끝)
    private void BuildAura(int texLong, int texShort)
    {
        const int pad = 3;
        int w = texShort + pad * 2, h = texLong + pad * 2;
        EnsureCanvas(ref auraCanvas, w, h, new Vector2(0.5f, (float)pad / h), "Ultimate Aura");
        PixelCanvas cv = auraCanvas;
        cv.Clear();
        float half = texShort * 0.5f;
        Color32 inner = new Color32(255, 236, 160, 150), outer = WithAlpha32(Gold, 110), faint = new Color32(255, 170, 50, 70);
        for (int y = 0; y < h; y++)
        {
            float s = y - pad + 0.5f; // 칼끝에서부터의 거리(텍셀)
            if (s < -1.5f || s > texLong + 1.5f) continue;
            float along = s / texLong;
            float hw;
            if (s < 7f) hw = Mathf.Lerp(1f, half + 1.5f, (s + 1.5f) / 8.5f);  // 칼끝
            else if (along < 0.66f) hw = half + 1.5f;                          // 칼날
            else if (along < 0.82f) hw = half + 2.5f;                          // 가드
            else hw = half * 0.45f + 1.5f;                                     // 손잡이
            for (int x = 0; x < w; x++)
            {
                float d = Mathf.Abs(x + 0.5f - w * 0.5f);
                if (d <= hw - 1f) cv.Set(x, y, inner);
                else if (d <= hw) cv.Set(x, y, outer);
                else if (d <= hw + 0.8f && (x + y) % 2 == 0) cv.Set(x, y, faint);
            }
        }
        cv.Apply();
        aura.sprite = cv.sprite;
        aura.transform.localPosition = Vector3.zero;
        aura.transform.localRotation = Quaternion.identity;
        aura.transform.localScale = Vector3.one;
    }

    private void PreparePatterns()
    {
        float ppu = 1f / P;
        if (patternSprites != null && Mathf.Approximately(patternPpu, ppu)) return;
        ReleasePatterns();
        patternPpu = ppu;
        patternSprites = new Sprite[Patterns.Length];
        for (int i = 0; i < Patterns.Length; i++)
        {
            string[] rows = Patterns[i];
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Ultimate Pattern " + i
            };
            var pixels = new Color32[w * h];
            for (int row = 0; row < h; row++)
                for (int x = 0; x < w; x++)
                {
                    char ch = rows[row][x];
                    pixels[(h - 1 - row) * w + x] = ch == '@' ? White : ch == 'o' ? GoldLight : ch == '#' ? Gold : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(pixels);
            tex.Apply(false);
            patternSprites[i] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            patternSprites[i].name = tex.name;
        }
    }

    #endregion

    #region 오브젝트 준비

    private void EnsureObjects()
    {
        if (root == null)
        {
            // 플레이어는 씬을 넘어 유지되므로 그 아래에 두고, 매 프레임 월드 원점·크기 1로 되돌려 움직임을 따라가지 않게 함
            root = new GameObject("Ultimate Effects (pooled)").transform;
            root.SetParent(transform, false);
        }
        root.gameObject.layer = objectLayer;
        KeepRootInWorld();

        if (pixel == null)
        {
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point, name = "Ultimate Pixel" };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        Ensure(ref dark, "Dark", root, DarkOrder);
        Ensure(ref beamOuter, "Beam Outer", root, BeamOrder);
        Ensure(ref beamMid, "Beam Mid", root, BeamOrder + 1);
        Ensure(ref beamCore, "Beam Core", root, BeamOrder + 2);
        Ensure(ref marker, "Marker", root, MarkerOrder);
        Ensure(ref glint, "Sky Glint", root, GlintOrder);
        Ensure(ref groundFx, "Ground FX", root, GroundFxOrder);
        if (swordRoot == null)
        {
            swordRoot = new GameObject("Giant Sword").transform;
            swordRoot.SetParent(root, false);
        }
        Ensure(ref aura, "Aura", swordRoot, AuraOrder);
        Ensure(ref sword, "Sword", swordRoot, SwordOrder);
        for (int i = 0; i < ghosts.Length; i++)
        {
            if (ghostRoots[i] == null)
            {
                ghostRoots[i] = new GameObject("Sword Ghost " + i).transform;
                ghostRoots[i].SetParent(root, false);
            }
            Ensure(ref ghosts[i], "Sword", ghostRoots[i], GhostOrder);
        }
        Ensure(ref columnOuter, "Column Outer", root, ColumnOrder);
        Ensure(ref column, "Column", root, ColumnOrder + 1);
        Ensure(ref flash, "Flash", root, FlashOrder);
        foreach (Bit b in bits) if (b != null) Configure(b.r, ParticleOrder);
        foreach (Spark s in sparks) if (s != null) Configure(s.r, SparkOrder);
        foreach (Spike s in spikes) Configure(s.r, SpikeOrder);
    }

    private SpriteRenderer Ensure(ref SpriteRenderer r, string name, Transform parent, int order)
    {
        if (r == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            r = go.AddComponent<SpriteRenderer>();
            allRenderers.Add(r);
        }
        Configure(r, order);
        return r;
    }

    private void Configure(SpriteRenderer r, int order)
    {
        r.gameObject.layer = objectLayer;
        r.sharedMaterial = material;
        r.sortingLayerID = layerId;
        r.sortingOrder = order;
        r.enabled = false;
    }

    // 플레이어 검 스프라이트를 칼끝이 아래를 향하게, 길이 swordLength로. 부모 원점 = 칼끝
    private void ConfigureSword(SpriteRenderer r, SpriteRenderer weapon)
    {
        if (weapon == null) { r.sprite = null; return; }
        Sprite sprite = weapon.sprite;
        r.sprite = sprite;
        r.flipX = r.flipY = false;
        if (weapon.sharedMaterial != null) r.sharedMaterial = weapon.sharedMaterial;
        Bounds b = sprite.bounds;
        bool vertical = b.size.y > b.size.x; // 예전 검 스프라이트는 위를, mousword_sword는 오른쪽을 향함
        Quaternion rot = Quaternion.Euler(0f, 0f, vertical ? 180f : -90f);
        float s = swordLength / Mathf.Max(0.0001f, vertical ? b.size.y : b.size.x);
        Vector3 tip = vertical ? new Vector3(b.center.x, b.max.y, 0f) : new Vector3(b.max.x, b.center.y, 0f);
        r.transform.localRotation = rot;
        r.transform.localScale = new Vector3(s, s, 1f);
        r.transform.localPosition = -(rot * (tip * s));
    }

    private void EnsureCanvas(ref PixelCanvas canvas, int w, int h, Vector2 pivot, string name)
    {
        float ppu = 1f / P;
        if (canvas != null && canvas.Matches(w, h, pivot, ppu)) return;
        canvas?.Release();
        canvas = new PixelCanvas(w, h, pivot, ppu, name);
    }

    private void KeepRootInWorld()
    {
        if (root == null) return;
        root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Vector3 ps = transform.lossyScale;
        root.localScale = new Vector3(1f / NonZero(ps.x), 1f / NonZero(ps.y), 1f);
    }

    private void HideAll()
    {
        foreach (SpriteRenderer r in allRenderers) if (r != null) r.enabled = false;
    }

    #endregion

    #region 유틸

    private bool FindGround(float x, float centerY, out float y)
    {
        // 화면 중앙에서 아래로 먼저, 중앙이 벽 속이면 화면 위에서 아래로
        foreach (float startY in new[] { centerY, screenTop })
        {
            int n = Physics2D.Raycast(new Vector2(x, startY), Vector2.down, solidFilter, rayHits, startY - screenBottom);
            if (n > 0 && rayHits[0].distance > 0f) { y = rayHits[0].point.y; return true; }
        }
        y = 0f;
        return false;
    }

    private bool IsSolid(float x, float y) => Physics2D.OverlapPoint(new Vector2(x, y), solidFilter, overlapHits) > 0;

    // 땅 속에서 아래로 내려가며 처음 빈 곳의 높이
    private float SkipSolid(float x, float y, float bottom)
    {
        while (y > bottom && IsSolid(x, y)) y -= P;
        return y;
    }

    private int TexelX(float x) => Mathf.FloorToInt((x - originX) / P);
    private int TexelY(float y) => Mathf.RoundToInt((y - originY) / P);
    private float TexelCenterX(int ix) => originX + (ix + 0.5f) * P;
    private float TexelCenterY(int iy) => originY + (iy + 0.5f) * P;

    private static SpriteRenderer FindSword(Transform weaponRoot)
    {
        if (weaponRoot == null) return null;
        SpriteRenderer best = null;
        float longest = 0f;
        foreach (SpriteRenderer r in weaponRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (r.sprite == null) continue;
            Vector3 size = r.sprite.bounds.size;
            float length = Mathf.Max(size.x, size.y);
            if (length > longest) { longest = length; best = r; }
        }
        return best;
    }

    private void SetQuad(SpriteRenderer r, Vector2 center, float width, float height, Color color)
    {
        r.sprite = pixel;
        r.transform.position = new Vector3(center.x, center.y, z);
        r.transform.localScale = new Vector3(width, height, 1f);
        r.color = color;
        r.enabled = color.a > 0.001f && width > 0f && height > 0f;
    }

    // 픽셀 격자(낙하 지점 기준)에 맞춰 위치를 반올림해 픽셀이 흔들리지 않게 함
    private Vector3 Snap(float x, float y) =>
        new Vector3(impactX + Mathf.Round((x - impactX) / P) * P, groundY + Mathf.Round((y - groundY) / P) * P, z);

    private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    private static Color32 WithAlpha32(Color32 c, byte a) => new Color32(c.r, c.g, c.b, a);
    private static float NonZero(float v) => Mathf.Abs(v) < 0.0001f ? 1f : v;

    private void ReleasePatterns()
    {
        if (patternSprites == null) return;
        foreach (Sprite s in patternSprites)
        {
            if (s == null) continue;
            ReleaseAsset(s.texture);
            ReleaseAsset(s);
        }
        patternSprites = null;
    }

    private static void ReleaseAsset(Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset);
        else DestroyImmediate(asset);
    }

    #endregion

    private void OnDisable() => Cancel();

    private void OnDestroy()
    {
        Cancel();
        ReleasePatterns();
        markerCanvas?.Release();
        auraCanvas?.Release();
        screenCanvas?.Release();
        foreach (Spike s in spikes) s.canvas?.Release();
        if (pixel != null)
        {
            ReleaseAsset(pixel.texture);
            ReleaseAsset(pixel);
        }
    }
}
