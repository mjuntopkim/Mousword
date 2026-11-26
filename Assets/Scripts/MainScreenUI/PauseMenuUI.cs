using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static GameMenuKit;

/// <summary>
/// ESC 일시정지 메뉴. 씬에 배치하지 않아도 게임 시작 시 자동으로 만들어져 씬을 넘어 유지된다.
/// - ESC: 게임을 멈추고(Time.timeScale = 0, 소리 정지) 반투명 화면 + PAUSE + 버튼 두 개
/// - 계속하기(또는 ESC 한 번 더): 일시정지를 풀고 그대로 이어서 진행
/// - 메인 메뉴: 화면이 검게 덮인 뒤 유지되던 플레이어·HUD 등을 정리하고 메인 메뉴 씬으로
/// 메인 메뉴·엔딩 화면, 플레이어가 없거나 쓰러져 있을 때, 게임 오버 화면이 떠 있을 때는 열리지 않는다.
/// </summary>
[DefaultExecutionOrder(10000)] // 히트스톱(HitFeedback)이 끝나며 시간을 되돌려도 마지막에 다시 멈춰 둔다
public class PauseMenuUI : MonoBehaviour
{
    private const float OverlayAlpha = 0.6f;
    private const float OpenTime = 0.2f;
    private const float CloseTime = 0.15f;
    private const float FadeToBlackTime = 0.35f;
    private const float FadeFromBlackTime = 0.45f;

    private static readonly Color DeepNavy = new Color(0.07f, 0.08f, 0.16f);

    private static PauseMenuUI instance;

    /// <summary>일시정지 중 (플레이어 조작 스크립트는 이 동안 입력을 무시한다)</summary>
    public static bool IsPaused { get; private set; }

    private Canvas canvas;
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

    private Coroutine routine;
    private bool busy;                       // 닫히는 중 / 메인 메뉴로 넘어가는 중
    private float savedTimeScale = 1f;
    private bool savedCursorVisible;
    private CursorLockMode savedCursorLock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        IsPaused = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("[PauseMenu]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<PauseMenuUI>();
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        instance = null;
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
        if (koreanFont != null) Destroy(koreanFont);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || busy) return;
        if (IsPaused) Resume();
        else if (CanPause()) Pause();
    }

    private void LateUpdate()
    {
        // 일시정지 중에 다른 곳(히트스톱 종료 등)이 시간을 되돌려 놓으면 다시 멈춘다
        if (IsPaused && Time.timeScale != 0f) Time.timeScale = 0f;
    }

    private static bool CanPause()
    {
        if (GameOverUI.IsShowing) return false;
        if (FindAnyObjectByType<MainMenuController>() != null) return false;
        if (FindAnyObjectByType<EndingController>() != null) return false;

        var player = FindAnyObjectByType<PlayerMove>();
        if (player == null) return false;
        var status = player.GetComponent<PlayerStatus>();
        if (status != null && status.IsDead) return false;          // 쓰러지는 중 ~ 다 일어설 때까지
        return true;
    }

    #region 열기 / 닫기
    private void Pause()
    {
        if (canvas == null) Build();

        IsPaused = true;
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;

        savedCursorVisible = Cursor.visible;
        savedCursorLock = Cursor.lockState;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureEventSystem();
        canvas.enabled = true;
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(OpenRoutine());
    }

    private void Resume()
    {
        if (!IsPaused) return;
        busy = true;
        buttonsGroup.interactable = false;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        // 바로 게임을 다시 흐르게 하고, 메뉴는 짧게 걷어낸다
        IsPaused = false;
        Time.timeScale = HitFeedback.IsHitStopped ? 0f : (savedTimeScale > 0f ? savedTimeScale : 1f);
        AudioListener.pause = false;
        Cursor.visible = savedCursorVisible;
        Cursor.lockState = savedCursorLock;

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(CloseRoutine());
    }

    private IEnumerator OpenRoutine()
    {
        buttonsGroup.interactable = false;
        buttonsGroup.blocksRaycasts = true;
        float t = 0f;
        while (t < OpenTime)
        {
            t += Time.unscaledDeltaTime;
            float k = EaseOutCubic(Mathf.Clamp01(t / OpenTime));
            overlay.color = new Color(0f, 0f, 0f, OverlayAlpha * k);
            titleGroup.alpha = k;
            title.localScale = Vector3.one * Mathf.LerpUnclamped(1.15f, 1f, EaseOutBack(Mathf.Clamp01(t / OpenTime)));
            divider.sizeDelta = new Vector2(Mathf.Lerp(0f, 520f, k), 6f);
            buttonsGroup.alpha = k;
            buttonsRoot.anchoredPosition = new Vector2(0f, Mathf.Lerp(-170f, -150f, k));
            yield return null;
        }
        SetVisible(1f);
        buttonsGroup.interactable = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        routine = null;
    }

    private IEnumerator CloseRoutine()
    {
        float startOverlay = overlay.color.a / OverlayAlpha;
        float t = 0f;
        while (t < CloseTime)
        {
            t += Time.unscaledDeltaTime;
            SetVisible(startOverlay * (1f - EaseOutCubic(Mathf.Clamp01(t / CloseTime))));
            yield return null;
        }
        Hide();
        busy = false;
        routine = null;
    }

    private void SetVisible(float k)
    {
        overlay.color = new Color(0f, 0f, 0f, OverlayAlpha * k);
        titleGroup.alpha = k;
        title.localScale = Vector3.one;
        divider.sizeDelta = new Vector2(520f * k, 6f);
        buttonsGroup.alpha = k;
        buttonsRoot.anchoredPosition = new Vector2(0f, -150f);
    }

    private void Hide()
    {
        SetVisible(0f);
        buttonsGroup.interactable = false;
        buttonsGroup.blocksRaycasts = false;
        canvas.enabled = false;
        DropOwnEventSystem();
    }
    #endregion

    #region 버튼 동작
    private void OnContinue()
    {
        if (busy || !IsPaused) return;
        Resume();
    }

    private void OnMainMenu()
    {
        if (busy || !IsPaused) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(MainMenuRoutine());
    }

    private IEnumerator MainMenuRoutine()
    {
        busy = true;
        buttonsGroup.interactable = false;
        black.raycastTarget = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        yield return FadeBlack(0f, 1f, FadeToBlackTime);

        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        DestroyPersistentObjects(gameObject);

        AsyncOperation op = LoadScene(MainMenuScenePath);
        if (op != null)
            while (!op.isDone) yield return null;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetVisible(0f);
        DropOwnEventSystem();

        yield return FadeBlack(1f, 0f, FadeFromBlackTime);
        black.raycastTarget = false;
        Hide();
        busy = false;
        routine = null;
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
    #endregion

    #region 화면 구성
    private void Build()
    {
        canvas = CreateCanvas(gameObject, 450);       // HUD 위, 게임 오버 화면 아래

        Font latin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        koreanFont = CreateKoreanFont();
        Font buttonFont = koreanFont != null ? koreanFont : latin;

        // 반투명 검은 화면 (뒤의 게임 클릭 차단)
        overlay = Stretch("Overlay", transform).gameObject.AddComponent<Image>();
        overlay.color = new Color(0f, 0f, 0f, 0f);

        // PAUSE
        title = Rect("Title", transform, new Vector2(0f, 120f), new Vector2(1400f, 200f));
        titleGroup = title.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        var titleText = Label(title, "PAUSE", latin, 130, Color.white);
        var outline = titleText.gameObject.AddComponent<Outline>();
        outline.effectColor = DeepNavy;
        outline.effectDistance = new Vector2(6f, -6f);
        var shadow = titleText.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -12f);

        divider = Rect("Divider", title, new Vector2(0f, -100f), new Vector2(0f, 6f));
        divider.gameObject.AddComponent<Image>().color = Gold;

        // 버튼
        buttonsRoot = Rect("Buttons", transform, new Vector2(0f, -150f), new Vector2(600f, 220f));
        buttonsGroup = buttonsRoot.gameObject.AddComponent<CanvasGroup>();
        buttonsGroup.alpha = 0f;
        buttonsGroup.interactable = false;
        buttonsGroup.blocksRaycasts = false;
        continueButton = MenuButton(buttonsRoot, "Continue Button", "계속하기", new Vector2(0f, 50f), buttonFont);
        mainMenuButton = MenuButton(buttonsRoot, "Main Menu Button", "메인 메뉴", new Vector2(0f, -50f), buttonFont);
        LinkVertical(continueButton, mainMenuButton);
        continueButton.onClick.AddListener(OnContinue);
        mainMenuButton.onClick.AddListener(OnMainMenu);

        // 장면 전환용 완전 검은 화면 (맨 위)
        black = Stretch("Transition Black", transform).gameObject.AddComponent<Image>();
        black.color = new Color(0f, 0f, 0f, 0f);
        black.raycastTarget = false;

        canvas.enabled = false;
    }

    // 버튼을 누르려면 EventSystem 이 필요: 씬에 없으면 메뉴가 열려 있는 동안만 직접 만든다
    private void EnsureEventSystem()
    {
        if (ownEventSystem != null || EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
        ownEventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        ownEventSystem.transform.SetParent(transform, false);
    }

    private void DropOwnEventSystem()
    {
        if (ownEventSystem == null) return;
        Destroy(ownEventSystem);
        ownEventSystem = null;
    }
    #endregion
}
