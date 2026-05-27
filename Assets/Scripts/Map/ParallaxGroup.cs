using UnityEngine;

public class ParallaxGroup : MonoBehaviour
{
    [SerializeField] private Transform cam;
    [SerializeField] private float parallaxEffectX = 0.5f;
    [SerializeField] private float parallaxEffectY = 1.0f;
    [SerializeField] private float backgroundWidth;
    [SerializeField] private float jumpThreshold = 1.5f;
    [SerializeField] private float offsetY = 0f;

    private Transform[] backgrounds;
    private Vector3 lastCamPos;
    private bool isFirstFrame = true;


    void Start()
    {
        if(cam == null)
        {
            if(Camera.main != null)
            {
                cam = Camera.main.transform;
            }
        }

        backgrounds = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            backgrounds[i] = transform.GetChild(i);
        }

        SnapToCamera();
    }

    void LateUpdate()
    {
        Vector3 camDelta = cam.position - lastCamPos;

        if (isFirstFrame || camDelta.magnitude > 5f)
        {
            transform.position += camDelta;

            lastCamPos = cam.position;
            isFirstFrame = false;
            return;
        }

        transform.position += new Vector3(camDelta.x * parallaxEffectX, camDelta.y * parallaxEffectY, 0);

        lastCamPos = cam.position;

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

    private void SnapToCamera()
    {
        transform.position = new Vector3(cam.position.x, cam.position.y + offsetY, transform.position.z);
        lastCamPos = cam.position;
    }
}