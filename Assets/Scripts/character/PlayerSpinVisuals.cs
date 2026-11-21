using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 회전베기 연출. 실제 무기(WeaponAim)는 그대로 보이며 회전하고, 그 위에
/// 몸 포즈(반 바퀴마다 좌우 반전 + 찌그러짐), 검 뒤를 따라오는 원호 스미어, 검 잔상,
/// 준비 동작의 검 끝 반짝임, 적중 스파크를 그린다.
/// 판정은 PlayerSkillController가 하고, 여기서는 무기의 실제 각도를 읽어 그리기만 한다.
/// 스미어 반경은 판정 반경과 같아서 보이는 원 = 실제 판정 범위가 된다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSpinVisuals : MonoBehaviour
{
    public const float WindupTime = 0.08f;   // ① 준비 구간 길이(초)
    public const float WindupAngle = 30f;    // 준비 때 검을 진행 반대 방향으로 당기는 각도
    public const float RecoverTime = 0.1f;   // ③ 마무리 구간 길이(초)

    private const int SmearDirections = 16;  // 픽셀이 깨지지 않도록 22.5° 간격으로 미리 구운 스미어 방향 수
    private const float TrailTime = 0.06f;   // 스미어가 남는 시간. 빠르게 돌수록 꼬리가 길어짐
    private const float MinTrailAngle = 120f, MaxTrailAngle = 240f; // 스미어 길이(도) 범위
    private const float SquashTime = 0.06f;  // 반 바퀴마다 몸이 찌그러지는 최대 시간
    private const int MaxSmearRadius = 160;  // 스미어 텍스처 반경 상한(픽셀)
    private const int SparkPoolSize = 8;
    private const float SparkLifetime = 0.1f;
    private const float HistoryLength = 0.15f;

    private static readonly float[] GhostDelays = { 0.02f, 0.04f, 0.06f };
    private static readonly float[] GhostAlphas = { 0.5f, 0.32f, 0.18f };
    private static readonly Color GhostTint = new Color(0.55f, 0.8f, 1f, 1f);

    private static readonly Vector2 WindupScale = new Vector2(1.08f, 0.9f);  // 몸을 낮추는 준비 자세
    private static readonly Vector2 SpinScale = new Vector2(1.04f, 0.95f);   // 회전 중 기본 자세
    private static readonly Vector2 SquashScale = new Vector2(0.78f, 1.06f); // 반전 순간 가로로 찌그러짐

    // 검기 스미어와 같은 파란 계열 4단계 (검에 가까울수록 밝음)
    private static readonly Color32[] SmearPalette =
    {
        new Color32(235, 252, 255, 255),
        new Color32(160, 225, 255, 235),
        new Color32(95, 165, 255, 200),
        new Color32(70, 105, 235, 150),
    };

    // '@' 흰색, 'o' 하늘색, '#' 파란색. 위에서 아래 순서.
    private static readonly string[][] SparkPatterns =
    {
        new[] // 0: 준비 동작 검 끝 반짝임
        {
            "....#....",
            "....#....",
            "....o....",
            "...ooo...",
            "##oo@oo##",
            "...ooo...",
            "....o....",
            "....#....",
            "....#....",
        },
        new[] // 1: 적중 스파크 (터짐)
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
        new[] // 2: 적중 스파크 (흩어짐)
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

    private enum Phase { None, Windup, Spin, Recover }

    private struct Sample
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    private Phase phase = Phase.None;
    private float phaseTime;
    private SpriteRenderer source, pose, smear, glint, weaponSword;
    private readonly SpriteRenderer[] ghosts = new SpriteRenderer[3];
    private readonly SpriteRenderer[] sparks = new SpriteRenderer[SparkPoolSize];
    private readonly float[] sparkAge = new float[SparkPoolSize];
    private Transform weapon, effectsRoot;
    private readonly List<Sample> history = new List<Sample>();
    private bool previousForceOff;
    private float facing, radius, lastSwordAngle, traveled, squashTimer;
    private float trailAngle = MinTrailAngle, squashDuration = SquashTime;
    private int halfTurns;
    private Vector2 currentScale = Vector2.one, recoverFrom = Vector2.one;

    private Sprite[] smearFrames, sparkFrames;
    private int smearRadiusPx;
    private float smearPpu, smearDirection, smearTrail;

    public bool IsPlaying => phase != Phase.None;

    /// <summary>
    /// 준비 동작 시작. facing: 바라보는 방향(±1), spinDirection: 회전 방향(+ 반시계, - 시계),
    /// degreesPerSecond: 회전 구간의 각속도 (스미어 길이와 몸 찌그러짐 시간을 맞추는 데 사용).
    /// </summary>
    public void Begin(SpriteRenderer body, Transform weaponRoot, float spinRadius, float facingSign, float spinDirection,
        float degreesPerSecond)
    {
        Cancel();
        if (body == null || body.sprite == null || weaponRoot == null || spinRadius <= 0f) return;

        float speed = Mathf.Max(degreesPerSecond, 1f);
        trailAngle = Mathf.Clamp(speed * TrailTime, MinTrailAngle, MaxTrailAngle);
        // 반 바퀴 도는 시간의 절반까지만 찌그러짐 (아주 빠를 때 계속 찌그러진 채로 있지 않도록)
        squashDuration = Mathf.Min(SquashTime, 180f / speed * 0.5f);

        // 대시 착지 포즈가 몸을 잠깐 숨기고 있을 수 있으므로 먼저 끝냄 (검기 연출과 같은 이유)
        var dash = GetComponent<PlayerDashVisuals>();
        if (dash != null) dash.Cancel();

        source = body;
        weapon = weaponRoot;
        radius = spinRadius;
        facing = facingSign < 0f ? -1f : 1f;
        weaponSword = FindSword(weaponRoot);

        // 캐릭터 시트 1픽셀의 월드 크기 (32px 셀 기준)
        float pixel = body.sprite.bounds.size.y * Mathf.Abs(body.transform.lossyScale.y) / 32f;
        PrepareSprites(spinRadius, pixel, spinDirection);
        EnsureRenderers(body);

        int layer = weaponSword != null ? weaponSword.sortingLayerID : body.sortingLayerID;
        int order = weaponSword != null ? weaponSword.sortingOrder : body.sortingOrder + 2;
        Material swordMaterial = weaponSword != null ? weaponSword.sharedMaterial : body.sharedMaterial;
        Configure(pose, body, body.sharedMaterial, body.sortingLayerID, body.sortingOrder);
        Configure(smear, body, body.sharedMaterial, layer, order - 2);
        foreach (var g in ghosts) Configure(g, body, swordMaterial, layer, order - 1);
        Configure(glint, body, body.sharedMaterial, layer, order + 1);
        foreach (var s in sparks) Configure(s, body, body.sharedMaterial, layer, order + 2);

        previousForceOff = body.forceRenderingOff;
        body.forceRenderingOff = true;
        effectsRoot.gameObject.SetActive(true);

        history.Clear();
        lastSwordAngle = weapon.eulerAngles.z;
        traveled = 0f;
        halfTurns = 0;
        squashTimer = 0f;
        currentScale = Vector2.one;
        phase = Phase.Windup;
        phaseTime = 0f;
        Tick(0f);
    }

    /// <summary>② 회전 구간 시작.</summary>
    public void BeginSpin()
    {
        if (phase == Phase.None) return;
        phase = Phase.Spin;
        phaseTime = 0f;
        traveled = 0f;
        halfTurns = 0;
    }

    /// <summary>③ 마무리 구간 시작. RecoverTime 뒤 스스로 정리된다.</summary>
    public void EndSpin()
    {
        if (phase == Phase.None) return;
        recoverFrom = currentScale;
        phase = Phase.Recover;
        phaseTime = 0f;
    }

    /// <summary>적중 위치에 작은 픽셀 스파크를 띄운다.</summary>
    public void Spark(Vector3 position)
    {
        if (phase == Phase.None || sparkFrames == null || effectsRoot == null) return;
        int slot = 0;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (!sparks[i].enabled) { slot = i; break; }
            if (sparkAge[i] > sparkAge[slot]) slot = i;
        }
        var s = sparks[slot];
        s.transform.position = position;
        s.sprite = sparkFrames[1];
        s.color = source != null ? source.color : Color.white;
        s.enabled = true;
        sparkAge[slot] = 0f;
    }

    public void Cancel()
    {
        if (phase != Phase.None && source != null) source.forceRenderingOff = previousForceOff;
        phase = Phase.None;
        history.Clear();
        if (pose != null) pose.enabled = false;
        if (effectsRoot != null)
        {
            foreach (var r in effectsRoot.GetComponentsInChildren<SpriteRenderer>(true)) r.enabled = false;
            effectsRoot.gameObject.SetActive(false);
        }
    }

    private void LateUpdate() => Tick(Time.deltaTime);

    private void Tick(float delta)
    {
        UpdateSparks(delta);
        if (phase == Phase.None) return;
        if (source == null || weapon == null) { Cancel(); return; }
        phaseTime += delta;

        // 무기의 실제(렌더링되는) 각도를 따라감
        float swordAngle = weapon.eulerAngles.z;
        float step = Mathf.Abs(Mathf.DeltaAngle(lastSwordAngle, swordAngle));
        lastSwordAngle = swordAngle;
        if (phase == Phase.Spin)
        {
            traveled += step;
            int turns = Mathf.FloorToInt(traveled / 180f);
            if (turns != halfTurns)
            {
                halfTurns = turns;
                squashTimer = squashDuration;
            }
        }
        squashTimer = Mathf.Max(0f, squashTimer - delta);
        RecordSample();

        bool visible = source.enabled && !previousForceOff;
        UpdatePose(visible);
        UpdateSmear(visible, swordAngle);
        UpdateGhosts(visible);
        UpdateGlint(visible, swordAngle);

        if (phase == Phase.Recover && phaseTime >= RecoverTime) Cancel();
    }

    private void UpdatePose(bool visible)
    {
        Vector2 scale;
        bool flipped = (halfTurns & 1) == 1;
        switch (phase)
        {
            case Phase.Windup:
                scale = Vector2.Lerp(Vector2.one, WindupScale, EaseOut(phaseTime / WindupTime));
                flipped = false;
                break;
            case Phase.Spin:
                scale = Vector2.Lerp(SpinScale, SquashScale, squashTimer / squashDuration);
                break;
            default: // Recover: 원래 방향의 기본 자세로 복귀
                scale = Vector2.Lerp(recoverFrom, Vector2.one, EaseOut(phaseTime / RecoverTime));
                flipped = false;
                break;
        }
        currentScale = scale;

        pose.sprite = source.sprite; // 이동 중에도 걷기 애니메이션 프레임을 그대로 사용
        pose.color = source.color;
        pose.flipY = source.flipY;
        pose.flipX = (facing < 0f) != flipped;
        pose.enabled = visible && pose.sprite != null;
        if (pose.sprite == null) return;

        // 발바닥(스프라이트 아래쪽)을 기준으로 늘리고 줄임
        Bounds b = pose.sprite.bounds;
        pose.transform.localPosition = new Vector3(b.center.x * (1f - scale.x), b.min.y * (1f - scale.y), 0f);
        pose.transform.localScale = new Vector3(scale.x, scale.y, 1f);
    }

    private void UpdateSmear(bool visible, float swordAngle)
    {
        bool show = visible && smearFrames != null && (phase == Phase.Spin || phase == Phase.Recover);
        smear.enabled = show;
        if (!show) return;

        // 회전 중에는 검 각도에 가장 가까운 방향 프레임, 마무리 때는 마지막 프레임을 유지
        if (phase == Phase.Spin)
        {
            int index = Mathf.RoundToInt(Mathf.Repeat(swordAngle, 360f) / (360f / SmearDirections)) % SmearDirections;
            smear.sprite = smearFrames[index];
        }

        // 막 돌기 시작했을 때는 지나온 만큼만 보이도록 서서히 나타남
        float alpha = Mathf.Clamp01(traveled / trailAngle);
        float scale = 1f;
        if (phase == Phase.Recover)
        {
            float t = Mathf.Clamp01(phaseTime / RecoverTime);
            alpha *= 1f - t;
            scale = 1f + 0.15f * t; // 원형으로 퍼지며 사라짐
        }

        smear.transform.SetPositionAndRotation(weapon.position, Quaternion.identity);
        smear.transform.localScale = Vector3.one * scale;
        Color c = source.color;
        c.a *= alpha;
        smear.color = c;
    }

    private void UpdateGhosts(bool visible)
    {
        bool active = visible && weaponSword != null && weaponSword.sprite != null &&
                      (phase == Phase.Spin || phase == Phase.Recover);
        float fade = phase == Phase.Recover ? 1f - Mathf.Clamp01(phaseTime / RecoverTime) : 1f;

        for (int i = 0; i < ghosts.Length; i++)
        {
            var g = ghosts[i];
            if (!active || !TryGetSample(Time.time - GhostDelays[i], out Sample s))
            {
                g.enabled = false;
                continue;
            }
            g.sprite = weaponSword.sprite;
            g.flipX = weaponSword.flipX;
            g.flipY = weaponSword.flipY;
            g.transform.SetPositionAndRotation(s.position, s.rotation);
            g.transform.localScale = s.scale;
            Color c = GhostTint;
            c.a = GhostAlphas[i] * fade;
            g.color = c;
            g.enabled = true;
        }
    }

    private void UpdateGlint(bool visible, float swordAngle)
    {
        bool show = visible && phase == Phase.Windup && sparkFrames != null;
        glint.enabled = show;
        if (!show) return;

        float rad = swordAngle * Mathf.Deg2Rad;
        glint.sprite = sparkFrames[0];
        glint.transform.position = weapon.position + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
        glint.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.1f, Mathf.Clamp01(phaseTime / WindupTime));
        glint.color = source.color;
    }

    private void UpdateSparks(float delta)
    {
        if (effectsRoot == null || sparkFrames == null) return;
        for (int i = 0; i < sparks.Length; i++)
        {
            var s = sparks[i];
            if (s == null || !s.enabled) continue;
            sparkAge[i] += delta;
            if (sparkAge[i] >= SparkLifetime) s.enabled = false;
            else s.sprite = sparkFrames[sparkAge[i] < SparkLifetime * 0.5f ? 1 : 2];
        }
    }

    private void RecordSample()
    {
        if (weaponSword == null) return;
        Transform t = weaponSword.transform;
        history.Add(new Sample { time = Time.time, position = t.position, rotation = t.rotation, scale = t.lossyScale });
        while (history.Count > 0 && history[0].time < Time.time - HistoryLength) history.RemoveAt(0);
    }

    private bool TryGetSample(float time, out Sample sample)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].time <= time) { sample = history[i]; return true; }
        }
        sample = default;
        return false;
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }

    private static SpriteRenderer FindSword(Transform weaponRoot)
    {
        SpriteRenderer best = null;
        float longest = 0f;
        foreach (var r in weaponRoot.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (r.sprite == null) continue;
            Vector3 size = r.sprite.bounds.size, scale = r.transform.lossyScale;
            float length = Mathf.Max(size.x * Mathf.Abs(scale.x), size.y * Mathf.Abs(scale.y));
            if (length > longest) { longest = length; best = r; }
        }
        return best;
    }

    private void EnsureRenderers(SpriteRenderer body)
    {
        if (pose == null) pose = CreateRenderer("Spin Pose", body.transform);
        else if (pose.transform.parent != body.transform) pose.transform.SetParent(body.transform, false);

        if (effectsRoot != null) return;
        // 이펙트는 몸의 반전·스케일 영향을 받지 않도록 월드 루트에 둔다
        effectsRoot = new GameObject("Spin Effects").transform;
        effectsRoot.gameObject.layer = body.gameObject.layer;
        smear = CreateRenderer("Smear", effectsRoot);
        glint = CreateRenderer("Glint", effectsRoot);
        for (int i = 0; i < ghosts.Length; i++) ghosts[i] = CreateRenderer("Ghost " + i, effectsRoot);
        for (int i = 0; i < sparks.Length; i++) sparks[i] = CreateRenderer("Spark " + i, effectsRoot);
    }

    private static SpriteRenderer CreateRenderer(string name, Transform parent)
    {
        var child = new GameObject(name);
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.enabled = false;
        return renderer;
    }

    private static void Configure(SpriteRenderer r, SpriteRenderer body, Material material, int layer, int order)
    {
        r.sharedMaterial = material;
        r.sortingLayerID = layer;
        r.sortingOrder = order;
        r.maskInteraction = body.maskInteraction;
        r.enabled = false;
    }

    // ---------- 픽셀 스프라이트 생성 ----------

    private void PrepareSprites(float spinRadius, float pixel, float spinDirection)
    {
        // 1 텍스처 픽셀 = 캐릭터 시트 1픽셀 크기. 반경이 너무 크면 상한에 맞춰 픽셀을 키움
        int radiusPx = Mathf.Clamp(Mathf.RoundToInt(spinRadius / Mathf.Max(pixel, 0.0001f)), 8, MaxSmearRadius);
        float ppu = radiusPx / spinRadius;
        float direction = spinDirection < 0f ? -1f : 1f;
        if (smearFrames != null && radiusPx == smearRadiusPx && Mathf.Approximately(ppu, smearPpu) &&
            direction == smearDirection && Mathf.Approximately(trailAngle, smearTrail))
            return;

        ReleaseSprites();
        smearRadiusPx = radiusPx;
        smearPpu = ppu;
        smearDirection = direction;
        smearTrail = trailAngle;

        smearFrames = new Sprite[SmearDirections];
        for (int i = 0; i < SmearDirections; i++)
            smearFrames[i] = BuildSmear(radiusPx, i * 360f / SmearDirections, direction, trailAngle, ppu, i);

        sparkFrames = new Sprite[SparkPatterns.Length];
        for (int i = 0; i < SparkPatterns.Length; i++)
            sparkFrames[i] = BuildPattern(SparkPatterns[i], ppu, "SpinSpark_" + i);
    }

    // 검(bladeAngle) 뒤로 trail만큼 따라오는 초승달 모양 원호. 꼬리로 갈수록 가늘고 어두워짐
    private static Sprite BuildSmear(int r, float bladeAngle, float direction, float trail, float ppu, int index)
    {
        int size = r * 2 + 2;
        var pixels = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > r) continue;

                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float behind = Mathf.Repeat((bladeAngle - angle) * direction, 360f); // 검에서 뒤로 몇 도
                if (behind > trail) continue;

                float t = behind / trail;
                float inner = r * (0.45f + 0.45f * t);
                float outer = r * (1f - 0.08f * t);
                if (dist < inner || dist > outer) continue;
                if (t > 0.8f && ((x + y) & 1) == 0) continue; // 꼬리 끝은 체크무늬 디더

                int band = t < 0.12f ? 0 : t < 0.35f ? 1 : t < 0.65f ? 2 : 3;
                if (band > 0 && dist > outer - 1.5f) band--; // 바깥 테두리는 한 단계 밝게
                pixels[y * size + x] = SmearPalette[band];
            }
        }
        return CreateSprite(pixels, size, size, ppu, "SpinSmear_" + index);
    }

    private static Sprite BuildPattern(string[] rows, float ppu, string name)
    {
        int h = rows.Length, w = rows[0].Length;
        var pixels = new Color32[w * h];
        for (int row = 0; row < h; row++)
        {
            for (int x = 0; x < w; x++)
            {
                Color32 color;
                switch (rows[row][x])
                {
                    case '@': color = new Color32(255, 255, 255, 255); break;
                    case 'o': color = new Color32(170, 230, 255, 255); break;
                    case '#': color = new Color32(90, 160, 255, 255); break;
                    default: continue;
                }
                pixels[(h - 1 - row) * w + x] = color;
            }
        }
        return CreateSprite(pixels, w, h, ppu, name);
    }

    private static Sprite CreateSprite(Color32[] pixels, int w, int h, float ppu, string name)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name,
        };
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        sprite.name = name;
        return sprite;
    }

    private void ReleaseSprites()
    {
        foreach (var set in new[] { smearFrames, sparkFrames })
        {
            if (set == null) continue;
            foreach (var s in set)
            {
                if (s == null) continue;
                Release(s.texture);
                Release(s);
            }
        }
        smearFrames = sparkFrames = null;
    }

    private void OnDisable() => Cancel();

    private void OnDestroy()
    {
        Cancel();
        if (effectsRoot != null) Release(effectsRoot.gameObject);
        ReleaseSprites();
    }

    private static void Release(Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset);
        else DestroyImmediate(asset);
    }
}
