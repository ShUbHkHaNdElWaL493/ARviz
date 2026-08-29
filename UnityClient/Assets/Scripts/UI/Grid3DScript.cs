using UnityEngine;
using UnityEngine.UI;

public class Grid3D : MonoBehaviour
{

    [Header("UI Settings")]
    public Toggle ARToggle;

    [Header("Tracking Settings")]
    public Transform robotAnchor;

    [Header("Grid Settings")]
    public Color lineColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
    public int textureSize = 256;
    public int lineThickness = 3; 

    private MeshRenderer meshRenderer;

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();

        Texture2D tex = new Texture2D(textureSize, textureSize);
        tex.wrapMode = TextureWrapMode.Repeat;
        Color backgroundColor = Camera.main.backgroundColor;

        for (int x = 0; x < textureSize; x++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                bool isLine = (x < lineThickness || y < lineThickness);
                tex.SetPixel(x, y, isLine ? lineColor : backgroundColor);
            }
        }
        tex.Apply();

        Material gridMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        gridMat.SetFloat("_Surface", 1);
        gridMat.mainTexture = tex;
        gridMat.mainTextureScale = new Vector2(10, 10); 
        meshRenderer.material = gridMat;

        if (ARToggle != null)
        {
            ARToggle.onValueChanged.AddListener(OnARToggled);
            OnARToggled(ARToggle.isOn);
        }
    }

    private void OnARToggled(bool isARActive)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = !isARActive;
        }
    }

    void OnDestroy()
    {
        if (ARToggle != null)
        {
            ARToggle.onValueChanged.RemoveListener(OnARToggled);
        }
    }
}