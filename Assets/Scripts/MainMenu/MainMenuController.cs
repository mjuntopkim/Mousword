using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public const string FirstStagePath = "Assets/Scenes/Main/1 - 1.unity";
    private const string VolumePreference = "MasterVolume";

    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button closeOptionsButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeLabel;

    private bool isLoading;
    private Font menuFont;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ApplyKoreanFont();
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePreference, 1f));
        volumeSlider.SetValueWithoutNotify(AudioListener.volume);
        UpdateVolumeLabel(AudioListener.volume);
        optionsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    private void Start()
    {
        Select(startButton);
    }

    public void StartGame()
    {
        if (isLoading)
            return;

        if (!Application.CanStreamedLevelBeLoaded(FirstStagePath))
        {
            Debug.LogError("1-1 씬이 빌드 씬 목록에 없습니다: " + FirstStagePath, this);
            return;
        }

        isLoading = true;
        startButton.interactable = false;
        PlayerPrefs.Save();
        SceneManager.LoadSceneAsync(FirstStagePath, LoadSceneMode.Single);
    }

    public void OpenOptions()
    {
        if (isLoading)
            return;

        mainPanel.SetActive(false);
        optionsPanel.SetActive(true);
        Select(volumeSlider);
    }

    public void CloseOptions()
    {
        PlayerPrefs.Save();
        optionsPanel.SetActive(false);
        mainPanel.SetActive(true);
        Select(optionsButton);
    }

    public void SetVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat(VolumePreference, volume);
        UpdateVolumeLabel(volume);
    }

    public void QuitGame()
    {
        PlayerPrefs.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void UpdateVolumeLabel(float volume)
    {
        volumeLabel.text = "전체 음량  " + Mathf.RoundToInt(volume * 100f) + "%";
    }

    private static void Select(Selectable selectable)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }

    private void ApplyKoreanFont()
    {
        // Use an installed Korean font without adding a licensed OS font to the project.
        string[] installedFonts = Font.GetOSInstalledFontNames();
        string[] candidates = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" };
        foreach (string candidate in candidates)
        {
            if (Array.IndexOf(installedFonts, candidate) < 0)
                continue;

            menuFont = Font.CreateDynamicFontFromOSFont(candidate, 32);
            foreach (Text label in GetComponentsInChildren<Text>(true))
            {
                // Keep the Latin wordmark's separate display typeface.
                if (Array.Exists(label.text.ToCharArray(), character => character > 127))
                    label.font = menuFont;
            }
            return;
        }
    }

    private void OnDestroy()
    {
        if (menuFont != null)
            Destroy(menuFont);
    }
}
