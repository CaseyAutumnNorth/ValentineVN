using UnityEngine;

namespace Naninovel
{
    /// <summary>
    /// Converts <see cref="T:byte[]"/> raw data of a .jpg or .png image to <see cref="Sprite"/>.
    /// </summary>
    public class JpgOrPngToSpriteConverter : IRawConverter<Sprite>
    {
        public RawDataRepresentation[] Representations { get; } = {
            new(".png", "image/png"),
            new(".jpg", "image/jpeg")
        };

        public Sprite ConvertBlocking (byte[] obj, string name)
        {
            var texture = new Texture2D(2, 2);
            texture.name = name;
            texture.LoadImage(obj, true);
            var rect = new Rect(0, 0, texture.width, texture.height);
            var sprite = Sprite.Create(texture, rect, Vector2.one * .5f);
            return sprite;
        }

        public UniTask<Sprite> Convert (byte[] obj, string name) => UniTask.FromResult(ConvertBlocking(obj, name));

        public object ConvertBlocking (object obj, string name) => ConvertBlocking(obj as byte[], name);

        public async UniTask<object> Convert (object obj, string name) => await Convert(obj as byte[], name);
    }
}
