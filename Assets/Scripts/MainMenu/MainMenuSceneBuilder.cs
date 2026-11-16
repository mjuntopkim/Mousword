#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class MainMenuSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private static Font font;

    // Also used with Unity's -executeMethod when generating the initial scene.
    [MenuItem("Tools/Mousword/Create Main Menu Scene")]
    public static void CreateScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0, 0, -10);
        var camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.09f, 0.13f);

        var canvasObject = new GameObject("Main Menu Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MainMenuController), typeof(MainMenuPresentation));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var composition = Rect("Pixel Menu Composition", canvasObject.transform, Vector2.zero, new Vector2(1920, 1080));
        var art = Rect("Animated Forest and Sword", composition, Vector2.zero, new Vector2(1920, 1080));
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/MainMenu/PixelForestMenu.png");
        if (texture == null)
            throw new System.InvalidOperationException("PixelForestMenu.png background is missing.");
        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
        var backgroundImage = art.gameObject.AddComponent<RawImage>();
        backgroundImage.texture = texture;
        backgroundImage.raycastTarget = false;
        const string materialPath = "Assets/Art/MainMenu/PixelMenuAtmosphere.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Mousword/MenuAtmosphere"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        backgroundImage.material = material;
        var presentation = new SerializedObject(canvasObject.GetComponent<MainMenuPresentation>());
        presentation.FindProperty("background").objectReferenceValue = backgroundImage;
        presentation.FindProperty("composition").objectReferenceValue = composition;
        presentation.ApplyModifiedPropertiesWithoutUndo();
        var controller = canvasObject.GetComponent<MainMenuController>();
        var mainPanel = Rect("Main Panel", composition, new Vector2(500, -90), new Vector2(450, 420));
        var start = Button("Start Button", mainPanel, "시작", new Vector2(0, 140));
        var options = Button("Options Button", mainPanel, "옵션", new Vector2(0, 14));
        var quit = Button("Quit Button", mainPanel, "종료", new Vector2(0, -112));
        UnityEventTools.AddPersistentListener(start.onClick, controller.StartGame);
        UnityEventTools.AddPersistentListener(options.onClick, controller.OpenOptions);
        UnityEventTools.AddPersistentListener(quit.onClick, controller.QuitGame);

        var optionsPanel = Rect("Options Panel", composition, new Vector2(500, -90), new Vector2(500, 420));
        Label("Options Title", optionsPanel, "옵션", new Vector2(0, 140), new Vector2(500, 80), 52);
        var volumeLabel = Label("Volume Label", optionsPanel, "전체 음량  100%", new Vector2(0, 40), new Vector2(500, 60), 32);
        var sliderRect = Rect("Master Volume Slider", optionsPanel, new Vector2(0, -30), new Vector2(360, 44));
        // Give the whole control a usable hit area, including the space around the thin track.
        sliderRect.gameObject.AddComponent<Image>().color = Color.clear;
        var slider = sliderRect.gameObject.AddComponent<Slider>();
        var background = Rect("Background", sliderRect, Vector2.zero, new Vector2(360, 8));
        background.gameObject.AddComponent<Image>().color = new Color(0.33f, 0.57f, 0.55f);
        var handleArea = Rect("Handle Area", sliderRect, Vector2.zero, new Vector2(336, 44));
        var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(24, 0));
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(1f, 0.79f, 0.34f);
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 1;
        UnityEventTools.AddPersistentListener(slider.onValueChanged, controller.SetVolume);
        var close = Button("Close Options Button", optionsPanel, "닫기", new Vector2(0, -150));
        var closeLabel = close.GetComponentInChildren<Text>();
        closeLabel.alignment = TextAnchor.MiddleCenter;
        closeLabel.fontSize = 52;
        var closeVisual = new SerializedObject(close.GetComponent<MainMenuButtonVisual>());
        closeVisual.FindProperty("centeredLabel").boolValue = true;
        closeVisual.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(close.onClick, controller.CloseOptions);

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("mainPanel").objectReferenceValue = mainPanel.gameObject;
        serialized.FindProperty("optionsPanel").objectReferenceValue = optionsPanel.gameObject;
        serialized.FindProperty("startButton").objectReferenceValue = start;
        serialized.FindProperty("optionsButton").objectReferenceValue = options;
        serialized.FindProperty("closeOptionsButton").objectReferenceValue = close;
        serialized.FindProperty("volumeSlider").objectReferenceValue = slider;
        serialized.FindProperty("volumeLabel").objectReferenceValue = volumeLabel;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        optionsPanel.gameObject.SetActive(false);

        EditorSceneManager.SaveScene(scene, ScenePath);
        // Preserve existing stage order while making the menu the game's entry scene.
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        if (!scenes.Any(s => s.path == MainMenuController.FirstStagePath))
            scenes.Add(new EditorBuildSettingsScene(MainMenuController.FirstStagePath, true));
        foreach (var buildScene in scenes.Where(s => s.path == MainMenuController.FirstStagePath))
            buildScene.enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Main menu created and registered as the first build scene: " + ScenePath);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 0.94f, 0.77f);
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;
        return label;
    }

    private static Button Button(string name, Transform parent, string text, Vector2 position)
    {
        var rect = Rect(name, parent, position, new Vector2(430, 104));
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        var label = Label("Label", rect, text, Vector2.zero, new Vector2(250, 104), 68);
        label.alignment = TextAnchor.MiddleLeft;
        var diamondRect = Rect("Selection Diamond", rect, new Vector2(-165, 0), new Vector2(20, 20));
        diamondRect.localRotation = Quaternion.Euler(0, 0, 45);
        var diamond = diamondRect.gameObject.AddComponent<Image>();
        diamond.color = Color.clear;
        diamond.raycastTarget = false;
        var visual = new SerializedObject(rect.gameObject.AddComponent<MainMenuButtonVisual>());
        visual.FindProperty("label").objectReferenceValue = label;
        visual.FindProperty("diamond").objectReferenceValue = diamond;
        visual.ApplyModifiedPropertiesWithoutUndo();
        return button;
    }
}
#endif
