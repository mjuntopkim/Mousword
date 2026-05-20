using UnityEngine;

public class ParallaxGroup : MonoBehaviour
{
    [SerializeField] private Transform cam;
    [SerializeField] private float parallaxEffectX = 0.5f;
    [SerializeField] private float parallaxEffectY = 1.0f;
    [SerializeField] private float backgroundWidth;
    [SerializeField] private float jumpThreshold = 1.5f;

    private Transform[] backgrounds;
    private float startPosY;
    private float startCamY;

    void Start()
    {
        backgrounds = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            backgrounds[i] = transform.GetChild(i);
        }

        startPosY = transform.position.y;
        startCamY = cam.position.y;
    }

    void LateUpdate()
    {
        float targetY = startPosY + (cam.position.y - startCamY) * parallaxEffectY;
        transform.position = new Vector3(cam.position.x * parallaxEffectX, targetY, transform.position.z);

        for (int i = 0; i < backgrounds.Length; i++)
        {
            float distX = cam.position.x - backgrounds[i].position.x;

            if (distX > backgroundWidth * jumpThreshold)
            {
                backgrounds[i].localPosition += Vector3.right * backgroundWidth * backgrounds.Length;
            }
            else if (distX < -backgroundWidth * jumpThreshold)
            {
                backgrounds[i].localPosition += Vector3.left * backgroundWidth * backgrounds.Length;
            }
        }
    }
}