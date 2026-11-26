using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 보스(골렘)가 죽으면 엔딩 씬으로 넘어가게 하는 컴포넌트
// 붙이는 곳: Boss 씬의 아무 오브젝트 (예: 빈 오브젝트 "BossClearToEnding")
public class BossClearToEnding : MonoBehaviour
{
    [SerializeField] private string endingSceneName = "Ending";
    [SerializeField] private float delayAfterBossDeath = 2.5f; // 보스 사망 연출을 보여줄 시간(초)

    private bool isTriggered = false;

    private void OnEnable()
    {
        Golem.OnBossDead += HandleBossDead;
    }

    private void OnDisable()
    {
        Golem.OnBossDead -= HandleBossDead;
    }

    private void HandleBossDead()
    {
        if (isTriggered)
        {
            return;
        }

        isTriggered = true;
        StartCoroutine(GoToEnding());
    }

    private IEnumerator GoToEnding()
    {
        // 보스 사망 후에는 플레이어 조작 잠금
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            PlayerMove move = player.GetComponent<PlayerMove>();
            if (move != null)
            {
                move.IsMovementLocked = true;
            }
        }

        yield return new WaitForSeconds(delayAfterBossDeath);

        if (!Application.CanStreamedLevelBeLoaded(endingSceneName))
        {
            Debug.LogError("엔딩 씬이 빌드 씬 목록에 없습니다: " + endingSceneName, this);
            yield break;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TransitionToEnding(endingSceneName);
        }
        else
        {
            // Boss 씬을 단독 테스트하는 등 GameManager가 없을 때
            Debug.LogWarning("BossClearToEnding: GameManager가 없어 페이드와 오브젝트 정리 없이 엔딩 씬을 로드합니다.", this);
            SceneManager.LoadScene(endingSceneName);
        }
    }
}
