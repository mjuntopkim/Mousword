using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] private string targetSpawnPointName;
    [SerializeField] private Animator fadeAnimator;
    [SerializeField] private float fadeTime = 1f;
    [SerializeField] private float fadeHoldTime = 1f;

    public bool IsTransitioning { get; private set; } = false;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void TransitionToNextScene(string nextSceneName, string spawnPointName)
    {
        if (IsTransitioning)
        {
            return;
        }

        targetSpawnPointName = spawnPointName;
        StartCoroutine(LoadSceneSequence(nextSceneName));
    }

    private IEnumerator LoadSceneSequence(string nextScenename)
    {
        IsTransitioning = true;

        if(fadeAnimator != null)
        {
            fadeAnimator.SetTrigger("StartFadeOut");
        }
        yield return new WaitForSeconds(fadeTime);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextScenename);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        MovePlayerToSpawn();

        yield return new WaitForSeconds(fadeHoldTime);

        if(fadeAnimator != null)
        {
            fadeAnimator.SetTrigger("StartFadeIn");
        }

        IsTransitioning = false;
    }

    // [추가] 엔딩 씬으로 전환 (플레이어/카메라를 데려가지 않음)
    // 페이드 아웃 → 엔딩 씬 로드 → DontDestroyOnLoad로 남아 있던 오브젝트(플레이어, 카메라, HUD, HitFeedback 등)와 GameManager 자신을 모두 삭제
    public void TransitionToEnding(string endingSceneName)
    {
        if (IsTransitioning)
        {
            return;
        }

        StartCoroutine(LoadEndingSequence(endingSceneName));
    }

    private IEnumerator LoadEndingSequence(string endingSceneName)
    {
        IsTransitioning = true;

        if (fadeAnimator != null)
        {
            fadeAnimator.SetTrigger("StartFadeOut");
        }
        yield return new WaitForSeconds(fadeTime);

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(endingSceneName);
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 엔딩 씬의 카메라가 준비된 뒤에 정리해야 화면이 비지 않음
        // GameManager도 DontDestroyOnLoad 씬에 있으므로 gameObject.scene = DontDestroyOnLoad 씬
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root != gameObject)
            {
                Destroy(root);
            }
        }

        // 메인 메뉴 → 1스테이지에서 새 GameManager가 생성되도록 자신도 삭제 (Instance는 자동으로 null 취급)
        Destroy(gameObject);
    }

    private void MovePlayerToSpawn()
    {
        GameObject spawnPoint = GameObject.Find(targetSpawnPointName);
        GameObject player = GameObject.FindWithTag("Player");
        if(spawnPoint != null)
        {
            if(player != null)
            {
                player.transform.position = spawnPoint.transform.position;
            }
        }

        var move = player.GetComponent<PlayerMove>();
        move.IsMovementLocked = false;
    }
}
