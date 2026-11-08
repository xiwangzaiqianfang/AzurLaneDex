using AzurLaneDex.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 配队分享码（文本）：把「多舰队」配队压成紧凑 JSON → GZip → Base64Url，得到一串“乱码”，
    /// 只能由本软件解析回配队（<see cref="Decode"/>）。前缀 <c>ALD1:</c> 保持不变。
    /// </summary>
    public static class TeamShareCode
    {
        private const string Prefix = "ALD1:";

        private static readonly JsonSerializerOptions JsonOpt = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        /// <summary>分享码内的单支舰队。</summary>
        private sealed class FleetDto
        {
            [JsonPropertyName("n")] public string Name { get; set; } = "";
            [JsonPropertyName("u")] public List<int> ShipIds { get; set; } = new();
            [JsonPropertyName("r")] public List<bool> Retrofits { get; set; } = new();
            [JsonPropertyName("a")] public List<int>? Augments { get; set; }
            [JsonPropertyName("e")] public Dictionary<string, Dictionary<int, int>>? Equips { get; set; }
        }

        /// <summary>分享码内容。</summary>
        private sealed class TeamDto
        {
            [JsonPropertyName("n")] public string Name { get; set; } = "";
            [JsonPropertyName("d")] public string Description { get; set; } = "";
            [JsonPropertyName("k")] public string SupportKind { get; set; } = "carrier";
            [JsonPropertyName("f")] public List<FleetDto> Surface { get; set; } = new();
            [JsonPropertyName("b")] public FleetDto? Submarine { get; set; }
            [JsonPropertyName("p")] public FleetDto? Support { get; set; }
        }

        /// <summary>生成分享码（水面舰队 + 水下 + 支援，含每槽改造与配装）。</summary>
        public static string Encode(Team team)
        {
            team.Normalize();

            var dto = new TeamDto
            {
                Name = team.Name ?? "",
                Description = team.Description ?? "",
                SupportKind = team.SupportKind,
                Surface = team.Surface.Select(ToDto).ToList(),
                Submarine = ToDto(team.Submarine),
                Support = ToDto(team.Support)
            };

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOpt);
            return Prefix + ToBase64Url(Compress(json));
        }

        /// <summary>解析分享码；格式不符/解压失败返回 null。容忍空白、换行与缺失前缀。</summary>
        public static Team? Decode(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            string text = code.Trim()
                .Replace("\r", "").Replace("\n", "").Replace(" ", "").Replace("\t", "");
            if (text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                text = text.Substring(Prefix.Length);

            try
            {
                byte[] json = Decompress(FromBase64Url(text));
                var dto = JsonSerializer.Deserialize<TeamDto>(json, JsonOpt);
                if (dto == null || dto.Surface.Count == 0) return null;

                var team = new Team
                {
                    Name = dto.Name ?? "",
                    Description = dto.Description ?? "",
                    SupportKind = string.IsNullOrEmpty(dto.SupportKind) ? "carrier" : dto.SupportKind,
                    Surface = dto.Surface.Select(FromDto).ToList(),
                    Submarine = FromDto(dto.Submarine),
                    Support = FromDto(dto.Support)
                };
                team.Normalize();
                return team;
            }
            catch (Exception ex)
            {
                LogService.Warning("分享码解析失败：" + ex.Message, nameof(TeamShareCode));
                return null;
            }
        }

        private static FleetDto ToDto(Fleet fleet)
        {
            int count = fleet.ShipIds.Count;
            var equips = new Dictionary<string, Dictionary<int, int>>();
            for (int i = 0; i < count; i++)
            {
                var map = fleet.Equips != null && i < fleet.Equips.Count ? fleet.Equips[i] : null;
                if (map != null && map.Count > 0) equips[i.ToString()] = new Dictionary<int, int>(map);
            }

            return new FleetDto
            {
                Name = fleet.Name ?? "",
                ShipIds = new List<int>(fleet.ShipIds),
                Retrofits = new List<bool>(fleet.Retrofits ?? Enumerable.Repeat(false, count)),
                Augments = new List<int>(fleet.Augments ?? Enumerable.Repeat(0, count)),
                Equips = equips.Count > 0 ? equips : null
            };
        }

        private static Fleet FromDto(FleetDto? dto)
        {
            var fleet = new Fleet();
            if (dto == null) return fleet;

            fleet.Name = dto.Name ?? "";
            fleet.ShipIds = new List<int>(dto.ShipIds);
            fleet.Retrofits = new List<bool>(dto.Retrofits);
            fleet.Augments = dto.Augments != null ? new List<int>(dto.Augments) : new List<int>();

            int count = fleet.ShipIds.Count;
            fleet.Normalize(count);
            if (dto.Equips != null)
            {
                foreach (var kv in dto.Equips)
                {
                    if (!int.TryParse(kv.Key, out int index) || index < 0 || index >= count) continue;
                    fleet.Equips![index] = new Dictionary<int, int>(kv.Value);
                }
            }
            return fleet;
        }

        private static byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
                gzip.Write(data, 0, data.Length);
            return output.ToArray();
        }

        private static byte[] Decompress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var gzip = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            gzip.CopyTo(output);
            return output.ToArray();
        }

        private static string ToBase64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] FromBase64Url(string text)
        {
            string s = text.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 1: s += "==="; break;
            }
            return Convert.FromBase64String(s);
        }
    }
}
