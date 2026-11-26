using UnityEngine;
using UnityEngine.EventSystems;

public class EndingButtonHover : MonoBehaviour, IPointerExitHandler
{
    public void OnPointerExit(PointerEventData d)
    {
        var es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject == gameObject)
            es.SetSelectedGameObject(null);
    }
}
