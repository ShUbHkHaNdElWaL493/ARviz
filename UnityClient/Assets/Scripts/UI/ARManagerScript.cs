using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class ARManager : MonoBehaviour
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
    public float markerSize;

    private Vector3 basePosition;
    private Quaternion baseRotation;

    private bool trackerInitialized = false;
    private Color32[] pixelBuffer;
    private float[] tvec = new float[3];
    private float[] rvec = new float[3];

    void Start()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
        }

        if (robotAnchor != null)
        {
            basePosition = robotAnchor.localPosition;
            baseRotation = robotAnchor.localRotation;
        }

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

    private void SetChildrenActive(bool isActive)
    {
        if (robotAnchor == null) return;

        foreach (Transform child in robotAnchor)
        {
            if (child.gameObject.activeSelf != isActive)
            {
                child.gameObject.SetActive(isActive);
            }
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
            if (robotAnchor != null)
            {
                robotAnchor.localPosition = basePosition;
                robotAnchor.localRotation = baseRotation;
                SetChildrenActive(true); 
            }
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
            float vFovRad = Camera.main.fieldOfView * Mathf.Deg2Rad;
            float expectedFocalLength = (height / 2f) / Mathf.Tan(vFovRad / 2f);

            float fx = expectedFocalLength; 
            float fy = expectedFocalLength;
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

        if (found)
        {
            SetChildrenActive(true);

            if (robotAnchor != null)
            {
                Vector3 position = new Vector3(tvec[0], -tvec[1], tvec[2]);
                position.z = Mathf.Clamp(position.z, 0.01f, 90f);
                robotAnchor.localPosition = position;

                float angleRad = Mathf.Sqrt(rvec[0]*rvec[0] + rvec[1]*rvec[1] + rvec[2]*rvec[2]);
                if (angleRad > 0.001f)
                {
                    Vector3 axis = new Vector3(-rvec[0]/angleRad, rvec[1]/angleRad, -rvec[2]/angleRad);
                    Quaternion cvRotation = Quaternion.AngleAxis(angleRad * Mathf.Rad2Deg, axis);
                    robotAnchor.localRotation = cvRotation * Quaternion.Euler(90, 0, 0);
                }
            }
        }
        else
        {
            SetChildrenActive(false);
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