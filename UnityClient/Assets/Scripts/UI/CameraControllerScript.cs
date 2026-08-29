using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CameraControllerScript : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Toggle ARToggle;
    
    [Header("Mouse Speeds")]
    public float mouseOrbitSpeed = 0.25f;
    public float mousePanSpeed = 0.05f;
    public float mouseZoomSpeed = 0.1f;

    [Header("Touch Speeds")]
    public float touchOrbitSpeed = 0.25f;
    public float touchPanSpeed = 0.05f;
    public float touchZoomSpeed = 0.1f;

    private float startXAngle;
    private float startYAngle;
    private float startDistance;
    private Vector3 startTargetPosition;

    private float xAngle = 35f;
    private float yAngle = 0f;
    private float distance = 1f;
    private float minDistance = 0.5f;
    private float maxDistance = 10f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        if (angles.x != 0) xAngle = angles.x;
        if (angles.y != 0) yAngle = angles.y;

        startXAngle = xAngle;
        startYAngle = yAngle;
        startDistance = distance;
        
        if (target != null)
        {
            startTargetPosition = target.position;
        }

        if (ARToggle != null)
        {
            ARToggle.onValueChanged.AddListener(ResetCamera);
            ResetCamera(ARToggle.isOn); 
        }
    }

    private void ResetCamera(bool isARActive)
    {
        if (isARActive)
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }
        else
        {
            xAngle = startXAngle;
            yAngle = startYAngle;
            distance = startDistance;

            if (target != null)
            {
                target.position = startTargetPosition;
            }

            Quaternion rotation = Quaternion.Euler(xAngle, yAngle, 0);
            transform.position = target.position - (rotation * Vector3.forward * distance);
            transform.rotation = rotation;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (ARToggle != null && ARToggle.isOn) return;

        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            HandleTouch();
        }
        else if (Mouse.current != null)
        {
            HandleMouse();
        }

        Quaternion rotation = Quaternion.Euler(xAngle, yAngle, 0);
        transform.position = target.position - (rotation * Vector3.forward * distance);
        transform.rotation = rotation;
    }

    void HandleMouse()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -= scroll * mouseZoomSpeed * distance;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        bool isRightClick = Mouse.current.rightButton.isPressed;
        bool isMiddleClick = Mouse.current.middleButton.isPressed;
        bool isShift = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        if (isRightClick && !isShift)
        {
            yAngle += mouseDelta.x * mouseOrbitSpeed;
            xAngle -= mouseDelta.y * mouseOrbitSpeed;
            xAngle = Mathf.Clamp(xAngle, -89f, 89f);
        }

        if (isMiddleClick || (isShift && isRightClick))
        {
            Vector3 right = transform.right; right.y = 0; right.Normalize();
            Vector3 forward = transform.forward; forward.y = 0; forward.Normalize();
            target.position -= (right * mouseDelta.x + forward * mouseDelta.y) * mousePanSpeed;
        }
    }

    void HandleTouch()
    {
        int activeTouches = 0;
        foreach (var touch in Touchscreen.current.touches)
        {
            if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Moved || 
                touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Stationary)
            {
                activeTouches++;
            }
        }

        if (activeTouches == 1)
        {
            Vector2 touchDelta = Touchscreen.current.primaryTouch.delta.ReadValue();
            yAngle += touchDelta.x * touchOrbitSpeed;
            xAngle -= touchDelta.y * touchOrbitSpeed;
            xAngle = Mathf.Clamp(xAngle, -89f, 89f);
        }
        else if (activeTouches == 2)
        {
            var t0 = Touchscreen.current.touches[0];
            var t1 = Touchscreen.current.touches[1];

            Vector2 t0Pos = t0.position.ReadValue();
            Vector2 t1Pos = t1.position.ReadValue();
            Vector2 t0Delta = t0.delta.ReadValue();
            Vector2 t1Delta = t1.delta.ReadValue();

            float prevDistance = Vector2.Distance(t0Pos - t0Delta, t1Pos - t1Delta);
            float curDistance = Vector2.Distance(t0Pos, t1Pos);
            float pinchDelta = prevDistance - curDistance;

            distance += pinchDelta * touchZoomSpeed * distance;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            Vector2 avgDelta = (t0Delta + t1Delta) / 2f;
            Vector3 right = transform.right; right.y = 0; right.Normalize();
            Vector3 forward = transform.forward; forward.y = 0; forward.Normalize();
            target.position -= (right * avgDelta.x + forward * avgDelta.y) * touchPanSpeed;
        }
    }

    void OnDestroy()
    {
        if (ARToggle != null)
        {
            ARToggle.onValueChanged.RemoveListener(ResetCamera);
        }
    }
}