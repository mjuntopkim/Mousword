using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피격 플래시: 대상과 자식의 보이는 SpriteRenderer 를 잠깐 단색(흰색) 머티리얼로 바꿨다가 되돌린다.
/// 실제 시간(unscaled)으로 흐르므로 히트스톱 중에도 정상적으로 끝난다.
/// 사용: HitFlash.Play(monster, 0.06f);
/// </summary>
[DisallowMultipleComponent]
public sealed class HitFlash : MonoBehaviour
{
    private const string ShaderName = "Mousword/SpriteHitFlash";

    private static Material flashMaterial;
    private static bool shaderMissing;

    private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    private readonly List<Material> originals = new List<Material>();
    private static readonly List<SpriteRenderer> scratch = new List<SpriteRenderer>();

    private float remaining;
    private bool flashing;

    public static void Play(Component target, float duration)
    {
        if (target == null || duration <= 0f) return;
        Material m = GetMaterial();
        if (m == null) return;

        HitFlash flash = target.GetComponent<HitFlash>();
        if (flash == null) flash = target.gameObject.AddComponent<HitFlash>();
        flash.Begin(m, duration);
    }

    private static Material GetMaterial()
    {
        if (flashMaterial != null) return flashMaterial;
        if (shaderMissing) return null;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            shaderMissing = true;
            Debug.LogWarning($"[HitFlash] 셰이더 '{ShaderName}' 를 찾지 못해 피격 플래시를 끕니다.");
            return null;
        }
        flashMaterial = new Material(shader) { name = "Hit Flash (runtime)", hideFlags = HideFlags.DontSave };
        flashMaterial.SetColor("_FlashColor", Color.white);
        return flashMaterial;
    }

    private void Begin(Material m, float duration)
    {
        if (!flashing)
        {
            renderers.Clear();
            originals.Clear();
            GetComponentsInChildren(false, scratch);
            foreach (SpriteRenderer sr in scratch)
            {
                if (!sr.enabled || sr.sprite == null || sr.color.a <= 0.01f) continue; // 투명한 히트박스 등은 제외
                renderers.Add(sr);
                originals.Add(sr.sharedMaterial);
                sr.sharedMaterial = m;
            }
            scratch.Clear();
            flashing = renderers.Count > 0;
        }
        remaining = Mathf.Max(remaining, duration);
    }

    private void LateUpdate()
    {
        if (!flashing) return;
        remaining -= Time.unscaledDeltaTime;
        if (remaining <= 0f) Restore();
    }

    private void Restore()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr != null && sr.sharedMaterial == flashMaterial) sr.sharedMaterial = originals[i];
        }
        renderers.Clear();
        originals.Clear();
        remaining = 0f;
        flashing = false;
    }

    private void OnDisable()
    {
        Restore();
    }
}
