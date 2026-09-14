using UnityEngine;
using UnityEngine.UI;

public class SignPost : MonoBehaviour
{
    [Header("UI오브젝트 연결")]
    [SerializeField] private GameObject bubbleUI;
    [SerializeField] private Image image;

    [Header("UI내부 그림")]
    [SerializeField] private Sprite signSprite;

    private void Start()
    {
        if(bubbleUI != null)
        {
            bubbleUI.SetActive(false);
        }

        InitSignContent();
    }

    private void InitSignContent()
    {
        if(image != null)
        {
            if(signSprite != null)
            {
                image.sprite = signSprite;
                image.gameObject.SetActive(true);
            }
            else
            {
                image.gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if(bubbleUI != null)
            {
                bubbleUI.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            if(bubbleUI != null)
            {
                bubbleUI.SetActive(false);
            }
        }
    }
}
