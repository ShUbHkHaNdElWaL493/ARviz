using UnityEngine;
using UnityEngine.UI;
using System.Runtime.InteropServices;

[RequireComponent(typeof(RawImage))]
public class ARVisionManager : MonoBehaviour
{
    [DllImport("aruco_tracker")]
    private static extern void InitTracker(float fx, float fy, float cx, float cy);

    [DllImport("aruco_tracker")]
    private static extern bool ProcessFrame(System.IntPtr imageData, int width, int height, float markerLengthMeters, 
                                            float[] outTvec, float[] outRvec);

    [Header("UI Elements")]
    public Toggle ARToggle;
    private RawImage rawImage;
    private WebCamTexture webCamTexture;
    private AspectRatioFitter ratioFitter;

    [Header("Tracking Settings")]
    public Transform robotAnchor;
    public float markerSize = 0.1f;

    private bool trackerInitialized = false;
    private Color32[] pixelBuffer;
    private float[] tvec = new float[3];
    private float[] rvec = new float[3];

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
        if (webCamTexture == null || !webCamTexture.isPlaying || webCamTexture.width < 100) return;

        int width = webCamTexture.width;
        int height = webCamTexture.height;

        float ratio = (float)width / (float)height;
        ratioFitter.aspectRatio = ratio;

        if (ARToggle != null && ARToggle.isOn)
        {
            ProcessTracking(width, height);
        }
    }

    private void ProcessTracking(int width, int height)
    {
        if (!trackerInitialized)
        {
            float fx = width; 
            float fy = width;
            float cx = width / 2f;
            float cy = height / 2f;
            InitTracker(fx, fy, cx, cy);
            trackerInitialized = true;
        }

        if (pixelBuffer == null || pixelBuffer.Length != width * height)
            pixelBuffer = new Color32[width * height];

        webCamTexture.GetPixels32(pixelBuffer);
        GCHandle pinHandle = GCHandle.Alloc(pixelBuffer, GCHandleType.Pinned);
        System.IntPtr pixelPtr = pinHandle.AddrOfPinnedObject();

        bool found = ProcessFrame(pixelPtr, width, height, markerSize, tvec, rvec);
        pinHandle.Free();

        if (found && robotAnchor != null)
        {
            Vector3 position = new Vector3(tvec[0], -tvec[1], tvec[2]);
            robotAnchor.localPosition = position;

            float angleRad = Mathf.Sqrt(rvec[0]*rvec[0] + rvec[1]*rvec[1] + rvec[2]*rvec[2]);
            if (angleRad > 0.001f)
            {
                Vector3 axis = new Vector3(-rvec[0]/angleRad, rvec[1]/angleRad, -rvec[2]/angleRad);
                robotAnchor.localRotation = Quaternion.AngleAxis(angleRad * Mathf.Rad2Deg, axis);
            }
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