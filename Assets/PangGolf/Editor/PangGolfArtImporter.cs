using System.IO;
using UnityEditor;

namespace PangGolf.EditorTools
{
    /// <summary>
    /// Import settings for art dropped into Assets/PangGolf/Resources/Art:
    /// terrain textures become readable (needed for mowing stripes) and
    /// files named *_Normal / *_nor* are imported as normal maps.
    /// </summary>
    public class PangGolfArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.Contains("PangGolf/Resources/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (n.EndsWith("_normal") || n.Contains("_nor") || n.Contains("normal"))
                ti.textureType = TextureImporterType.NormalMap;
            if (path.Contains("/Art/Terrain/"))
            {
                ti.isReadable = true;
                ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
                ti.anisoLevel = 8;
                ti.mipmapEnabled = true;
                ti.maxTextureSize = 2048;
            }
        }
    }
}
