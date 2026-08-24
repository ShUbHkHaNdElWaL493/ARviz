using UnityEngine;

public class DrawerButtonsScript : MonoBehaviour
{
    [Header("Drawers")]
    public RectTransform leftDrawer;
    public RectTransform rightDrawer;

    [Header("Buttons")]
    public RectTransform leftButton;
    public RectTransform rightButton;

    private float slideSpeed = 10f;

    private Vector2 leftDrawerOpenPos, leftDrawerClosedPos;
    private Vector2 rightDrawerOpenPos, rightDrawerClosedPos;
    
    private Vector2 leftBtnOpenPos, leftBtnClosedPos;
    private Vector2 rightBtnOpenPos, rightBtnClosedPos;

    private bool isLeftOpen = true;
    private bool isRightOpen = true;

    void Start()
    {
        float leftWidth = leftDrawer.rect.width;
        float rightWidth = rightDrawer.rect.width;

        leftDrawerOpenPos = leftDrawer.anchoredPosition;
        leftDrawerClosedPos = new Vector2(leftDrawerOpenPos.x - leftWidth, leftDrawerOpenPos.y);

        rightDrawerOpenPos = rightDrawer.anchoredPosition;
        rightDrawerClosedPos = new Vector2(rightDrawerOpenPos.x + rightWidth, rightDrawerOpenPos.y);

        leftBtnClosedPos = leftButton.anchoredPosition;
        leftBtnOpenPos = new Vector2(leftBtnClosedPos.x + leftWidth, leftBtnClosedPos.y);

        rightBtnClosedPos = rightButton.anchoredPosition;
        rightBtnOpenPos = new Vector2(rightBtnClosedPos.x - rightWidth, rightBtnClosedPos.y);

        leftButton.anchoredPosition = leftBtnOpenPos;
        rightButton.anchoredPosition = rightBtnOpenPos;
    }

    void Update()
    {
        Vector2 targetLeftDrawer = isLeftOpen ? leftDrawerOpenPos : leftDrawerClosedPos;
        leftDrawer.anchoredPosition = Vector2.Lerp(leftDrawer.anchoredPosition, targetLeftDrawer, slideSpeed * Time.deltaTime);

        Vector2 targetLeftBtn = isLeftOpen ? leftBtnOpenPos : leftBtnClosedPos;
        leftButton.anchoredPosition = Vector2.Lerp(leftButton.anchoredPosition, targetLeftBtn, slideSpeed * Time.deltaTime);

        Vector2 targetRightDrawer = isRightOpen ? rightDrawerOpenPos : rightDrawerClosedPos;
        rightDrawer.anchoredPosition = Vector2.Lerp(rightDrawer.anchoredPosition, targetRightDrawer, slideSpeed * Time.deltaTime);

        Vector2 targetRightBtn = isRightOpen ? rightBtnOpenPos : rightBtnClosedPos;
        rightButton.anchoredPosition = Vector2.Lerp(rightButton.anchoredPosition, targetRightBtn, slideSpeed * Time.deltaTime);
    }

    public void ToggleLeft()
    {
        isLeftOpen = !isLeftOpen;
    }

    public void ToggleRight()
    {
        isRightOpen = !isRightOpen;
    }
}