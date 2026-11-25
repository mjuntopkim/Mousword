using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 타격감 공용 연출: 화면 흔들림 + 히트스톱.
/// - 흔들림은 Cinemachine이 카메라를 옮긴 뒤, 화면을 그리는 순간에만 카메라를 살짝 밀었다가 되돌린다.
/// - 히트스톱은 Time.timeScale 을 잠깐 0으로 만든다.
/// - 모든 타이밍은 실제 시간(unscaled)으로 흐르며, 킥 흔들림은 히트스톱이 끝난 뒤 시작한다.
/// 처음 호출될 때 숨겨진 오브젝트를 하나 만들어 씬이 바뀌어도 유지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class HitFeedback : MonoBehaviour
{
    private const float KickPeak = 0.2f;     // 흔들림 길이 중 가장 크게 밀리는 지점 (0~1)
    private const float JitterRatio = 0.35f; // 방향 킥 대비 랜덤 떨림 크기
    private const float MaxStep = 0.05f;     // 프레임이 끊겨도 한 번에 건너뛰지 않도록

    private static HitFeedback instance;
    private static bool quitting;

    // 킥 흔들림
    private Vector2 kickDir;
    private float kickStrength, kickTime, kickElapsed, kickJitter;
    private bool kicking;

    // 히트스톱
    private float stopRemaining, prevTimeScale;
    private bool stopping;

    // 다른 연출(궁극기 등)이 매 프레임 넣어주는 흔들림
    private readonly Dictionary<object, Vector3> external = new Dictionary<object, Vector3>();

    private Camera cam;
    private Vector3 offset, savedCamPos;
    private Camera appliedCam;
    private bool applied;

    public static bool IsHitStopped => instance != null && instance.stopping;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() // 도메인 리로드를 끈 에디터에서도 플레이마다 초기화
    {
        instance = null;
        quitting = false;
    }

    private static HitFeedback Instance
    {
        get
        {
            if (instance != null) return instance;
            if (!Application.isPlaying || quitting) return null;
            var go = new GameObject("[HitFeedback]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitFeedback>();
            return instance;
        }
    }

    #region 공개 API

    /// <summary>
    /// 방향성 화면 흔들림. 화면(그려지는 세상)이 direction 쪽으로 "툭" 밀렸다가 떨리며 돌아온다.
    /// strength = 최대 밀림 폭 / 화면 높이. 이미 더 센 흔들림이 진행 중이면 무시한다.
    /// </summary>
    public static void Shake(Vector2 direction, float strength, float duration)
    {
        StartKick(direction, strength, duration, JitterRatio);
    }

    /// <summary>떨림 없이 화면이 direction 쪽으로 한 번 부드럽게 밀렸다가 돌아오는 킥 (대시 출발 등).</summary>
    public static void Kick(Vector2 direction, float strength, float duration)
    {
        StartKick(direction, strength, duration, 0f);
    }

    private static void StartKick(Vector2 direction, float strength, float duration, float jitter)
    {
        if (strength <= 0f || duration <= 0f) return;
        HitFeedback s = Instance;
        if (s == null) return;
        if (s.kicking && s.RemainingKickStrength() > strength) return;

        s.kickDir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.zero;
        s.kickStrength = strength;
        s.kickTime = duration;
        s.kickJitter = jitter;
        s.kickElapsed = 0f;
        s.kicking = true;
    }

    /// <summary>게임 전체를 duration(실제 시간) 동안 멈춘다. 진행 중이면 더 긴 쪽으로 연장.</summary>
    public static void HitStop(float duration)
    {
        if (duration <= 0f) return;
        HitFeedback s = Instance;
        if (s == null) return;

        if (s.stopping)
        {
            s.stopRemaining = Mathf.Max(s.stopRemaining, duration);
            return;
        }
        if (Time.timeScale <= 0f) return; // 이미 멈춰 있음 (일시정지 등) → 건드리지 않음

        s.prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        s.stopping = true;
        s.stopRemaining = duration;
    }

    /// <summary>owner 가 직접 계산한 흔들림 오프셋(월드 단위)을 이번 프레임부터 적용. 끝나면 ClearShakeOffset.</summary>
    public static void SetShakeOffset(object owner, Vector3 worldOffset)
    {
        if (owner == null) return;
        HitFeedback s = Instance;
        if (s == null) return;
        s.external[owner] = worldOffset;
    }

    public static void ClearShakeOffset(object owner)
    {
        if (owner == null || instance == null) return;
        instance.external.Remove(owner);
    }

    #endregion

    private float RemainingKickStrength()
    {
        if (!kicking || kickTime <= 0f) return 0f;
        return kickStrength * Mathf.Clamp01(1f - kickElapsed / kickTime);
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        RestoreCamera();
        EndHitStop();
    }

    private void OnApplicationQuit()
    {
        quitting = true;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);

        if (stopping)
        {
            stopRemaining -= dt;
            if (stopRemaining <= 0f) EndHitStop();
        }

        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;

        Vector3 o = Vector3.zero;

        // 킥 흔들림: 히트스톱 동안에는 멈춰 있다가 끝난 뒤 시작
        if (kicking && !stopping)
        {
            kickElapsed += dt;
            float u = kickElapsed / kickTime;
            if (u >= 1f)
            {
                kicking = false;
            }
            else if (cam != null)
            {
                float screenH = cam.orthographic ? cam.orthographicSize * 2f : 10f;
                float amp = kickStrength * screenH;
                // 빠르게 최대로 밀렸다가(0→KickPeak) 제곱 곡선으로 돌아옴
                float push = u < KickPeak ? u / KickPeak : Sq((1f - u) / (1f - KickPeak));
                float jitter = Sq(1f - u) * kickJitter;
                Vector2 r = kickDir * push;
                if (jitter > 0f) r += Random.insideUnitCircle * jitter;
                // 화면이 kickDir 쪽으로 밀려 보이도록 카메라는 반대로 이동
                o -= (Vector3)(r * amp);
            }
        }

        foreach (Vector3 v in external.Values) o += v;
        offset = o;
    }

    private static float Sq(float x) => x * x;

    private void EndHitStop()
    {
        if (!stopping) return;
        stopping = false;
        stopRemaining = 0f;
        if (Time.timeScale == 0f) Time.timeScale = prevTimeScale; // 그 사이 다른 곳에서 바꿨다면 건드리지 않음
    }

    private void RestoreCamera()
    {
        if (applied && appliedCam != null) appliedCam.transform.position = savedCamPos;
        applied = false;
        appliedCam = null;
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (applied || camera != cam || offset == Vector3.zero) return;
        savedCamPos = camera.transform.position;
        camera.transform.position = savedCamPos + offset;
        appliedCam = camera;
        applied = true;
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!applied || camera != appliedCam) return;
        RestoreCamera();
    }
}
