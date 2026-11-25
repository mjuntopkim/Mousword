using Unity.Cinemachine;
using UnityEngine;

public class IntroBoss : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource slamImpulseSource; // 내려찍기 시 카메라 흔들림
    [SerializeField] private ParticleSystem dustParticle;

    public void Slam()
    {
        slamImpulseSource.GenerateImpulse();

        if(dustParticle != null)
        {
            dustParticle.Play();
        }
    }
}
