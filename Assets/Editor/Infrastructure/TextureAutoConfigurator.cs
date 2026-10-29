using UnityEditor;
using UnityEngine;

public class TextureAutoConfigurator : AssetPostprocessor
{
    // This method runs automatically right before a texture is imported
    void OnPreprocessTexture()
    {
        // Get the texture importer for the asset currently being processed
        TextureImporter textureImporter = (TextureImporter)assetImporter;

        // Check if this is the very first time the asset is being imported
        if (textureImporter.importSettingsMissing)
        {
            // Set the filter mode to Point (pixelated/no filtering)
            textureImporter.filterMode = FilterMode.Point;

            // Force the texture to use uncompressed or truecolor if needed, 
            // or modify default platform settings for the max size
            TextureImporterPlatformSettings defaultSettings = textureImporter.GetDefaultPlatformTextureSettings();
            defaultSettings.maxTextureSize = 256;
            
            textureImporter.SetPlatformTextureSettings(defaultSettings);
        }
    }
}