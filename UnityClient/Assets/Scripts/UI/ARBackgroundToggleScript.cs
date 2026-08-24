using UnityEngine;
using UnityEngine.UI;

public class ARBackgroundToggleScript : MonoBehaviour
{
    [Header("UI Toggles")]
    public Toggle arViewToggle;

    [Header("Target Objects")]
    public RawImage arBackgroundFeed;

    void Start()
    {
        if (arViewToggle != null)
        {
            arViewToggle.onValueChanged.AddListener(ToggleARBackground);
            ToggleARBackground(arViewToggle.isOn);
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
    
    void OnDestroy()
    {
        if (arViewToggle != null) arViewToggle.onValueChanged.RemoveListener(ToggleARBackground);
    }
}