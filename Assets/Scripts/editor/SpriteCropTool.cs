using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// YORU editor tool: Crop Sprite To Visible Pixels — June 2026
///
/// Fixes icons that look tiny because the image is mostly empty transparent space
/// with the actual picture sitting small in one corner (the seed icon problem).
///
/// How to use:
///   1. In the Project window, click the icon image (the .png).
///   2. Right-click it > YORU > Crop Sprite To Visible Pixels.
///   3. Done. The file is cropped IN PLACE so everything that already uses it
///      (like the kodama_offering item) keeps working with zero rewiring.
///      A safety copy of the original is saved next to it as NAME_backup.png.
///
/// What it does: finds the smallest rectangle that contains all visible (non
/// transparent) pixels, adds a small breathing margin, throws away the rest,
/// and re-imports. The visible picture then FILLS the image, so it fills the
/// inventory slot too.
/// </summary>
public static class SpriteCropTool
{
    private const byte AlphaThreshold = 10;   // pixels more transparent than this count as empty
    private const float MarginPercent = 0.03f; // small breathing border kept around the picture

    [MenuItem("Assets/YORU/Crop Sprite To Visible Pixels", true)]
    private static bool Validate()
    {
        return Selection.activeObject is Texture2D;
    }

    [MenuItem("Assets/YORU/Crop Sprite To Visible Pixels")]
    private static void Crop()
    {
        Texture2D selected = Selection.activeObject as Texture2D;
        if (selected == null) return;

        string path = AssetDatabase.GetAssetPath(selected);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Crop Sprite", "Could not read the import settings for this image.", "OK");
            return;
        }

        // 1. Temporarily make the texture readable at full quality so we can inspect pixels.
        bool wasReadable = importer.isReadable;
        TextureImporterCompression wasCompression = importer.textureCompression;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Texture2D readable = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Color32[] pixels = readable.GetPixels32();
        int width = readable.width;
        int height = readable.height;

        // 2. Find the smallest box containing every visible pixel.
        int minX = width, maxX = -1, minY = height, maxY = -1;
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[row + x].a > AlphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < 0)
        {
            RestoreImporter(importer, wasReadable, wasCompression);
            EditorUtility.DisplayDialog("Crop Sprite", "This image is completely transparent. Nothing to crop.", "OK");
            return;
        }

        // 3. Add a small margin and clamp to the image edges.
        int margin = Mathf.RoundToInt(Mathf.Max(width, height) * MarginPercent);
        minX = Mathf.Max(0, minX - margin);
        minY = Mathf.Max(0, minY - margin);
        maxX = Mathf.Min(width - 1, maxX + margin);
        maxY = Mathf.Min(height - 1, maxY + margin);

        int newW = maxX - minX + 1;
        int newH = maxY - minY + 1;

        float visiblePercent = 100f * (newW * (float)newH) / (width * (float)height);
        if (visiblePercent > 95f)
        {
            RestoreImporter(importer, wasReadable, wasCompression);
            EditorUtility.DisplayDialog("Crop Sprite",
                "The picture already fills this image (over 95% of it). Cropping would change nothing.\n\n" +
                "If the icon still looks small in the inventory, the problem is the slot UI, not the image.", "OK");
            return;
        }

        // 4. Safety copy of the original, then write the cropped image over the same file
        //    so every existing reference (item assets, prefabs) keeps working untouched.
        string backupPath = Path.ChangeExtension(path, null) + "_backup.png";
        if (!File.Exists(backupPath)) File.Copy(path, backupPath);

        Texture2D cropped = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
        cropped.SetPixels(readable.GetPixels(minX, minY, newW, newH));
        cropped.Apply();
        File.WriteAllBytes(path, cropped.EncodeToPNG());
        Object.DestroyImmediate(cropped);

        // 5. Restore the original import settings and re-import the now-cropped file.
        RestoreImporter(importer, wasReadable, wasCompression);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(backupPath, ImportAssetOptions.ForceUpdate);

        EditorUtility.DisplayDialog("Crop Sprite",
            $"Done!\n\nBefore: {width} x {height} (picture used only {visiblePercent:F0}% of it)\n" +
            $"After: {newW} x {newH} (picture fills the image)\n\n" +
            $"Original saved as {Path.GetFileName(backupPath)}.", "OK");
    }

    private static void RestoreImporter(TextureImporter importer, bool wasReadable, TextureImporterCompression wasCompression)
    {
        importer.isReadable = wasReadable;
        importer.textureCompression = wasCompression;
        importer.SaveAndReimport();
    }
}
