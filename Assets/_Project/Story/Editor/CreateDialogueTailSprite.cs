using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates a simple transparent PNG triangle sprite for the dialogue bubble tail.
/// </summary>
public static class CreateDialogueTailSprite
{
    private const string OutputFolder = "Assets/_Project/Story/Sprites/UI";
    private const string OutputPath = OutputFolder + "/DialogueBubbleTail_Triangle.png";

    [MenuItem("Tools/Story/Create Dialogue Tail Triangle Sprite")]
    public static void Create()
    {
        EnsureFolder("Assets/_Project");
        EnsureFolder("Assets/_Project/Story");
        EnsureFolder("Assets/_Project/Story/Sprites");
        EnsureFolder("Assets/_Project/Story/Sprites/UI");

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false, linear: true);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1, 1, 1, 0);
        Color fill = Color.white;

        // Fill transparent.
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = clear;
        }

        // Draw a downward triangle whose top edge touches the top border (for seamless connection).
        // Coordinate: (0,0) bottom-left in texture space.
        int topY = size - 1;
        int tipY = 2; // leave tiny padding at bottom
        int tipX = size / 2;
        int topLeftX = Mathf.RoundToInt(size * 0.1f);
        int topRightX = Mathf.RoundToInt(size * 0.9f);

        for (int y = tipY; y <= topY; y++)
        {
            float t = (y - tipY) / Mathf.Max(1f, (topY - tipY));
            // At tip: width 1px, at top: full width (topRightX-topLeftX)
            float halfW = Mathf.Lerp(0.5f, (topRightX - topLeftX) * 0.5f, t);
            int xMin = Mathf.Clamp(Mathf.RoundToInt(tipX - halfW), 0, size - 1);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(tipX + halfW), 0, size - 1);
            for (int x = xMin; x <= xMax; x++)
            {
                pixels[y * size + x] = fill;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(OutputPath, png);
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

        // Configure importer as Sprite (2D and UI).
        var importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }

        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath));
        Debug.Log("已生成: " + OutputPath);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        if (slash <= 0)
        {
            return;
        }

        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }
}

