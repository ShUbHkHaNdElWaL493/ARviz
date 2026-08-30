using GLTFast;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RobotModelScript : MonoBehaviour
{

    [Header("UI Settings")]
    public Toggle visualizationToggle;
    public TMP_InputField topicInputField;
    public RectTransform jointStatesContent;
    public GameObject statusPill;

    [Header("Target Settings")]
    public Transform arucoMarker;

    private string robotDescriptionTopic;
    private Vector3 meshRotationOffset = new Vector3(0, 90, 0);

    private bool URDFLoaded = false;
    private bool isVisible = false;
    private string subscribedUrdfTopic = ""; 
    private bool isSubscribedToJointStates = false;
    
    private GameObject robotRootNode;

    private Dictionary<string, Transform> joint_nodes = new Dictionary<string, Transform>();
    private Dictionary<string, Quaternion> initial_rotations = new Dictionary<string, Quaternion>();
    private Dictionary<string, Vector3> joint_axes = new Dictionary<string, Vector3>();
    private Dictionary<string, TMP_Text> ui_joint_texts = new Dictionary<string, TMP_Text>();

    private TMP_Text statusPillText;
    private const float STATUS_PILL_DURATION = 3f;

    void Start()
    {
        if (visualizationToggle != null)
            visualizationToggle.onValueChanged.AddListener(OnToggleChanged);

        if (statusPill != null)
        {
            statusPillText = statusPill.GetComponentInChildren<TMP_Text>(true);
            statusPill.SetActive(false);
        }
    }

    private void OnToggleChanged(bool isOn)
    {
        if (isOn)
        {
            string topic = topicInputField.text;
            if (string.IsNullOrWhiteSpace(topic))
            {
                ShowStatusError("[RobotModel] Robot_Description_Topic is empty!");
                visualizationToggle.SetIsOnWithoutNotify(false);
                return;
            }

            if (!string.IsNullOrEmpty(subscribedUrdfTopic) && subscribedUrdfTopic != topic)
            {
                ClearExistingModel(subscribedUrdfTopic);
            }

            robotDescriptionTopic = topic;
            isVisible = true;

            if (robotRootNode != null && URDFLoaded)
            {
                robotRootNode.SetActive(true);
                foreach (var text in ui_joint_texts.Values) 
                    text.gameObject.SetActive(true);
            }
            else
            {
                BeginVisualization();
            }
        }
        else
        {
            isVisible = false;
            StopVisualization();
        }
    }

    public void BeginVisualization()
    {
        if (subscribedUrdfTopic != robotDescriptionTopic)
        {
            ROSConnection.GetOrCreateInstance().Subscribe<StringMsg>(robotDescriptionTopic, URDFCallback);
            subscribedUrdfTopic = robotDescriptionTopic;
        }

        if (!isSubscribedToJointStates)
        {
            ROSConnection.GetOrCreateInstance().Subscribe<JointStateMsg>("/joint_states", JointStatesCallback);
            isSubscribedToJointStates = true;
        }
        
        Debug.Log("Listening to ROS topics in the background. Waiting for URDF...");
    }

    public void StopVisualization()
    {
        if (robotRootNode != null) 
            robotRootNode.SetActive(false);
        
        foreach (var textItem in ui_joint_texts.Values)
        {
            if (textItem != null) textItem.gameObject.SetActive(false);
        }
    }

    private void ClearExistingModel(string oldTopic)
    {
        URDFLoaded = false;

        if (!string.IsNullOrEmpty(oldTopic))
        {
            ROSConnection.GetOrCreateInstance().Unsubscribe(oldTopic);
        }
        
        subscribedUrdfTopic = "";
        if (robotRootNode != null) Destroy(robotRootNode);

        joint_nodes.Clear();
        initial_rotations.Clear();
        joint_axes.Clear();

        foreach (var textItem in ui_joint_texts.Values)
        {
            if (textItem != null) Destroy(textItem.gameObject);
        }
        ui_joint_texts.Clear();
    }

    void URDFCallback(StringMsg msg)
    {
        if (URDFLoaded) return;
        
        URDFLoaded = true;
        
        if (robotRootNode != null) Destroy(robotRootNode);
        
        robotRootNode = new GameObject("RobotModel");
        
        Transform targetParent = arucoMarker != null ? arucoMarker : this.transform;
        robotRootNode.transform.SetParent(targetParent, false);

        XDocument xDoc = XDocument.Parse(msg.data);
        
        Dictionary<string, XElement> linkElements = new Dictionary<string, XElement>();
        foreach (XElement link in xDoc.Descendants("link"))
        {
            string name = link.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name)) linkElements[name] = link;
        }

        List<XElement> jointElements = xDoc.Descendants("joint").ToList();
        HashSet<string> childLinks = new HashSet<string>();
        
        foreach (XElement joint in jointElements)
        {
            string childName = joint.Element("child")?.Attribute("link")?.Value;
            if (!string.IsNullOrEmpty(childName)) childLinks.Add(childName);
        }

        string rootLinkName = linkElements.Keys.FirstOrDefault(l => !childLinks.Contains(l));
        if (string.IsNullOrEmpty(rootLinkName))
        {
            ShowStatusError("Could not find a root link in the URDF.");
            return;
        }

        BuildLink(rootLinkName, linkElements, jointElements, robotRootNode.transform);
    }

    void BuildLink(string linkName, Dictionary<string, XElement> linkElements, List<XElement> allJoints, Transform parentTransform)
    {
        if (!linkElements.TryGetValue(linkName, out XElement linkXml)) return;

        GameObject linkObj = new GameObject($"Link: {linkName}");
        linkObj.transform.SetParent(parentTransform, false);

        foreach (XElement visual in linkXml.Elements("visual"))
        {
            GameObject visualObj = new GameObject("Visual");
            visualObj.transform.SetParent(linkObj.transform, false);

            ApplyOrigin(visualObj.transform, visual.Element("origin"));

            XElement mesh = visual.Element("geometry")?.Element("mesh");
            if (mesh != null)
            {
                string filename = mesh.Attribute("filename")?.Value;
                string scaleStr = mesh.Attribute("scale")?.Value ?? "1 1 1";
                
                if (!string.IsNullOrEmpty(filename))
                {
                    string url = ResolveMeshUrl(filename);
                    if (!string.IsNullOrEmpty(url)) _ = LoadMeshAsync(url, visualObj.transform, scaleStr);
                }
            }
        }

        var childJoints = allJoints.Where(j => j.Element("parent")?.Attribute("link")?.Value == linkName).ToList();
        foreach (XElement joint in childJoints)
        {
            BuildJoint(joint, linkElements, allJoints, linkObj.transform);
        }
    }

    void BuildJoint(XElement jointXml, Dictionary<string, XElement> linkElements, List<XElement> allJoints, Transform parentTransform)
    {
        string jointName = jointXml.Attribute("name")?.Value ?? "unnamed_joint";
        string childLinkName = jointXml.Element("child")?.Attribute("link")?.Value;

        GameObject jointObj = new GameObject($"Joint: {jointName}");
        jointObj.transform.SetParent(parentTransform, false);

        ApplyOrigin(jointObj.transform, jointXml.Element("origin"));

        Vector3 mappedAxis = Vector3.up; 
        XElement axisEl = jointXml.Element("axis");
        if (axisEl != null)
        {
            string xyz = axisEl.Attribute("xyz")?.Value ?? "0 0 1";
            float[] a = ParseFloats(xyz, 3);
            mappedAxis = ROS2UnityAxis(a[0], a[1], a[2]);
        }

        joint_nodes[jointName] = jointObj.transform;
        initial_rotations[jointName] = jointObj.transform.localRotation;
        joint_axes[jointName] = mappedAxis;

        if (!string.IsNullOrEmpty(childLinkName))
        {
            BuildLink(childLinkName, linkElements, allJoints, jointObj.transform);
        }
    }

    async Task LoadMeshAsync(string url, Transform parentTransform, string scaleStr)
    {
        var gltf = new GltfImport();
        bool success = await gltf.Load(url);
        
        if (success)
        {
            GameObject meshNode = new GameObject("GLTF_Mesh");
            meshNode.transform.SetParent(parentTransform, false);
            
            meshNode.transform.localPosition = Vector3.zero;
            meshNode.transform.localRotation = Quaternion.Euler(meshRotationOffset);

            float[] s = ParseFloats(scaleStr, 3, 1f);
            meshNode.transform.localScale = ROS2UnityScale(s[0], s[1], s[2]);

            await gltf.InstantiateMainSceneAsync(meshNode.transform);
        }
    }

    void JointStatesCallback(JointStateMsg msg)
    {
        if (!isVisible || joint_nodes.Count == 0 || msg.name.Length != msg.position.Length) return;
        
        for (int i = 0; i < msg.name.Length; i++)
        {
            string jointName = msg.name[i];
            float position = (float)msg.position[i];

            if (joint_nodes.TryGetValue(jointName, out Transform jointTf) && joint_axes.TryGetValue(jointName, out Vector3 axis))
            {
                float angleDeg = (float)(-position * Mathf.Rad2Deg);
                Quaternion rotationOffset = Quaternion.AngleAxis(angleDeg, axis);
                jointTf.localRotation = initial_rotations[jointName] * rotationOffset;
                
                if (jointStatesContent != null)
                {
                    if (!ui_joint_texts.TryGetValue(jointName, out TMP_Text textComponent))
                    {
                        GameObject newTextObj = new GameObject($"UI_{jointName}");
                        newTextObj.transform.SetParent(jointStatesContent, false);

                        textComponent = newTextObj.AddComponent<TextMeshProUGUI>();
                        textComponent.fontSize = 16;
                        textComponent.color = Color.white;
                        textComponent.alignment = TextAlignmentOptions.Left;
                        
                        ui_joint_texts[jointName] = textComponent;
                    }
                    textComponent.text = $"{jointName}: {position:F3} rad";
                }
            }
        }
    }

    void ShowStatusError(string message)
    {
        if (statusPill == null) return;
        
        if (statusPillText == null) 
            statusPillText = statusPill.GetComponentInChildren<TMP_Text>(true);
            
        if (statusPillText != null)
            statusPillText.text = message;
            
        statusPill.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(HideStatusPillDelay());
    }

    IEnumerator HideStatusPillDelay()
    {
        yield return new WaitForSeconds(STATUS_PILL_DURATION);
        statusPill.SetActive(false);
    }

    void ApplyOrigin(Transform target, XElement originEl)
    {
        if (originEl == null) return;

        float[] pos = ParseFloats(originEl.Attribute("xyz")?.Value ?? "0 0 0", 3);
        float[] rot = ParseFloats(originEl.Attribute("rpy")?.Value ?? "0 0 0", 3);

        target.localPosition = ROS2UnityPos(pos[0], pos[1], pos[2]);
        target.localRotation = ROS2UnityRot(rot[0], rot[1], rot[2]);
    }

    Vector3 ROS2UnityPos(float x, float y, float z) => new Vector3(-y, z, x);
    Vector3 ROS2UnityAxis(float x, float y, float z) => new Vector3(-y, z, x).normalized;
    Vector3 ROS2UnityScale(float x, float y, float z) => new Vector3(Mathf.Abs(y), Mathf.Abs(z), Mathf.Abs(x));

    Quaternion ROS2UnityRot(float r, float p, float y)
    {
        Quaternion qX = Quaternion.AngleAxis(-r * Mathf.Rad2Deg, Vector3.forward);
        Quaternion qY = Quaternion.AngleAxis(-p * Mathf.Rad2Deg, Vector3.left);
        Quaternion qZ = Quaternion.AngleAxis(-y * Mathf.Rad2Deg, Vector3.up);      
        return qZ * qY * qX;
    }

    float[] ParseFloats(string input, int count, float defaultValue = 0f)
    {
        float[] result = new float[count];
        for (int i = 0; i < count; i++) result[i] = defaultValue;

        string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < Mathf.Min(parts.Length, count); i++)
        {
            float.TryParse(parts[i], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result[i]);
        }
        return result;
    }

    string ResolveMeshUrl(string rawPath)
    {
        string hostIp = ROSConnection.GetOrCreateInstance().RosIPAddress;

        if (rawPath.StartsWith("package://")) return rawPath.Replace("package://", $"http://{hostIp}:8000/assets/");
        if (rawPath.StartsWith("file://"))
        {
            string[] splitKeywords = { "/share/", "/src/" };
            foreach (string keyword in splitKeywords)
            {
                int index = rawPath.IndexOf(keyword);
                if (index != -1) return $"http://{hostIp}:8000/assets/{rawPath.Substring(index + keyword.Length)}";
            }
        }
        return null;
    }
}