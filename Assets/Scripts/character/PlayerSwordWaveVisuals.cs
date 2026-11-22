using UnityEngine;

/// <summary>
/// Sword wave cast: the body plays poses taken from mousword_character-Sheet while the equipped sword
/// (same sprite and on-screen size as the WeaponAim sword) is raised overhead and slammed down with a pixel smear.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSwordWaveVisuals : MonoBehaviour
{
    /// <summary>Playback speed of the whole cast; frame timings below are divided by this.</summary>
    public const float SpeedMultiplier = 1.5f;
    // Slow wind-up with a held apex, a two-frame strike, an impact hit-stop (wave release), then an eased recovery.
    private static readonly float[] BaseFrameDurations = { 0.05f, 0.055f, 0.06f, 0.04f, 0.025f, 0.025f, 0.09f, 0.05f, 0.05f, 0.05f, 0.05f };
    private const int ReleaseFrame = 6; // impact frame, where the wave is released
    private static readonly float[] FrameDurations = System.Array.ConvertAll(BaseFrameDurations, d => d / SpeedMultiplier);
    // Sword angle around the hand, degrees, facing right (0 = forward, 90 = straight up).
    private static readonly float[] SwordAngles = { 190f, 150f, 105f, 100f, 45f, -5f, -38f, -48f, -40f, -32f, -25f };
    // Hand position per pose, in sheet pixels from the body centre (y up); follows each pose's lean.
    private static readonly Vector2[] HandPixels =
    {
        new Vector2(4f, -6f), new Vector2(4f, -5f), new Vector2(6f, -6f), new Vector2(6f, -6f),
        new Vector2(9f, -10f), new Vector2(10f, -10f), new Vector2(10f, -11f), new Vector2(10f, -11f),
        new Vector2(10f, -8f), new Vector2(8f, -6f), new Vector2(5f, -5f)
    };
    private static readonly int[] SmearFrame = { -1, -1, -1, -1, 0, 1, 2, 3, -1, -1, -1 };
    // Star glint on the blade tip: small while the blade gathers at the apex, large on impact.
    private static readonly int[] GlintFrame = { -1, -1, 0, 0, -1, -1, 1, -1, -1, -1, -1 };

    /// <summary>Seconds from Begin to the impact frame, where the wave is released.</summary>
    public static readonly float ReleaseTime = Sum(FrameDurations, ReleaseFrame);
    /// <summary>Total length of the cast animation in seconds.</summary>
    public static readonly float Duration = Sum(FrameDurations, FrameDurations.Length);

    // Ultimate: hold the raised-overhead pose until ReleaseHold().
    private const int ApexFrame = 3;
    private static readonly float ApexEnd = Sum(FrameDurations, ApexFrame + 1);
    /// <summary>Seconds from ReleaseHold to the impact frame.</summary>
    public static readonly float ReleaseLead = FrameDurations[ApexFrame + 1] + FrameDurations[ApexFrame + 2];

    private static float Sum(float[] values, int count)
    {
        float total = 0f;
        for (int i = 0; i < count; i++) total += values[i];
        return total;
    }

    private const int PoseSize = 48;                       // 32px sheet cell centred in a 48px pose cell
    private const int SmearWidth = 96, SmearHeight = 112;  // hand pivot at (24, 64) from the top-left
    private const float SmearRadius = 55f;                 // smear drawn for a 55px blade
    private const float GripRatio = 4f / 72f;              // hand holds mousword_sword at handle pixel 4 of 72
    private const int GlintSize = 9;

    private SpriteRenderer source, pose, sword, smear, glint;
    private Transform rig, arm;
    private Sprite[] poseFrames, smearFrames, glintFrames;
    private Vector3 rigCentre;
    private float rigPixel, rigDirection, tipDistance;
    private float spritePpu;
    private SpriteRenderer[] weaponRenderers = new SpriteRenderer[0];
    private bool[] weaponWasHidden = new bool[0];
    private bool active, previousForceOff, holding;
    private float elapsed;
    private int frame;
    private int sortLayer, sortBase;

    public bool IsPlaying => active;
    /// <summary>World size of one character-sheet pixel during the current cast.</summary>
    public float PixelWorldSize { get; private set; }

    /// <param name="holdAtApex">Stop on the raised-overhead pose until ReleaseHold() is called (used by the ultimate).</param>
    public void Begin(SpriteRenderer body, Transform weaponRoot, float facing, bool holdAtApex = false)
    {
        Cancel();
        if (body == null || body.sprite == null) return;
        // A dash landing pose can still be hiding the body for a few frames after the dash ends.
        // End it first, otherwise that temporary hide is saved as the body's real state and restored when the cast ends.
        var dash = GetComponent<PlayerDashVisuals>();
        if (dash != null) dash.Cancel();
        if (!PrepareSprites(body.sprite.pixelsPerUnit)) return;
        source = body;
        float direction = facing < 0f ? -1f : 1f;

        // Same apparent size as the 32px sheet cell, whichever body sprite is currently shown.
        float size = body.sprite.bounds.size.y / (32f / body.sprite.pixelsPerUnit);
        float localPixel = body.sprite.bounds.size.y / 32f;
        Vector3 centre = body.sprite.bounds.center;
        PixelWorldSize = localPixel * Mathf.Abs(body.transform.lossyScale.y);

        pose = EnsureRenderer(ref pose, "SwordWave Pose", body.transform);
        pose.transform.localPosition = centre;
        pose.transform.localScale = Vector3.one * size;
        pose.flipX = direction < 0f;
        pose.flipY = body.flipY;

        if (rig == null)
        {
            rig = new GameObject("SwordWave Rig").transform;
            rig.gameObject.layer = body.gameObject.layer;
            rig.SetParent(body.transform, false);
            arm = new GameObject("Arm").transform;
            arm.gameObject.layer = body.gameObject.layer;
            arm.SetParent(rig, false);
        }
        rig.gameObject.SetActive(true);
        rigCentre = centre;
        rigPixel = localPixel;
        rigDirection = direction;
        rig.localRotation = Quaternion.identity;
        rig.localScale = new Vector3(direction, 1f, 1f); // mirror the whole swing when facing left

        smear = EnsureRenderer(ref smear, "Smear", rig);
        glint = EnsureRenderer(ref glint, "Glint", rig);
        sword = EnsureRenderer(ref sword, "Skill Sword", arm);

        // Hide the mouse-aimed sword and reuse its sprite at its exact on-screen size.
        weaponRenderers = weaponRoot != null ? weaponRoot.GetComponentsInChildren<SpriteRenderer>(false) : new SpriteRenderer[0];
        weaponWasHidden = new bool[weaponRenderers.Length];
        SpriteRenderer weaponSword = null;
        float longest = 0f;
        for (int i = 0; i < weaponRenderers.Length; i++)
        {
            var r = weaponRenderers[i];
            weaponWasHidden[i] = r.forceRenderingOff;
            r.forceRenderingOff = true;
            if (r.sprite == null) continue;
            float length = BladeLength(r);
            if (length > longest) { longest = length; weaponSword = r; }
        }

        float bladePixels = SmearRadius;
        if (weaponSword != null)
        {
            ConfigureSword(weaponSword);
            bladePixels = longest / Mathf.Max(0.0001f, PixelWorldSize);
        }
        else sword.enabled = false;
        smear.transform.localScale = Vector3.one * size * (bladePixels / SmearRadius);
        glint.transform.localScale = Vector3.one * size;
        tipDistance = bladePixels * (1f - GripRatio) * localPixel;

        foreach (var r in new[] { pose, smear, sword, glint })
        {
            r.sharedMaterial = body.sharedMaterial;
            r.maskInteraction = body.maskInteraction;
        }
        sortLayer = body.sortingLayerID;
        sortBase = body.sortingOrder;
        ApplySorting();

        previousForceOff = body.forceRenderingOff;
        body.forceRenderingOff = true;
        elapsed = 0f;
        frame = -1;
        holding = holdAtApex;
        active = true;
        ApplyFrame(0);
    }

    /// <summary>Draw the cast on another sorting layer/order (the ultimate keeps the player above its screen darkening).</summary>
    public void SetSorting(int layerId, int baseOrder)
    {
        if (!active) return;
        sortLayer = layerId;
        sortBase = baseOrder;
        ApplySorting();
    }

    /// <summary>Continue from the held overhead pose into the slam.</summary>
    public void ReleaseHold()
    {
        if (!holding) return;
        holding = false;
        elapsed = Mathf.Max(elapsed, ApexEnd);
    }

    private void ApplySorting()
    {
        foreach (var r in new[] { pose, smear, sword, glint }) r.sortingLayerID = sortLayer;
        smear.sortingOrder = sortBase + 1;
        pose.sortingOrder = sortBase + 3;
        glint.sortingOrder = sortBase + 5;
        if (frame >= 0) sword.sortingOrder = sortBase + (frame <= ApexFrame ? 2 : 4);
    }

    public void Cancel()
    {
        holding = false;
        if (active && source != null) source.forceRenderingOff = previousForceOff;
        for (int i = 0; i < weaponRenderers.Length; i++)
            if (weaponRenderers[i] != null) weaponRenderers[i].forceRenderingOff = weaponWasHidden[i];
        weaponRenderers = new SpriteRenderer[0];
        weaponWasHidden = new bool[0];
        active = false;
        if (pose != null) pose.enabled = false;
        if (rig != null) rig.gameObject.SetActive(false);
    }

    private void LateUpdate() => Tick(Time.deltaTime);

    private void Tick(float delta)
    {
        if (!active) return;
        if (source == null) { Cancel(); return; }
        elapsed += delta;
        if (holding) elapsed = Mathf.Min(elapsed, ApexEnd - 0.0001f);
        float end = 0f;
        for (int i = 0; i < FrameDurations.Length; i++)
        {
            end += FrameDurations[i];
            if (elapsed < end) { ApplyFrame(i); return; }
        }
        Cancel();
    }

    private void ApplyFrame(int index)
    {
        bool visible = source.enabled && !previousForceOff;
        pose.color = smear.color = sword.color = glint.color = source.color;
        pose.enabled = visible;
        if (index == frame) return;
        frame = index;
        pose.sprite = poseFrames[index];
        Vector2 hand = HandPixels[index];
        rig.localPosition = rigCentre + new Vector3(hand.x * rigDirection, hand.y, 0f) * rigPixel;
        float angle = SwordAngles[index];
        arm.localRotation = Quaternion.Euler(0f, 0f, angle);
        int g = GlintFrame[index];
        glint.enabled = visible && g >= 0;
        if (g >= 0)
        {
            glint.sprite = glintFrames[g];
            float rad = angle * Mathf.Deg2Rad;
            glint.transform.localPosition = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * tipDistance;
        }
        // Behind the body while raised, in front once it comes down.
        sword.sortingOrder = sortBase + (index <= ApexFrame ? 2 : 4);
        sword.enabled = visible && sword.sprite != null;
        int s = SmearFrame[index];
        smear.enabled = visible && s >= 0;
        if (s >= 0) smear.sprite = smearFrames[s];
    }

    private void ConfigureSword(SpriteRenderer weaponSword)
    {
        var sprite = weaponSword.sprite;
        sword.sprite = sprite;
        sword.flipX = sword.flipY = false;
        Bounds b = sprite.bounds;
        bool vertical = b.size.y > b.size.x; // older sword sprites point up; mousword_sword points right
        Quaternion turn = vertical ? Quaternion.Euler(0f, 0f, -90f) : Quaternion.identity;
        Vector3 grip = vertical
            ? new Vector3(b.center.x, b.min.y + GripRatio * b.size.y, 0f)
            : new Vector3(b.min.x + GripRatio * b.size.x, b.center.y, 0f);

        Vector3 target = Abs(weaponSword.transform.lossyScale);
        Vector3 parent = Abs(arm.lossyScale);
        Vector3 local = new Vector3(target.x / Mathf.Max(0.0001f, parent.x), target.y / Mathf.Max(0.0001f, parent.y), 1f);
        sword.transform.localScale = local;
        sword.transform.localRotation = turn;
        sword.transform.localPosition = -(turn * Vector3.Scale(grip, local));
    }

    private static float BladeLength(SpriteRenderer r)
    {
        Vector3 size = r.sprite.bounds.size, scale = Abs(r.transform.lossyScale);
        return size.y > size.x ? size.y * scale.y : size.x * scale.x;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private SpriteRenderer EnsureRenderer(ref SpriteRenderer renderer, string name, Transform parent)
    {
        if (renderer == null)
        {
            var child = new GameObject(name);
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            renderer = child.AddComponent<SpriteRenderer>();
        }
        renderer.enabled = false;
        return renderer;
    }

    private bool PrepareSprites(float ppu)
    {
        if (poseFrames != null && Mathf.Approximately(spritePpu, ppu)) return true;
        var poseSheet = Resources.Load<Texture2D>("SwordWave/SwordWavePose");
        var smearSheet = Resources.Load<Texture2D>("SwordWave/SwordWaveSmear");
        var glintSheet = Resources.Load<Texture2D>("SwordWave/SwordWaveGlint");
        if (poseSheet == null || smearSheet == null || glintSheet == null)
        {
            Debug.LogWarning("Sword wave sprites missing: Resources/SwordWave/SwordWavePose, SwordWaveSmear, SwordWaveGlint", this);
            return false;
        }
        ReleaseSprites();
        spritePpu = ppu;
        poseFrames = new Sprite[FrameDurations.Length];
        for (int i = 0; i < poseFrames.Length; i++)
        {
            poseFrames[i] = Sprite.Create(poseSheet, new Rect(i * PoseSize, 0, PoseSize, PoseSize), new Vector2(0.5f, 0.5f), ppu);
            poseFrames[i].name = "SwordWavePose_" + i;
        }
        smearFrames = new Sprite[4];
        var pivot = new Vector2(24f / SmearWidth, (SmearHeight - 64f) / SmearHeight);
        for (int i = 0; i < smearFrames.Length; i++)
        {
            smearFrames[i] = Sprite.Create(smearSheet, new Rect(i * SmearWidth, 0, SmearWidth, SmearHeight), pivot, ppu);
            smearFrames[i].name = "SwordWaveSmear_" + i;
        }
        glintFrames = new Sprite[2];
        for (int i = 0; i < glintFrames.Length; i++)
        {
            glintFrames[i] = Sprite.Create(glintSheet, new Rect(i * GlintSize, 0, GlintSize, GlintSize), new Vector2(0.5f, 0.5f), ppu);
            glintFrames[i].name = "SwordWaveGlint_" + i;
        }
        return true;
    }

    private void ReleaseSprites()
    {
        foreach (var set in new[] { poseFrames, smearFrames, glintFrames })
            if (set != null) foreach (var s in set) if (s != null) Release(s);
        poseFrames = smearFrames = glintFrames = null;
    }

    private void OnDisable() => Cancel();

    private void OnDestroy()
    {
        Cancel();
        ReleaseSprites();
    }

    private static void Release(Object asset)
    {
        if (Application.isPlaying) Destroy(asset);
        else DestroyImmediate(asset);
    }
}
