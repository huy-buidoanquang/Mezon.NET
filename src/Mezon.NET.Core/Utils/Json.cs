using Newtonsoft.Json;

namespace Mezon.NET.Utils
{
    public static class Json
    {
        public static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
            NullValueHandling = NullValueHandling.Include
        };

        internal static JsonSerializer Serializer = JsonSerializer.Create(JsonSerializerSettings);
    }
}
