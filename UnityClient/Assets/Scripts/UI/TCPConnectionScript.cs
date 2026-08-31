using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;
using System.Collections;

public class TCPConnection : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField ipInput;
    public TMP_InputField portInput;
    public Button connectButton;

    [Header("Status Pill HUD")]
    public GameObject statusPill;
    private TMP_Text statusPillText;
    private const float STATUS_PILL_DURATION = 3f;
    
    private Coroutine hidePillCoroutine; 
    private Coroutine connectionCoroutine;
    
    private TMP_Text buttonText;
    private bool isConnected = false;
    private bool isConnecting = false;

    public static event System.Action OnROSDisconnectedEvent;

    void Start()
    {
        if (statusPill != null)
        {
            statusPillText = statusPill.GetComponentInChildren<TMP_Text>(true);
            statusPill.SetActive(false);
        }

        if (connectButton != null)
        {
            buttonText = connectButton.GetComponentInChildren<TMP_Text>();
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        }

        var ros = Unity.Robotics.ROSTCPConnector.ROSConnection.GetOrCreateInstance();
        ros.Disconnect();

        string savedIP = PlayerPrefs.GetString("ROS_IP", ros.RosIPAddress);
        int savedPort = PlayerPrefs.GetInt("ROS_PORT", ros.RosPort);

        if (ipInput != null) ipInput.text = savedIP;
        if (portInput != null) portInput.text = savedPort.ToString();
    }

    private void OnConnectButtonClicked()
    {
        if (isConnecting) return;

        if (isConnected)
        {
            DisconnectFromROS();
            return;
        }

        string ip = ipInput != null ? ipInput.text : "";
        string portText = portInput != null ? portInput.text : "";

        if (string.IsNullOrWhiteSpace(ip))
        {
            ShowStatusMessage("Invalid IP Address!");
            return;
        }

        if (!int.TryParse(portText, out int port))
        {
            ShowStatusMessage("Invalid Port Number!");
            return;
        }

        PlayerPrefs.SetString("ROS_IP", ip);
        PlayerPrefs.SetInt("ROS_PORT", port);
        PlayerPrefs.Save();

        if (connectionCoroutine != null) StopCoroutine(connectionCoroutine);
        connectionCoroutine = StartCoroutine(ConnectSequence(ip, port));
    }

    private IEnumerator ConnectSequence(string ip, int port)
    {
        isConnecting = true;
        if (connectButton != null) connectButton.interactable = false;
        if (buttonText != null) buttonText.text = "Connecting...";
        ShowStatusMessage($"Connecting to {ip}...");

        var ros = Unity.Robotics.ROSTCPConnector.ROSConnection.GetOrCreateInstance();

        ros.Disconnect();
        yield return new WaitForSeconds(0.2f);
        ros.RosIPAddress = ip;
        ros.RosPort = port;
        ros.Connect();

        float timer = 0f;
        float connectionWaitTime = 1.0f; 
        
        while (timer < connectionWaitTime)
        {
            if (!ros.HasConnectionThread || ros.HasConnectionError)
            {
                break;
            }
            timer += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (ros.HasConnectionThread && !ros.HasConnectionError)
        {
            isConnected = true;
            if (buttonText != null) buttonText.text = "Disconnect";
            ShowStatusMessage("Desktop server connected.");
        }
        else
        {
            isConnected = false;
            ros.Disconnect();
            if (buttonText != null) buttonText.text = "Connect";
            ShowStatusMessage("Desktop server not connected.");
        }

        isConnecting = false;
        if (connectButton != null) connectButton.interactable = true;
    }

    private void DisconnectFromROS()
    {
        if (connectionCoroutine != null) StopCoroutine(connectionCoroutine);
        
        if (connectButton != null) connectButton.interactable = false;
        if (buttonText != null) buttonText.text = "Disconnecting...";

        StartCoroutine(DisconnectSequence());
    }

    private IEnumerator DisconnectSequence()
    {
        OnROSDisconnectedEvent?.Invoke();

        yield return new WaitForSeconds(0.2f);

        var ros = Unity.Robotics.ROSTCPConnector.ROSConnection.GetOrCreateInstance();
        ros.Disconnect();
        
        isConnected = false;
        isConnecting = false;
        
        if (buttonText != null) buttonText.text = "Connect";
        if (connectButton != null) connectButton.interactable = true;

        ShowStatusMessage("Disconnected from desktop server.");
    }

    private void ShowStatusMessage(string message)
    {
        if (statusPill == null || statusPillText == null) return;
        statusPillText.text = message;
        statusPill.SetActive(true);
        if (hidePillCoroutine != null) StopCoroutine(hidePillCoroutine);
        hidePillCoroutine = StartCoroutine(HideStatusPillDelay());
    }

    private IEnumerator HideStatusPillDelay()
    {
        yield return new WaitForSeconds(STATUS_PILL_DURATION);
        statusPill.SetActive(false);
    }
}