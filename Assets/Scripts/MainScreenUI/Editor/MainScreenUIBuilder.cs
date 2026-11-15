using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Mousword.MainScreenUI.Editor
{
    public static class MainScreenUIBuilder
    {
        private const string ScenePath = "Assets/Scenes/Player_test4.unity";
        private const string RootName = "Main Screen UI";
        private static Font font;

        [MenuItem("Tools/Mousword/Add Player_test4 Skill Cooldowns")]
        public static void AddCooldowns()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath || Application.isPlaying)
                throw new InvalidOperationException("Open Player_test4 in edit mode first.");
            var root = scene.GetRootGameObjects().Single(x => x.name == RootName);
            var player = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PlayerSkillController>())
                .Single(x => x.isActiveAndEnabled);
            Undo.RegisterFullObjectHierarchyUndo(root, "Add Skill Cooldown UI");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var skills = root.transform.Find("Safe Area/Skills - Bottom Center");
            var views = new SkillCooldownUI[4];
            for (int i = 0; i < 4; i++)
            {
                var slot = skills.GetChild(i);
                var icon = slot.Find("Existing Skill Icon");
                var shade = icon.Find("Cooldown Shade") as RectTransform;
                if (shade == null) shade = Stretch("Cooldown Shade", icon);
                var graphic = shade.GetComponent<SkillCooldownUI>();
                if (graphic == null) graphic = shade.gameObject.AddComponent<SkillCooldownUI>();
                var labelRect = shade.Find("Remaining Seconds") as RectTransform;
                if (labelRect == null)
                    labelRect = Rect("Remaining Seconds", shade, new Vector2(.5f, .5f),
                        new Vector2(.5f, .5f), Vector2.zero, new Vector2(276, 108));
                var label = labelRect.GetComponent<Text>();
                if (label == null) label = Label(labelRect, "", 96, TextAnchor.MiddleCenter);
                label.fontSize = 96;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                labelRect.localScale = Vector3.one / 3;
                graphic.Configure(label);
                views[i] = graphic;
                slot.Find("Key Badge").SetAsLastSibling();
            }
            var binding = root.GetComponent<PlayerSkillCooldownUI>();
            if (binding == null) binding = root.AddComponent<PlayerSkillCooldownUI>();
            binding.Configure(player, views);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MAIN_SCREEN_COOLDOWNS_OK: four authoritative cooldown bindings saved.");
        }

        [MenuItem("Tools/Mousword/Refine Player_test4 Main Screen UI")]
        public static void Refine()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open Player_test4 first.");
            var root = scene.GetRootGameObjects().Single(x => x.name == RootName);
            Undo.RegisterFullObjectHierarchyUndo(root, "Refine Main Screen UI");
            var skills = (RectTransform)root.transform.Find("Safe Area/Skills - Bottom Center");
            skills.sizeDelta = new Vector2(424, 100);
            for (int i = 0; i < skills.childCount; i++)
            {
                var slot = (RectTransform)skills.GetChild(i);
                slot.anchoredPosition = new Vector2(i * 108, 0);
                slot.sizeDelta = new Vector2(100, 100);
                slot.GetComponent<Image>().color = Color.clear;
            }
            string[] keys = { "Shift", "Q", "E", "R" };
            for (int i = 0; i < skills.childCount; i++)
            {
                var slot = skills.GetChild(i);
                var label = (slot.Find("Key Badge/Key Label") ?? slot.Find("Key Label")).GetComponent<Text>();
                var badge = slot.Find("Key Badge") as RectTransform;
                if (badge == null)
                {
                    badge = Rect("Key Badge", slot, new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(2, -2), new Vector2(i == 0 ? 52 : 28, 26));
                    AddImage(badge, new Color32(12, 20, 28, 230));
                }
                badge.SetAsLastSibling();
                label.rectTransform.SetParent(badge, false);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
                label.rectTransform.pivot = new Vector2(.5f, .5f);
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.rectTransform.sizeDelta = badge.sizeDelta * 3;
                label.rectTransform.localScale = Vector3.one / 3;
                label.alignment = TextAnchor.MiddleCenter;
                label.fontSize = (i == 0 ? 16 : 20) * 3;
                label.text = keys[i];
            }
            foreach (var label in root.GetComponentsInChildren<Text>(true))
            {
                if (label.name == "Remaining Seconds" || label.name == "Key Label") continue;
                // Rasterize at triple resolution, then display at the intended UI size.
                // Reapplying the command is safe: restore the logical rectangle first.
                var rect = label.rectTransform;
                if (rect.localScale.x < .5f) rect.sizeDelta /= 3;
                int size = label.name == "Key Label" ? 24 : 28;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, label.name == "Key Label" ? 32 : 30) * 3;
                rect.localScale = Vector3.one / 3;
                label.fontSize = size * 3;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.fontStyle = FontStyle.Normal;
                var shadow = label.GetComponent<Shadow>();
                if (shadow != null) shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }
            root.transform.localScale = Vector3.one;
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("MAIN_SCREEN_UI_REFINE_OK: sharp text, equal slot spacing, scene canvas visible.");
        }

        [MenuItem("Tools/Mousword/Build Player_test4 Main Screen UI")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var players = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PlayerStatus>(true))
                .Where(x => x.gameObject.activeInHierarchy && x.enabled).ToArray();
            if (players.Length != 1) throw new InvalidOperationException("Expected exactly one PlayerStatus in Player_test4.");
            if (scene.GetRootGameObjects().Any(x => x.name == RootName))
                throw new InvalidOperationException("Main Screen UI already exists; refusing to overwrite it.");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(PlayerMainScreenUI));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var safe = Stretch("Safe Area", root.transform);
            var vitals = Rect("Vitals - Top Left", safe, new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -40), new Vector2(420, 140));
            var hp = Gauge(vitals, "Health", 0, 420, 26, new Color32(210, 70, 79, 255), "HP   100 / 100", out var hpText);
            var mp = Gauge(vitals, "Mana", -76, 336, 18, new Color32(66, 149, 211, 255), "MP   50 / 50", out var mpText);
            var skills = Rect("Skills - Bottom Center", safe, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 36), new Vector2(446, 124));
            string[] assets = { "Dash", "Spin", "Wave", "Ultimate" };
            string[] names = { "Dash - 대시", "Spin - 회전베기", "Sword Wave - 검기", "Ultimate - 궁극기" };
            string[] keys = { "Shift", "Q", "E", "R" };
            for (int i = 0; i < assets.Length; i++)
            {
            float x = i * 116;
                var slot = Rect(names[i], skills, Vector2.zero, Vector2.zero, new Vector2(x, 0), new Vector2(100, 124));
                AddImage(slot, new Color32(10, 23, 33, 175));
                var iconRect = Rect("Existing Skill Icon", slot, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -4), new Vector2(92, 92));
                var icon = iconRect.gameObject.AddComponent<RawImage>();
                icon.raycastTarget = false;
                string path = $"Assets/Picture/SkillIcons/Skill_{assets[i]}.png";
                icon.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icon.texture == null) throw new InvalidOperationException("Missing skill texture: " + path);
                var key = Rect("Key Label", slot, Vector2.zero, Vector2.zero, new Vector2(0, 3), new Vector2(100, 24));
                Label(key, keys[i], 17, TextAnchor.MiddleCenter);
            }
            root.GetComponent<PlayerMainScreenUI>().Configure(players[0], hp, mp, hpText, mpText, safe);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
            Refine();
            Debug.Log("MAIN_SCREEN_UI_BUILD_OK: Player_test4; four existing icons; read-only HP/MP binding.");
        }

        private static RectTransform Gauge(Transform parent, string name, float y, float width, float height, Color color, string initial, out Text text)
        {
            var group = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(width, 62));
            var label = Rect("Value", group, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(width, 26));
            text = Label(label, initial, 20, TextAnchor.MiddleLeft);
            var border = Rect("Silver Edge", group, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -30), new Vector2(width, height));
            AddImage(border, new Color32(157, 172, 183, 255));
            var track = Stretch("Dark Track", border, 2);
            AddImage(track, new Color32(14, 24, 33, 255));
            var inset = Stretch("Fill Area", track, 2);
            var fill = Stretch("Fill", inset);
            AddImage(fill, color);
            var shine = Stretch("Pixel Highlight", fill);
            shine.anchorMin = new Vector2(0, .75f);
            AddImage(shine, new Color(1, 1, 1, .28f));
            return fill;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretch(string name, Transform parent, float inset = 0)
        {
            var rect = Rect(name, parent, Vector2.zero, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            return rect;
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(RectTransform rect, string value, int size, TextAnchor alignment)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.text = value;
            text.color = new Color32(244, 234, 210, 255);
            text.raycastTarget = false;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .9f);
            shadow.effectDistance = new Vector2(1, -2);
            return text;
        }
    }
}
