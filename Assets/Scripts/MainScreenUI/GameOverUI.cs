using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 오버 화면. 씬에 따로 배치하지 않고 PlayerDeathSequence 가 호출할 때 코드로 만든다
/// (플레이어가 씬을 넘어 유지되므로 어느 스테이지에서 죽어도 같은 화면이 뜬다).
/// 연출: 검은 반투명 화면 → GAME OVER (확대되며 자리잡음) + 금색 선 → 버튼 두 개.
/// 화면이 다 어두워지면 게임을 완전히 멈춘다 (Time.timeScale = 0). 연출은 모두 실제 시간으로 흐른다.
/// - 계속하기: 쓰러진 그 자리에서 체력·마나를 가득 채우고 일어나 그대로 이어서 진행
/// - 메인 화면: 유지되던 플레이어·HUD·카메라·GameManager 를 정리하고 메인 메뉴 씬으로
/// </summary>
public class GameOverUI : MonoBehaviour
{
    public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    // 타이밍 (실제 시간, 초) — 게임 오버 화면이 뜨기 시작한 순간 기준
    private const float OverlayFade = 0.8f;
    private const float OverlayAlpha = 0.6f;
    private const float TitleDelay = 0.4f;
    private const float TitleTime = 0.6f;
    private const float ButtonsDelay = 1.2f;
    private const float ButtonsTime = 0.35f;
    private const float FadeToBlackTime = 0.35f;
    private const float FadeFromBlackTime = 0.45f;
    private const float ContinueFadeTime = 0.5f;

    private static readonly Color Cream = new Color(1f, 0.94f, 0.77f);
    private static readonly Color Gold = new Color(1f, 0.79f, 0.34f);
    private static readonly Color BloodRed = new Color(0.62f, 0.07f, 0.04f);

    private static GameOverUI current;

    private PlayerDeathSequence owner;
    private Image overlay;
    private CanvasGroup titleGroup;
    private RectTransform title;
    private RectTransform divider;
    private CanvasGroup buttonsGroup;
    private RectTransform buttonsRoot;
    private Button continueButton;
    private Button mainMenuButton;
    private Image black;
    private GameObject ownEventSystem;
    private Font koreanFont;
    private bool busy;
    private bool savedCursorVisible;
    private CursorLockMode savedCursorLock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => current = null;

    public static void Show(PlayerDeathSequence owner)
    {
        if (current != null) return;
        var go = new GameObject("[GameOverUI]");
        DontDestroyOnLoad(go);
        current = go.AddComponent<GameOverUI>();
        current.owner = owner;
        current.Build();
        current.StartCoroutine(current.Intro());
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
        if (koreanFont != null) Destroy(koreanFont);
    }

    #region 화면 구성
    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;                    // HUD 위
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        Font latin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        koreanFont = CreateKoreanFont();
        Font buttonFont = koreanFont != null ? koreanFont : latin;

        // 반투명 검은 화면 (뒤의 게임 클릭 차단)
        overlay = Stretch("Overlay", transform).gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0f);

        // GAME OVER
        var titleGo = Rect("Title", transform, new Vector2(0f, 120f), new Vector2(1400f, 200f));
        title = titleGo;
        titleGroup = titleGo.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        var titleText = Label(titleGo, "GAME OVER", latin, 150, Color.white);
        var outline = titleText.gameObject.AddComponent<Outline>();
        outline.effectColor = BloodRed;
        outline.effectDistance = new Vector2(6f, -6f);
        var shadow = titleText.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -14f);

        // 제목 아래 금색 선 (가운데에서 양옆으로 펼쳐짐)
        divider = Rect("Divider", titleGo, new Vector2(0f, -112f), new Vector2(0f, 6f));
        divider.gameObject.AddComponent<Image>().color = Gold;

        // 버튼
        buttonsRoot = Rect("Buttons", transform, new Vector2(0f, -150f), new Vector2(600f, 220f));
        buttonsGroup = buttonsRoot.gameObject.AddComponent<CanvasGroup>();
        buttonsGroup.alpha = 0f;
        buttonsGroup.interactable = false;
        buttonsGroup.blocksRaycasts = false;
        continueButton = MakeButton("Continue Button", "계속하기", new Vector2(0f, 50f), buttonFont);
        mainMenuButton = MakeButton("Main Menu Button", "메인 화면", new Vector2(0f, -50f), buttonFont);
        continueButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = mainMenuButton, selectOnUp = mainMenuButton };
        mainMenuButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = continueButton, selectOnDown = continueButton };
        continueButton.onClick.AddListener(OnContinue);
        mainMenuButton.onClick.AddListener(OnMainMenu);

        // 장면 전환용 완전 검은 화면 (맨 위)
        black = Stretch("Transition Black", transform).gameObject.AddComponent<Image>();
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;

        if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
        {
            ownEventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            ownEventSystem.transform.SetParent(transform, false);
        }
    }

    private Button MakeButton(string name, string text, Vector2 position, Font font)
    {
        var rect = Rect(name, buttonsRoot, position, new Vector2(460f, 84f));
        var hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);           // 클릭 영역만 담당
        var button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;

        var label = Label(Stretch("Label", rect), text, font, 48, Cream);
        label.fontStyle = FontStyle.Bold;
        var labelShadow = label.gameObject.AddComponent<Shadow>();
        labelShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        labelShadow.effectDistance = new Vector2(0f, -4f);

        var left = Diamond("Diamond L", rect, -150f);
        var right = Diamond("Diamond R", rect, 150f);
        rect.gameObject.AddComponent<GameOverButton>().Configure(label, left, right);
        return button;
    }

    // 선택 표시용 다이아몬드 (45도 돌린 사각형)
    private static Image Diamond(string name, RectTransform parent, float x)
    {
        var rect = Rect(name, parent, new Vector2(x, 0f), new Vector2(16f, 16f));
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(Gold.r, Gold.g, Gold.b, 0f);
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform Stretch(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Text Label(RectTransform rect, string text, Font font, int size, Color color)
    {
        var label = rect.gameObject.AddComponent<Text>();
        label.text = text;
        label.font = font;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    // 메인 메뉴와 같은 방식: 설치된 한글 글꼴을 사용 (글꼴 파일을 프로젝트에 넣지 않음)
    private static Font CreateKoreanFont()
    {
        string[] installed = Font.GetOSInstalledFontNames();
        string[] candidates = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" };
        foreach (string name in candidates)
            if (Array.IndexOf(installed, name) >= 0)
                return Font.CreateDynamicFontFromOSFont(name, 48);
        return null;
    }
    #endregion

    #region 등장 연출
    private IEnumerator Intro()
    {
        savedCursorVisible = Cursor.visible;
        savedCursorLock = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        float t = 0f;
        bool frozen = false;
        float end = ButtonsDelay + ButtonsTime;
        while (t < end)
        {
            t += Time.unscaledDeltaTime;

            float o = Mathf.Clamp01(t / OverlayFade);
            overlay.color = new Color(0f, 0f, 0f, OverlayAlpha * EaseOutCubic(o));
            if (!frozen && o >= 1f)
            {
                Time.timeScale = 0f;                    // 화면이 다 어두워지면 게임 정지
                frozen = true;
            }

            float u = Mathf.Clamp01((t - TitleDelay) / TitleTime);
            titleGroup.alpha = Mathf.Clamp01(u * 2f);
            title.localScale = Vector3.one * Mathf.LerpUnclamped(1.35f, 1f, EaseOutBack(u));
            divider.sizeDelta = new Vector2(Mathf.Lerp(0f, 620f, EaseOutCubic(Mathf.Clamp01((t - TitleDelay - 0.25f) / 0.5f))), 6f);

            float b = Mathf.Clamp01((t - ButtonsDelay) / ButtonsTime);
            buttonsGroup.alpha = EaseOutCubic(b);
            buttonsRoot.anchoredPosition = new Vector2(0f, Mathf.Lerp(-180f, -150f, EaseOutCubic(b)));
            yield return null;
        }
        if (!frozen) Time.timeScale = 0f;

        buttonsGroup.interactable = true;
        buttonsGroup.blocksRaycasts = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
    #endregion

    #region 버튼 동작
    private void OnContinue()
    {
        if (busy) return;
        StartCoroutine(ContinueRoutine());
    }

    private void OnMainMenu()
    {
        if (busy) return;
        StartCoroutine(MainMenuRoutine());
    }

    private void BeginTransition()
    {
        busy = true;
        buttonsGroup.interactable = false;
        black.raycastTarget = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // 쓰러진 그 자리에서 일어나 그대로 이어서 진행: 게임을 다시 흐르게 하고, 화면이 걷히는 동안 일어난다
    private IEnumerator ContinueRoutine()
    {
        BeginTransition();
        black.raycastTarget = false;
        Time.timeScale = 1f;
        if (owner != null) owner.Revive();
        Cursor.visible = savedCursorVisible;
        Cursor.lockState = savedCursorLock;

        float startOverlay = overlay.color.a, startTitle = titleGroup.alpha, startButtons = buttonsGroup.alpha;
        float t = 0f;
        while (t < ContinueFadeTime)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - EaseOutCubic(Mathf.Clamp01(t / ContinueFadeTime));
            overlay.color = new Color(0f, 0f, 0f, startOverlay * k);
            titleGroup.alpha = startTitle * k;
            buttonsGroup.alpha = startButtons * k;
            title.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, k);
            yield return null;
        }
        Destroy(gameObject);
    }

    private IEnumerator MainMenuRoutine()
    {
        BeginTransition();
        yield return FadeBlack(0f, 1f, FadeToBlackTime);
        Time.timeScale = 1f;

        // 스테이지 사이에서 유지되던 오브젝트(플레이어·HUD·카메라·GameManager 등) 정리
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root == gameObject || root.GetComponent<HitFeedback>() != null) continue;
            Destroy(root);
        }

        AsyncOperation op = LoadScene(MainMenuScenePath);
        if (op != null)
            while (!op.isDone) yield return null;
        DropOwnEventSystem();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        HideContents();

        yield return FadeBlack(1f, 0f, FadeFromBlackTime);
        Destroy(gameObject);
    }

    private void HideContents()
    {
        overlay.color = new Color(0f, 0f, 0f, 0f);
        titleGroup.alpha = 0f;
        buttonsGroup.alpha = 0f;
        buttonsGroup.blocksRaycasts = false;
    }

    // 새 씬의 EventSystem 과 겹치지 않도록, 직접 만든 EventSystem 은 씬 전환 후 제거
    private void DropOwnEventSystem()
    {
        if (ownEventSystem == null) return;
        Destroy(ownEventSystem);
        ownEventSystem = null;
    }

    private IEnumerator FadeBlack(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        black.color = new Color(0f, 0f, 0f, to);
    }

    // 빌드 목록에 없는 테스트 씬(Player_test 등)도 에디터에서는 다시 불러올 수 있게 한다
    private static AsyncOperation LoadScene(string path)
    {
#if UNITY_EDITOR
        if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
        {
            Debug.LogError("빌드 씬 목록에 없는 씬이라 불러올 수 없습니다: " + path);
            return null;
        }
        return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
    }
    #endregion
}
