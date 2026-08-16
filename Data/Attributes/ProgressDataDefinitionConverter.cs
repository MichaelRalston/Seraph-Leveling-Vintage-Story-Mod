using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using SeraphLeveling.Data.Attributes;
using Vintagestory.API.Server;

namespace SeraphLeveling.Data
{
    public class ProgressDataDefinitionConverter<D, PD> : JsonConverter
        where D : AttributeModifierDefinition<D, PD>, IConstructable<D, PD>
        where PD : AttributeModifierProgressData<D, PD>
    {
        private readonly ISaveableAttribute _definition;

        public ProgressDataDefinitionConverter(ISaveableAttribute definition)
        {
            _definition = definition;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(PD);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;

            // 1. Parse player progress properties (credits, levels, etc.)
            JObject jo = JObject.Load(reader);

            // 2. Invoke the exact primary constructor using the valid definition instance
            var progressData = (PD)Activator.CreateInstance(typeof(PD), _definition);


            // 3. Populate internal properties/fields from the json payload
            using (JsonReader objectReader = jo.CreateReader())
            {
                serializer.Populate(objectReader, progressData);
            }

            return progressData;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override bool CanWrite => false;
    };

    public static class Conversion
    {
        public static void DeserializeWithDefinition<D, PD>(string json, D definition, string key, ICoreServerAPI serverApi)
                            where D : AttributeModifierDefinition<D, PD>, IConstructable<D, PD>
                            where PD : AttributeModifierProgressData<D, PD>
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { IgnoreSerializableAttribute = true },
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };

            settings.Converters.Add(new ProgressDataDefinitionConverter<D, PD>(definition));

            definition.ProgressDictionary.TryAdd(key, JsonConvert.DeserializeObject<PD>(json, settings));
            definition.PersistProgress(serverApi);
        }

        public static string Serialize(object obj)
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { IgnoreSerializableAttribute = true },
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                ObjectCreationHandling = ObjectCreationHandling.Replace
            };
            return JsonConvert.SerializeObject(obj, settings);
        }

        public static void PortData<D, PD>(D legacyDefinition, D definition, ICoreServerAPI serverApi)
                            where D : AttributeModifierDefinition<D, PD>, IConstructable<D, PD>
                            where PD : AttributeModifierProgressData<D, PD>
        {
            legacyDefinition.LoadProgress(serverApi);
            definition.LoadProgress(serverApi);
            if (!legacyDefinition.ProgressDictionary.IsEmpty && definition.ProgressDictionary.IsEmpty)
            {
                var snapshot = legacyDefinition.ProgressDictionary.ToArray();
                serverApi.Logger.Debug($"[SeraphLeveling] Porting legacy {definition.Id} for {snapshot.Length} players.");
                foreach (var kvp in snapshot)
                {
                    string json = Serialize(kvp.Value);
                    DeserializeWithDefinition<D, PD>(json, definition, kvp.Key, serverApi);
                }
            }
        }
    }
}