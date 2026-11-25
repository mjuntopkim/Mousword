using UnityEngine;

public class StagePortal : MonoBehaviour
{
    [SerializeField] private string nextSceneName;
    [SerializeField] private string targetSpawnPointName;
    [SerializeField] private float recoveryHPAmount = 100f;  //HP 회복량
    [SerializeField] private float recoveryManaAmount = 100f;    //마나 회복량

    private PlayerStatus playerStatus;

    private void Awake(){
        playerStatus = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerStatus>();
    }

    public void Nextstage(){
        if(playerStatus != null)
        {
            var player = GameObject.FindWithTag("Player");
            var move = player.GetComponent<PlayerMove>();
            move.IsMovementLocked = true;

            Debug.Log("회복 호출됨");
            playerStatus.RecoverHp(recoveryHPAmount); // HP 회복
            playerStatus.RecoverMp(recoveryManaAmount); // 마나 회복

            GameManager.Instance.TransitionToNextScene(nextSceneName, targetSpawnPointName);
        }
    }
}
