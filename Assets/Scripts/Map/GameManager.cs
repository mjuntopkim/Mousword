using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField] private string targetSpawnPointName;
    [SerializeField] private Animator fadeAnimator;
    [SerializeField] private float fadeTime = 1f;

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
        targetSpawnPointName = spawnPointName;
        StartCoroutine(LoadSceneSequence(nextSceneName));
    }

    private IEnumerator LoadSceneSequence(string nextScenename)
    {
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

        if(fadeAnimator != null)
        {
            fadeAnimator.SetTrigger("StartFadeIn");
        }
    }

    private void MovePlayerToSpawn()
    {
        GameObject spawnPoint = GameObject.Find(targetSpawnPointName);
        if(spawnPoint != null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if(player != null)
            {
                player.transform.position = spawnPoint.transform.position;
            }
        }
    }
}
