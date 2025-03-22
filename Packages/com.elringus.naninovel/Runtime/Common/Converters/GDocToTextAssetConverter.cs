using System.Text;
using UnityEngine;

namespace Naninovel
{
    public class GDocToTextAssetConverter : IGoogleDriveConverter<TextAsset>
    {
        public RawDataRepresentation[] Representations { get; } = {
            new(null, "application/vnd.google-apps.document")
        };

        public string ExportMimeType => "text/plain";

        public TextAsset ConvertBlocking (byte[] obj, string name)
        {
            var textAsset = new TextAsset(Encoding.UTF8.GetString(obj));
            textAsset.name = name;
            return textAsset;
        }

        public UniTask<TextAsset> Convert (byte[] obj, string name) => UniTask.FromResult(ConvertBlocking(obj, name));

        public object ConvertBlocking (object obj, string name) => ConvertBlocking(obj as byte[], name);

        public async UniTask<object> Convert (object obj, string name) => await Convert(obj as byte[], name);
    }
}
