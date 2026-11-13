using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace JellyfinPlayer.Shared
{
    public static class Json
    {
        public static T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)serializer.ReadObject(ms);
            }
        }

        public static string Serialize(object value)
        {
            var serializer = new DataContractJsonSerializer(value.GetType());
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, value);
                return Encoding.UTF8.GetString(ms.ToArray(), 0, (int)ms.Length);
            }
        }
    }
}
