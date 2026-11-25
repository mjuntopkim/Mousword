using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mousword.MainScreenUI.Editor
{
    /// <summary>
    /// Turns Player_test5's HUD into a prefab and places it in Main/1 - 1 and Main/2.
    /// The HUD in 1 - 1 is bound to that scene's player and survives the transition to 2
    /// (PersistentHud). The copy in 2 only exists so the scene can be played on its own;
    /// at runtime it removes itself when the HUD from 1 - 1 is already present.
    /// Player_test5 itself is left unchanged.
    /// </summary>
    public static class MainScreenUIMainScenes
    {
        private const string HudName = "Main Screen UI";
        private const string SourceScene = "Assets/Scenes/Player_test/Player_test5.unity";
        private const string PrefabPath = "Assets/Prefab/Main Screen UI.prefab";
        private const string FirstScene = "Assets/Scenes/Main/1 - 1.unity";
        private const string SecondScene = "Assets/Scenes/Main/2.unity";

        [MenuItem("Tools/Mousword/Apply HUD To Main Scenes (1-1, 2)")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previousScene = SceneManager.GetActiveScene().path;

            // The fade screen lives on GameManager's overlay canvas; keep the HUD underneath it.
            var first = EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
            var fadeCanvas = Root(first, "GameManager").GetComponentInChildren<Canvas>(true);
            if (fadeCanvas == null) throw new InvalidOperationException("1 - 1 GameManager has no Canvas.");
            int hudOrder = fadeCanvas.sortingOrder - 1;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab(hudOrder);
            else Debug.Log($"HUD prefab already exists, reusing it: {PrefabPath}");

            first = EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
            PlaceInScene(first, prefab, bindPlayer: true);
            var second = EditorSceneManager.OpenScene(SecondScene, OpenSceneMode.Single);
            PlaceInScene(second, prefab, bindPlayer: false);

            if (!string.IsNullOrEmpty(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            Selection.activeObject = prefab;
            Debug.Log($"MAIN_SCENES_HUD_OK: prefab={PrefabPath}, sortingOrder={hudOrder}, placed in 1 - 1 (bound) and 2.");
        }

        private static GameObject CreatePrefab(int sortingOrder)
        {
            var source = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            var copy = UnityEngine.Object.Instantiate(Root(source, HudName));
            try
            {
                copy.name = HudName;
                // A prefab cannot reference scene objects; each placement binds (or looks up) its own player.
                SetReference(copy.GetComponent<PlayerMainScreenUI>(), "player", null);
                SetReference(copy.GetComponent<PlayerSkillCooldownUI>(), "player", null);
                if (copy.GetComponent<PersistentHud>() == null) copy.AddComponent<PersistentHud>();
                var canvas = copy.GetComponent<Canvas>();
                if (canvas != null && canvas.sortingOrder > sortingOrder) canvas.sortingOrder = sortingOrder;

                var prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath, out bool saved);
                if (!saved || prefab == null) throw new InvalidOperationException($"Saving {PrefabPath} failed.");
                return prefab;
            }
            finally
            {
                // Player_test5 is reopened/replaced without saving, so it stays untouched.
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        private static void PlaceInScene(Scene scene, GameObject prefab, bool bindPlayer)
        {
            if (scene.GetRootGameObjects().Any(x => x.name == HudName))
            {
                Debug.LogWarning($"{scene.name} already contains {HudName}; skipped.");
                return;
            }

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            hud.transform.SetAsLastSibling();

            if (bindPlayer)
            {
                var status = scene.GetRootGameObjects()
                    .Where(x => x.CompareTag("Player") && x.activeInHierarchy)
                    .Select(x => x.GetComponent<PlayerStatus>())
                    .Single(x => x != null);
                var controller = status.GetComponent<PlayerSkillController>();
                if (controller == null) throw new InvalidOperationException($"{scene.name} player has no PlayerSkillController.");
                SetReference(hud.GetComponent<PlayerMainScreenUI>(), "player", status);
                SetReference(hud.GetComponent<PlayerSkillCooldownUI>(), "player", controller);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Saving {scene.path} failed.");
        }

        private static GameObject Root(Scene scene, string name)
        {
            var root = scene.GetRootGameObjects().FirstOrDefault(x => x.name == name);
            if (root == null) throw new InvalidOperationException($"{scene.path} has no root object named '{name}'.");
            return root;
        }

        private static void SetReference(Component component, string property, UnityEngine.Object value)
        {
            if (component == null) throw new InvalidOperationException($"HUD is missing a component for '{property}'.");
            var serialized = new SerializedObject(component);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
