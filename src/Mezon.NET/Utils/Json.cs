using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mezon.NET.Utils
{
    public static class Json
    {
        public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, SerializerOptions);

        public static string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, SerializerOptions);
    }
}
