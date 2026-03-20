using UnityEngine;
using UnityEngine.EventSystems;

public class MenuHoldFillButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private Tower tower;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (tower != null)
        {
            tower.StartHoldFill();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (tower != null)
        {
            tower.StopHoldFill();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tower != null)
        {
            tower.StopHoldFill();
        }
    }
}