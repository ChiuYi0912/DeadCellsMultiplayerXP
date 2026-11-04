using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace DeadCellsMultiplayerX.Utils
{
    public static class JsonOptions
    {
        public static readonly JsonSerializerOptions Pretty = Create(o =>
        {
            o.WriteIndented = true;
            o.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            o.IncludeFields = true;
            o.PropertyNameCaseInsensitive = true;
            o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            o.NumberHandling = JsonNumberHandling.AllowReadingFromString;
            o.Converters.Add(new JsonStringEnumConverter());
        });

        private static JsonSerializerOptions Create(Action<JsonSerializerOptions> configure)
        {
            var o = new JsonSerializerOptions();
            configure(o);
            o.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
            o.MakeReadOnly();
            return o;
        }
    }
}