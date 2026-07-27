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
        while(player == null)
        {
            player = GameObject.FindWithTag("Player");
            yield return null;
        }

        if(TryGetComponent<CinemachineCamera>(out var vcam))
        {
            vcam.Follow = player.transform;
        }
    }
}
