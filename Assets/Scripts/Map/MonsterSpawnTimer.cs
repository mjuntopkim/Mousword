using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class MonsterSpawnTimer : MonoBehaviour
{
    //소환을 맡길 스포너
    [SerializeField] private MonsterSpawner monsterSpawner;

    //소환 간격 (초)
    [SerializeField] private float spawnInterval = 5f;

    //시작하자마자 한 번 소환할지 여부
    [SerializeField] private bool spawnImmediately = true;

    //자동 종료까지의 시간 (0 이하이면 StopTimer()를 호출할 때까지 계속)
    [SerializeField] private float duration = 0f;

    //씬 시작 시 자동으로 타이머 시작
    [SerializeField] private bool startOnAwake = false;

    //남은 몬스터 확인 간격 (초)
    [SerializeField] private float clearCheckInterval = 0.3f;

    //타이머 시작 시 호출
    public UnityEvent onTimerStarted;

    //소환 반복이 끝났을 때 호출 (duration 경과 또는 StopTimer)
    public UnityEvent onTimerEnded;

    //타이머가 끝난 뒤 남은 몬스터까지 모두 죽었을 때 호출 (예: DoorController.OpenDoor 연결)
    public UnityEvent onCleared;

    private Coroutine timerRoutine;
    private Coroutine clearRoutine;

    public bool IsRunning => timerRoutine != null;

    private void Start()
    {
        if (startOnAwake)
        {
            StartTimer();
        }
    }

    //이 시점부터 일정 간격으로 소환 시작
    public void StartTimer()
    {
        if (monsterSpawner == null)
        {
            Debug.LogWarning("MonsterSpawnTimer: MonsterSpawner가 지정되지 않았습니다.");
            return;
        }

        if (timerRoutine != null)
        {
            StopCoroutine(timerRoutine);
        }

        if (clearRoutine != null)
        {
            StopCoroutine(clearRoutine);
            clearRoutine = null;
        }

        timerRoutine = StartCoroutine(TimerRoutine());
        onTimerStarted?.Invoke();
    }

    //반복 소환 종료 (남은 몬스터가 모두 죽으면 onCleared 호출)
    public void StopTimer()
    {
        if (timerRoutine == null)
        {
            return;
        }

        StopCoroutine(timerRoutine);
        EndTimer();
    }

    private IEnumerator TimerRoutine()
    {
        float elapsed = 0f;

        if (spawnImmediately)
        {
            monsterSpawner.RespawnDeadMonsters();
        }

        while (duration <= 0f || elapsed < duration)
        {
            yield return new WaitForSeconds(spawnInterval);
            elapsed += spawnInterval;

            //지속시간이 지났으면 소환하지 않고 종료
            if (duration > 0f && elapsed > duration)
            {
                break;
            }

            monsterSpawner.RespawnDeadMonsters();
        }

        EndTimer();
    }

    private void EndTimer()
    {
        timerRoutine = null;
        onTimerEnded?.Invoke();
        clearRoutine = StartCoroutine(WaitForClearRoutine());
    }

    private IEnumerator WaitForClearRoutine()
    {
        while (monsterSpawner.AliveCount > 0)
        {
            yield return new WaitForSeconds(clearCheckInterval);
        }

        clearRoutine = null;
        onCleared?.Invoke();
    }

    private void OnDisable()
    {
        //비활성화 시에는 이벤트 없이 조용히 정리
        StopAllCoroutines();
        timerRoutine = null;
        clearRoutine = null;
    }
}
