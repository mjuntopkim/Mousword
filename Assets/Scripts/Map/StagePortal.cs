using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class StagePortal : MonoBehaviour
{
    [SerializeField] private string nextSceneName;
    [SerializeField] private string targetSpawnPointName;
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private float recoveryHPAmount = 100f;  //HP 회복량
    [SerializeField] private float recoveryManaAmount = 100f;    //마나 회복량

    [SerializeField] private float doorDropDuration = 0.25f;
    [SerializeField] private float waitAfterHit = 0.5f;

    private PlayerStatus playerStatus;

    private bool isActivated = false;

    private void Awake(){
        playerStatus = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerStatus>();
    }

    public void Nextstage(){
        if(playerStatus != null)
        {
            Debug.Log("회복 호출됨");
            playerStatus.RecoverHp(recoveryHPAmount); // HP 회복
            playerStatus.RecoverMp(recoveryManaAmount); // 마나 회복

            GameManager.Instance.TransitionToNextScene(nextSceneName, targetSpawnPointName);
        }
    }

    /*private void OnTriggerEnter2D(Collider2D collision)
    {
        if(GameManager.Instance != null && GameManager.Instance.IsTransitioning)
        {
            return;
        }

        if(collision.CompareTag("Player") && !isActivated)
        {
            isActivated = true;
            StartCoroutine(CinematicDoorSequence());
        }
    }*/

    /*private IEnumerator CinematicDoorSequence()
    {
        if(doorAnimator != null)
        {
            doorAnimator.SetFloat("Speed", 1f);
            doorAnimator.Play("RockDrop", 0, 0f);
        }

        yield return new WaitForSeconds(doorDropDuration);

        if(impulseSource != null)
        {
            impulseSource.GenerateImpulse();
        }

        yield return new WaitForSeconds(waitAfterHit);
    }*/
}
