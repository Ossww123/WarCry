using UnityEngine.EventSystems;
using UnityEngine;

public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private int hoverSoundIndex = 2;
    [SerializeField] private int clickSoundIndex = 3;

    private void Awake()
    {
        if (Application.isBatchMode)
        {
            Debug.Log("[UIButtonSound] Headless 모드 - 비활성화됨");
            this.enabled = false;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(hoverSoundIndex);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(clickSoundIndex);
    }
}
