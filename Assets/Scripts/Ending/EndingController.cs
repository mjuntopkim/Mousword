using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class EndingController : MonoBehaviour
{
    const string MenuPath = "Assets/Scenes/MainMenu.unity";

    [SerializeField] Button backBtn;

    bool busy;
    Font fnt;

    void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetFont();
    }

    void Update()
    {
        var es = EventSystem.current;
        if (busy || es == null || es.currentSelectedGameObject != null)
            return;

        var k = Keyboard.current;
        var g = Gamepad.current;
        bool kp = k != null && (k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame
            || k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame
            || k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame);
        bool gp = g != null && (g.dpad.up.wasPressedThisFrame || g.dpad.down.wasPressedThisFrame
            || g.leftStick.up.wasPressedThisFrame || g.leftStick.down.wasPressedThisFrame);
        if (kp || gp)
            es.SetSelectedGameObject(backBtn.gameObject);
    }

    public void GoBack()
    {
        if (busy)
            return;

        if (!Application.CanStreamedLevelBeLoaded(MenuPath))
        {
            Debug.LogError("MainMenu 씬이 빌드 씬 목록에 없습니다: " + MenuPath, this);
            return;
        }

        busy = true;
        backBtn.interactable = false;
        SceneManager.LoadSceneAsync(MenuPath, LoadSceneMode.Single);
    }

    void SetFont()
    {
        string[] fs = Font.GetOSInstalledFontNames();
        string[] cs = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" };
        foreach (string c in cs)
        {
            if (Array.IndexOf(fs, c) < 0)
                continue;

            fnt = Font.CreateDynamicFontFromOSFont(c, 32);
            foreach (Text t in GetComponentsInChildren<Text>(true))
                t.font = fnt;
            return;
        }
    }

    void OnDestroy()
    {
        if (fnt != null)
            Destroy(fnt);
    }
}
