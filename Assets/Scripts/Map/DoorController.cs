using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class DoorController : MonoBehaviour
{
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private ParticleSystem dustParticle;

    [SerializeField] private string dropTriggerName = "Drop";
    [SerializeField] private string dropStateName = "RockDrop";
    [SerializeField] private string speedParameterName = "Speed";

    [SerializeField] private float openDuration = 0.5f;
    [SerializeField] private float doorDropDuration = 0.25f;
    [SerializeField] private float afterCloseDelay = 0f; // 닫힘 애니메이션 종료 후 추가 대기 시간

    // 문 닫힘 애니메이션이 완전히 끝난 뒤 호출 (예: StagePortal.Nextstage 연결)
    public UnityEvent onDoorClosed;

    private List<GameObject> trackedMonsters = new List<GameObject>();
    private bool isClose = false;

    private void Awake()
    {
        if (doorAnimator == null)
        {
            doorAnimator = GetComponent<Animator>();
        }

        if (doorCollider == null)
        {
            doorCollider = GetComponent<Collider2D>();
        }
    }

    public void CloseDoor()
    {
        if (isClose)
        {
            return;
        }

        isClose = true;

        StartCoroutine(ParticleStart());
    }

    private IEnumerator ParticleStart()
    {
        if (doorAnimator != null)
        {
            doorAnimator.SetFloat(speedParameterName, 1f);
            doorAnimator.Play(dropStateName, 0, 0.0f);
        }

        yield return new WaitForSeconds(doorDropDuration);

        if(dustParticle != null)
        {
            dustParticle.Play();
        }

        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }

        // 문 닫힘 애니메이션이 끝까지 재생될 때까지 대기
        if (doorAnimator != null)
        {
            yield return null; // Play() 적용 대기
            while (true)
            {
                AnimatorStateInfo info = doorAnimator.GetCurrentAnimatorStateInfo(0);
                if (!info.IsName(dropStateName) || info.normalizedTime >= 1f)
                {
                    break;
                }
                yield return null;
            }
        }

        if (afterCloseDelay > 0f)
        {
            yield return new WaitForSeconds(afterCloseDelay);
        }

        onDoorClosed?.Invoke();
    }

    public void TrackMonsters(List<GameObject> monsters)
    {
        trackedMonsters.AddRange(monsters);
        StartCoroutine(MonitorMonsterRoutine());
    }

    private IEnumerator MonitorMonsterRoutine()
    {
        while (trackedMonsters.Count > 0)
        {
            yield return new WaitForSeconds(0.3f);

            trackedMonsters.RemoveAll(monster => monster == null);
        }

        OpenDoor();
    }

    public void OpenDoor()
    {
        isClose = false;

        if (doorAnimator != null)
        {
            doorAnimator.ResetTrigger(dropTriggerName);
            doorAnimator.SetFloat(speedParameterName, -1f);
            doorAnimator.Play(dropStateName, 0, 1.0f);
        }

        if (doorCollider != null)
        {
            StartCoroutine(DisableCollider(openDuration));
        }
    }

    private IEnumerator DisableCollider(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (doorCollider != null)
        {
            doorCollider.enabled = false;
        }
    }
}