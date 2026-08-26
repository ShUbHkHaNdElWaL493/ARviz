using UnityEngine;

public class Grid3D : MonoBehaviour
{
    public Color lineColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
    public int textureSize = 256;
    public int lineThickness = 3; 

    void Start()
    {
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

        GetComponent<MeshRenderer>().material = gridMat;
    }
}