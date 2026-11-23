using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mousword.MainScreenUI.Editor
{
    public static class MainScreenUICopy
    {
        [MenuItem("Tools/Mousword/Copy HUD To Player_test5")]
        public static void CopyToTest5()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var target = EditorSceneManager.OpenScene("Assets/Scenes/Player_test5.unity");
            if (target.GetRootGameObjects().Any(x => x.name == "Main Screen UI"))
                throw new InvalidOperationException("Player_test5 already contains Main Screen UI; no overwrite performed.");
            var status = target.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PlayerStatus>())
                .Single(x => x.isActiveAndEnabled);
            var controller = status.GetComponent<PlayerSkillController>();
            if (controller == null) throw new InvalidOperationException("Target player has no PlayerSkillController.");
            var source = EditorSceneManager.OpenScene("Assets/Scenes/Player_test4.unity", OpenSceneMode.Additive);
            GameObject copy;
            try
            {
                var original = source.GetRootGameObjects().Single(x => x.name == "Main Screen UI");
                copy = UnityEngine.Object.Instantiate(original);
                copy.name = original.name;
                SceneManager.MoveGameObjectToScene(copy, target);
                var statusBinding = new SerializedObject(copy.GetComponent<PlayerMainScreenUI>());
                statusBinding.FindProperty("player").objectReferenceValue = status;
                statusBinding.ApplyModifiedPropertiesWithoutUndo();
                var skills = copy.transform.Find("Safe Area/Skills - Bottom Center");
                var slots = Enumerable.Range(0, 4).Select(i => skills.GetChild(i)
                    .GetComponentInChildren<SkillCooldownUI>(true)).ToArray();
                if (slots.Any(x => x == null)) throw new InvalidOperationException("Source cooldown UI is incomplete.");
                copy.GetComponent<PlayerSkillCooldownUI>().Configure(controller, slots);
            }
            finally { EditorSceneManager.CloseScene(source, true); }
            SceneManager.SetActiveScene(target);
            Canvas.ForceUpdateCanvases();
            EditorSceneManager.MarkSceneDirty(target);
            if (!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Saving Player_test5 failed.");
            Selection.activeGameObject = copy;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("PLAYER_TEST5_HUD_OK: copied full HUD and rebound target player.");
        }
    }
}
