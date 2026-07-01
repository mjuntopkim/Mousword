using UnityEngine;

/// <summary>
/// 토크 물리로 캐릭터가 흔들리고 회전하므로, 카메라를 Player 의 "자식"으로 두면
/// 화면이 같이 회전/떨려서 멀미가 납니다.
///
/// 사용법:
///  1) Main Camera 를 Player (1) 자식에서 빼내 씬 루트로 옮기세요.
///  2) Main Camera 에 이 스크립트를 붙이고 target 에 Player (1) 을 연결하세요.
///
/// 위치만 부드럽게 따라가고, 회전은 따라가지 않습니다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;          // 따라갈 대상 (Player (1))
    [SerializeField] Vector2 offset = Vector2.zero;
    [Tooltip("따라가는 부드러움. 작을수록 빠르게 붙음.")]
    [SerializeField] float smoothTime = 0.15f;

    Vector3 velocity;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z      // 카메라 z(깊이)는 유지
        );

        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref velocity, smoothTime);
    }
}
