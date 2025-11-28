using System.IO;
using UnityEditor;
using UnityEngine;

public class AssetNamingEnforcer : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string assetPath in importedAssets)
        {
            if (assetPath.EndsWith(".prefab"))
            {
                EnforceNamingConvention(assetPath, "P_");
            }
            else if (assetPath.EndsWith(".mat"))
            {
                EnforceNamingConvention(assetPath, "M_");
            }
            else if (assetPath.EndsWith(".png") || assetPath.EndsWith(".jpg") || assetPath.EndsWith(".tga"))
            {
                HandleTextureOrSpriteNaming(assetPath);
            }
            else if (assetPath.EndsWith(".anim") || assetPath.EndsWith(".controller"))
            {
                EnforceNamingConvention(assetPath, "A_");
            }
        }
    }

    private static void EnforceNamingConvention(string assetPath, string prefix)
    {
        string fileName = Path.GetFileNameWithoutExtension(assetPath);
        // Remove existing prefix if it matches the expected type (e.g., T_ or S_)
        if (fileName.StartsWith("T_") || fileName.StartsWith("S_") || fileName.StartsWith("P_") || fileName.StartsWith("M_") || fileName.StartsWith("A_"))
        {
            fileName = fileName.Substring(2); // Remove the first two characters (prefix + underscore)
        }
        string newName = prefix + fileName;
        if (Path.GetFileNameWithoutExtension(assetPath) != newName)
        {
            AssetDatabase.RenameAsset(assetPath, newName);
            Debug.Log($"Renamed {Path.GetFileNameWithoutExtension(assetPath)} to {newName}");
        }
    }

    private static void HandleTextureOrSpriteNaming(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            if (importer.textureType == TextureImporterType.Sprite)
            {
                EnforceNamingConvention(assetPath, "S_");
            }
            else
            {
                EnforceNamingConvention(assetPath, "T_");
            }
        }
    }
}
