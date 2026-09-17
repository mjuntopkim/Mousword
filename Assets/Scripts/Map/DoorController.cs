using UnityEngine;
using System.Collections;

public class DoorController : MonoBehaviour
{
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private Collider2D doorCollider;

    [SerializeField] private string dropTriggerName = "Drop";
    [SerializeField] private string OpenTriggerName = "Open";

    [SerializeField] private float openDuration = 0.5f;

    private int remainingMonsters = 0;
    private bool isClose = false;

    private void Awake()
    {
        if (doorAnimator == null)
        {
            doorAnimator.GetComponent<Animator>();
        }
        if (doorCollider == null)
        {
            doorCollider.GetComponent<Collider2D>();
        }
    }

    public void CloseDoor()
    {
        if (isClose)
        {
            return;
        }

        isClose = true;

        if (doorAnimator != null)
        {
            doorAnimator.ResetTrigger(OpenTriggerName);
            doorAnimator.SetTrigger(dropTriggerName);
        }

        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }
    }

    public void SetMonsterCount(int count)
    {
        remainingMonsters = count;

        if (remainingMonsters <= 0)
        {
            OpenDoor();
        }
    }

    public void MonsterDied()
    {
        if (!isClose)
        {
            return;
        }

        remainingMonsters--;

        if (remainingMonsters <= 0)
        {
            OpenDoor();
        }
    }

    public void OpenDoor()
    {
        isClose = false;

        if(doorAnimator != null)
        {
            doorAnimator.ResetTrigger(dropTriggerName);
            doorAnimator.SetTrigger(OpenTriggerName);
        }

        if(doorCollider != null)
        {
            StartCoroutine(DisableCollider(openDuration));
        }
    }

    private IEnumerator DisableCollider(float delay)
    {
        yield return new WaitForSeconds(delay);
        if(doorCollider != null)
        {
            doorCollider.enabled = false;
        }
    }
}
