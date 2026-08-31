using UnityEngine;
using TMPro;

public class CameraPropertiesUI : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public Transform robotAnchor;
    
    [Header("UI Text Elements")]
    public TMP_Text positionText;
    public TMP_Text rotationText;
    public TMP_Text distanceText;
    public TMP_Text fovText;

    private float updateTimer = 0f;
    private const float UPDATE_INTERVAL = 0.1f;

    void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer >= UPDATE_INTERVAL)
        {
            UpdateUI();
            updateTimer = 0f;
        }
    }

    private void UpdateUI()
    {
        if (mainCamera == null) return;

        Vector3 pos = mainCamera.transform.position;
        if (positionText != null)
            positionText.text = $"Position: X:{pos.x:F2} Y:{pos.y:F2} Z:{pos.z:F2}";

        Vector3 rot = mainCamera.transform.eulerAngles;
        if (rotationText != null)
            rotationText.text = $"Rotation: {rot.x:F1}° {rot.y:F1}° {rot.z:F1}°";

        if (fovText != null)
            fovText.text = $"Field of View: {mainCamera.fieldOfView:F1}°";

        if (distanceText != null)
        {
            if (robotAnchor != null && robotAnchor.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(mainCamera.transform.position, robotAnchor.position);
                distanceText.text = $"Distance to Marker: {dist:F2} m";
            }
            else
            {
                distanceText.text = "Distance to Marker: Not Tracking";
            }
        }
    }
}