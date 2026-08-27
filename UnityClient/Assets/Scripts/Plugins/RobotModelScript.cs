using GLTFast;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unity.Robotics.ROSTCPConnector;
using UnityEngine;

public class RobotModelScript : MonoBehaviour
{
    [Header("ROS2 Settings")]
    public string robotDescriptionTopic = "/robot_description";
    public string jointStatesTopic = "/joint_states";

    private Vector3 meshRotationOffset = new Vector3(0, 90, 0);
    private bool URDFLoaded = false;
    private GameObject robotRootNode;

    private Dictionary<string, Transform> joint_nodes = new Dictionary<string, Transform>();
    private Dictionary<string, Quaternion> initial_rotations = new Dictionary<string, Quaternion>();
    private Dictionary<string, Vector3> joint_axes = new Dictionary<string, Vector3>();

    public void SetTopic(string topic) => robotDescriptionTopic = topic;

    public void BeginVisualization()
    {
        if (URDFLoaded) return;

        ROSConnection.GetOrCreateInstance().Subscribe<StringMsg>(robotDescriptionTopic, URDFCallback);
        ROSConnection.GetOrCreateInstance().Subscribe<JointStateMsg>(jointStatesTopic, JointStatesCallback);
        
        Debug.Log("Subscribed. Waiting for URDF...");
    }

    void URDFCallback(StringMsg msg)
    {
        if (URDFLoaded) return;
        URDFLoaded = true;
        
        Debug.Log("URDF received! Constructing pure kinematic tree...");

        if (robotRootNode != null) Destroy(robotRootNode);
        robotRootNode = new GameObject("RobotVisualizer");
        robotRootNode.transform.SetParent(this.transform, false);

        XDocument xDoc = XDocument.Parse(msg.data);
        
        Dictionary<string, XElement> linkElements = new Dictionary<string, XElement>();
        foreach (XElement link in xDoc.Descendants("link"))
        {
            string name = link.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name)) linkElements[name] = link;
        }

        List<XElement> jointElements = xDoc.Descendants("joint").ToList();

        // Find the root link (a link that is never a child in any joint)
        HashSet<string> childLinks = new HashSet<string>();
        foreach (XElement joint in jointElements)
        {
            string childName = joint.Element("child")?.Attribute("link")?.Value;
            if (!string.IsNullOrEmpty(childName)) childLinks.Add(childName);
        }

        string rootLinkName = linkElements.Keys.FirstOrDefault(l => !childLinks.Contains(l));
        if (string.IsNullOrEmpty(rootLinkName))
        {
            Debug.LogError("Could not find a root link in the URDF.");
            return;
        }

        // Recursively build the tree starting from the root link
        BuildLink(rootLinkName, linkElements, jointElements, robotRootNode.transform);
        
        Debug.Log("Robot tree constructed and meshes are downloading.");
    }

    void BuildLink(string linkName, Dictionary<string, XElement> linkElements, List<XElement> allJoints, Transform parentTransform)
    {
        if (!linkElements.TryGetValue(linkName, out XElement linkXml)) return;

        // 1. Create Link Node
        GameObject linkObj = new GameObject($"Link: {linkName}");
        linkObj.transform.SetParent(parentTransform, false);

        // 2. Process Visuals
        foreach (XElement visual in linkXml.Elements("visual"))
        {
            GameObject visualObj = new GameObject("Visual");
            visualObj.transform.SetParent(linkObj.transform, false);

            // Apply Visual Origin (Fix applied here)
            ApplyOrigin(visualObj.transform, visual.Element("origin"));

            XElement mesh = visual.Element("geometry")?.Element("mesh");
            if (mesh != null)
            {
                string filename = mesh.Attribute("filename")?.Value;
                string scaleStr = mesh.Attribute("scale")?.Value ?? "1 1 1";
                
                if (!string.IsNullOrEmpty(filename))
                {
                    string url = ResolveMeshUrl(filename);
                    if (!string.IsNullOrEmpty(url))
                    {
                        // Fire and forget the async mesh loader
                        _ = LoadMeshAsync(url, visualObj.transform, scaleStr);
                    }
                }
            }
        }

        // 3. Find and Build Child Joints
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

        // 1. Create Joint Node
        GameObject jointObj = new GameObject($"Joint: {jointName}");
        jointObj.transform.SetParent(parentTransform, false);

        // 2. Apply Joint Origin (Fix applied here)
        ApplyOrigin(jointObj.transform, jointXml.Element("origin"));

        // 3. Parse and Map Joint Axis
        Vector3 mappedAxis = Vector3.up; 
        XElement axisEl = jointXml.Element("axis");
        if (axisEl != null)
        {
            string xyz = axisEl.Attribute("xyz")?.Value ?? "0 0 1";
            float[] a = ParseFloats(xyz, 3);
            mappedAxis = ROS2UnityAxis(a[0], a[1], a[2]);
        }

        // 4. Cache Kinematic Data
        joint_nodes[jointName] = jointObj.transform;
        initial_rotations[jointName] = jointObj.transform.localRotation;
        joint_axes[jointName] = mappedAxis;

        // 5. Recursively build the child link attached to this joint
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
            
            // Apply the user-configurable rotation matrix (Euler angles) here
            meshNode.transform.localPosition = Vector3.zero;
            meshNode.transform.localRotation = Quaternion.Euler(meshRotationOffset);

            // Apply specific scale from URDF <mesh scale="...">
            float[] s = ParseFloats(scaleStr, 3, 1f);
            meshNode.transform.localScale = ROS2UnityScale(s[0], s[1], s[2]);

            await gltf.InstantiateMainSceneAsync(meshNode.transform);
        }
        else
        {
            Debug.LogError($"GLTFast failed to load: {url}");
        }
    }

    void JointStatesCallback(JointStateMsg msg)
    {
        if (joint_nodes.Count == 0 || msg.name.Length != msg.position.Length) return;
        
        for (int i = 0; i < msg.name.Length; i++)
        {
            string jointName = msg.name[i];
            if (joint_nodes.TryGetValue(jointName, out Transform jointTf) && joint_axes.TryGetValue(jointName, out Vector3 axis))
            {
                // Multiply by -1 to convert ROS right-handed rotation to Unity left-handed rotation
                float angleDeg = (float)(-msg.position[i] * Mathf.Rad2Deg);
                Quaternion rotationOffset = Quaternion.AngleAxis(angleDeg, axis);
                
                jointTf.localRotation = initial_rotations[jointName] * rotationOffset;
            }
        }
    }

    // --- Core Coordinate Converters (ROS Right-Handed Z-Up to Unity Left-Handed Y-Up) ---

    void ApplyOrigin(Transform target, XElement originEl)
    {
        if (originEl == null) return;

        string xyz = originEl.Attribute("xyz")?.Value ?? "0 0 0";
        string rpy = originEl.Attribute("rpy")?.Value ?? "0 0 0";

        float[] pos = ParseFloats(xyz, 3);
        float[] rot = ParseFloats(rpy, 3);

        target.localPosition = ROS2UnityPos(pos[0], pos[1], pos[2]);
        target.localRotation = ROS2UnityRot(rot[0], rot[1], rot[2]);
    }

    Vector3 ROS2UnityPos(float x, float y, float z) => new Vector3(-y, z, x);
    Vector3 ROS2UnityAxis(float x, float y, float z) => new Vector3(-y, z, x).normalized;
    Vector3 ROS2UnityScale(float x, float y, float z) => new Vector3(Mathf.Abs(y), Mathf.Abs(z), Mathf.Abs(x));

    Quaternion ROS2UnityRot(float r, float p, float y)
    {
        // FIX: ROS uses Right-Hand Rule, Unity uses Left-Hand Rule.
        // We MUST negate the angles (-r, -p, -y) to match the proper rotation direction.
        Quaternion qX = Quaternion.AngleAxis(-r * Mathf.Rad2Deg, Vector3.forward); // ROS X -> Unity Z
        Quaternion qY = Quaternion.AngleAxis(-p * Mathf.Rad2Deg, Vector3.left);    // ROS Y -> Unity -X
        Quaternion qZ = Quaternion.AngleAxis(-y * Mathf.Rad2Deg, Vector3.up);      // ROS Z -> Unity Y
        
        // URDF RPY is extrinsic fixed-axis X -> Y -> Z.
        // In Unity, multiplying qZ * qY * qX applies X, then Y, then Z.
        return qZ * qY * qX;
    }

    // --- Helpers ---

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

        if (rawPath.StartsWith("package://"))
        {
            return rawPath.Replace("package://", $"http://{hostIp}:8000/assets/");
        }
        else if (rawPath.StartsWith("file://"))
        {
            string[] splitKeywords = { "/share/", "/src/" };
            foreach (string keyword in splitKeywords)
            {
                int index = rawPath.IndexOf(keyword);
                if (index != -1)
                {
                    string relativePkgPath = rawPath.Substring(index + keyword.Length);
                    return $"http://{hostIp}:8000/assets/{relativePkgPath}";
                }
            }
        }
        return null;
    }

    void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && 
            UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            BeginVisualization();
        }
    }
}