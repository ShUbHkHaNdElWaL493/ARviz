using GLTFast;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.UrdfImporter;
using UnityEngine;

public class RobotModelScript : MonoBehaviour
{
    [Header("Network Settings")]
    public string robot_description_topic = "/robot_description";
    public string joint_states_topic = "/joint_states";

    private bool urdf_loaded = false;
    private string dummy_stl_path;

    private Dictionary<string, Transform> joint_nodes = new Dictionary<string, Transform>();
    private Dictionary<string, Quaternion> initial_rotations = new Dictionary<string, Quaternion>();

    public void SetTopic(string topic) => robot_description_topic = topic;

    public void BeginVisualization()
    {
        if (urdf_loaded) return;

        dummy_stl_path = Path.Combine(Application.persistentDataPath, "dummy.stl");
        string dummyStl = "solid dummy\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendfacet\nendsolid dummy";
        File.WriteAllText(dummy_stl_path, dummyStl);

        string current_ip = ROSConnection.GetOrCreateInstance().RosIPAddress;

        ROSConnection.GetOrCreateInstance().Subscribe<StringMsg>(robot_description_topic, URDFCallback);
        ROSConnection.GetOrCreateInstance().Subscribe<JointStateMsg>(joint_states_topic, JointStatesCallback);

        Debug.Log($"Waiting for URDF on {robot_description_topic} at {current_ip}...");
    }

    async void URDFCallback(StringMsg msg)
    {
        if (urdf_loaded) return;
        urdf_loaded = true;

        Debug.Log("URDF received! Parsing as a pure visual hierarchy...");

        XDocument x_doc = XDocument.Parse(msg.data);

        Dictionary<string, string> joint_to_child_link = new Dictionary<string, string>();
        foreach (XElement joint in x_doc.Descendants("joint"))
        {
            string j_name = joint.Attribute("name")?.Value;
            string c_link = joint.Element("child")?.Attribute("link")?.Value;
            if (!string.IsNullOrEmpty(j_name) && !string.IsNullOrEmpty(c_link))
            {
                joint_to_child_link[j_name] = c_link;
            }
        }

        foreach (XElement link in x_doc.Descendants("link"))
        {
            foreach (XElement visual in link.Elements("visual"))
            {
                XElement mesh = visual.Element("geometry")?.Element("mesh");
                if (mesh != null)
                {
                    string file_name = mesh.Attribute("filename")?.Value;
                    if (!string.IsNullOrEmpty(file_name))
                    {
                        string http_url = ResolveMeshUrl(file_name);
                        if (!string.IsNullOrEmpty(http_url))
                        {
                            visual.SetAttributeValue("name", "GLTF_URL|" + http_url);
                            mesh.SetAttributeValue("filename", "file://" + dummy_stl_path);
                        }
                    }
                }
            }
            link.Elements("collision").Remove();
            link.Elements("inertial").Remove();
        }

        string temp_urdf_path = Path.Combine(Application.persistentDataPath, "temp_robot.urdf");
        x_doc.Save(temp_urdf_path);

        Unity.Robotics.UrdfImporter.ImportSettings settings = new Unity.Robotics.UrdfImporter.ImportSettings { 
            chosenAxis = Unity.Robotics.UrdfImporter.ImportSettings.axisType.yAxis,
            convexMethod = Unity.Robotics.UrdfImporter.ImportSettings.convexDecomposer.unity
        };

        GameObject robot_root = UrdfRobotExtensions.CreateRuntime(temp_urdf_path, settings);
        robot_root.transform.SetParent(this.transform, false);

        foreach (var joint in robot_root.GetComponentsInChildren<Unity.Robotics.UrdfImporter.UrdfJoint>()) Destroy(joint);
        foreach (var ab in robot_root.GetComponentsInChildren<ArticulationBody>()) Destroy(ab);

        Transform[] all_tfs = robot_root.GetComponentsInChildren<Transform>(true);
        foreach (var kvp in joint_to_child_link)
        {
            string joint_name = kvp.Key;
            string child_link = kvp.Value;
            foreach (Transform tf in all_tfs)
            {
                if (tf.name == child_link)
                {
                    joint_nodes[joint_name] = tf;
                    initial_rotations[joint_name] = tf.localRotation;
                    break;
                }
            }
        }

        robot_root.SetActive(false);
        await LoadMeshesAsync(robot_root);
        robot_root.SetActive(true);

        Debug.Log("Robot fully assembled and ready to animate!");
    }

    void JointStatesCallback(JointStateMsg msg)
    {
        if (joint_nodes.Count == 0 || msg.name.Length != msg.position.Length) return;
        for (int i = 0; i < msg.name.Length; i++)
        {
            string joint_name = msg.name[i];
            if (joint_nodes.TryGetValue(joint_name, out Transform joint_tf))
            {
                float angle_deg = (float)(-msg.position[i] * Mathf.Rad2Deg);
                Quaternion joint_rotation = Quaternion.AngleAxis(angle_deg, Vector3.up);
                joint_tf.localRotation = initial_rotations[joint_name] * joint_rotation;
            }
        }
    }

    async Task LoadMeshesAsync(GameObject robot_root)
    {
        List<Task> downloadTasks = new List<Task>();
        Transform[] all_transforms = robot_root.GetComponentsInChildren<Transform>(true);
        foreach (Transform tf in all_transforms)
        {
            if (tf.name.StartsWith("GLTF_URL|"))
            {
                string url = tf.name.Substring("GLTF_URL|".Length);
                downloadTasks.Add(DownloadAndAttachMesh(tf, url));
            }
        }
        await Task.WhenAll(downloadTasks);
    }

    async Task DownloadAndAttachMesh(Transform tf, string url)
    {
        Transform geometry_anchor = tf;
        MeshRenderer dummy_renderer = tf.GetComponentInChildren<MeshRenderer>();
        if (dummy_renderer != null)
        {
            geometry_anchor = dummy_renderer.transform; 
            Destroy(dummy_renderer.GetComponent<MeshFilter>());
            Destroy(dummy_renderer);
        }

        var gltf = new GltfImport();
        bool success = await gltf.Load(url);
        if (success)
        {
            GameObject visual_node = new GameObject("VisualMesh");
            visual_node.transform.SetParent(geometry_anchor, false);
            await gltf.InstantiateMainSceneAsync(visual_node.transform);
        }
        else
        {
            Debug.LogError($"glTFast failed to download or parse: {url}");
        }
    }

    string ResolveMeshUrl(string raw_path)
    {
        string host_ip = ROSConnection.GetOrCreateInstance().RosIPAddress;

        if (raw_path.StartsWith("package://"))
        {
            return raw_path.Replace("package://", $"http://{host_ip}:8000/assets/");
        }
        else if (raw_path.StartsWith("file://"))
        {
            string[] split_keywords = { "/share/", "/src/" };
            foreach (string keyword in split_keywords)
            {
                int index = raw_path.IndexOf(keyword);
                if (index != -1)
                {
                    string relative_pkg_path = raw_path.Substring(index + keyword.Length);
                    return $"http://{host_ip}:8000/assets/{relative_pkg_path}";
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