using AzurLaneDex.Models;
using AzurLaneDex.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using static AzurLaneDex.Models.ShipStatic;

namespace AzurLaneDex.Services
{
    public class ShipMigrator : IShipMigrator
    {
        private readonly IShipDataStore _dataStore;
        private static readonly Dictionary<string, int> FactionToId = new()
        {
            ["白鹰"] = (int)Faction.EagleUnion,
            ["皇家"] = (int)Faction.RoyalNavy,
            ["重樱"] = (int)Faction.SakuraEmpire,
            ["铁血"] = (int)Faction.IronBlood,
            ["东煌"] = (int)Faction.DragonEmpery,
            ["撒丁帝国"] = (int)Faction.Sardegna,
            ["北方联合"] = (int)Faction.NorthernUnion,
            ["自由鸢尾"] = (int)Faction.FreeFrench,
            ["维希教廷"] = (int)Faction.Vichya,
            ["郁金王国"] = (int)Faction.Tulip,
            ["晶环联盟"] = (int)Faction.CrystalLeague,
            ["飓风"] = (int)Faction.Tempesta,
            ["其他"] = (int)Faction.Other,
            ["超次元游戏海王星"] = (int)Faction.Collab_Nep,
            ["哔哩哔哩"] = (int)Faction.Collab_Bilibili,
            ["传颂之物"] = (int)Faction.Collab_Utawarerumono,
            ["绊爱"] = (int)Faction.Collab_KizunaAI,
            ["Hololive"] = (int)Faction.Collab_Hololive,
            ["死或生沙滩排球"] = (int)Faction.Collab_DoAXVV,
            ["偶像大师"] = (int)Faction.Collab_Idolmaster,
            ["SSSS"] = (int)Faction.Collab_SSSS,
            ["莱莎的炼金工房"] = (int)Faction.Collab_Ryza,
            ["闪乱神乐"] = (int)Faction.Collab_Senran,
            ["出包王女"] = (int)Faction.Collab_Toloveru,
            ["黑岩射手"] = (int)Faction.Collab_BRS,
            ["地城邂逅"] = (int)Faction.Collab_Danmachi,
            ["优米雅的炼金工房"] = (int)Faction.Collab_Yumia,
            ["约会大作战V"] = (int)Faction.Collab_DAL,
            ["破敌之炬"] = (int)Faction.Meta_Flame,
            ["湮烬之核"] = (int)Faction.Meta_Core,
            ["构造之理"] = (int)Faction.Meta_Reason,
            ["逐光之焰"] = (int)Faction.Meta_Light,
            ["摇曳之火"] = (int)Faction.Meta_Fire,
        };
        private static readonly Dictionary<string, int> ShipClassToId = new()
        {
            ["驱逐"] = (int)ShipClass.DD,
            ["轻巡"] = (int)ShipClass.CL,
            ["重巡"] = (int)ShipClass.CA,
            ["超巡"] = (int)ShipClass.CB,
            ["重炮"] = (int)ShipClass.BM,
            ["战巡"] = (int)ShipClass.BC,
            ["战列"] = (int)ShipClass.BB,
            ["航战"] = (int)ShipClass.BBV,
            ["航母"] = (int)ShipClass.CV,
            ["轻航"] = (int)ShipClass.CVL,
            ["维修"] = (int)ShipClass.AR,
            ["潜艇"] = (int)ShipClass.SS,
            ["潜母"] = (int)ShipClass.SSV,
            ["运输"] = (int)ShipClass.AE,
            ["风帆"] = (int)ShipClass.Sail,
        };
        private static readonly Dictionary<string, int> RarityToId = new()
        {
            ["普通"] = (int)Rarity.N,
            ["稀有"] = (int)Rarity.R,
            ["精锐"] = (int)Rarity.SR,
            ["超稀有"] = (int)Rarity.SSR,
            ["海上传奇"] = (int)Rarity.UR,
            ["最高方案"] = (int)Rarity.Decisive,
            ["决战方案"] = (int)Rarity.Ultimate,
        };
        private static readonly Dictionary<string, int> AttributeToId = new()
        {
            ["无"] = (int)AttributeType.None,
            ["耐久"] = (int)AttributeType.HP,
            ["炮击"] = (int)AttributeType.FP,
            ["雷击"] = (int)AttributeType.TRP,
            ["防空"] = (int)AttributeType.AA,
            ["航空"] = (int)AttributeType.AVI,
            ["命中"] = (int)AttributeType.ACC,
            ["装填"] = (int)AttributeType.RLD,
            ["机动"] = (int)AttributeType.EVA,
            ["反潜"] = (int)AttributeType.ASW,
        };

        public ShipMigrator(IShipDataStore dataStore)
        {
            _dataStore = dataStore;
        }

        public bool IsOldFormat(string jsonContent)
        {
            if (string.IsNullOrEmpty(jsonContent)) return false;
            return !jsonContent.Contains("\"faction_id\"") && !jsonContent.Contains("\"name\":{");
        }

        public async Task<bool> MigrateAsync()
        {
            try
            {
                var staticPath = Path.Combine(App.DataRoot, "static", "ships_static.json");
                if (!File.Exists(staticPath)) return false;
                var oldJson = await File.ReadAllTextAsync(staticPath);
                if (!IsOldFormat(oldJson)) return false;

                using var doc = JsonDocument.Parse(oldJson);
                var root = doc.RootElement;
                var shipsArray = root.GetProperty("ships");
                var newShips = new List<ShipStatic>();
                foreach (var oldShip in shipsArray.EnumerateArray())
                {
                    newShips.Add(MigrateSingleShip(oldShip));
                }
                newShips = newShips.OrderBy(s => s.Id).ToList();
                var newStatic = new StaticData
                {
                    Version = BuildVersion(newShips.Count, 0),
                    Ships = newShips
                };
                var backup = staticPath + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Copy(staticPath, backup);
                var json = JsonSerializer.Serialize(newStatic, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(staticPath, json);
                return true;
            }
            catch (Exception ex)
            {
                LogService.Error("数据迁移失败", "ShipMigrator", ex);
                return false;
            }
        }

        private string BuildVersion(int shipCount, int revision)
        {
            string date = DateTime.Now.ToString("yyyyMMdd");
            return $"2.0.{shipCount}.{revision}.{date}";
        }

        public ShipStatic MigrateSingleShip(JsonElement old)
        {
            // ========== 辅助函数 ==========
            string GetChineseString(JsonElement elem, string fallback = "")
            {
                if (elem.ValueKind == JsonValueKind.String)
                    return elem.GetString() ?? fallback;
                if (elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty("zh-Hans", out var zh))
                    return zh.GetString() ?? fallback;
                return fallback;
            }

            string GetStringProp(string propName) =>
                old.TryGetProperty(propName, out var elem) && elem.ValueKind == JsonValueKind.String ? elem.GetString() ?? "" : "";

            string GetOldAcquireText(string propName)
            {
                if (!old.TryGetProperty(propName, out var elem)) return "";
                if (elem.ValueKind == JsonValueKind.String)
                    return elem.GetString() ?? "";
                if (elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty("zh-Hans", out var zh))
                    return zh.GetString() ?? "";
                return "";
            }

            List<string> SplitIntoSentences(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return new List<string>();
                var separators = new char[] { '、', '，', ',', '；', ';', '\n', '\r' };
                var parts = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                return parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            }

            void AddEntry(List<AcquireEntry> entries, string tag, List<string>? paramsList = null)
            {
                if (paramsList == null) paramsList = new List<string>();
                if (entries.Any(e => e.Tag == tag)) return;
                entries.Add(new AcquireEntry { Tag = tag, Parameters = paramsList, CustomText = new LocalizedString() });
            }

            int ExtractNumber(string text, string pattern)
            {
                var match = Regex.Match(text, pattern);
                return match.Success && int.TryParse(match.Groups[1].Value, out int val) ? val : 0;
            }

            // ========== 解析旧字段 ==========
            int id = old.GetProperty("id").GetInt32();
            string nameChs = GetChineseString(old.GetProperty("name"));
            string altNameChs = old.TryGetProperty("alt_name", out var altElem) ? GetChineseString(altElem) : "";
            string factionChs = old.TryGetProperty("faction", out var facElem) ? GetChineseString(facElem) : "";
            string shipClassChs = old.TryGetProperty("ship_class", out var scElem) ? GetChineseString(scElem) : "";
            string rarityChs = old.TryGetProperty("rarity", out var raElem) ? GetChineseString(raElem) : "";

            // 属性加成
            int obtainAttrId = (int)AttributeType.None;
            int obtainValue = 0;
            if (old.TryGetProperty("obtain_bonus_attr", out var obAttrElem) && obAttrElem.ValueKind == JsonValueKind.String)
            {
                string obtainAttrStr = obAttrElem.GetString()?.Trim();
                if (!string.IsNullOrEmpty(obtainAttrStr))
                {
                    obtainAttrId = AttributeToId.GetValueOrDefault(obtainAttrStr, (int)AttributeType.None);
                    if (old.TryGetProperty("obtain_bonus_value", out var obValElem) && obValElem.ValueKind == JsonValueKind.Number)
                        obtainValue = obValElem.GetInt32();
                }
            }

            int level120AttrId = (int)AttributeType.None;
            int level120Value = 0;
            if (old.TryGetProperty("level120_bonus_attr", out var lvAttrElem) && lvAttrElem.ValueKind == JsonValueKind.String)
            {
                string level120AttrStr = lvAttrElem.GetString()?.Trim();
                if (!string.IsNullOrEmpty(level120AttrStr))
                {
                    level120AttrId = AttributeToId.GetValueOrDefault(level120AttrStr, (int)AttributeType.None);
                    if (old.TryGetProperty("level120_bonus_value", out var lvValElem) && lvValElem.ValueKind == JsonValueKind.Number)
                        level120Value = lvValElem.GetInt32();
                }
            }

            // 适用舰种
            List<int> obtainAffectIds = new List<int>();
            if (old.TryGetProperty("obtain_affects", out var affectsElem) && affectsElem.ValueKind == JsonValueKind.Array)
            {
                var affects = affectsElem.EnumerateArray()
                    .Select(x => x.GetString()?.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                obtainAffectIds = MapShipClassListStatic(affects);
            }
            if (obtainAffectIds.Count == 0 && old.TryGetProperty("ship_class", out var shipClassElem) && shipClassElem.ValueKind == JsonValueKind.String)
            {
                string shipClass = shipClassElem.GetString()?.Trim();
                if (!string.IsNullOrEmpty(shipClass))
                    obtainAffectIds = MapShipClassListStatic(new List<string> { shipClass });
            }

            List<int> level120AffectIds = new List<int>();
            if (old.TryGetProperty("level120_affects", out var lvAffectsElem) && lvAffectsElem.ValueKind == JsonValueKind.Array)
            {
                var affects = lvAffectsElem.EnumerateArray()
                    .Select(x => x.GetString()?.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                level120AffectIds = MapShipClassListStatic(affects);
            }
            else
            {
                level120AffectIds = obtainAffectIds;
            }

            int factionId = FactionToId.GetValueOrDefault(factionChs, (int)Faction.Other);
            int shipClassId = ShipClassToId.GetValueOrDefault(shipClassChs, (int)ShipClass.DD);
            int rarityId = RarityToId.GetValueOrDefault(rarityChs, (int)Rarity.N);

            ShipCategory category = ShipCategory.Normal;
            if (old.TryGetProperty("category", out var catElem) && catElem.ValueKind == JsonValueKind.Number)
                category = (ShipCategory)catElem.GetInt32();
            else if (factionChs == "META" || nameChs.Contains("META"))
                category = ShipCategory.META;

            int categoryOrder = old.TryGetProperty("category_order", out var co) ? co.GetInt32() : 0;
            int gameOrder = old.TryGetProperty("game_order", out var go) ? go.GetInt32() : 0;

            string acquireMainText = GetOldAcquireText("acquire_main");
            string acquireDetailText = GetOldAcquireText("acquire_detail");
            string buildTimeRaw = GetStringProp("build_time");
            string buildTime = CleanBuildTime(buildTimeRaw, id);
            string shopExchange = GetStringProp("shop_exchange");
            bool isPermanent = old.TryGetProperty("is_permanent", out var perm) && perm.ValueKind == JsonValueKind.True;
            string debutEventChs = GetOldAcquireText("debut_event");
            string releaseDate = GetStringProp("release_date");
            string notesChs = GetOldAcquireText("notes");
            bool canRemodel = old.TryGetProperty("can_remodel", out var cr) && cr.ValueKind == JsonValueKind.True;
            string remodelDate = GetStringProp("remodel_date");
            bool canSpecialGear = old.TryGetProperty("can_special_gear", out var csg) && csg.ValueKind == JsonValueKind.True;
            string specialGearName = GetStringProp("special_gear_name");
            string specialGearDate = GetStringProp("special_gear_date");
            string specialGearAcquire = GetStringProp("special_gear_acquire");
            int techPointsObtain = old.TryGetProperty("tech_points_obtain", out var tpO) && tpO.ValueKind == JsonValueKind.Number ? tpO.GetInt32() : 0;
            int techPointsMax = old.TryGetProperty("tech_points_max", out var tpM) && tpM.ValueKind == JsonValueKind.Number ? tpM.GetInt32() : 0;
            int techPoints120 = old.TryGetProperty("tech_points_120", out var tp120) && tp120.ValueKind == JsonValueKind.Number ? tp120.GetInt32() : 0;

            // 打捞地点
            List<string> dropLocations = new List<string>();
            if (old.TryGetProperty("drop_locations", out var drops) && drops.ValueKind == JsonValueKind.Array)
                dropLocations = drops.EnumerateArray().Select(x => x.GetString() ?? "").ToList();

            // 初始化 acquireEntries 列表
            List<AcquireEntry> acquireEntries = new List<AcquireEntry>();

            // 拆分 drop_locations 并补充 acquire_46
            var processedDrops = new List<string>();
            foreach (var loc in dropLocations)
            {
                var parts = SplitDropLocationString(loc);
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    processedDrops.Add(part);
                    if (part.StartsWith("作战档案"))
                    {
                        var parsed = ParseArchiveLocation(part);
                        if (parsed != null)
                        {
                            EnsureAcquire46Entry(acquireEntries, parsed.ArchiveName, parsed.Stages);
                        }
                    }
                }
            }
            dropLocations = processedDrops;

            // ========== 构建 AcquireEntries ==========
            string fullText = acquireMainText + "，" + acquireDetailText;

            // ----- 1. 建造 -----
            bool isUnbuildable = fullText.Contains("无法建造") || (buildTimeRaw != null && buildTimeRaw.Contains("无法建造"));
            if (isUnbuildable)
            {
                AddEntry(acquireEntries, "acquire_11");
            }
            else
            {
                var match = Regex.Match(buildTimeRaw, @"[（(]([^）)]+)[）)]");
                if (match.Success)
                {
                    string pool = match.Groups[1].Value;
                    if (pool.Contains("轻型")) AddEntry(acquireEntries, "acquire_7");
                    else if (pool.Contains("重型")) AddEntry(acquireEntries, "acquire_8");
                    else if (pool.Contains("特型") || pool.Contains("特种")) AddEntry(acquireEntries, "acquire_9");
                    else if (pool.Contains("期间限定")) AddEntry(acquireEntries, "acquire_10");
                }
                else
                {
                    if (fullText.Contains("轻型池建造")) AddEntry(acquireEntries, "acquire_7");
                    else if (fullText.Contains("重型池建造")) AddEntry(acquireEntries, "acquire_8");
                    else if (fullText.Contains("特型池建造") || fullText.Contains("特种池建造")) AddEntry(acquireEntries, "acquire_9");
                    else if (fullText.Contains("期间限定建造")) AddEntry(acquireEntries, "acquire_10");
                }
            }

            // ----- 2. 打捞（包括 drop_locations 中的档案掉落）-----
            bool hasDrop = dropLocations != null && dropLocations.Any();
            bool isUndroppable = fullText.Contains("无法打捞") || fullText.Contains("不可打捞") ||
                                 (dropLocations != null && dropLocations.Any(loc => loc.Contains("无法打捞") || loc.Contains("不可打捞")));

            if (isUndroppable)
            {
                AddEntry(acquireEntries, "acquire_50");
            }
            else if (hasDrop)
            {
                AddEntry(acquireEntries, "acquire_2");
            }
            else
            {
                AddEntry(acquireEntries, "acquire_50");
            }

            // 作战档案通关60次（acquire_47）
            var archive60Match = Regex.Match(fullText, @"作战档案通关60次(.+?)(?:获得|：)(.+)");
            if (archive60Match.Success)
                AddEntry(acquireEntries, "acquire_47", new List<string> { archive60Match.Groups[1].Value, archive60Match.Groups[2].Value });

            // 互斥清理：若有 acquire_50 则移除其他打捞标签
            if (acquireEntries.Any(e => e.Tag == "acquire_50"))
            {
                acquireEntries.RemoveAll(e => e.Tag == "acquire_2" || e.Tag == "acquire_3" || e.Tag == "acquire_4" ||
                                              e.Tag == "acquire_5" || e.Tag == "acquire_46" || e.Tag == "acquire_47");
            }

            // ----- 3. 商店兑换 -----
            bool hasShop = !string.IsNullOrEmpty(shopExchange);
            bool isUnexchangeable = fullText.Contains("无法兑换") || (hasShop && shopExchange.Contains("无法兑换"));
            if (isUnexchangeable)
            {
                AddEntry(acquireEntries, "acquire_51");
            }
            else if (hasShop)
            {
                var coreMatch = Regex.Match(shopExchange, @"核心(?:月度兑换)?(?:商店)?(\d+)核心数据兑换");
                if (coreMatch.Success)
                    AddEntry(acquireEntries, "acquire_17", new List<string> { coreMatch.Groups[1].Value });

                var medalMatch = Regex.Match(shopExchange, @"勋章商店(\d+)荣誉勋章兑换");
                if (medalMatch.Success)
                    AddEntry(acquireEntries, "acquire_18", new List<string> { medalMatch.Groups[1].Value });

                var fleetMatch = Regex.Match(shopExchange, @"(\d+)舰队币");
                if (fleetMatch.Success)
                    AddEntry(acquireEntries, "acquire_14", new List<string> { fleetMatch.Groups[1].Value });

                var meritMatch = Regex.Match(shopExchange, @"(\d+)功勋");
                if (meritMatch.Success)
                    AddEntry(acquireEntries, "acquire_15", new List<string> { meritMatch.Groups[1].Value });

                if (shopExchange.StartsWith("结晶："))
                {
                    string crystalName = shopExchange.Substring(3).Trim();
                    AddEntry(acquireEntries, "acquire_60", new List<string> { "", crystalName });
                }
                else
                {
                    var metaCrystalMatch = Regex.Match(shopExchange, @"META商店(\d+)破碎结晶兑换结晶：(.+)");
                    if (metaCrystalMatch.Success)
                        AddEntry(acquireEntries, "acquire_60", new List<string> { metaCrystalMatch.Groups[1].Value, metaCrystalMatch.Groups[2].Value });
                    else
                    {
                        var metaMatch = Regex.Match(shopExchange, @"(\d+)破碎结晶");
                        if (metaMatch.Success)
                            AddEntry(acquireEntries, "acquire_16", new List<string> { metaMatch.Groups[1].Value });
                    }
                }

                if (shopExchange.Contains("原型商店"))
                    AddEntry(acquireEntries, "acquire_19");

                var activityShopMatch = Regex.Match(fullText, @"(.+?)活动商店(.+?)兑换");
                if (activityShopMatch.Success)
                {
                    string eventName = activityShopMatch.Groups[1].Value;
                    string detail = activityShopMatch.Groups[2].Value;
                    int pt = ExtractNumber(detail, @"(\d+)");
                    AddEntry(acquireEntries, "acquire_20", new List<string> { eventName, pt.ToString(), "" });
                }

                if (!acquireEntries.Any(e => e.Tag.StartsWith("acquire_14") || e.Tag.StartsWith("acquire_15") ||
                                             e.Tag == "acquire_18" || e.Tag == "acquire_19" || e.Tag == "acquire_17" ||
                                             e.Tag == "acquire_16" || e.Tag == "acquire_20" || e.Tag == "acquire_60"))
                {
                    if (shopExchange.Contains("兑换") || shopExchange.Contains("商店"))
                        AddEntry(acquireEntries, "acquire_24");
                }
            }

            // ----- 4. 任务奖励 / 活动赠送 -----
            if (fullText.Contains("日/周常任务")) AddEntry(acquireEntries, "acquire_41");
            if (fullText.Contains("月度签到")) AddEntry(acquireEntries, "acquire_42");
            if (fullText.Contains("活动任务")) AddEntry(acquireEntries, "acquire_43");
            if (fullText.Contains("主线普通关卡三星奖励")) AddEntry(acquireEntries, "acquire_44");
            if (fullText.Contains("大型EX") || fullText.Contains("中型SP")) AddEntry(acquireEntries, "acquire_45");
            if (fullText.Contains("世界巡游")) AddEntry(acquireEntries, "acquire_37");
            if (fullText.Contains("布里支援计划")) AddEntry(acquireEntries, "acquire_39");
            if (fullText.Contains("活动获取")) AddEntry(acquireEntries, "acquire_55");

            // ----- 5. 邀请函 / 贺年卡 -----
            var inviteMatches = Regex.Matches(fullText, @"(\d{4})年?(?:宴会|周年|庆典)邀请函");
            foreach (Match m in inviteMatches)
                AddEntry(acquireEntries, "acquire_27", new List<string> { m.Groups[1].Value });

            var heMatch = Regex.Match(fullText, @"贺年卡（([甲乙丙丁戊己庚辛壬癸])([子丑寅卯辰巳午未申酉戌亥])）");
            if (heMatch.Success)
            {
                string gan = heMatch.Groups[1].Value;
                string zhi = heMatch.Groups[2].Value;
                string year = "";
                var yearMatch = Regex.Match(fullText, @"（(\d{4})）");
                if (yearMatch.Success) year = yearMatch.Groups[1].Value;
                AddEntry(acquireEntries, "acquire_25", new List<string> { gan, zhi, year });
            }

            // ----- 6. 科研 -----
            var researchMatch = Regex.Match(fullText, @"科研(\d+)期");
            if (researchMatch.Success)
                AddEntry(acquireEntries, "acquire_22", new List<string> { researchMatch.Groups[1].Value });

            // ----- 7. 礼包购买 -----
            var giftMatch = Regex.Match(fullText, @"(.+?)礼包购买");
            if (giftMatch.Success)
                AddEntry(acquireEntries, "acquire_21", new List<string> { giftMatch.Groups[1].Value });

            // ----- 8. 勋章支援 -----
            if (fullText.Contains("勋章支援"))
                AddEntry(acquireEntries, "acquire_12");

            // ----- 9. 指挥官等级奖励 -----
            var levelRewardMatch = Regex.Match(fullText, @"指挥官等级达到(\d+)级(?:获得|奖励)");
            if (levelRewardMatch.Success)
                AddEntry(acquireEntries, "acquire_59", new List<string> { levelRewardMatch.Groups[1].Value });

            // ----- 10. 剩余未匹配文本 -----
            var allSentences = SplitIntoSentences(fullText);
            var standardTags = TagLibrary.GetAllTags().Where(t => t.MatchRegex != null && t.Tag != "acquire_custom").ToList();
            foreach (var sentence in allSentences)
            {
                bool alreadyHandled = false;
                if (acquireEntries.Any(e => e.Tag == "acquire_2" && sentence.Contains("打捞"))) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_7" && sentence.Contains("轻型"))) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_8" && sentence.Contains("重型"))) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_9" && sentence.Contains("特型"))) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_10" && sentence.Contains("期间限定"))) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_5" && sentence.Contains("仅限打捞"))) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_12" && sentence.Contains("勋章支援"))) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_41") && sentence.Contains("日常")) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_42") && sentence.Contains("签到")) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_43") && sentence.Contains("活动任务")) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_44") && sentence.Contains("三星")) alreadyHandled = true;
                if (acquireEntries.Any(e => e.Tag == "acquire_24") && sentence.Contains("兑换")) alreadyHandled = true;
                if (alreadyHandled) continue;

                bool matched = false;
                foreach (var tagDef in standardTags)
                {
                    var match = tagDef.MatchRegex.Match(sentence);
                    if (match.Success)
                    {
                        var entry = new AcquireEntry { Tag = tagDef.Tag };
                        for (int i = 1; i <= tagDef.ParamCount && i < match.Groups.Count; i++)
                            entry.Parameters.Add(match.Groups[i].Value);
                        while (entry.Parameters.Count < tagDef.ParamCount)
                            entry.Parameters.Add("");
                        if (!acquireEntries.Any(e => e.Tag == entry.Tag))
                            acquireEntries.Add(entry);
                        matched = true;
                        break;
                    }
                }
                if (!matched && !string.IsNullOrWhiteSpace(sentence) && !sentence.All(c => "，、。；".Contains(c)))
                {
                    var customLoc = new LocalizedString();
                    customLoc["zh-Hans"] = sentence;
                    acquireEntries.Add(new AcquireEntry { Tag = "acquire_custom", CustomText = customLoc });
                }
            }

            // ========== 互斥规则强制清理 ==========
            if (acquireEntries.Any(e => e.Tag == "acquire_5"))
            {
                acquireEntries.RemoveAll(e => e.Tag.StartsWith("acquire_7") || e.Tag.StartsWith("acquire_8") ||
                                              e.Tag.StartsWith("acquire_9") || e.Tag.StartsWith("acquire_10") ||
                                              e.Tag.StartsWith("acquire_14") || e.Tag.StartsWith("acquire_15") ||
                                              e.Tag == "acquire_19" || e.Tag == "acquire_24" || e.Tag == "acquire_20");
            }
            if (acquireEntries.Any(e => e.Tag == "acquire_11"))
            {
                acquireEntries.RemoveAll(e => e.Tag.StartsWith("acquire_7") || e.Tag.StartsWith("acquire_8") ||
                                              e.Tag.StartsWith("acquire_9") || e.Tag.StartsWith("acquire_10"));
            }
            if (acquireEntries.Any(e => e.Tag == "acquire_50"))
            {
                acquireEntries.RemoveAll(e => e.Tag == "acquire_2" || e.Tag == "acquire_3" || e.Tag == "acquire_4" ||
                                              e.Tag == "acquire_5" || e.Tag == "acquire_46" || e.Tag == "acquire_47");
            }
            if (acquireEntries.Any(e => e.Tag == "acquire_51"))
            {
                acquireEntries.RemoveAll(e => e.Tag.StartsWith("acquire_14") || e.Tag.StartsWith("acquire_15") ||
                                              e.Tag == "acquire_19" || e.Tag == "acquire_24" || e.Tag == "acquire_20");
            }
            if (id == 1 || id == 2 || id == 3)
            {
                acquireEntries.RemoveAll(entry =>
                    entry.Tag == "acquire_21" &&
                    entry.Parameters.Count == 1 &&
                    (entry.Parameters[0].Contains("兑换、赠送") || entry.Parameters[0].Contains("日/周常任务"))
                );
            }

            // ========== 构建多语言对象 ==========
            LocalizedString CreateLoc(string chs, string chtFallback = null) => new LocalizedString
            {
                ["zh-Hans"] = chs ?? "",
                ["zh-Hant"] = string.IsNullOrEmpty(chtFallback) ? (chs ?? "") : chtFallback,
                ["en"] = "",
                ["ja"] = ""
            };

            // ========== 解析专属兵装 ==========
            List<SpecialGearEntry> gearEntries = new List<SpecialGearEntry>();
            LocalizedString localizedGearName = new LocalizedString();
            if (canSpecialGear && !string.IsNullOrEmpty(specialGearName))
                localizedGearName["zh-Hans"] = specialGearName;

            if (canSpecialGear && !string.IsNullOrEmpty(specialGearAcquire))
            {
                var gearTags = TagLibrary.GetAllGearTags();
                bool matched = false;
                foreach (var tagDef in gearTags)
                {
                    if (tagDef.MatchRegex == null) continue;
                    var match = tagDef.MatchRegex.Match(specialGearAcquire);
                    if (match.Success)
                    {
                        var entry = new SpecialGearEntry { Tag = tagDef.Tag };
                        for (int i = 1; i <= tagDef.ParamCount && i < match.Groups.Count; i++)
                            entry.Parameters.Add(match.Groups[i].Value);
                        while (entry.Parameters.Count < tagDef.ParamCount)
                            entry.Parameters.Add("");
                        gearEntries.Add(entry);
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    var custom = new LocalizedString();
                    custom["zh-Hans"] = specialGearAcquire;
                    gearEntries.Add(new SpecialGearEntry { Tag = "gear_custom", CustomText = custom });
                }
            }

            // ========== 返回新的 ShipStatic ==========
            return new ShipStatic
            {
                Id = id,
                Name = CreateLoc(nameChs, altNameChs),
                AltName = CreateLoc(altNameChs, ""),
                FactionId = factionId,
                ShipClassId = shipClassId,
                RarityId = rarityId,
                GameOrder = gameOrder,
                Category = category,
                CategoryOrder = categoryOrder,
                AcquireEntries = acquireEntries,
                AcquireMainLegacy = CreateLoc(acquireMainText, ""),
                AcquireDetailLegacy = CreateLoc(acquireDetailText, ""),
                BuildTime = buildTime,
                DropLocations = dropLocations,
                ShopExchange = shopExchange,
                IsPermanent = isPermanent,
                DebutEvent = CreateLoc(debutEventChs, ""),
                ReleaseDate = releaseDate,
                Notes = CreateLoc(notesChs, ""),
                CanRemodel = canRemodel,
                RemodelDate = remodelDate,
                CanSpecialGear = canSpecialGear,
                SpecialGearName = localizedGearName,
                SpecialGearDate = specialGearDate,
                SpecialGearEntries = gearEntries,
                SpecialGearAcquire = new LocalizedString(),
                ObtainBonusAttrId = obtainAttrId,
                ObtainBonusValue = obtainValue,
                ObtainAffectClassIds = obtainAffectIds,
                Level120BonusAttrId = level120AttrId,
                Level120BonusValue = level120Value,
                Level120AffectClassIds = level120AffectIds,
                TechPointsObtain = techPointsObtain,
                TechPointsMax = techPointsMax,
                TechPoints120 = techPoints120
            };
        }

        private static List<int> MapShipClassListStatic(List<string> chineseClasses)
        {
            if (chineseClasses == null) return new List<int>();
            var ids = new List<int>();
            foreach (var cls in chineseClasses)
                if (ShipClassToId.TryGetValue(cls, out var id))
                    ids.Add(id);
            return ids;
        }

        private List<int> MapShipClassList(List<string> chineseClasses) => MapShipClassListStatic(chineseClasses);

        private static string CleanBuildTime(string buildTime, int shipId)
        {
            if (shipId == 103 && (buildTime == "吸血鬼" || string.IsNullOrEmpty(buildTime)))
                return "00:26:00";
            var match = Regex.Match(buildTime, @"\d{2}:\d{2}:\d{2}");
            return match.Success ? match.Value : "";
        }

        private static List<string> SplitDropLocationString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            var parts = raw.Split(new[] { '、', '，', ',' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        private class ArchiveInfo { public string ArchiveName; public string Stages; }

        private static ArchiveInfo? ParseArchiveLocation(string loc)
        {
            if (!loc.StartsWith("作战档案")) return null;

            // 标准格式：作战档案xxx：yyy
            int colonIdx = loc.IndexOfAny(new[] { '：', ':' });
            if (colonIdx > "作战档案".Length)
            {
                string archiveName = loc.Substring("作战档案".Length, colonIdx - "作战档案".Length).Trim();
                string stages = loc.Substring(colonIdx + 1).Trim();
                return new ArchiveInfo { ArchiveName = archiveName, Stages = stages };
            }

            // 彩蛋掉落格式：作战档案神圣的悲喜剧D1/D2彩蛋掉落
            var eggMatch = Regex.Match(loc, @"作战档案(.+?)([A-Z]\d+/[A-Z]\d+)彩蛋掉落");
            if (eggMatch.Success)
            {
                return new ArchiveInfo
                {
                    ArchiveName = eggMatch.Groups[1].Value.Trim(),
                    Stages = eggMatch.Groups[2].Value.Trim()
                };
            }

            // 图例格式：作战档案微层混合A图/C图彩蛋掉落
            var mapMatch = Regex.Match(loc, @"作战档案(.+?)([A-Z]图/[A-Z]图)彩蛋掉落");
            if (mapMatch.Success)
            {
                return new ArchiveInfo
                {
                    ArchiveName = mapMatch.Groups[1].Value.Trim(),
                    Stages = mapMatch.Groups[2].Value.Trim()
                };
            }

            return null;
        }

        private static void EnsureAcquire46Entry(List<AcquireEntry> entries, string archiveName, string stages)
        {
            if (entries == null) return;
            bool exists = entries.Any(e => e.Tag == "acquire_46" &&
                                           e.Parameters.Count >= 2 &&
                                           e.Parameters[0] == archiveName &&
                                           e.Parameters[1] == stages);
            if (!exists)
            {
                entries.Add(new AcquireEntry
                {
                    Tag = "acquire_46",
                    Parameters = new List<string> { archiveName, stages },
                    CustomText = new LocalizedString()
                });
            }
        }
    }
}