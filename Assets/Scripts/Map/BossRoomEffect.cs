using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class BossRoomEffect : MonoBehaviour
{
    [Header("Room")]
    [SerializeField] private MonsterSpawner bossSpawner;
    [SerializeField] private float doorWait = 0.6f;          // DoorController.doorDropDuration(0.25) + 여유

    [Header("Camera")]
    [SerializeField] private GameObject introCam;           // CM_BossIntro (비활성 시작)
    [SerializeField] private float cameraBlendTime = 2f;    // Brain Default Blend 시간과 동일하게

    [Header("Intro Boss")]
    [SerializeField] private GameObject introBoss;          // 비활성 시작
    [SerializeField] private Animator introBossAnim;
    [SerializeField] private string slamState = "IntroSlam";
    [SerializeField] private float afterLandDelay = 0.3f;   // 착지 후 잠깐 정지

    private bool played;

    public void PlayIntro()                                 // PlayerChecker.onPlayerEnter에 연결
    {
        if (played) return;
        played = true;
        StartCoroutine(IntroRoutine());
    }

    private IEnumerator IntroRoutine()
    {
        // 1. 플레이어 정지
        var player = GameObject.FindWithTag("Player");
        var move = player.GetComponent<PlayerMove>();
        move.IsMovementLocked = true;

        // 2. 연출 카메라로 전환
        introCam.SetActive(true);
        yield return new WaitForSeconds(cameraBlendTime);

        // 3. 연출용 보스 낙하 + 내려찍기
        introBoss.SetActive(true);
        introBossAnim.Play(slamState, 0, 0f);
        yield return null;
        yield return new WaitForSeconds(introBossAnim.GetCurrentAnimatorStateInfo(0).length);
        yield return new WaitForSeconds(afterLandDelay);

        // 4. 실제 보스로 교체 (MonsterSpawner가 생성 + 문 추적까지 처리)
        introBoss.SetActive(false);
        bossSpawner.SpawnMonsters();

        // 5. 카메라 복귀
        introCam.SetActive(false);
        yield return new WaitForSeconds(cameraBlendTime);

        // 6. 전투 시작
        move.IsMovementLocked = false;
    }
}