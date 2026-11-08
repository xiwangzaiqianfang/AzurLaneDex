using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.Resources;

namespace AzurLaneDex.ViewModels
{
    /// <summary>
    /// 舰船视图模型：包装一个静态数据（<see cref="ShipStatic"/>）与一个动态状态
    /// （<see cref="ShipState"/>，键为 Id/ShipId），向 XAML 提供只读显示属性与
    /// 可写状态属性。数据驱动模式：全部静态信息来自 ShipStatic，本类不做数据推导。
    /// </summary>
    public class ShipViewModel : INotifyPropertyChanged
    {
        private readonly ShipStatic _static;
        private ShipState _state;
        private bool _isSelected;
        private readonly ResourceLoader _loader = ResourceLoader.GetForViewIndependentUse();

        public ShipViewModel(ShipStatic staticShip, ShipState state)
        {
            _static = staticShip ?? throw new ArgumentNullException(nameof(staticShip));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        // ================= 基础标识 =================

        /// <summary>数据源舰船 id（= ShipState.ShipId 主键）。</summary>
        public int Id => _static.Id;

        /// <summary>图鉴排序号。</summary>
        public int GameOrder => _static.GameOrder;

        /// <summary>全服英文前缀名（头像/立绘文件命名基准）。</summary>
        public string RawName => _static.GlobalName;

        /// <summary>图鉴编号显示（优先数据源 WikiNumber，如 "NO.077"；缺失回退 NO.xxx）。</summary>
        public string DisplayId
        {
            get
            {
                if (!string.IsNullOrEmpty(_static.WikiNumber))
                    return _static.WikiNumber;
                return $"NO.{Id:D3}";
            }
        }

        // ================= 名称与多语言显示 =================

        /// <summary>显示名（本地化；改造后追加资源后缀）。</summary>
        public string DisplayName
        {
            get
            {
                string baseName = _static.Name.GetLocalized();
                if (Retrofitted && _static.Retrofit?.CanRetrofit == true &&
                    !baseName.EndsWith(_loader.GetString("ShipName_RemodelSuffix")))
                {
                    return baseName + _loader.GetString("ShipName_RemodelSuffix");
                }
                return baseName;
            }
        }

        /// <summary>别名列表（ships.json aliases 等）。</summary>
        public List<string> Aliases => _static.Aliases;

        /// <summary>别名拼接文本（搜索用）。</summary>
        public string AliasesText => string.Join(", ", _static.Aliases);

        /// <summary>舰级（本地化）。</summary>
        public string ClassName => _static.Class.GetLocalized();

        /// <summary>声优（本地化）。</summary>
        public string CV => _static.CV.GetLocalized();

        /// <summary>画师。</summary>
        public string Artist => _static.Artist;

        public string ShipType => LocalizationHelper.GetEnumString("ShipType", (int)_static.Type);
        public string Faction => LocalizationHelper.GetEnumString("Faction", (int)_static.Faction);
        public string Rarity => LocalizationHelper.GetEnumString("Rarity", (int)_static.Rarity);
        public string Category => LocalizationHelper.GetEnumString("ShipCategory", (int)_static.Category);

        // ================= 枚举（筛选/排序用） =================

        public ShipCategory CategoryEnum => _static.Category;
        public ShipType ShipTypeEnum => _static.Type;

        /// <summary>数据库舰种 id（ship_types.id；DDG 先锋=2 / 主力=3 等，用于编队槽位与装备过滤）。</summary>
        public int ShipTypeId => _static.TypeId;

        /// <summary>本舰可用的专属兵装（DB spweapons；图标 Assets/icon/item/spweapon/{Id}.png）。</summary>
        public IReadOnlyList<UniqueAugmentInfo> Augments => _static.UniqueAugments ?? new List<UniqueAugmentInfo>();
        public Faction FactionEnum => _static.Faction;
        public Rarity RarityEnum => _static.Rarity;

        /// <summary>是否为布里（数据位掩码判定）。</summary>
        public bool IsBulin => _static.IsBulin;

        // ================= 静态数据透传（详情页等消费） =================

        /// <summary>每档属性（含成长系数）。</summary>
        public List<ShipStatsLevel> StatsLevels => _static.StatsLevels;

        /// <summary>每档装备槽（外层=突破档）。</summary>
        public List<List<EquipmentSlotInfo>> SlotLevels => _static.SlotLevels;

        /// <summary>基础（0 破）档装备槽——过渡用，详情页按需取具体档位。</summary>
        public List<EquipmentSlotInfo> EquipmentSlots =>
            _static.SlotLevels.Count > 0 ? _static.SlotLevels[0] : new List<EquipmentSlotInfo>();

        /// <summary>专属兵装（unique_aug），无则 null。</summary>
        public UniqueAugmentInfo? UniqueAugment => _static.UniqueAugment;

        /// <summary>是否有专属兵装。</summary>
        public bool CanSpecialGear => _static.UniqueAugment != null;

        /// <summary>每档突破信息。</summary>
        public List<LimitBreakInfo> LimitBreaks => _static.LimitBreaks;

        /// <summary>技能列表（装载期已展开）。</summary>
        public List<Skill> Skills => _static.Skills;

        /// <summary>改造数据（可改造船非空）。</summary>
        public RetrofitData? Retrofit => _static.Retrofit;

        /// <summary>科研数据（科研船非空）。</summary>
        public ResearchData? Research => _static.Research;

        /// <summary>META 舰专属数据（非 META 为 null）。</summary>
        public MetaData? Meta => _static.Meta;

        /// <summary>获取方式数据。</summary>
        public AcquisitionData Acquisition => _static.Acquisition;

        /// <summary>皮肤列表（含默认皮占位，见 Skin.Id=-1 语义）。</summary>
        public List<Skin> Skins => _static.Skins;

        /// <summary>是否有誓约皮肤。</summary>
        public bool HasOathSkin => _static.HasOathSkin;

        /// <summary>按皮肤分组的台词。</summary>
        public List<SkinVoiceGroup> WordsBySkin => _static.WordsBySkin;

        /// <summary>讨厌的礼物物品 id。</summary>
        public List<int> GiftDislikeIds => _static.GiftDislikeIds;

        /// <summary>讨厌的礼物名称（与 <see cref="GiftDislikeIds"/> 下标对齐；数据库缺名称时为空串）。</summary>
        public List<string> GiftDislikeNames => _static.GiftDislikeNames;

        /// <summary>讨厌的礼物图标相对路径（与 <see cref="GiftDislikeNames"/> 下标对齐；空=无图）。</summary>
        public List<string> GiftDislikeIcons => _static.GiftDislikeIcons;

        /// <summary>讨厌的礼物品质（与 <see cref="GiftDislikeNames"/> 下标对齐；0=未知）。</summary>
        public List<int> GiftDislikeRarities => _static.GiftDislikeRarities;

        /// <summary>作强化材料的属性经验（null=不可强化）。</summary>
        public EnhanceExp? EnhanceExp => _static.StrengthenExp;

        /// <summary>能否用作强化材料。</summary>
        public bool CanBeEnhanceMaterial => _static.StrengthenExp != null;

        /// <summary>舰队科技（无科技点的船为 null）。</summary>
        public FleetTech? FleetTech => _static.FleetTech;

        // ================= 日期与事件 =================

        /// <summary>实装日期（yyyy-MM-dd；数据按文本存储，无需再换算）。</summary>
        public string ReleaseDate => _static.DateText;

        /// <summary>图鉴编号（DB `codex_no` + `codex_series` 前缀补零，如 NO.005 / NO.Collab001）。</summary>
        public string WikiNumber => _static.WikiNumber;

        /// <summary>和谐名（DB `name_codes.code` 按中文原名反查；无对照时为空）。</summary>
        public string HarmonyName => _static.HarmonyName;

        /// <summary>图鉴六维评价（DB `character_performance`）；null=数据库未提供。</summary>
        public ShipPerformance? Performance => _static.Performance;

        /// <summary>建造池文本（DB `characters.build_pool`）。</summary>
        public string BuildPool => _static.Acquisition.BuildPool;

        /// <summary>常驻。</summary>
        public bool IsPermanent => _static.IsPermanent;

        /// <summary>实装活动名（优先 DB `characters.release_event_id`，缺失时退回获取方式里的活动名）。</summary>
        public string RelatedEvent
        {
            get
            {
                string release = _static.ReleaseEvent?.GetLocalized() ?? "";
                return !string.IsNullOrEmpty(release)
                    ? release
                    : _static.Events.FirstOrDefault()?.GetLocalized() ?? "";
            }
        }

        /// <summary>别名：与 RelatedEvent 相同（兼容旧引用，详见 RelatedEvent）。</summary>
        public string DebutEvent => RelatedEvent;

        // ================= 科技加成（舰队科技任务，用于筛选/属性页） =================

        /// <summary>获得时加成（fleet_tech.collect）。</summary>
        public TechTaskBonus? ObtainBonus => _static.FleetTech?.Collect;

        /// <summary>120 级加成（fleet_tech.level120）。</summary>
        public TechTaskBonus? Level120Bonus => _static.FleetTech?.Level120;

        /// <summary>获得时加成属性（无加成/无效键返回 None）。</summary>
        public AttributeType ObtainBonusAttrEnum => BonusAttrOf(_static.FleetTech?.Collect);

        /// <summary>120 级加成属性。</summary>
        public AttributeType Level120BonusAttrEnum => BonusAttrOf(_static.FleetTech?.Level120);

        /// <summary>是否有获得时加成。</summary>
        public bool HasObtainBonus => HasBonusOf(_static.FleetTech?.Collect);

        /// <summary>是否有 120 级加成。</summary>
        public bool HasLevel120Bonus => HasBonusOf(_static.FleetTech?.Level120);

        private static AttributeType BonusAttrOf(TechTaskBonus? bonus)
        {
            if (bonus == null || bonus.Value == 0 || string.IsNullOrEmpty(bonus.Stat))
                return AttributeType.None;
            return DataCodeMaps.StatKeyToAttribute(bonus.Stat);
        }

        private static bool HasBonusOf(TechTaskBonus? bonus) =>
            bonus != null && bonus.Value != 0 && !string.IsNullOrEmpty(bonus.Stat);

        // ================= 头像 =================

        // ---- 皮肤/头像切换（后缀优先级：皮肤 > 改造 _g > 誓约 _h > 小舰船 _younv > μ 舰装 _idol > 无） ----
        private int _currentSkinId;

        /// <summary>当前选中的皮肤 id；0 = 基础立绘（显示偏好，不持久化到 ShipState）。</summary>
        public int CurrentSkinId
        {
            get => _currentSkinId;
            set
            {
                if (_currentSkinId != value && value >= 0)
                {
                    _currentSkinId = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvatarUri));
                    OnPropertyChanged(nameof(IsAvatarDownloaded));
                }
            }
        }

        /// <summary>可用皮肤 id 列表（0=默认立绘；数据源里的默认皮由 0 代表，不重复列出）。</summary>
        public List<int> AvailableSkins
        {
            get
            {
                var list = new List<int> { 0 };
                if (_static.Skins != null)
                {
                    foreach (var skin in _static.Skins)
                    {
                        if (skin.Kind == GameSkinKind.Default) continue;   // 默认皮 = 0
                        if (skin.Id > 0 && !list.Contains(skin.Id))
                            list.Add(skin.Id);
                    }
                }
                return list;
            }
        }

        /// <summary>切换皮肤显示（仅允许 AvailableSkins 范围内的 id）。</summary>
        public void SwitchSkin(int skinId) => CurrentSkinId = skinId;

        /// <summary>切换为誓约皮 id（存在时）；否则回到基础立绘。</summary>
        public void SwitchToOathSkin()
        {
            if (_static.HasOathSkin && _static.Skins != null)
            {
                var oath = _static.Skins.FirstOrDefault(s => s.Kind == GameSkinKind.Oath);
                CurrentSkinId = oath?.Id ?? 0;
            }
            else CurrentSkinId = 0;
        }

        /// <summary>
        /// 皮肤在资源文件名中的序号（1 起；默认皮=1，誓约 `_h` 与改造 `_g` 不占序号）。
        /// 实测规则：跳过誓约/改造后按皮肤行顺序计数，命中率 88.7%（当前代码原先的“序号+1”只有 48%）。
        /// </summary>
        private int SkinOrdinal(int skinId)
        {
            if (_static.Skins == null) return 0;
            int ordinal = 0;
            foreach (var skin in _static.Skins)
            {
                if (skin.Kind == GameSkinKind.Oath || skin.Kind == GameSkinKind.Retrofit) continue;
                ordinal++;
                if (skin.Id == skinId) return ordinal;
            }
            return 0;
        }

        /// <summary>布里（通用）船：头像固定落在 Common/Universal，文件名带 UNIV 前缀。</summary>
        private bool IsBulinShip => _static.Flags.HasFlag(ShipFlags.Bulin);

        /// <summary>
        /// 头像文件名基底（资源文件命名规范，实测自 Assets/squareicon）：
        /// 每个名字变体都同时尝试「阵营前缀 + 名字」与「纯名字」两种形式。
        /// </summary>
        private List<string> AvatarNameBases
        {
            get
            {
                var bases = new List<string>();
                string name = _static.GlobalName;
                if (string.IsNullOrWhiteSpace(name)) return bases;

                var prefixList = DataCodeMaps.AvatarFilePrefixes(_static.Category, _static.Faction, IsBulinShip);
                foreach (string core in NameVariants(name))
                {
                    foreach (string p in prefixList)
                    {
                        if (p.Length == 0) continue;
                        // 数据库的 global_name 已带阵营前缀时不再叠加（MOT Jernbarden 不会变成 MOT MOT …），
                        // 同时补一个去掉该前缀的写法（部分资源不带前缀）
                        if (core.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        {
                            AddUnique(bases, core.Substring(p.Length).TrimStart());
                            continue;
                        }
                        AddUnique(bases, p + core);
                    }
                    AddUnique(bases, core);
                }
                return bases;
            }
        }

        /// <summary>
        /// 名字变体（数据源与资源命名习惯不同时的兜底）：
        /// 原名 → 去内部 id 尾缀（Fubuki_301011）→ 去括注（Marco Polo (Throne Vanguard)）→
        /// 长音（Yuudachi → Yūdachi）→ META 点/空格互换（X META ⇄ X.META）→ 中点统一（・ → ·）。
        /// </summary>
        private static List<string> NameVariants(string name)
        {
            var list = new List<string>();
            AddUnique(list, name);
            string noId = StripIdSuffix(name);
            AddUnique(list, noId);
            string noParen = StripParenthetical(name);
            AddUnique(list, noParen);
            AddUnique(list, StripIdSuffix(noParen));
            // 数据源带「Retrofit」「μ」标记但资源名不含（μ 兵装由 _idol 后缀表达）
            string noRetro = name.Replace(" Retrofit", "").Replace(" retrofit", "").TrimEnd();
            AddUnique(list, noRetro);
            AddUnique(list, StripIdSuffix(noRetro));
            string noMu = name.Replace(" μ", "").Replace("μ", "").TrimEnd();
            AddUnique(list, noMu);
            AddUnique(list, StripIdSuffix(noMu));

            int count = list.Count;
            for (int i = 0; i < count; i++)
            {
                string v = list[i];
                AddUnique(list, Macronize(v));
                AddUnique(list, MetaDot(v));
                AddUnique(list, MetaSpace(v));
                AddUnique(list, v.Replace('・', '·'));
                AddUnique(list, v.Replace(" ", ""));          // 无空格写法（LeHardi / KizunaAI）
                AddUnique(list, v.Replace('-', ' '));         // I-13 ⇄ I 13
                AddUnique(list, v.Replace(' ', '-'));         // Duguay Trouin ⇄ Duguay-Trouin
                AddUnique(list, FoldDiacritics(v));           // Giosuè → Giosue
            }

            // 第二轮：反向命名习惯——资源侧多用 Hepburn 双写（Fusō→Fusou / Hiryū→Hiryuu）、
            // 每个词首字母大写（An shan → An Shan）、联动船名「名在前」（Amami Haruka → Haruka Amami）
            var pass2 = new List<string>(list);
            foreach (var v in pass2)
            {
                AddUnique(list, HepburnExpand(v));
                AddUnique(list, LongVowelTail(v));
                AddUnique(list, TitleCaseWords(v));
                AddUnique(list, SwapTwoWords(v));
                AddUnique(list, SwapLastTwoWords(v));
                AddUnique(list, StripApostrophes(v));          // Jun'yō → Junyou
            }

            // 第三轮：对第二轮新写法再套通用差异（META 点/空格、去空格、变音折叠）
            var pass3 = new List<string>(list);
            foreach (var v in pass3)
            {
                AddUnique(list, MetaDot(v));
                AddUnique(list, MetaSpace(v));
                AddUnique(list, v.Replace(" ", ""));
                AddUnique(list, FoldDiacritics(v));
            }
            return list;
        }

        /// <summary>长音符 → Hepburn 双写（资源侧常见 Fusou / Hiryuu / Houshou 写法）。</summary>
        private static string HepburnExpand(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name
                .Replace("ō", "ou").Replace("Ō", "Ou")
                .Replace("ū", "uu").Replace("Ū", "Uu")
                .Replace("ā", "aa").Replace("Ā", "Aa")
                .Replace("ē", "ei").Replace("Ē", "Ei");
        }

        /// <summary>
        /// 词尾长音补写（数据源无长音符时：Fuso→Fusou、Hiryu→Hiryuu、Fuso.META→Fusou META）。
        /// 按空格分词逐个判断，整名只生成一个变体，避免组合爆炸。
        /// </summary>
        private static string LongVowelTail(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string spaced = MetaSpace(name);              // Hiryu.META → Hiryu META
            var parts = spaced.Split(' ');
            bool changed = false;
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Length == 0) continue;
                char last = p[p.Length - 1];
                if (last == 'o' || last == 'O' || last == 'u' || last == 'U')
                {
                    parts[i] = p + "u";                   // 長音统一写成 u（Fusou / Hiryuu）
                    changed = true;
                }
            }
            return changed ? string.Join(" ", parts) : name;
        }

        /// <summary>每个词首字母大写（An shan → An Shan / Real bey → Real Bey）。</summary>
        private static string TitleCaseWords(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var parts = name.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i];
                if (p.Length == 0) continue;
                if (char.IsLower(p[0])) parts[i] = char.ToUpperInvariant(p[0]) + p.Substring(1);
                else if (char.IsUpper(p[0]))
                    parts[i] = char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1) : "");
            }
            return string.Join(" ", parts);
        }

        /// <summary>两词名前后互换（Amami Haruka → Haruka Amami，联动资源按「名 姓」命名）。</summary>
        private static string SwapTwoWords(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Contains('.')) return name;
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 2 ? parts[1] + " " + parts[0] : name;
        }

        /// <summary>三词及以上只互换最后两个词（MOT Adventure Portsmouth → MOT Portsmouth Adventure）。</summary>
        private static string SwapLastTwoWords(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Contains('.')) return name;
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return name;
            (parts[^1], parts[^2]) = (parts[^2], parts[^1]);
            return string.Join(" ", parts);
        }

        /// <summary>去掉撇号（资源侧 Junyou / Queen Annes 写法）。</summary>
        private static string StripApostrophes(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name.Replace("'", "").Replace("’", "");
        }

        private static void AddUnique(List<string> list, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!list.Contains(value)) list.Add(value);
        }

        /// <summary>去掉名字末尾的 "_数字" 内部 id 尾缀（无尾缀时原样返回）。</summary>
        private static string StripIdSuffix(string name)
        {
            int i = name.LastIndexOf('_');
            if (i <= 0 || i >= name.Length - 1) return name;
            for (int k = i + 1; k < name.Length; k++)
                if (!char.IsDigit(name[k])) return name;
            return name.Substring(0, i);
        }

        /// <summary>去掉名字末尾的「 (…)」括注。</summary>
        private static string StripParenthetical(string name)
        {
            int i = name.LastIndexOf('(');
            if (i <= 0) return name;
            // 仅当括号在结尾且为全角/半角混排时去掉
            if (name.TrimEnd().EndsWith(")", StringComparison.Ordinal))
                return name.Substring(0, i).TrimEnd();
            return name;
        }

        /// <summary>长音写法 → 带长音符（uu→ū / oo→ō / aa→ā / ou→ō），用于匹配日文罗马音资源名。</summary>
        private static string Macronize(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            return name
                .Replace("uu", "ū")
                .Replace("oo", "ō")
                .Replace("aa", "ā")
                .Replace("ou", "ō");
        }

        /// <summary>「X META」→「X.META」（文件命名存在两种写法）。</summary>
        private static string MetaDot(string name) =>
            name.EndsWith(" META", StringComparison.Ordinal) ? name.Substring(0, name.Length - 5) + ".META" : name;

        /// <summary>「X.META」→「X META」。</summary>
        private static string MetaSpace(string name) =>
            name.EndsWith(".META", StringComparison.Ordinal) ? name.Substring(0, name.Length - 5) + " META" : name;

        /// <summary>去掉变音符号（Giosuè→Giosue、Téméraire→Temeraire），资源命名偶有省略注音。</summary>
        private static string FoldDiacritics(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                switch (c)
                {
                    case 'à': case 'á': case 'â': case 'ã': case 'ä': case 'å': sb.Append('a'); break;
                    case 'è': case 'é': case 'ê': case 'ë': sb.Append('e'); break;
                    case 'ì': case 'í': case 'î': case 'ï': sb.Append('i'); break;
                    case 'ò': case 'ó': case 'ô': case 'õ': case 'ö': case 'ø': sb.Append('o'); break;
                    case 'ù': case 'ú': case 'û': case 'ü': sb.Append('u'); break;
                    case 'ñ': sb.Append('n'); break;
                    case 'ç': sb.Append('c'); break;
                    case 'ý': case 'ÿ': sb.Append('y'); break;
                    case 'œ': sb.Append("oe"); break;
                    case 'æ': sb.Append("ae"); break;
                    case 'ß': sb.Append("ss"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 基础文件名（无皮肤时）：改造 _g / 誓约 _h / 小舰船 _younv / μ 兵装 _idol / 无后缀。
        /// 返回多候选（global_name 优先，其次 name(en)），首个为推荐名。
        /// </summary>
        private List<string> BaseAvatarFileNames() => BaseAvatarFileNames(Retrofitted);

        /// <summary>按指定改造状态生成基础头像候选（配队槽位可独立选择改造/非改造形态）。</summary>
        private List<string> BaseAvatarFileNames(bool retrofitted)
        {
            var result = new List<string>();
            // 后缀按原始 global_name 判定（前缀不影响「Little/μ」识别）
            string raw = _static.GlobalName ?? "";
            string suffix = retrofitted ? "_g" : Oath ? "_h"
                : raw.StartsWith("Little", StringComparison.OrdinalIgnoreCase) ? "_younv"
                : raw.Contains('μ') ? "_idol" : "";
            foreach (var n in AvatarNameBases)
                result.Add($"{n}{suffix}.png");
            return result;
        }

        /// <summary>
        /// 皮肤显示文件名候选（顺序即优先级）：默认皮走基础立绘名；誓约/改造用 _h/_g；
        /// 普通皮肤后缀 = 跳过誓约/改造后的序号；数据源皮肤 id 全名兜底；
        /// 每组同时尝试 global_name 与 name(en) 两种基底。
        /// </summary>
        private List<string> SkinAvatarFileNames(int skinId)
        {
            var list = new List<string>();
            var skin = _static.Skins?.FirstOrDefault(s => s.Id == skinId);
            if (skin == null) return list;
            if (skin.Kind == GameSkinKind.Default) return BaseAvatarFileNames();

            foreach (var n in AvatarNameBases)
            {
                if (skin.Kind == GameSkinKind.Oath) list.Add($"{n}_h.png");
                else if (skin.Kind == GameSkinKind.Retrofit) list.Add($"{n}_g.png");
            }
            int ordinal = SkinOrdinal(skinId);
            foreach (var n in AvatarNameBases)
            {
                if (ordinal > 0) list.Add($"{n}_{ordinal}.png");
                list.Add($"{n}_{skinId}.png");
            }
            return list;
        }

        /// <summary>当前显示头像文件名候选（首个为推荐名；皮肤未选中时为默认立绘名）。</summary>
        public List<string> AvatarFileNameCandidates =>
            CurrentSkinId > 0
                ? SkinAvatarFileNames(CurrentSkinId)
                : BaseAvatarFileNames();

        /// <summary>当前显示头像文件名（推荐名；含皮肤/改造/誓约/小舰船/μ 后缀）。</summary>
        public string AvatarFileName => AvatarFileNameCandidates[0];

        /// <summary>
        /// 头像相对目录（squareicon/{类型}/{阵营}），见 DataCodeMaps。
        /// META 不分类（直接 META/），布里固定 Common/Universal。
        /// </summary>
        public string AvatarFolderRel => DataCodeMaps.AvatarRelativeFolder(_static.Category, _static.Faction, IsBulinShip);

        /// <summary>文件名候选去扩展名（供详情页拼接 ms-appx 候选）。</summary>
        public List<string> AvatarFileBases =>
            AvatarFileNameCandidates
                .Select(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? f.Substring(0, f.Length - 4) : f)
                .ToList();

        /// <summary>推荐文件名去扩展名。</summary>
        public string AvatarFileBase => AvatarFileBases[0];

        /// <summary>squareicon 相对基路径候选（不含扩展名），如 "Common/Eagle Union/USS Enterprise_3"。</summary>
        public List<string> AvatarSquareiconRelBases =>
            AvatarFolderRel.EndsWith("/") || AvatarFolderRel.Length == 0
                ? AvatarFileBases
                : AvatarFileBases.Select(f => AvatarFolderRel + "/" + f).ToList();

        /// <summary>推荐 squareicon 相对基路径（不含扩展名）。</summary>
        public string AvatarSquareiconRelBase => AvatarSquareiconRelBases[0];

        /// <summary>
        /// 本地头像实际路径：在 avatars/（与头像包 squareicon/ 内容对应）的规则目录下
        /// 按候选名逐一查找；兼容旧"阵营数字目录"；均不存在返回空串。
        /// </summary>
        private string ResolveLocalAvatarPath() => ResolveLocalAvatarPath(AvatarFileNameCandidates);

        /// <summary>按给定候选文件名在本地头像目录（含旧布局）查找。</summary>
        private string ResolveLocalAvatarPath(IReadOnlyList<string> candidates)
        {
            string folder = AvatarFolderRel.Replace('/', '\\');
            foreach (var fileName in candidates)
            {
                string newPath = Path.Combine(App.DataRoot, "avatars", folder, fileName);
                if (File.Exists(newPath)) return newPath;
            }

            // 旧布局兼容（factionId 数字目录）
            foreach (var fileName in candidates)
            {
                string legacyPath = Path.Combine(App.DataRoot, "avatars", ((int)_static.Faction).ToString(), fileName);
                if (File.Exists(legacyPath)) return legacyPath;
            }
            return "";
        }

        /// <summary>本地头像是否已下载（新规则目录或旧目录）。</summary>
        public bool IsAvatarDownloaded => !string.IsNullOrEmpty(ResolveLocalAvatarPath());

        /// <summary>头像 URI：本地已下载 → ms-appdata(file) 本地路径；否则回退包内资源
        /// Assets/icon/square/{类型}/{阵营}/{候选文件}——按候选顺序取第一个真实存在的文件，
        /// 全部缺失时用推荐名（由界面回退占位图）。</summary>
        public string AvatarUri
        {
            get
            {
                string localPath = ResolveLocalAvatarPath();
                if (!string.IsNullOrEmpty(localPath))
                    return LocalAvatarUri(localPath);

                string? rel = Services.AvatarFiles.ResolvePackageImageRel(this);
                if (!string.IsNullOrEmpty(rel))
                    return "ms-appx:///Assets/icon/square/" + rel;
                return $"ms-appx:///Assets/icon/square/{AvatarFolderRel}/{AvatarFileName}";
            }
        }

        /// <summary>
        /// 指定「改造 / 非改造」形态的头像 URI（配队槽位可独立选择形态，不影响该船自身状态）。
        /// 皮肤选中时按皮肤候选；否则按改造后缀（_g）或基础立绘查找。
        /// </summary>
        public string AvatarUriFor(bool retrofitted)
        {
            var candidates = CurrentSkinId > 0 ? SkinAvatarFileNames(CurrentSkinId) : BaseAvatarFileNames(retrofitted);
            if (candidates.Count == 0) return "";

            string localPath = ResolveLocalAvatarPath(candidates);
            if (!string.IsNullOrEmpty(localPath)) return LocalAvatarUri(localPath);

            string? rel = Services.AvatarFiles.ResolvePackageImageRel(AvatarFolderRel, candidates);
            if (!string.IsNullOrEmpty(rel)) return "ms-appx:///Assets/icon/square/" + rel;
            return $"ms-appx:///Assets/icon/square/{AvatarFolderRel}/{candidates[0]}";
        }

        /// <summary>
        /// 指定皮肤的头像 URI（皮肤行用）。候选名按「{船名}_{序号}.png」规则生成，
        /// 再由 <see cref="Services.AvatarFiles"/> 在 icon/square 全树定位实际文件；找不到返回空串。
        /// </summary>
        public string SkinAvatarUri(int skinId)
        {
            var candidates = skinId > 0 ? SkinAvatarFileNames(skinId) : BaseAvatarFileNames();
            if (candidates.Count == 0) return "";
            string? rel = Services.AvatarFiles.ResolvePackageImageRel(AvatarFolderRel, candidates);
            return string.IsNullOrEmpty(rel) ? "" : "ms-appx:///Assets/icon/square/" + rel;
        }

        /// <summary>本地文件 → 可被 BitmapImage 加载的 URI（打包环境用 ms-appdata，否则 file://）。</summary>
        private static string LocalAvatarUri(string localPath)
        {
            bool packaged = App.IsPackaged;

            var segments = localPath.Replace('\\', '/').TrimStart('/').Split('/');
            string escaped = string.Join("/", segments.Select(Uri.EscapeDataString));
            return packaged
                ? "ms-appdata:///local/" + escaped
                : "file:///" + escaped;
        }

        /// <summary>本地头像路径（仅当文件存在返回路径，否则空串；供详情页流式加载）。</summary>
        public string LocalAvatarPath => ResolveLocalAvatarPath();

        /// <summary>
        /// 头像稀有度背景 URI：按**有效稀有度**取图（改造后 +1 档，背景同步换成新稀有度）；
        /// 普通/联动按稀有度，科研最高方案 bg_4_0 / 决战 bg_5_0，META 变体。
        /// </summary>
        public string AvatarBgUri => RarityBackground.ShipUri(_static.Category, EffectiveRarityEnum);

        /// <summary>头像外框（按有效稀有度；装备类不套用）。</summary>
        public string AvatarBorderUri => RarityBackground.BorderUri(_static.Category, EffectiveRarityEnum);

        /// <summary>是否有头像外框（无框时模板隐藏该层，避免占位图顶上来）。</summary>
        public bool HasAvatarBorder => !string.IsNullOrEmpty(AvatarBorderUri);

        /// <summary>
        /// 头像外框的边距：不同档位的框画布比例不同（普通/META 竖长、科研金与彩略高），
        /// 按视觉验收给定值分别微调，勿改成统一值：
        /// META（4_1，228×322）→ -2,0,-1,-1；彩框（5 / 5_0）→ -1,-1,-1,-1；
        /// 科研金（4_0）→ 0,-2,0,-2；其余（1~4）→ -2,-2,-2,-2。
        /// </summary>
        public Thickness AvatarBorderMargin
        {
            get
            {
                // 档位由视觉验收给定：META 竖长框、DR 科研彩、PRY 科研金各一档，其余（含普通 UR）统一
                if (_static.Category == ShipCategory.META) return new Thickness(-2, 0, -1, -1);
                if (EffectiveRarityEnum == AzurLaneDex.Models.Rarity.Ultimate) return new Thickness(-1, -1, -1, -1);
                if (EffectiveRarityEnum == AzurLaneDex.Models.Rarity.Decisive) return new Thickness(0, -2, 0, -2);
                return new Thickness(-2, -2, -2, -2);
            }
        }


        /// <summary>誓约边框：仅已誓约时非空。</summary>
        public string OathFrameUri => Oath ? RarityBackground.OathFrameUri(_static.Category, EffectiveRarityEnum) : "";

        /// <summary>是否显示誓约边框。</summary>
        public bool ShowOathFrame => Oath;


        // ================= 有效稀有度（改造后 +1） =================

        /// <summary>
        /// 指定改造形态的稀有度（改造完成且该船可改造时 +1；UR 封顶；与用户自身状态无关）。
        /// 配队槽位可按「改造 / 非改造」分别取用。
        /// </summary>
        public Rarity RarityFor(bool retrofitted)
        {
            if (retrofitted && _static.Retrofit?.CanRetrofit == true)
            {
                int idx = (int)_static.Rarity;
                if (idx >= 1 && idx < 5) return (Rarity)(idx + 1);
            }
            return _static.Rarity;
        }

        /// <summary>指定改造形态的稀有度背景 URI（配队槽位用）。</summary>
        public string AvatarBgUriFor(bool retrofitted)
            => RarityBackground.ShipUri(_static.Category, RarityFor(retrofitted));

        /// <summary>指定形态的头像外框。</summary>
        public string AvatarBorderUriFor(bool retrofitted)
            => RarityBackground.BorderUri(_static.Category, RarityFor(retrofitted));

        /// <summary>指定形态的誓约边框（未誓约时为空）。</summary>
        public string OathFrameUriFor(bool retrofitted)
            => Oath ? RarityBackground.OathFrameUri(_static.Category, RarityFor(retrofitted)) : "";

        /// <summary>
        /// 有效稀有度枚举：改造完成且该船可改造时在 N..SSR 范围内 +1（UR 已封顶不再增加；
        /// 沿用旧行为，数据侧暂无改造稀有度字段）。供背景、筛选等非文本用途使用。
        /// </summary>
        public Rarity EffectiveRarityEnum => RarityFor(Retrofitted);

        /// <summary>有效稀有度的显示文本（改造后 +1 档）。</summary>
        public string EffectiveRarity
        {
            get
            {
                var effective = EffectiveRarityEnum;
                return effective == _static.Rarity
                    ? Rarity
                    : LocalizationHelper.GetEnumString("Rarity", (int)effective);
            }
        }

        // ================= 动态状态（ShipState 读写，属性变更通知） =================

        public bool Owned
        {
            get => _state.Owned;
            set { if (_state.Owned != value) { _state.Owned = value; OnPropertyChanged(); OnPropertyChanged(nameof(BreakthroughDisplay)); } }
        }

        public int Breakthrough
        {
            get => _state.Breakthrough;
            set { if (_state.Breakthrough != value) { _state.Breakthrough = value; OnPropertyChanged(); OnPropertyChanged(nameof(BreakthroughDisplay)); OnPropertyChanged(nameof(IsMaxBreakthrough)); } }
        }

        public Dictionary<int, int> SkillLevels => _state.SkillLevels;

        public EnhanceCompleted EnhanceCompleted => _state.EnhanceCompleted;

        public bool AffectionMax
        {
            get => _state.AffectionMax;
            set { _state.AffectionMax = value; OnPropertyChanged(); }
        }

        public bool Oath
        {
            get => _state.Oath;
            set
            {
                if (_state.Oath != value)
                {
                    _state.Oath = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AvatarUri));
                    OnPropertyChanged(nameof(IsAvatarDownloaded));
                }
            }
        }

        public bool Level120
        {
            get => _state.Level120;
            set { _state.Level120 = value; OnPropertyChanged(); }
        }

        public bool Level125
        {
            get => _state.Level125;
            set { _state.Level125 = value; OnPropertyChanged(); }
        }

        public bool Retrofitted
        {
            get => _state.Retrofitted;
            set
            {
                if (_state.Retrofitted != value)
                {
                    _state.Retrofitted = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(AvatarUri));
                    OnPropertyChanged(nameof(IsAvatarDownloaded));
                    OnPropertyChanged(nameof(EffectiveRarity));
                    OnPropertyChanged(nameof(EffectiveRarityEnum));
                    // 改造后稀有度 +1 → 头像/列表的稀有度背景同步换成新档次
                    OnPropertyChanged(nameof(AvatarBgUri));
                }
            }
        }

        public Dictionary<string, bool> RetrofitNodes => _state.RetrofitNodes;

        public bool SpecialGearObtained
        {
            get => _state.SpecialGearObtained;
            set { _state.SpecialGearObtained = value; OnPropertyChanged(); }
        }

        public int ResearchLevel
        {
            get => _state.ResearchLevel;
            set { _state.ResearchLevel = value; OnPropertyChanged(); }
        }

        public int FateLevel
        {
            get => _state.FateLevel;
            set { _state.FateLevel = value; OnPropertyChanged(); }
        }

        public List<int> OwnedSkins => _state.OwnedSkins;

        // ================= 计算属性 =================

        /// <summary>是否满破（拥有且突破≥3）。</summary>
        public bool IsMaxBreakthrough => Owned && Breakthrough >= 3;

        /// <summary>突破显示：满破=⭐，否则数字。</summary>
        public string BreakthroughDisplay
        {
            get
            {
                if (!Owned) return "0";
                return Breakthrough >= 3 ? "⭐" : Breakthrough.ToString();
            }
        }

        /// <summary>列表勾选状态（非持久化）。</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        // ================= 方法 =================

        public ShipState GetState() => _state;

        public ShipStatic GetStaticCopy() => _static;

        /// <summary>头像包更新完成后通知头像相关属性刷新（列表/详情重新加载图片）。</summary>
        public void NotifyAvatarChanged()
        {
            OnPropertyChanged(nameof(AvatarUri));
            OnPropertyChanged(nameof(IsAvatarDownloaded));
            OnPropertyChanged(nameof(AvatarFileName));
            OnPropertyChanged(nameof(LocalAvatarPath));
        }

        /// <summary>语言切换后通知全部本地化显示属性刷新（数据文本即时生效）。</summary>
        public void NotifyLocalizationChanged()
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(ClassName));
            OnPropertyChanged(nameof(CV));
            OnPropertyChanged(nameof(ShipType));
            OnPropertyChanged(nameof(Faction));
            OnPropertyChanged(nameof(Rarity));
            OnPropertyChanged(nameof(Category));
            OnPropertyChanged(nameof(EffectiveRarity));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
