using System.Text;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace Liv.Lck.Core.Serialization
{
    [Preserve]
    internal class LckJsonSerializer : ILckSerializer
    {
        public SerializationType SerializationType => SerializationType.JsonUTF8;

        [Preserve]
        public LckJsonSerializer()
        {
        }

        public byte[] Serialize(object data)
        {
            var serializedContext = JsonConvert.SerializeObject(data);
            return Encoding.UTF8.GetBytes(serializedContext);
        }

        public T Deserialize<T>(byte[] data)
        {
            var json = Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}

