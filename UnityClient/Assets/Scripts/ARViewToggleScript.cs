using UnityEngine;
using UnityEngine.UI;

public class ARViewToggleScript : MonoBehaviour
{
    [Header("UI Toggles")]
    public Toggle arViewToggle;
    public Toggle robotModelToggle;

    [Header("Target Objects")]
    public RawImage arBackgroundFeed;
    public GameObject robotRootObject;

    void Start()
    {
        if (arViewToggle != null)
        {
            arViewToggle.onValueChanged.AddListener(ToggleARBackground);
            ToggleARBackground(arViewToggle.isOn);
        }

        if (robotModelToggle != null)
        {
            robotModelToggle.onValueChanged.AddListener(ToggleRobotModel);
            ToggleRobotModel(robotModelToggle.isOn);
        }
    }

    private void ToggleARBackground(bool isVisible)
    {
        if (arBackgroundFeed != null)
        {
            arBackgroundFeed.enabled = isVisible;
            WebCamTexture camTexture = arBackgroundFeed.texture as WebCamTexture;
            if (camTexture != null)
            {
                if (isVisible)
                {
                    camTexture.Play();
                }
                else
                {
                    camTexture.Pause();
                }
            }
        }
    }

    private void ToggleRobotModel(bool isVisible)
    {
        if (robotRootObject != null)
        {
            robotRootObject.SetActive(isVisible);
        }
    }
    
    void OnDestroy()
    {
        if (arViewToggle != null) arViewToggle.onValueChanged.RemoveListener(ToggleARBackground);
        if (robotModelToggle != null) robotModelToggle.onValueChanged.RemoveListener(ToggleRobotModel);
    }
}