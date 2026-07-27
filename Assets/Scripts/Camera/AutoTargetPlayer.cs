using UnityEngine;
using System.Collections;
using Unity.Cinemachine;

public class AutoTargetPlayer : MonoBehaviour
{
    private void OnEnable()
    {
        StartCoroutine(FindFollowPlayerRoutine());
    }

    private IEnumerator FindFollowPlayerRoutine()
    {
        GameObject player = null;
        int maxFind = 10;
        int currentFind = 0;

        while(player == null && currentFind < maxFind)
        {
            player = GameObject.FindWithTag("Player");
            currentFind++;
            yield return null;
        }

        if(player != null && TryGetComponent<CinemachineCamera>(out var vcam))
        {
            vcam.Follow = player.transform;
        }
        else if(player == null)
        {
            Debug.Log("플레이어를 찾지 못함");
        }
    }
}
