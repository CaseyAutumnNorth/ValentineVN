using UnityEngine;

namespace Naninovel
{
    /// <summary>
    /// Converts <see cref="T:byte[]"/> raw data of a .png or .jpg image to <see cref="Texture2D"/>.
    /// </summary>
    public class JpgOrPngToTextureConverter : IRawConverter<Texture2D>
    {
        public RawDataRepresentation[] Representations { get; } = {
            new(".png", "image/png"),
            new(".jpg", "image/jpeg")
        };

        public Texture2D ConvertBlocking (byte[] obj, string name)
        {
            var texture = new Texture2D(2, 2);
            texture.name = name;
            texture.LoadImage(obj, true);
            return texture;
        }

        public UniTask<Texture2D> Convert (byte[] obj, string name) => UniTask.FromResult(ConvertBlocking(obj, name));

        public object ConvertBlocking (object obj, string name) => ConvertBlocking(obj as byte[], name);

        public async UniTask<object> Convert (object obj, string name) => await Convert(obj as byte[], name);
    }
}
