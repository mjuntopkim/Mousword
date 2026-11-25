using UnityEngine;
using Unity.Cinemachine;

public class CameraBoundsSwitcher : MonoBehaviour
{
    [SerializeField] private CinemachineConfiner2D confiner; // 카메라 컨피너
    [SerializeField] private Collider2D newBounds; // 새로운 카메라 경계

    public void CameraBoundsChanged()
    {
        confiner.BoundingShape2D = newBounds; // 플레이어가 트리거에 들어오면 새로운 경계로 변경
        
        confiner.InvalidateBoundingShapeCache(); // 경계 변경 후 캐시 무효화
    }
}
