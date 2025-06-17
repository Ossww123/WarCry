using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject borderImage;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (borderImage != null)
            borderImage.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (borderImage != null)
            borderImage.SetActive(false);
    }
}
