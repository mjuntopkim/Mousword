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

    private readonly Particle[] particles = new Particle[32];
    private SpriteRenderer source, pose;
    private Sprite[] frames;
    private Sprite pixel;
    private GameObject effectsRoot;
    private bool active, recovering, previousForceOff;
    private float elapsed, duration, emissionTime, direction, unit;
    private int nextParticle, emissionIndex;
    private Vector3 previousPosition;
    private const float RecoveryDuration = 0.07f;

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
        float size = body.sprite.bounds.size.y / (32f / body.sprite.pixelsPerUnit);
        pose.transform.localScale = Vector3.one * size;
        pose.transform.localPosition = Vector3.zero;
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
        elapsed = emissionTime = 0f;
        emissionIndex = 0;
        active = true;
        previousPosition = body.transform.position;
        EmitWind();
        if (grounded) EmitDust();
    }

    public void End(bool grounded)
    {
        if (!active || source == null) return;
        active = false;
        recovering = true;
        elapsed = 0f;
        pose.sprite = frames[3];
        if (grounded) EmitDust();
        EmitWind();
    }

    public void Cancel()
    {
        if (source != null && (active || recovering)) source.forceRenderingOff = previousForceOff;
        active = recovering = false;
        if (pose != null) pose.enabled = false;
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
                // Don't lay down trails while blocked by a wall or while paused.
                if (delta > 0f && Vector3.Distance(previousPosition, source.transform.position) > unit * 0.5f)
                {
                    emissionTime += delta;
                    if (emissionTime >= 0.04f)
                    {
                        emissionTime %= 0.04f;
                        EmitGhost();
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
        var tint = new Color(0.5f, 0.95f, 0.88f, 0.45f * source.color.a);
        var position = pose.transform.position + Vector3.left * direction * unit * 10f;
        Spawn(pose.sprite, position, pose.transform.lossyScale, tint, 0.14f, Vector3.left * direction * unit * 5f, false);
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

    private void EmitDust()
    {
        for (int i = 0; i < 5; i++)
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
