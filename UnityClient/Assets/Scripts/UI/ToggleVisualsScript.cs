using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleVisuals : MonoBehaviour
{
    [Header("Colours")]
    public Color offColor = Color.gray;
    public Color onColor = Color.green;
    
    private float travelDistance = 30f;
    private float slideSpeed = 10f;

    private Toggle toggle;
    private RectTransform handle;
    private Image backgroundImage;

    private Vector2 offPosition;
    private Vector2 onPosition;

    void Start()
    {
        toggle = GetComponent<Toggle>();
        backgroundImage = toggle.targetGraphic as Image;
        if (toggle.graphic != null)
        {
            handle = toggle.graphic.rectTransform;
        }
        else
        {
            Transform handleObj = transform.Find("Handle") ?? transform.Find("Background/Handle");
            if (handleObj != null) handle = handleObj.GetComponent<RectTransform>();
        }

        if (handle == null || backgroundImage == null)
        {
            Debug.LogError("Could not find the Handle or Background! Make sure they exist as children.", this);
            return;
        }

        offPosition = handle.anchoredPosition;
        onPosition = new Vector2(offPosition.x + travelDistance, offPosition.y);
    }

    void Update()
    {
        if (handle == null || backgroundImage == null) return;

        Vector2 targetPos = toggle.isOn ? onPosition : offPosition;
        handle.anchoredPosition = Vector2.Lerp(handle.anchoredPosition, targetPos, slideSpeed * Time.deltaTime);

        Color targetColor = toggle.isOn ? onColor : offColor;
        backgroundImage.color = Color.Lerp(backgroundImage.color, targetColor, slideSpeed * Time.deltaTime);
    }
}