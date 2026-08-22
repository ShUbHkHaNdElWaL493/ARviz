using GLTFast;
using RosMessageTypes.Std;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.UrdfImporter;
using UnityEngine;

public class DynamicRobotVisualizer : MonoBehaviour
{
    [Header("Network Settings")]
    public string urdf_topic = "/robot_description";
    private bool urdf_loaded = false;
    public void SetTopic(string topic) => urdf_topic = topic;

    public void BeginVisualization()
    {
        if (urdf_loaded) return;

        string current_ip = ROSConnection.GetOrCreateInstance().RosIPAddress;
        
        ROSConnection.GetOrCreateInstance().Subscribe<StringMsg>(urdf_topic, URDFCallback);
        Debug.Log($"Waiting for URDF on {urdf_topic} at {current_ip}...");
    }

    void URDFCallback(StringMsg msg)
    {
        if (urdf_loaded) return;
        urdf_loaded = true;

        Debug.Log("URDF received! Parsing and stripping visuals...");

        XDocument x_doc = XDocument.Parse(msg.data);
        Dictionary<string, string> link_to_mesh_url = new Dictionary<string, string>();

        foreach (XElement link in x_doc.Descendants("link"))
        {
            string link_name = link.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(link_name)) continue;

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
                            link_to_mesh_url[link_name] = http_url;
                        }
                    }
                }
            }

            link.Elements("visual").Remove();
            link.Elements("collision").Remove();
        }

        string temp_urdf_path = Path.Combine(Application.persistentDataPath, "temp_robot.urdf");
        x_doc.Save(temp_urdf_path);

        Unity.Robotics.UrdfImporter.ImportSettings settings = new Unity.Robotics.UrdfImporter.ImportSettings { 
            chosenAxis = Unity.Robotics.UrdfImporter.ImportSettings.axisType.yAxis,
            convexMethod = Unity.Robotics.UrdfImporter.ImportSettings.convexDecomposer.unity
        };
        
        GameObject robot_root = UrdfRobotExtensions.CreateRuntime(temp_urdf_path, settings);
        robot_root.transform.SetParent(this.transform, false);

        ArticulationBody root_body = robot_root.GetComponentInChildren<ArticulationBody>();
        if (root_body != null)
        {
            root_body.immovable = true;
        }

        LoadMeshes(robot_root, link_to_mesh_url);
    }

    async void LoadMeshes(GameObject robot_root, Dictionary<string, string> link_to_mesh_url)
    {
        foreach (var kvp in link_to_mesh_url)
        {
            string link_name = kvp.Key;
            string url = kvp.Value;

            Transform link_transform = FindDeepChild(robot_root.transform, link_name);
            
            if (link_transform != null && (url.EndsWith(".dae", System.StringComparison.OrdinalIgnoreCase) || url.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)))
            {
                var gltf = new GltfImport();
                bool success = await gltf.Load(url);
                if (success)
                {
                    GameObject visual_node = new GameObject("VisualMesh");
                    visual_node.transform.SetParent(link_transform, false);
                    await gltf.InstantiateMainSceneAsync(visual_node.transform);
                }
                else
                {
                    Debug.LogError($"glTFast failed to download or parse: {url}");
                }
            }
        }
    }

    Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
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
            Debug.LogWarning($"Could not map file:// path to HTTP server asset: {raw_path}");
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