using UnityEngine;

/// <summary>
/// 짧게 살다 사라지는 픽셀 이펙트(파편·가루·잔상·섬광) 공용 풀.
/// 처음 쓸 때 현재 씬에 하나 생기고 씬과 함께 사라진다. 투사체가 먼저 사라져도 이펙트는 끝까지 재생된다.
/// 게임 시간(deltaTime)으로 흐르므로 히트스톱 동안 함께 멈춘다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PixelFxPool : MonoBehaviour
{
    private const int Capacity = 128;

    private sealed class Bit
    {
        public SpriteRenderer renderer;
        public Vector3 velocity, startScale, endScale;
        public Color color;
        public float age, lifetime, drag;
    }

    private static PixelFxPool instance;
    private static Sprite pixel;

    private readonly Bit[] bits = new Bit[Capacity];
    private int next;

    /// <summary>1×1 흰 픽셀 (PPU 1 → scale 이 곧 월드 크기).</summary>
    public static Sprite Pixel
    {
        get
        {
            if (pixel != null) return pixel;
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point, name = "PixelFx Pixel", hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            pixel.name = "PixelFx Pixel";
            pixel.hideFlags = HideFlags.DontSave;
            return pixel;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    /// <summary>
    /// 이펙트 하나를 띄운다. sortLike 의 정렬 레이어·순서(+orderOffset)·머티리얼·오브젝트 레이어를 따른다.
    /// endScale = 수명이 끝날 때의 크기 배율, drag = 초당 감속 비율. 플레이 중이 아니면 null.
    /// </summary>
    public static SpriteRenderer Spawn(Sprite sprite, Vector3 position, Quaternion rotation, Vector3 scale, Color color,
        float lifetime, Vector3 velocity, SpriteRenderer sortLike, int orderOffset, float drag = 0f, float endScale = 1f)
    {
        if (sprite == null || lifetime <= 0f || !Application.isPlaying) return null;
        if (instance == null) instance = new GameObject("[PixelFx]").AddComponent<PixelFxPool>();
        return instance.Emit(sprite, position, rotation, scale, color, lifetime, velocity, sortLike, orderOffset, drag, endScale);
    }

    private SpriteRenderer Emit(Sprite sprite, Vector3 position, Quaternion rotation, Vector3 scale, Color color,
        float lifetime, Vector3 velocity, SpriteRenderer sortLike, int orderOffset, float drag, float endScale)
    {
        int slot = next++ % Capacity;
        Bit b = bits[slot];
        if (b == null)
        {
            var child = new GameObject("Fx " + slot);
            child.transform.SetParent(transform, false);
            b = bits[slot] = new Bit { renderer = child.AddComponent<SpriteRenderer>() };
        }

        SpriteRenderer r = b.renderer;
        r.transform.SetPositionAndRotation(position, rotation);
        r.transform.localScale = scale;
        r.sprite = sprite;
        r.flipX = r.flipY = false;
        if (sortLike != null)
        {
            r.gameObject.layer = sortLike.gameObject.layer;
            r.sharedMaterial = sortLike.sharedMaterial;
            r.sortingLayerID = sortLike.sortingLayerID;
            r.sortingOrder = sortLike.sortingOrder + orderOffset;
        }
        r.color = color;
        r.enabled = true;

        b.color = color;
        b.velocity = velocity;
        b.startScale = scale;
        b.endScale = scale * endScale;
        b.age = 0f;
        b.lifetime = lifetime;
        b.drag = drag;
        return r;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        foreach (Bit b in bits)
        {
            if (b == null || !b.renderer.enabled) continue;
            b.age += dt;
            float t = b.age / b.lifetime;
            if (t >= 1f) { b.renderer.enabled = false; continue; }
            Transform tr = b.renderer.transform;
            tr.position += b.velocity * dt;
            if (b.drag > 0f) b.velocity *= Mathf.Max(0f, 1f - b.drag * dt);
            tr.localScale = Vector3.Lerp(b.startScale, b.endScale, t);
            b.renderer.color = new Color(b.color.r, b.color.g, b.color.b, b.color.a * (1f - t));
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
