using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class ARVisionManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Toggle ARToggle;

    private RawImage rawImage;
    private WebCamTexture webCamTexture;
    private AspectRatioFitter ratioFitter;

    void Start()
    {
        rawImage = GetComponent<RawImage>();

        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("No camera found!");
            return;
        }

        webCamTexture = new WebCamTexture(devices[0].name, 1280, 720, 30);
        rawImage.texture = webCamTexture;

        ratioFitter = GetComponent<AspectRatioFitter>();
        if (ratioFitter == null)
        {
            ratioFitter = gameObject.AddComponent<AspectRatioFitter>();
        }
        ratioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        if (ARToggle != null)
        {
            ARToggle.onValueChanged.AddListener(ToggleCameraFeed);
            ToggleCameraFeed(ARToggle.isOn); 
        }
    }

    private void ToggleCameraFeed(bool isVisible)
    {
        if (webCamTexture == null) return;

        rawImage.enabled = isVisible;

        if (isVisible)
        {
            webCamTexture.Play();
        }
        else
        {
            webCamTexture.Stop(); 
        }
    }

    void Update()
    {
        if (webCamTexture != null && webCamTexture.isPlaying && webCamTexture.width > 100)
        {
            float ratio = (float)webCamTexture.width / (float)webCamTexture.height;
            ratioFitter.aspectRatio = ratio;
        }
    }

    void OnDestroy()
    {
        if (ARToggle != null)
        {
            ARToggle.onValueChanged.RemoveListener(ToggleCameraFeed);
        }
        
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
        }
    }
}