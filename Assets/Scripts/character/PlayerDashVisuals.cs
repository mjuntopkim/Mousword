using UnityEngine;

/// <summary>Dash-only sprite playback and a bounded pool of short-lived pixel effects.</summary>
[DisallowMultipleComponent]
public sealed class PlayerDashVisuals : MonoBehaviour
{
    private sealed class Particle
    {
        public SpriteRenderer renderer;
        public Vector3 velocity;
        public Vector3 scale;
        public Color color;
        public float age, lifetime;
        public bool streak;
    }

    private readonly Particle[] particles = new Particle[64];
    private SpriteRenderer source, pose;
    private Sprite[] frames;
    private Sprite pixel;
    private GameObject effectsRoot;
    private bool active, recovering, previousForceOff;
    private float elapsed, duration, emissionTime, ghostTime, direction, unit, poseSize, footExtent;
    private int nextParticle, emissionIndex;
    private Vector3 previousPosition;
    private const float RecoveryDuration = 0.07f;

    // 액션감 보강
    private const float GhostInterval = 0.02f, WindInterval = 0.04f;  // 잔상 / 바람선 간격
    private const float GhostAlpha = 0.6f, GhostLifetime = 0.16f;
    private const float StretchX = 1.25f, StretchY = 0.85f;           // 출발 순간 몸 비율
    private const float StretchRecover = 0.45f;                       // 대시 길이 중 원래 비율로 돌아오는 구간
    private const int BurstRingCount = 12, StartDustCount = 8, BrakeDustCount = 6;

    public void Begin(SpriteRenderer body, float facing, float dashDuration, bool grounded)
    {
        Cancel();
        if (body == null || body.sprite == null) return;
        // Same rule as the sword wave cast: never capture another effect's temporary hide as the body's real state.
        var swordWave = GetComponent<PlayerSwordWaveVisuals>();
        if (swordWave != null) swordWave.Cancel();
        source = body;
        if (!PrepareSprites(body.sprite.pixelsPerUnit)) return;

        if (pose == null)
        {
            var child = new GameObject("Dash Pose");
            child.layer = body.gameObject.layer;
            child.transform.SetParent(body.transform, false);
            pose = child.AddComponent<SpriteRenderer>();
        }

        direction = facing < 0f ? -1f : 1f;
        // The reference sheet has 32-pixel cells. Keep its apparent size and foot line.
        poseSize = body.sprite.bounds.size.y / (32f / body.sprite.pixelsPerUnit);
        footExtent = frames[0].bounds.extents.y * poseSize;
        SetStretch(StretchX, StretchY);
        pose.sharedMaterial = body.sharedMaterial;
        pose.sortingLayerID = body.sortingLayerID;
        pose.sortingOrder = body.sortingOrder + 2;
        pose.maskInteraction = body.maskInteraction;
        pose.flipX = direction < 0f;
        pose.flipY = body.flipY;
        pose.color = body.color;
        pose.sprite = frames[0];
        pose.enabled = body.enabled;
        previousForceOff = body.forceRenderingOff;
        body.forceRenderingOff = true;
        unit = body.sprite.bounds.size.y * Mathf.Abs(body.transform.lossyScale.y) / 32f;
        duration = Mathf.Max(0.01f, dashDuration);
        elapsed = emissionTime = ghostTime = 0f;
        emissionIndex = 0;
        active = true;
        previousPosition = body.transform.position;
        EmitWind();
        EmitBurstRing();
        if (grounded) EmitDust(StartDustCount);
    }

    public void End(bool grounded)
    {
        if (!active || source == null) return;
        active = false;
        recovering = true;
        elapsed = 0f;
        pose.sprite = frames[3];
        SetStretch(1f, 1f);
        if (grounded) EmitBrakeDust();
        EmitWind();
    }

    public void Cancel()
    {
        if (source != null && (active || recovering)) source.forceRenderingOff = previousForceOff;
        active = recovering = false;
        if (pose != null)
        {
            pose.enabled = false;
            SetStretch(1f, 1f);
        }
    }

    // 몸 비율 변경. 발밑(스프라이트 아래쪽 끝) 높이는 그대로 유지
    private void SetStretch(float x, float y)
    {
        if (pose == null) return;
        pose.transform.localScale = new Vector3(poseSize * x, poseSize * y, 1f);
        pose.transform.localPosition = new Vector3(0f, -footExtent * (1f - y), 0f);
    }

    private void LateUpdate() => Tick(Time.deltaTime);

    private void Tick(float delta)
    {
        if (effectsRoot != null) effectsRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        if (active || recovering)
        {
            if (source == null) { Cancel(); return; }
            elapsed += delta;
            pose.color = source.color;
            pose.enabled = source.enabled && !previousForceOff;
            if (active)
            {
                float progress = elapsed / duration;
                pose.sprite = frames[progress < 0.16f ? 0 : (progress < 0.57f ? 1 : 2)];

                // 출발 때 늘어난 몸이 감속 구간 앞부분에 걸쳐 원래 비율로 돌아옴 (ease-out)
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress / StretchRecover), 2f);
                SetStretch(Mathf.Lerp(StretchX, 1f, k), Mathf.Lerp(StretchY, 1f, k));

                // Don't lay down trails while blocked by a wall or while paused.
                if (delta > 0f && Vector3.Distance(previousPosition, source.transform.position) > unit * 0.5f)
                {
                    ghostTime += delta;
                    if (ghostTime >= GhostInterval)
                    {
                        ghostTime %= GhostInterval;
                        EmitGhost();
                    }
                    emissionTime += delta;
                    if (emissionTime >= WindInterval)
                    {
                        emissionTime %= WindInterval;
                        EmitWind();
                    }
                }
                previousPosition = source.transform.position;
            }
            else if (elapsed >= RecoveryDuration) Cancel();
        }

        foreach (var p in particles)
        {
            if (p == null || !p.renderer.enabled) continue;
            p.age += delta;
            float t = p.age / p.lifetime;
            if (t >= 1f) { p.renderer.enabled = false; continue; }
            p.renderer.transform.position += p.velocity * delta;
            p.renderer.color = new Color(p.color.r, p.color.g, p.color.b, p.color.a * (1f - t));
            if (p.streak) p.renderer.transform.localScale = new Vector3(p.scale.x * (1f + t * 0.4f), p.scale.y, 1f);
        }
    }

    private bool PrepareSprites(float pixelsPerUnit)
    {
        if (frames != null) return true;
        var sheet = Resources.Load<Texture2D>("Dash/PlayerDash");
        if (sheet == null)
        {
            Debug.LogWarning("Dash sprite sheet missing: Resources/Dash/PlayerDash", this);
            return false;
        }
        frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = Sprite.Create(sheet, new Rect(i * 48, 0, 48, 40), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            frames[i].name = "Dash_" + i;
        }
        // A code-native one-pixel primitive for crisp wind lines and dust.
        var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point, name = "Dash Pixel" };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        return true;
    }

    private Particle Spawn(Sprite sprite, Vector3 position, Vector3 scale, Color color, float lifetime, Vector3 velocity, bool streak)
    {
        if (effectsRoot == null)
        {
            effectsRoot = new GameObject("Dash Effects (pooled)");
            // Match the persistent player's scene without inheriting its motion.
            effectsRoot.transform.SetParent(transform, false);
            effectsRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
        int slot = nextParticle++ % particles.Length;
        var p = particles[slot];
        if (p == null)
        {
            var child = new GameObject("Dash Particle " + slot);
            child.layer = source.gameObject.layer;
            child.transform.SetParent(effectsRoot.transform, false);
            p = particles[slot] = new Particle { renderer = child.AddComponent<SpriteRenderer>() };
        }
        p.renderer.transform.SetPositionAndRotation(position, Quaternion.identity);
        // Counteract the player's hierarchy scale and motion in Tick.
        var parentScale = effectsRoot.transform.lossyScale;
        p.renderer.transform.localScale = new Vector3(scale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)), scale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
        p.scale = p.renderer.transform.localScale;
        p.renderer.sprite = sprite;
        p.renderer.sharedMaterial = source.sharedMaterial;
        p.renderer.sortingLayerID = source.sortingLayerID;
        p.renderer.sortingOrder = source.sortingOrder + 1;
        p.renderer.maskInteraction = source.maskInteraction;
        p.renderer.flipX = sprite != pixel && pose.flipX;
        p.renderer.flipY = sprite != pixel && pose.flipY;
        p.renderer.color = p.color = color;
        p.renderer.enabled = true;
        p.age = 0f;
        p.lifetime = lifetime;
        p.velocity = velocity;
        p.streak = streak;
        return p;
    }

    private void EmitGhost()
    {
        // 방금 생긴 잔상(몸에 가까운 쪽)이 가장 진하고, 수명 동안 옅어지며 뒤쪽 잔상일수록 흐려짐
        var tint = new Color(0.5f, 0.95f, 0.88f, GhostAlpha * source.color.a);
        var position = pose.transform.position + Vector3.left * direction * unit * 10f;
        Spawn(pose.sprite, position, pose.transform.lossyScale, tint, GhostLifetime, Vector3.left * direction * unit * 5f, false);
    }

    // 출발 지점에 퍼지는 바람 고리 (월드에 남으므로 플레이어가 떠난 자리에서 퍼짐)
    private void EmitBurstRing()
    {
        Vector3 center = source.transform.TransformPoint(source.sprite.bounds.center) + Vector3.left * direction * unit * 4f;
        for (int i = 0; i < BurstRingCount; i++)
        {
            float a = (i + 0.5f) / BurstRingCount * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a) * 0.7f, Mathf.Sin(a), 0f);
            Vector3 velocity = (dir * 55f + Vector3.left * direction * 15f) * unit;
            Color tint = i % 3 == 0 ? new Color(1f, 0.97f, 0.82f, 0.85f) : new Color(0.6f, 0.95f, 0.88f, 0.75f);
            tint.a *= source.color.a;
            Spawn(pixel, center + dir * unit * 3f, Vector3.one * unit * (i % 3 == 0 ? 2f : 1f), tint, 0.13f, velocity, false);
        }
    }

    // 정지 순간 발밑에서 진행 방향 앞쪽으로 튀는 브레이크 먼지
    private void EmitBrakeDust()
    {
        Vector3 center = source.transform.TransformPoint(source.sprite.bounds.center);
        for (int i = 0; i < BrakeDustCount; i++)
        {
            Vector3 position = center + new Vector3(direction * (2f + i), -15f, 0f) * unit;
            Vector3 velocity = new Vector3(direction * (10f + i * 6f), 4f + (i % 3) * 6f, 0f) * unit;
            Spawn(pixel, position, Vector3.one * unit * (i % 2 + 1), new Color(0.76f, 0.7f, 0.54f, 0.7f * source.color.a), 0.18f + i * 0.015f, velocity, false);
        }
    }

    private void EmitWind()
    {
        for (int i = 0; i < 3; i++)
        {
            float y = (i - 1) * 6f + (emissionIndex % 2);
            Vector3 position = source.transform.position + new Vector3(-direction * (12f + i * 3f), y, 0f) * unit;
            Color tint = i == 1 ? new Color(1f, 0.95f, 0.72f, 0.65f) : new Color(0.57f, 0.94f, 0.86f, 0.55f);
            tint.a *= source.color.a;
            Spawn(pixel, position, new Vector3((14f + i * 5f) * unit, unit, 1f), tint, 0.11f, Vector3.left * direction * unit * 28f, true);
        }
        emissionIndex++;
    }

    private void EmitDust(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 center = source.transform.TransformPoint(source.sprite.bounds.center);
            Vector3 position = center + new Vector3(-direction * (4f + i), -15f, 0f) * unit;
            Vector3 velocity = new Vector3(-direction * (12f + i * 7f), 6f + (i % 3) * 7f, 0f) * unit;
            Spawn(pixel, position, Vector3.one * unit * (i % 2 + 1), new Color(0.76f, 0.7f, 0.54f, 0.65f * source.color.a), 0.17f + i * 0.015f, velocity, false);
        }
    }

    private void OnDisable()
    {
        Cancel();
        foreach (var p in particles) if (p != null) p.renderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (frames != null) foreach (var sprite in frames) Release(sprite);
        if (pixel != null) { Release(pixel.texture); Release(pixel); }
    }

    private static void Release(Object asset)
    {
        if (Application.isPlaying) Destroy(asset);
        else DestroyImmediate(asset);
    }
}
