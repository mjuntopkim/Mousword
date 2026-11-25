using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch checks and rendered frames for the sword wave cast, using the Player_test5 player.</summary>
public static class SwordWaveValidation
{
    private const string ScenePath = "Assets/Scenes/Player_test/Player_test5.unity";
    private const string OutDir = "output/SwordWave";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly float[] Durations = Array.ConvertAll(
        new[] { 0.05f, 0.055f, 0.06f, 0.04f, 0.025f, 0.025f, 0.09f, 0.05f, 0.05f, 0.05f, 0.05f },
        d => d / PlayerSwordWaveVisuals.SpeedMultiplier);
    private static int assertions;

    [MenuItem("Tools/Mousword/Validate Sword Wave Visuals")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        assertions = 0;
        Directory.CreateDirectory(OutDir);
        Camera camera = null;
        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var swordSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Picture/mousword_sword.png").OfType<Sprite>().First();
            var controller = UnityEngine.Object.FindObjectsByType<PlayerSkillController>(FindObjectsSortMode.None)
                .FirstOrDefault(c => c.GetComponentsInChildren<SpriteRenderer>(true).Any(r => r.sprite == swordSprite));
            Check(controller != null, "Player_test5 player carries mousword_sword");
            var animator = controller.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            Call(controller, "Start");
            var body = (SpriteRenderer)Field(controller, "bodySpriteRenderer");
            var pivot = (Transform)Field(controller, "weaponPivot");
            Check(body != null && pivot != null, "Body renderer and weapon pivot are connected");
            var weapon = pivot.GetComponentsInChildren<SpriteRenderer>().First(r => r.sprite == swordSprite);
            var skill = controller.SwordWaveSkillData;
            Check(skill != null && skill.projectilePrefab != null, "Sword wave skill and projectile prefab are connected");

            camera = new GameObject("SwordWave Review Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.16f, 0.21f);
            camera.transform.position = body.bounds.center + new Vector3(1.2f, 0.4f, -10f);

            // ---------- Facing right, aiming slightly up ----------
            pivot.rotation = Quaternion.Euler(0f, 0f, 15f);
            var move = controller.GetComponent<PlayerMove>();
            var playerRigid = controller.GetComponent<Rigidbody2D>();
            float gravity = playerRigid.gravityScale;
            bool moveEnabled = move != null && move.enabled;
            var routine = (IEnumerator)Call(controller, "SwordWaveCoroutine", skill);
            Check(routine.MoveNext(), "Cast enters the wind-up");
            bool stalled = (bool)Field(controller, "airStalling");
            if (stalled) Check(playerRigid.gravityScale == 0f && !move.enabled, "Air cast holds the player in place");
            var visuals = controller.GetComponent<PlayerSwordWaveVisuals>();
            Check(visuals != null && visuals.IsPlaying, "Cast visuals start");
            Check((bool)Field(controller, "isCastingSwordWave"), "Controller marks the cast as active");
            Check(body.forceRenderingOff && weapon.forceRenderingOff, "Body and mouse-aimed sword are hidden during the swing");
            Check(!body.flipX, "Right aim faces the body right");

            var rig = body.transform.Find("SwordWave Rig");
            var arm = rig.Find("Arm");
            var pose = body.transform.Find("SwordWave Pose").GetComponent<SpriteRenderer>();
            var skillSword = arm.Find("Skill Sword").GetComponent<SpriteRenderer>();
            var smear = rig.Find("Smear").GetComponent<SpriteRenderer>();
            Check(skillSword.sprite == swordSprite, "Swing uses the mousword_sword sprite");
            Check(Near(Abs(skillSword.transform.lossyScale), Abs(weapon.transform.lossyScale)), "Swing sword keeps the on-screen sword size " + skillSword.transform.lossyScale.ToString("F4") + " vs " + weapon.transform.lossyScale.ToString("F4"));
            Check(Mathf.Abs(pose.bounds.size.y - body.bounds.size.y * 1.5f) < 0.01f, "48px pose cell matches the 32px sheet scale");
            Check(Mathf.Abs(visuals.PixelWorldSize - body.bounds.size.y / 32f) < 0.0001f, "Pixel unit matches the sheet");

            Check(Mathf.Abs(PlayerSwordWaveVisuals.Duration - 0.545f / 1.5f) < 0.0005f, "Cast plays 1.5x faster");
            Check(Mathf.Abs(PlayerSwordWaveVisuals.ReleaseTime - 0.255f / 1.5f) < 0.0005f, "Wave releases 1.5x sooner");
            float[] angles ={ 190f, 150f, 105f, 100f, 45f, -5f, -38f, -48f, -40f, -32f, -25f };
            var glint = rig.Find("Glint").GetComponent<SpriteRenderer>();
            Call(visuals, "Tick", 0.001f);
            for (int i = 0; i < Durations.Length; i++)
            {
                if (i > 0) Call(visuals, "Tick", Durations[i - 1]);
                Check(pose.sprite.name == "SwordWavePose_" + i, "Pose frame " + i + " plays on time");
                Check(Mathf.Abs(Mathf.DeltaAngle(arm.localEulerAngles.z, angles[i])) < 0.1f, "Sword angle frame " + i);
                Check(smear.enabled == (i >= 4 && i <= 7), "Smear visibility frame " + i);
                Check(glint.enabled == (i == 2 || i == 3 || i == 6), "Glint visibility frame " + i);
                Check((skillSword.sortingOrder > pose.sortingOrder) == (i >= 4), "Sword layering frame " + i);
                Capture(camera, OutDir + "/right_" + i + ".png");
            }

            Check(routine.MoveNext(), "Cast reaches the impact frame");
            var wave = UnityEngine.Object.FindObjectsByType<SwordWave>(FindObjectsSortMode.None).Single();
            Check(Quaternion.Angle(wave.transform.rotation, Quaternion.Euler(0f, 0f, 15f)) < 0.1f, "Wave flies toward the mouse aim");
            var waveRenderer = wave.GetComponent<SpriteRenderer>();
            Check(waveRenderer.sprite != null && waveRenderer.sprite.name == "SwordWaveProjectile_0", "Wave plays the pixel crescent");
            float pop = (float)Field(wave, "currentPop");
            Check(pop > 1.2f, "Wave pops out larger on release");
            Check(Mathf.Abs(waveRenderer.sprite.bounds.size.y * Mathf.Abs(wave.transform.lossyScale.y) / pop - 80f * visuals.PixelWorldSize) < 0.001f,
                "Wave pixels match the character pixel size");
            Check(Vector2.Dot(wave.GetComponent<Rigidbody2D>().linearVelocity, wave.transform.right) > skill.projectileSpeed * 1.9f,
                "Wave launches at boosted speed");
            Call(wave, "SetPop", 1f); // 리뷰 캡처는 원래 크기로
            var box = wave.GetComponent<BoxCollider2D>();
            Check(box == null || box.size.y * Mathf.Abs(wave.transform.lossyScale.y) > 40f * visuals.PixelWorldSize, "Wave hit box covers the crescent");
            for (int f = 0; f < 4; f++)
            {
                wave.transform.position += wave.transform.right * 0.9f;
                waveRenderer.sprite = Field(wave, "frames") is Sprite[] frames ? frames[2 + f % 4] : waveRenderer.sprite;
            }
            Capture(camera, OutDir + "/right_wave_flight.png");
            UnityEngine.Object.DestroyImmediate(wave.gameObject);

            Call(visuals, "Tick", 1f);
            Check(!visuals.IsPlaying && !body.forceRenderingOff && !weapon.forceRenderingOff, "Body and sword return after the cast");
            Check(!routine.MoveNext(), "Cast coroutine finishes");
            Check(!(bool)Field(controller, "isCastingSwordWave"), "Controller allows the next cast");
            Check(!(bool)Field(controller, "airStalling") && playerRigid.gravityScale == gravity && (move == null || move.enabled == moveEnabled),
                "Air stall releases gravity and movement after the cast");

            // ---------- Facing left ----------
            pivot.rotation = Quaternion.Euler(0f, 0f, 170f);
            camera.transform.position = body.bounds.center + new Vector3(-1.2f, 0.4f, -10f);
            routine = (IEnumerator)Call(controller, "SwordWaveCoroutine", skill);
            routine.MoveNext();
            Check(body.flipX && pose.flipX && rig.localScale.x < 0f, "Left aim mirrors body, pose and swing");
            Call(visuals, "Tick", 0.001f);
            for (int i = 0; i < Durations.Length; i++)
            {
                if (i > 0) Call(visuals, "Tick", Durations[i - 1]);
                Capture(camera, OutDir + "/left_" + i + ".png");
            }
            routine.MoveNext();
            wave = UnityEngine.Object.FindObjectsByType<SwordWave>(FindObjectsSortMode.None).Single();
            Check((wave.transform.right).x < 0f, "Left wave flies left");
            Check(wave.GetComponent<SpriteRenderer>().flipY, "Left wave keeps its hook on top");
            wave.transform.position += wave.transform.right * 1.2f;
            Capture(camera, OutDir + "/left_wave_flight.png");
            UnityEngine.Object.DestroyImmediate(wave.gameObject);

            // ---------- Regression: sword wave cast during the dash landing pose ----------
            // Previously the cast saved the dash's temporary hide as the body's real state and left the body invisible.
            Call(visuals, "Tick", 1f);
            routine.MoveNext();
            foreach (var w in UnityEngine.Object.FindObjectsByType<SwordWave>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(w.gameObject);
            Check(!body.forceRenderingOff, "Body visible before the dash sequence");
            var dashRoutine = (IEnumerator)Call(controller, "DashCoroutine", controller.DashSkillData);
            while (dashRoutine.MoveNext()) { } // 대시는 물리 스텝마다 멈추므로 끝까지 돌려 착지 포즈 상태로
            var dashVisuals = controller.GetComponent<PlayerDashVisuals>();
            Check(body.forceRenderingOff, "Dash landing pose still hides the body");
            routine = (IEnumerator)Call(controller, "SwordWaveCoroutine", skill);
            routine.MoveNext();
            Call(dashVisuals, "Tick", 0.08f);
            Call(visuals, "Tick", 0.001f);
            Check(body.forceRenderingOff && pose.enabled, "Cast pose stays visible after the dash landing pose ends");
            Call(visuals, "Tick", 1f);
            routine.MoveNext();
            routine.MoveNext();
            foreach (var w in UnityEngine.Object.FindObjectsByType<SwordWave>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(w.gameObject);
            Check(!body.forceRenderingOff, "Body is visible after dash then sword wave");

            // Dash right after a cast, while the dash pose replaces the body.
            routine = (IEnumerator)Call(controller, "SwordWaveCoroutine", skill);
            routine.MoveNext();
            Call(dashVisuals, "Begin", body, 1f, 0.18f, true);
            Call(dashVisuals, "OnDisable");
            Check(!body.forceRenderingOff, "Body is visible after a dash interrupts the cast visuals");
            Call(controller, "OnDisable");

            // ---------- Interruption ----------
            routine = (IEnumerator)Call(controller, "SwordWaveCoroutine", skill);
            routine.MoveNext();
            Call(controller, "OnDisable");
            Check(!visuals.IsPlaying && !body.forceRenderingOff && !weapon.forceRenderingOff, "Interrupted cast restores visibility");
            Check(!(bool)Field(controller, "isCastingSwordWave"), "Interrupted cast clears the casting flag");
            Check(!(bool)Field(controller, "airStalling") && playerRigid.gravityScale == gravity, "Interrupted cast releases the air stall");

            File.WriteAllText(OutDir + "/validation.txt", "PASS: " + assertions + " sword wave checks.\n");
            Debug.Log("SWORD WAVE VALIDATION PASS: " + assertions + " checks");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            File.WriteAllText(OutDir + "/validation.txt", "FAIL after " + assertions + " checks: " + e.Message + "\n");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
        finally
        {
            if (camera != null) UnityEngine.Object.DestroyImmediate(camera.gameObject);
            if (!Application.isBatchMode && !string.IsNullOrEmpty(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
        }
    }

    private static object Call(object target, string method, params object[] args)
    {
        var info = target.GetType().GetMethod(method, Private | BindingFlags.Public);
        if (info == null) throw new MissingMethodException(target.GetType().Name, method);
        return info.Invoke(target, args);
    }

    private static object Field(object target, string name)
    {
        var info = target.GetType().GetField(name, Private | BindingFlags.Public);
        if (info == null) throw new MissingFieldException(target.GetType().Name, name);
        return info.GetValue(target);
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    private static bool Near(Vector3 a, Vector3 b) => ((Vector2)a - (Vector2)b).magnitude < 0.001f; // z scale is irrelevant for 2D sprites

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Sword wave check failed: " + description);
        assertions++;
    }

    private static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(800, 480, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(800, 480, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 800, 480), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(image);
    }
}
