using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models
{
    [JsonConverter(typeof(LocalizedStringConverter))]
    public class LocalizedString : Dictionary<string, string>
    {
        public LocalizedString() : base(StringComparer.OrdinalIgnoreCase) { }

        // 拷贝构造函数
        public LocalizedString(LocalizedString other) : base(other, StringComparer.OrdinalIgnoreCase) { }
    }
}