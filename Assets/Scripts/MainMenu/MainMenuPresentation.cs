using UnityEngine;
using UnityEngine.UI;

public class MainMenuPresentation : MonoBehaviour
{
    [SerializeField] private RawImage background;
    [SerializeField] private RectTransform composition;
    [SerializeField, Range(0f, 2f)] private float effectStrength = 1f;
    private Material animatedMaterial;
    private static readonly int MenuTime = Shader.PropertyToID("_MenuTime");
    private static readonly int Strength = Shader.PropertyToID("_Strength");

    private void Awake()
    {
        // Each menu owns its material; scene changes do not alter the source asset.
        animatedMaterial = new Material(background.material);
        background.material = animatedMaterial;
        FitComposition();
    }

    private void Update()
    {
        FitComposition();
        animatedMaterial.SetFloat(MenuTime, Time.unscaledTime);
        animatedMaterial.SetFloat(Strength, effectStrength);
    }

    private void FitComposition()
    {
        // Keep art, embedded wordmark and live buttons together at any aspect ratio.
        var canvasRect = (RectTransform)transform;
        float scale = Mathf.Min(canvasRect.rect.width / 1920f, canvasRect.rect.height / 1080f);
        composition.localScale = new Vector3(scale, scale, 1);
    }

    private void OnDestroy()
    {
        if (animatedMaterial != null) Destroy(animatedMaterial);
    }
}
