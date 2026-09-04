using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public sealed class ProductionAssetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
        }

        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            importer.mipmapEnabled = !assetPath.Contains("/UI/", StringComparison.OrdinalIgnoreCase);
            importer.streamingMipmaps = importer.mipmapEnabled;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
        }

        private void OnPreprocessAudio()
        {
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = assetPath.Contains("/Music/", StringComparison.OrdinalIgnoreCase)
                ? AudioClipLoadType.Streaming
                : AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            importer.defaultSampleSettings = settings;
        }
    }
}
