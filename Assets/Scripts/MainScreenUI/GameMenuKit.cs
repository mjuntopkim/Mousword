using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 오버 화면과 일시정지 메뉴가 같이 쓰는 코드로 만드는 UI 부품과 메인 메뉴 복귀 처리.
/// </summary>
public static class GameMenuKit
{
    public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    public static readonly Color Cream = new Color(1f, 0.94f, 0.77f);
    public static readonly Color Gold = new Color(1f, 0.79f, 0.34f);

    public static Canvas CreateCanvas(GameObject go, int sortingOrder)
    {
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Text Label(RectTransform rect, string text, Font font, int size, Color color)
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

    /// <summary>크림색 글자 + 선택 시 금색·양옆 다이아몬드 (메인 메뉴와 같은 느낌)</summary>
    public static Button MenuButton(RectTransform parent, string name, string text, Vector2 position, Font font)
    {
        var rect = Rect(name, parent, position, new Vector2(460f, 84f));
        var hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);           // 클릭 영역만 담당
        var button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;

        var label = Label(Stretch("Label", rect), text, font, 48, Cream);
        var labelShadow = label.gameObject.AddComponent<Shadow>();
        labelShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        labelShadow.effectDistance = new Vector2(0f, -4f);

        var left = Diamond("Diamond L", rect, -150f);
        var right = Diamond("Diamond R", rect, 150f);
        rect.gameObject.AddComponent<GameOverButton>().Configure(label, left, right);
        return button;
    }

    /// <summary>두 버튼을 위·아래 키로 오갈 수 있게 연결</summary>
    public static void LinkVertical(Button top, Button bottom)
    {
        top.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = bottom, selectOnUp = bottom };
        bottom.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = top, selectOnDown = top };
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

    // 메인 메뉴와 같은 방식: 설치된 한글 글꼴을 사용 (글꼴 파일을 프로젝트에 넣지 않음)
    public static Font CreateKoreanFont()
    {
        string[] installed = Font.GetOSInstalledFontNames();
        string[] candidates = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" };
        foreach (string name in candidates)
            if (Array.IndexOf(installed, name) >= 0)
                return Font.CreateDynamicFontFromOSFont(name, 48);
        return null;
    }

    public static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    public static float EaseOutBack(float x)
    {
        const float c1 = 1.4f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    /// <summary>
    /// 스테이지 사이에서 유지되던 오브젝트(플레이어·HUD·카메라·GameManager 등)를 정리.
    /// keep 자신, 화면 흔들림([HitFeedback]), 일시정지 메뉴는 남긴다.
    /// </summary>
    public static void DestroyPersistentObjects(GameObject keep)
    {
        foreach (GameObject root in keep.scene.GetRootGameObjects())
        {
            if (root == keep) continue;
            if (root.GetComponent<HitFeedback>() != null || root.GetComponent<PauseMenuUI>() != null) continue;
            UnityEngine.Object.Destroy(root);
        }
    }

    // 빌드 목록에 없는 테스트 씬(Player_test 등)도 에디터에서는 다시 불러올 수 있게 한다
    public static AsyncOperation LoadScene(string path)
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
}
