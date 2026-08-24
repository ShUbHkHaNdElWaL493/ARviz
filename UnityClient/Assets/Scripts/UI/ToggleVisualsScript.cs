using UnityEngine;
using UnityEngine.UI;

public class ToggleVisualsScript : MonoBehaviour
{
    [Header("UI Objects")]
    public Toggle toggle;
    public RectTransform handle;
    public Image backgroundImage;
    
    [Header("Colours")]
    public Color offColor = Color.gray;
    public Color onColor = Color.green;
    
    private float travelDistance = 30f;
    private float slideSpeed = 10f;

    private Vector2 offPosition;
    private Vector2 onPosition;

    void Start()
    {
        offPosition = handle.anchoredPosition;
        onPosition = new Vector2(offPosition.x + travelDistance, offPosition.y);
    }

    void Update()
    {
        Vector2 targetPos = toggle.isOn ? onPosition : offPosition;
        handle.anchoredPosition = Vector2.Lerp(handle.anchoredPosition, targetPos, slideSpeed * Time.deltaTime);

        Color targetColor = toggle.isOn ? onColor : offColor;
        backgroundImage.color = Color.Lerp(backgroundImage.color, targetColor, slideSpeed * Time.deltaTime);
    }
}