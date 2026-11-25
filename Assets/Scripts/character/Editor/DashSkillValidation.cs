using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch smoke checks and a rendered review of the real dash renderer.</summary>
public static class DashSkillValidation
{
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int assertions;

    [MenuItem("Tools/Mousword/Validate Dash Visuals")]
    public static void Run()
    {
        assertions = 0;
        var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var sheet = Resources.Load<Texture2D>("Dash/PlayerDash");
            Check(sheet != null && sheet.width == 192 && sheet.height == 40, "Four 48x40 frames load from Resources");
            Check(sheet.filterMode == FilterMode.Point, "Point filtering preserves pixels");
            for (int i = 0; i < 4; i++)
            {
                var pixels = sheet.GetPixels(i * 48, 0, 48, 40);
                Check(pixels.Count(c => c.a > 0.5f) > 150, "Frame " + i + " contains a complete pose");
                Check(pixels.Any(c => c.g > 0.7f && c.r < 0.3f), "Frame " + i + " retains green eyes");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player 1(MapScene Test).prefab");
            Check(prefab != null, "Gameplay player prefab exists");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var controller = player.GetComponent<PlayerSkillController>();
            var movement = player.GetComponent<PlayerMove>();
            var body = player.GetComponent<SpriteRenderer>();
            var rigid = player.GetComponent<Rigidbody2D>();
            player.GetComponent<Animator>().enabled = false;
            body.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Picture/character/mousword_character-Sheet.png")
                .OfType<Sprite>().First(s => s.name == "mousword_character-Sheet_0");
            Call(controller, "Start");
            var skill = controller.DashSkillData;
            Check(skill != null, "Existing dash skill stays connected");

            // Drive the exact gameplay coroutine without relying on keyboard input.
            body.flipX = false;
            float gravity = rigid.gravityScale;
            var collision = rigid.collisionDetectionMode;
            var routine = (IEnumerator)Call(controller, "DashCoroutine", skill);
            Check(routine.MoveNext(), "Dash enters its active phase");
            var visuals = player.GetComponent<PlayerDashVisuals>();
            Check(visuals != null && body.forceRenderingOff, "Dedicated dash pose replaces only the body renderer");
            Check(!movement.enabled && rigid.gravityScale == 0f, "Normal movement yields to dash physics");
            Check(rigid.linearVelocity.x > skill.dashForce * 1.5f, "Right dash bursts out faster than its average speed");
            Check(rigid.collisionDetectionMode == collision, "Dash preserves the existing collision detection mode");
            var pose = player.transform.Find("Dash Pose");
            Check(pose.localScale.x > pose.localScale.y, "Dash pose starts stretched along the dash");
            // Drain the per-physics-step speed curve: it decelerates and keeps the old travel distance.
            float travelled = 0f, previousSpeed = float.MaxValue;
            bool decelerating = true;
            int steps = 0;
            do
            {
                float speed = rigid.linearVelocity.x;
                travelled += speed * Time.fixedDeltaTime;
                decelerating &= speed <= previousSpeed + 1e-4f;
                previousSpeed = speed;
                steps++;
            } while (routine.MoveNext() && steps < 1000);
            Check(steps < 1000, "Dash finishes after the active interval");
            Check(decelerating && previousSpeed > 0f, "Dash speed eases out without stopping early");
            float expected = skill.dashForce * PlayerSkillController.DashPhysicsSteps(skill.dashDuration) * Time.fixedDeltaTime;
            Check(steps == PlayerSkillController.DashPhysicsSteps(skill.dashDuration), "Dash lasts its physics steps");
            Check(Mathf.Abs(travelled - expected) <= expected * 0.02f, "Dash keeps its previous travel distance");
            Check(movement.enabled && rigid.gravityScale == gravity && rigid.collisionDetectionMode == collision, "Movement and physics restore after dash");
            Call(visuals, "Tick", 0.08f);
            Check(!body.forceRenderingOff, "Recovery restores normal sprite visibility");

            body.flipX = true;
            movement.enabled = false;
            routine = (IEnumerator)Call(controller, "DashCoroutine", skill);
            routine.MoveNext();
            Check(rigid.linearVelocity.x < -skill.dashForce, "Left dash follows facing direction");
            Check(player.transform.Find("Dash Pose").GetComponent<SpriteRenderer>().flipX, "Left dash mirrors its dedicated pose");
            ((IDisposable)routine).Dispose();
            Check(!movement.enabled, "Cleanup preserves an already disabled movement component");
            Call(controller, "OnDisable");
            Check(!body.forceRenderingOff && rigid.gravityScale == gravity, "Interruption restores visibility and gravity");

            // Air dash emits wind, with no ground-only dust.
            Call(visuals, "OnDisable");
            visuals.Begin(body, 1f, 0.18f, false);
            Check(ActiveParticles(player) == 3 + 12, "Air dash starts with three wind lines and a wind ring, no dust");
            Call(visuals, "OnDisable");
            Check(ActiveParticles(player) == 0 && !body.forceRenderingOff, "Disabling visuals clears every pooled effect");

            // Render two directions with actual pooled trails in an isolated scene.
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            body.sharedMaterial = material;
            foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>())
                if (renderer != body && renderer.name != "Dash Pose") renderer.enabled = false;
            player.transform.position = new Vector3(-2f, 1.1f, 0f);
            visuals.Begin(body, 1f, 0.18f, true);
            for (int i = 0; i < 3; i++)
            {
                player.transform.position += Vector3.right * 0.65f;
                Call(visuals, "Tick", 0.041f);
            }
            Check(ActiveParticles(player) <= 64, "Effect pool stays bounded");
            Check(ActiveParticles(player) >= 6, "Moving dash leaves visible wind and ghost particles");
            File.WriteAllLines("output/DashSkill/particle-review.txt", player.GetComponentsInChildren<SpriteRenderer>()
                .Where(r => r.name.StartsWith("Dash Particle ") && r.enabled)
                .Select(r => r.name + " position=" + r.transform.position + " scale=" + r.transform.lossyScale +
                    " alpha=" + r.color.a + " bounds=" + r.bounds + " hidden=" + r.forceRenderingOff));
            Check(player.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Dash Particle ") && r.enabled)
                .All(r => r.sortingOrder > body.sortingOrder && r.sortingOrder < player.transform.Find("Dash Pose").GetComponent<SpriteRenderer>().sortingOrder),
                "Effects render above the background and below the dash pose");
            var cameraObject = new GameObject("Dash Review Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.8f;
            camera.transform.position = new Vector3(-0.5f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.105f);
            Directory.CreateDirectory("output/DashSkill");
            Capture(camera, "output/DashSkill/Dash-right.png");
            Call(visuals, "OnDisable");
            player.transform.position = new Vector3(1f, -0.5f, 0f);
            visuals.Begin(body, -1f, 0.18f, false);
            for (int i = 0; i < 3; i++)
            {
                player.transform.position += Vector3.left * 0.65f;
                Call(visuals, "Tick", 0.041f);
            }
            Capture(camera, "output/DashSkill/Dash-left-air.png");
            Call(visuals, "Tick", 0f);
            Check(body.forceRenderingOff, "Zero time step preserves active dash pose");
            Call(visuals, "OnDisable");
            UnityEngine.Object.DestroyImmediate(material);
            File.WriteAllText("output/DashSkill/validation.txt", "PASS: " + assertions + " dash checks.\n" +
                "Validated production prefab/coroutine, both directions, air/ground effects, recovery, interrupted cleanup, bounded pool, and pause.\n");
            Debug.Log("DASH VALIDATION PASS: " + assertions + " checks");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
        finally
        {
            foreach (var visual in UnityEngine.Object.FindObjectsByType<PlayerDashVisuals>(FindObjectsSortMode.None))
                if (visual.gameObject.scene == scene) Call(visual, "OnDisable");
            EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
        }
    }

    private static int ActiveParticles(GameObject player) => player.GetComponentsInChildren<SpriteRenderer>()
        .Count(r => r.name.StartsWith("Dash Particle ") && r.enabled);

    private static object Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Dash check failed: " + description);
        assertions++;
    }

    private static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(960, 480, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(960, 480, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 960, 480), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(image);
    }
}
