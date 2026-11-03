using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Moba.Editor
{
    /// <summary>
    /// 自定义 JFIF 格式图片导入器：
    /// 使 Unity AssetDatabase 将 .jfif 文件作为一等公民 Texture2D 与 Sprite 原生导入和引用。
    /// </summary>
    [ScriptedImporter(1, "jfif")]
    public class JfifImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            byte[] fileData = File.ReadAllBytes(ctx.assetPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(fileData))
            {
                texture.name = Path.GetFileNameWithoutExtension(ctx.assetPath) + "_Tex";
                ctx.AddObjectToAsset("texture", texture);

                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
                ctx.AddObjectToAsset("sprite", sprite);
                ctx.SetMainObject(sprite);
            }
        }
    }
}
