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
