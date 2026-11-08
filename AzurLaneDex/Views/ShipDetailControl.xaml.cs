using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using AzurLaneDex.Services;
using AzurLaneDex.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace AzurLaneDex.Views
{
    /// <summary>
    /// 舰船详情面板：接收 ShipViewModel，将新数据模型（ShipStatic 数据驱动结构）的字段
    /// 填充到各只读区块；状态复选框/滑杆直接修改 ViewModel 状态属性并落盘。
    /// XAML 布局与控件保持不变；ItemsControl 的数据行通过展示行（View Row）适配既有模板绑定。
    /// </summary>
    public sealed partial class ShipDetailControl : UserControl
    {
        private bool _isUpdating = false;
        private ShipViewModel? _currentShip;
        private int _avatarLoadToken = 0;
        private int _gearLoadToken = 0;
        private int _initEquipToken = 0;
        /// <summary>台词区皮肤选项（以 SelectedIndex 为准，见 ApplyLineSkinSelection）。</summary>
        private List<LineSkinOption> _lineSkinOptions = new();

        /// <summary>
        /// 动态值 TextBlock 的 x:Name 白名单：ClearDisplay 只清空这些运行时赋值元素，
        /// 绝不触碰 XAML 静态标签/卡片标题（避免一次清空后辅助文字永久丢失）。
        /// </summary>
        private static readonly System.Collections.Generic.HashSet<string> DynamicTextNames =
            new(System.StringComparer.Ordinal)
            {
                "ShipIdText", "ShipNameText", "AltNameText", "AliasText", "ClassText", "CategoryText",
                "ShipTypeText", "RarityText", "FactionText", "ReleaseDateText", "IsPermanentText", "CvText",
                "ArtistText", "RelatedEventText", "RemarksText", "NotesText",
                "WikiNumberText", "BuildPoolText",
                "HpText", "ArmorText", "FpText", "TrpText", "AaText", "AviText", "HitText", "EvaText",
                "AswText", "LuckText", "OilText", "SpeedText",
                "HpGradeText", "AaGradeText", "EvaGradeText", "AviGradeText", "TrpGradeText", "FpGradeText",
                "CollectPointsText", "LimitBreakPointsText", "Level120PointsText",
                "HasSpecialGearText", "GearNameText", "GearRarityText", "GearAttributeText", "GearEffectText",
                "GearUpgradeText", "GearIdText", "GearAcquireText",
                "CanRetrofitText", "RetrofitShipTypeChangedText", "TargetShipTypeText", "RetrofitSkillText",
                "PreReqFactionsText", "TechPointsText", "BlueprintRequiredText", "DevelopBlueprintRequiredText",
                "ResearchUnlockTasksText", "ResearchGainText", "ResearchChangeSkillText",
                "FateSimLevelText", "FateLevelText", "FateDescText", "FateBlueprintRequiredText",
                "CanBeEnhanceMaterialText", "EnhanceValueText", "EnhanceCapText", "CanRetireText", "RetirementRewardText",
                "EnhanceExpFpText", "EnhanceExpTrpText", "EnhanceExpAviText", "EnhanceExpRldText",
                "EnhanceItemsText", "ExtraEnhanceText", "ReferenceMarkdownText",
                "BreakthroughValueText", "ResearchLevelText", "AvatarStateText"
            };

        public ShipDetailControl()
        {
            this.InitializeComponent();
        }

        /// <summary>设置/刷新当前舰船；null 清空显示。</summary>
        public void SetShip(ShipViewModel? ship)
        {
            if (ship == null)
            {
                ClearDisplay();
                return;
            }

            // 增量更新（如果ID相同）
            if (_currentShip != null && _currentShip.Id == ship.Id)
            {
                bool remodelChanged = _currentShip.Retrofitted != ship.Retrofitted;
                _currentShip = ship;

                // 更新基本显示
                ShipNameText.Text = ship.DisplayName;
                RarityText.Text = ship.EffectiveRarity;

                // 状态控件
                OwnedCheckBox.IsChecked = ship.Owned;
                BreakthroughSlider.Value = ship.Breakthrough;
                BreakthroughValueText.Text = ship.Breakthrough.ToString();
                OathCheckBox.IsChecked = ship.Oath;
                Level120CheckBox.IsChecked = ship.Level120;
                RemodeledCheckBox.IsEnabled = CanRemodel(ship) && ship.Owned;
                RemodeledCheckBox.IsChecked = ship.Retrofitted;
                AffectionMaxCheckBox.IsChecked = ship.AffectionMax;
                Level125CheckBox.IsChecked = ship.Level125;
                ResearchLevelSlider.Value = ship.ResearchLevel;
                ResearchLevelText.Text = ship.ResearchLevel.ToString();
                FateLevelSlider.Value = ship.FateLevel;
                FateLevelText.Text = ship.FateLevel.ToString();

                // 兵装
                bool inc_hasGear = ship.UniqueAugment != null;
                SpecialGearExpander.Visibility = inc_hasGear ? Visibility.Visible : Visibility.Collapsed;
                SpecialGearObtainedCheckBox.Visibility = inc_hasGear ? Visibility.Visible : Visibility.Collapsed;
                if (inc_hasGear)
                {
                    FillGearTexts(ship);
                    _gearLoadToken++;
                    int inc_token = _gearLoadToken;
                    SetGearIconBackground(RarityBackground.AugmentUri());
                    SetDefaultGearIcon();
                    StartGearFadeIn();
                    LoadCustomGearIcon(ship.UniqueAugment!.Id, inc_token);
                }
                else
                {
                    GearNameText.Text = "";
                    GearRarityText.Text = "";
                    GearAttributeText.Text = "";
                    GearEffectText.Text = "";
                    GearUpgradeText.Text = "";
                    GearIdText.Text = "";
                    GearAcquireText.Text = "";
                    SetGearIconBackground();
                    SpecialGearImage.Opacity = 0;
                    SpecialGearImage.Source = null;
                    GearFadeInStoryboard.Stop();
                }

                // 改造头像刷新
                if (remodelChanged)
                {
                    int inc_token = ++_avatarLoadToken;
                    ShipAvatarImage.Opacity = 0;
                    LoadAndFadeInAvatar(inc_token);
                }

                UpdateControlStates();
                return;
            }

            // 全新设置
            _currentShip = ship;

            // 基本信息
            ShipIdText.Text = ship.Id.ToString();
            ShipNameText.Text = ship.DisplayName;
            AltNameText.Text = string.IsNullOrEmpty(ship.HarmonyName) ? "—" : ship.HarmonyName; // 和谐名（name_codes 反查）
            AliasText.Text = ship.AliasesText;
            ClassText.Text = ship.ClassName;
            CategoryText.Text = LocalizationHelper.GetEnumString("ShipCategory", (int)ship.CategoryEnum);
            ShipTypeText.Text = LocalizationHelper.GetEnumString("ShipType", (int)ship.ShipTypeEnum);
            RarityText.Text = ship.EffectiveRarity;
            FactionText.Text = LocalizationHelper.GetEnumString("Faction", (int)ship.FactionEnum);
            ReleaseDateText.Text = ship.ReleaseDate;
            IsPermanentText.Text = ship.IsPermanent ? "是" : "否";
            CvText.Text = ship.CV;
            ArtistText.Text = ship.Artist;
            RelatedEventText.Text = ship.RelatedEvent;
            WikiNumberText.Text = string.IsNullOrEmpty(ship.WikiNumber) ? "—" : ship.WikiNumber;
            BuildPoolText.Text = string.IsNullOrEmpty(ship.BuildPool) ? "—" : ship.BuildPool;
            RemarksText.Text = "";
            // 备注：只显示图鉴内的获取方式（in_codex = 1），完整清单在「获取方式」卡片
            var codexNotes = ship.Acquisition?.CodexNotes ?? new List<LocalizedString>();
            NotesText.Text = string.Join(Environment.NewLine,
                codexNotes.Select(n => n.GetLocalized()).Where(s => !string.IsNullOrEmpty(s)));

            // 属性（按数据源成长公式计算 100 级显示值；正式计算后续集中到 ShipStatsCalculator）
            var stats = ComputeLevel100Stats(ship.StatsLevels?.FirstOrDefault());
            HpText.Text = stats.Hp.ToString();
            ArmorText.Text = ArmorTextOf(stats.Armor);
            FpText.Text = stats.Fp.ToString();
            TrpText.Text = stats.Trp.ToString();
            AaText.Text = stats.Aa.ToString();
            AviText.Text = stats.Avi.ToString();
            HitText.Text = stats.Hit.ToString();
            EvaText.Text = stats.Eva.ToString();
            AswText.Text = stats.Asw.ToString();
            LuckText.Text = stats.Luck.ToString();
            OilText.Text = OilOf(ship).ToString();
            SpeedText.Text = stats.Speed.ToString("0.0");

            // 性能评级（DB character_performance：1~7 → F..S）
            var perf = ship.Performance;
            HpGradeText.Text = perf?.DurabilityGrade ?? "—";
            AaGradeText.Text = perf?.AaGrade ?? "—";
            EvaGradeText.Text = perf?.EvasionGrade ?? "—";
            AviGradeText.Text = perf?.AviationGrade ?? "—";
            TrpGradeText.Text = perf?.TorpedoGrade ?? "—";
            FpGradeText.Text = perf?.FirepowerGrade ?? "—";

            // 舰队科技
            var ft = ship.FleetTech;
            CollectPointsText.Text = (ft?.Collect?.Pts ?? 0).ToString();
            LimitBreakPointsText.Text = (ft?.LimitBreak?.Pts ?? 0).ToString();
            Level120PointsText.Text = (ft?.Level120?.Pts ?? 0).ToString();

            // 获取方式
            AcquireGroupsItemsControl.ItemsSource = BuildAcquireGroups(ship);

            // 装备槽（0 破档）
            EquipmentSlotsItemsControl.ItemsSource = BuildSlotViews(ship);

            // 初始装备（装备目录未装载时先出基础行，装载完再补名称/图标/武器属性）
            InitialEquipmentItemsControl.ItemsSource = BuildInitEquipViews(ship);
            RefreshInitEquipmentAsync(ship);

            // 专属兵装（无兵装时整张卡片收起：只收 Expander 会留下零高度的 Border 与多余间距）
            bool hasGear = ship.UniqueAugment != null;
            HasSpecialGearText.Text = hasGear ? "可拥有" : "无";
            SpecialGearBorder.Visibility = hasGear ? Visibility.Visible : Visibility.Collapsed;
            SpecialGearExpander.Visibility = hasGear ? Visibility.Visible : Visibility.Collapsed;
            SpecialGearObtainedCheckBox.Visibility = hasGear ? Visibility.Visible : Visibility.Collapsed;
            if (hasGear)
            {
                FillGearTexts(ship);

                // 图标加载（与增量分支一致：首次显示也必须加载，否则图标永远为空）
                _gearLoadToken++;
                int gearToken = _gearLoadToken;
                SetGearIconBackground(RarityBackground.AugmentUri());
                SetDefaultGearIcon();
                StartGearFadeIn();
                LoadCustomGearIcon(ship.UniqueAugment!.Id, gearToken);
            }
            else
            {
                GearNameText.Text = "";
                GearRarityText.Text = "";
                GearAttributeText.Text = "";
                GearEffectText.Text = "";
                GearUpgradeText.Text = "";
                GearIdText.Text = "";
                GearAcquireText.Text = "";
                SetGearIconBackground();
                GearFadeInStoryboard.Stop();
                SpecialGearImage.Opacity = 0;
                SpecialGearImage.Source = null;
            }

            // 技能
            BuildSkillCards(ship);

            // 改造（不可改造的船整张卡片收起，避免留下空白卡片和间距）
            var retro = ship.Retrofit;
            bool canRetrofit = retro?.CanRetrofit == true;
            RetrofitBorder.Visibility = canRetrofit ? Visibility.Visible : Visibility.Collapsed;
            CanRetrofitText.Text = canRetrofit ? "可改造" : "不可改造";
            RetrofitShipTypeChangedText.Text = retro?.TargetShipType != null ? "改造后舰种变化" : "舰种不变";
            TargetShipTypeText.Text = retro?.TargetShipType != null
                ? LocalizationHelper.GetEnumString("ShipType", (int)retro.TargetShipType.Value)
                : "—";
            RetrofitSkillText.Text = string.IsNullOrEmpty(retro?.RefitSkillName?.GetLocalized())
                ? ""
                : "改造获得技能：" + retro!.RefitSkillName.GetLocalized();
            RetrofitNodesItemsControl.ItemsSource = BuildRetrofitNodeViews(ship);

            // 科研（非科研船不显示该卡片，避免空白卡片与额外间距）
            var research = ship.Research;
            bool isResearch = research != null;
            ResearchBorder.Visibility = isResearch ? Visibility.Visible : Visibility.Collapsed;
            PreReqFactionsText.Text = isResearch && !string.IsNullOrEmpty(research!.UnlockCondition)
                ? research.UnlockCondition : "—";
            TechPointsText.Text = isResearch && research!.Price > 0 ? research.Price.ToString() : "—";
            ResearchTasksItemsControl.ItemsSource = BuildResearchTaskViews(research);
            BlueprintRequiredText.Text = isResearch && !string.IsNullOrEmpty(research!.StrengthenItemName)
                ? research.StrengthenItemName : "—";
            DevelopBonusItemsControl.ItemsSource = BuildDevelopBonusViews(research);
            DevelopBlueprintRequiredText.Text = isResearch
                ? (research!.StrengthenEffectCount > 0
                    ? $"开发效果 {research.StrengthenEffectCount} 项（数据库未提供数值）"
                    : "—")
                : "—";

            ResearchUnlockTasksText.Text = isResearch && research!.UnlockTaskTimes.Count > 0
                ? string.Join("\n", research.UnlockTaskTimes) : "—";
            ResearchGainText.Text = isResearch && !string.IsNullOrEmpty(research!.GainItemsText)
                ? research.GainItemsText : "—";
            ResearchChangeSkillText.Text = isResearch && !string.IsNullOrEmpty(research!.ChangeSkillText)
                ? research.ChangeSkillText : "—";

            // META 舰：突破 / 强化材料 / 技能任务
            var meta = ship.Meta;
            bool hasMeta = meta != null &&
                           (meta.Breakouts.Count > 0 || meta.RepairItems.Count > 0 || meta.SkillTasks.Count > 0);
            MetaBorder.Visibility = hasMeta ? Visibility.Visible : Visibility.Collapsed;
            if (hasMeta)
            {
                MetaBreakoutItemsControl.ItemsSource = meta!.Breakouts.Select(b => new
                {
                    Title = $"突破 {b.Level} 级",
                    Materials = string.IsNullOrEmpty(b.ItemsText) ? "材料：—" : "材料：" + b.ItemsText,
                    Effect = string.IsNullOrEmpty(b.EffectText) ? "效果：—" : "效果：" + b.EffectText
                }).ToList();

                MetaRepairItemsControl.ItemsSource = meta.RepairItems.Take(12).Select(r => new
                {
                    Line = $"{r.ItemName} ×{r.Count}｜经验 +{r.Exp}" + (string.IsNullOrEmpty(r.AttrText) ? "" : $"｜{r.AttrText}")
                }).ToList();

                MetaSkillTaskItemsControl.ItemsSource = meta.SkillTasks.Select(t => new
                {
                    Line = $"{t.SkillName} Lv{t.Level}｜需要经验 {t.NeedExp}｜解锁条件 {t.UnlockCount} 条｜升级任务 {t.LevelupCount} 条"
                }).ToList();
            }
            else
            {
                MetaBreakoutItemsControl.ItemsSource = null;
                MetaRepairItemsControl.ItemsSource = null;
                MetaSkillTaskItemsControl.ItemsSource = null;
            }
            if (isResearch && research!.Fate != null)
            {
                FateSimPanel.Visibility = Visibility.Visible;
                FateSimLevelText.Text = research.Fate.Level > 0 ? research.Fate.Level.ToString() : "5";
                FateDescText.Text = research.Fate.Stats.Count > 0
                    ? FormatStatDict(research.Fate.Stats, "属性加成: ")
                    : "（数据库未提供天运效果文本）";
                FateBlueprintRequiredText.Text = "—";
            }
            else
            {
                FateSimPanel.Visibility = Visibility.Collapsed;
            }

            // 皮肤
            SkinsItemsControl.ItemsSource = BuildSkinViews(ship);

            // 台词（先选皮肤，再按场景平铺；未选中的皮肤台词不展示）
            FillLineSkinComboBox(ship);

            // 礼物偏好（数据库仅"讨厌"清单；名称/图标/品质来自 items 表）
            var giftNames = ship.GiftDislikeNames;
            var giftIcons = ship.GiftDislikeIcons;
            var giftRarities = ship.GiftDislikeRarities;
            GiftPreferencesItemsControl.ItemsSource = ship.GiftDislikeIds.Select((id, i) =>
            {
                int rarity = i < giftRarities.Count ? giftRarities[i] : 0;
                return new GiftView
                {
                    GiftId = id,
                    DisplayName = i < giftNames.Count && !string.IsNullOrEmpty(giftNames[i])
                        ? $"{giftNames[i]}（{id}）"
                        : id.ToString(),
                    IconUri = AssetUri(i < giftIcons.Count ? giftIcons[i] : null, "icon/item/unkown.png"),
                    BgUri = RarityBackground.EquipUri(rarity),
                    HasBg = rarity > 0,
                    Preference = GiftPreferenceType.Dislike
                };
            }).ToList();

            // 强化/退役
            var exp = ship.EnhanceExp;
            var enhance = ship.GetStaticCopy().Enhance;
            CanBeEnhanceMaterialText.Text = ship.CanBeEnhanceMaterial ? "是" : "否";
            EnhanceValueText.Text = enhance == null || enhance.Value <= 0
                ? "—"
                : $"{enhance.Value}（强化素材合计属性经验：炮击 {enhance.AttrExp.ElementAtOrDefault(0)}、雷击 {enhance.AttrExp.ElementAtOrDefault(1)}、航空 {enhance.AttrExp.ElementAtOrDefault(3)}、装填 {enhance.AttrExp.ElementAtOrDefault(4)}）";
            EnhanceCapText.Text = enhance == null || (enhance.CapFp + enhance.CapTrp + enhance.CapAvi + enhance.CapRld + enhance.CapDur) == 0
                ? "—"
                : $"炮击 +{enhance.CapFp}｜雷击 +{enhance.CapTrp}｜航空 +{enhance.CapAvi}｜装填 +{enhance.CapRld}｜耐久 +{enhance.CapDur}";
            CanRetireText.Text = ship.IsBulin ? "否" : "是";
            RetirementRewardText.Text = BuildRetireText(ship);
            EnhanceExpFpText.Text = (exp?.Fp ?? 0).ToString();
            EnhanceExpTrpText.Text = (exp?.Trp ?? 0).ToString();
            EnhanceExpAviText.Text = (exp?.Avi ?? 0).ToString();
            EnhanceExpRldText.Text = (exp?.Rld ?? 0).ToString();
            EnhanceItemsText.Text = "—";
            ExtraEnhanceText.Text = "—";

            // 参考资料
            ReferenceMarkdownText.Text = "";

            // 状态控件（复选框等）
            FillSkinComboBox(ship);
            OwnedCheckBox.IsChecked = ship.Owned;
            BreakthroughSlider.Value = ship.Breakthrough;
            BreakthroughValueText.Text = ship.Breakthrough.ToString();
            OathCheckBox.IsChecked = ship.Oath;
            Level120CheckBox.IsChecked = ship.Level120;
            RemodeledCheckBox.IsEnabled = CanRemodel(ship) && ship.Owned;
            RemodeledCheckBox.IsChecked = ship.Retrofitted;
            SpecialGearObtainedCheckBox.IsChecked = ship.SpecialGearObtained;

            UpdateControlStates();

            // 头像
            int token = ++_avatarLoadToken;
            ShipAvatarImage.Opacity = 0;
            LoadAndFadeInAvatar(token);
        }

        // ================= 数据填充辅助（新模型 → 展示） =================

        private static bool CanRemodel(ShipViewModel ship) => ship.Retrofit?.CanRetrofit == true;

        /// <summary>
        /// 退役奖励文本：物资按舰种（`character_retire.coin`），勋章按稀有度取库内数值列
        /// （`medal_n/r/sr/ssr/ur`），UR 另有 `ur_extra_item_id × ur_extra_amount`。
        /// </summary>
        private static string BuildRetireText(ShipViewModel ship)
        {
            var retire = ship.GetStaticCopy().Retire;
            if (retire == null) return "—";

            var parts = new List<string>();
            if (retire.Coin > 0) parts.Add($"物资 {retire.Coin}");

            int medals = ship.RarityEnum switch
            {
                Rarity.N => retire.MedalN,
                Rarity.R => retire.MedalR,
                Rarity.SR => retire.MedalSr,
                Rarity.SSR => retire.MedalSsr,
                Rarity.UR => retire.MedalUr,
                _ => 0
            };
            if (medals > 0) parts.Add($"勋章 {medals}");
            if (retire.UrExtraAmount > 0 && ship.RarityEnum == Rarity.UR)
            {
                string itemName = string.IsNullOrEmpty(retire.UrExtraItemName)
                    ? $"道具 {retire.UrExtraItemId}"
                    : retire.UrExtraItemName;
                parts.Add($"{itemName} ×{retire.UrExtraAmount}");
            }
            return parts.Count == 0 ? "—" : string.Join("｜", parts);
        }

        private void FillGearTexts(ShipViewModel ship)
        {
            var aug = ship.UniqueAugment;
            GearNameText.Text = aug?.Name.GetLocalized() ?? "";
            GearRarityText.Text = aug == null
                ? "—"
                : LocalizationHelper.GetEnumString("Rarity", (int)aug.Rarity)
                  + (string.IsNullOrEmpty(aug.TypeName) ? "" : $"（适用：{aug.TypeName}）");
            GearAttributeText.Text = aug == null || aug.Attributes.Count == 0
                ? "—"
                : string.Join("、", aug.Attributes.Select(a => a.Display));
            GearEffectText.Text = aug == null
                ? "—"
                : BuildGearSkillText(aug.EffectSkillName, aug.EffectSkillDescription);
            GearUpgradeText.Text = aug == null || aug.UpgradeSkillName.Count == 0
                ? "—"
                : aug.UpgradeSkillName.GetLocalized();
            GearIdText.Text = aug?.Id.ToString() ?? "";
            GearAcquireText.Text = aug?.Craftable == true ? "图纸研发合成" : "获得方式见数据源";
        }

        /// <summary>兵装效果技能：名称 + 描述（数据库未提供时为 —）。</summary>
        private static string BuildGearSkillText(LocalizedString name, LocalizedString description)
        {
            string n = name.GetLocalized();
            string d = description.GetLocalized();
            if (string.IsNullOrEmpty(n) && string.IsNullOrEmpty(d)) return "—";
            if (string.IsNullOrEmpty(n)) return d;
            return string.IsNullOrEmpty(d) ? n : $"{n}：{d}";
        }

        private static string ArmorTextOf(ArmorType armor) => armor switch
        {
            ArmorType.Light => "轻甲",
            ArmorType.Medium => "中甲",
            ArmorType.Heavy => "重甲",
            _ => armor.ToString()
        };

        private static int OilOf(ShipViewModel ship)
        {
            // 油耗在 lb_data 的突破区间内；取最后（满破）档起始值展示
            var lbs = ship.LimitBreaks;
            return lbs.Count > 0 ? lbs[lbs.Count - 1].OilStart : 0;
        }

        /// <summary>按数据源成长公式计算等级 100 的属性快照（过渡实现，正式计算后续并入 ShipStatsCalculator）。</summary>
        private static ShipStatsView ComputeLevel100Stats(ShipStatsLevel? s)
        {
            var v = new ShipStatsView();
            if (s == null) return v;
            const int level = 100;
            v.Armor = s.Armor;
            v.Hp = Grow(s.Hp, s.Scaling?.Hp ?? 0, level);
            v.Fp = Grow(s.Fp, s.Scaling?.Fp ?? 0, level);
            v.Trp = Grow(s.Trp, s.Scaling?.Trp ?? 0, level);
            v.Aa = Grow(s.Aa, s.Scaling?.Aa ?? 0, level);
            v.Avi = Grow(s.Avi, s.Scaling?.Avi ?? 0, level);
            v.Rld = Grow(s.Rld, s.Scaling?.Rld ?? 0, level);
            v.Hit = Grow(s.Hit, s.Scaling?.Hit ?? 0, level);
            v.Eva = Grow(s.Eva, s.Scaling?.Eva ?? 0, level);
            v.Asw = Grow(s.Asw, s.Scaling?.Asw ?? 0, level);
            v.Luck = Grow(s.Luck, s.Scaling?.Luck ?? 0, level);
            v.Speed = s.Speed + (s.Scaling?.Speed ?? 0) * (level - 1) / 1000.0;
            return v;
        }

        private static int Grow(int baseValue, int scaling, int level) =>
            (int)Math.Floor(baseValue + scaling * (level - 1) / 1000.0);

        private static string FormatStatDict(Dictionary<string, int> dict, string prefix)
        {
            if (dict == null || dict.Count == 0) return prefix.TrimEnd(' ', ':');
            return prefix + string.Join("、", dict.Select(kv => $"{StatKeyZh(kv.Key)}+{kv.Value}"));
        }

        private static string StatKeyZh(string key) => key switch
        {
            "hp" => "耐久", "fp" => "炮击", "trp" => "雷击", "aa" => "防空",
            "avi" => "航空", "rld" => "装填", "hit" => "命中", "eva" => "机动",
            "asw" => "反潜", "spd" => "航速", "luck" => "幸运", _ => key
        };

        private static string EquipmentTypeName(int code) => code switch
        {
            1 => "驱逐主炮", 2 => "轻巡主炮", 3 => "重巡主炮", 4 => "战列主炮",
            5 => "鱼雷", 6 => "防空炮", 7 => "战斗机", 8 => "鱼雷机", 9 => "轰炸机",
            10 => "设备", 11 => "超巡主炮", 12 => "水上机", 13 => "潜艇鱼雷", 14 => "深水炸弹",
            15 => "反潜机", 17 => "反潜直升机", 18 => "货物", 20 => "导弹", 21 => "近炸防空炮",
            _ => $"类型{code}"
        };

        private static string EquipTypesText(IReadOnlyCollection<int> types)
        {
            if (types == null || types.Count == 0) return "—";
            return string.Join("、", types.Select(EquipmentTypeName));
        }

        /// <summary>
        /// 获取方式：按大类分组（建造 → 商店 → 任务 → 打捞 → 邀请函 → 祈愿 → 支援）。
        /// 非打捞类一律平铺不折叠；打捞按来源（主线/档案/活动/彩蛋）分组，
        /// 获取点 ≤5 全列、6~30 按来源折叠、&gt;30 只给摘要 + 检索提示。
        /// </summary>
        private static List<AcqGroupView> BuildAcquireGroups(ShipViewModel ship)
        {
            var groups = new List<AcqGroupView>();
            var acq = ship.Acquisition ?? new AcquisitionData();

            var build = BuildConstructionGroup(acq);
            if (build != null) groups.Add(build);

            var shop = BuildShopGroup(acq);
            if (shop != null) groups.Add(shop);

            var mission = BuildMissionGroup(acq);
            if (mission != null) groups.Add(mission);

            var drop = BuildDropGroup(acq);
            if (drop != null) groups.Add(drop);

            var invitation = BuildInvitationGroup(acq);
            if (invitation != null) groups.Add(invitation);

            var pray = BuildPrayGroup(acq);
            if (pray != null) groups.Add(pray);

            var support = BuildSupportGroup(acq);
            if (support != null) groups.Add(support);

            // 备注与参与活动同属获取途径说明，放最后，避免信息丢失
            /*var notes = BuildNoteGroup(acq);
            if (notes != null) groups.Add(notes);
            */
            if (groups.Count == 0)
            {
                var other = new AcqGroupView { Title = "其他" };
                other.Lines.Add(new AcqLineView { Text = "—" });
                groups.Add(other);
            }
            return groups;
        }

        /// <summary>备注/参与活动（原来混在获取方式里的说明行）。</summary>
        private static AcqGroupView? BuildNoteGroup(AcquisitionData acq)
        {
            var group = new AcqGroupView { Title = "备注" };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var n in acq.Notes)
            {
                string text = n.GetLocalized();
                if (!string.IsNullOrEmpty(text) && seen.Add(text))
                    group.Lines.Add(new AcqLineView { Text = text });
            }
            foreach (var e in acq.Events)
            {
                string text = e.GetLocalized();
                if (!string.IsNullOrEmpty(text) && seen.Add(text))
                    group.Lines.Add(new AcqLineView { Text = text, Detail = "（参与活动）" });
            }
            return group.HasLines ? group : null;
        }

        private static AcqGroupView? BuildConstructionGroup(AcquisitionData acq)
        {
            var group = new AcqGroupView { Title = "建造" };

            var pools = new List<string>();
            void AddPool(string pool)
            {
                if (!string.IsNullOrEmpty(pool) && !pools.Contains(pool)) pools.Add(pool);
            }
            AddPool(acq.BuildPool);
            if (acq.Light) AddPool("轻型池");
            if (acq.Heavy) AddPool("重型池");
            if (acq.Special) AddPool("特型池");
            foreach (var pool in acq.BuildPools) AddPool(pool);

            if (pools.Count > 0)
            {
                group.Lines.Add(new AcqLineView
                {
                    Text = "常驻建造：" + string.Join("、", pools),
                    Detail = string.IsNullOrEmpty(acq.BuildTime) ? "" : $"建造时间 {acq.BuildTime}"
                });
            }

            if (acq.LimitedEvent.HasAnyText())
            {
                group.Lines.Add(new AcqLineView
                {
                    Text = "限时建造：" + acq.LimitedEvent!.GetLocalized(),
                    Detail = string.IsNullOrEmpty(acq.LimitedText) ? "" : "UP：" + acq.LimitedText
                });
            }
            else if (acq.NonPermanent)
            {
                group.Lines.Add(new AcqLineView { Text = "限时开放建造", Detail = acq.LimitedText });
            }

            if (!string.IsNullOrEmpty(acq.ExchangeText))
                group.Lines.Add(new AcqLineView { Text = "保底兑换", Detail = acq.ExchangeText });

            return group.HasLines ? group : null;
        }

        private static AcqGroupView? BuildShopGroup(AcquisitionData acq)
        {
            var names = new List<string>(acq.ShopNames);
            foreach (var ut in acq.UnlockTypes)
            {
                string name = ut switch
                {
                    UnlockType.GuildShop => "军需商店",
                    UnlockType.MedalShop => "勋章商店",
                    UnlockType.CoreDataShop => "核心数据商店",
                    UnlockType.MeritShop => "功勋商店",
                    UnlockType.PrototypeShop => "原型商店",
                    UnlockType.Memento => "记忆晶片（档案）",
                    UnlockType.MetaShop => "META 商店",
                    UnlockType.MetaShowdown => "META 挑战",
                    UnlockType.Dossier => "机密档案",
                    UnlockType.Shipyard => "船坞",
                    UnlockType.PermanentUrPity => "常驻 UR 保底",
                    _ => ""
                };
                if (name.Length > 0 && !names.Contains(name)) names.Add(name);
            }

            if (names.Count == 0) return null;
            var group = new AcqGroupView { Title = "商店" };
            group.Lines.Add(new AcqLineView { Text = "兑换渠道：" + string.Join("、", names) });
            return group;
        }

        private static AcqGroupView? BuildMissionGroup(AcquisitionData acq)
        {
            var names = new List<string>(acq.MissionTexts);
            void Add(string name)
            {
                if (!names.Contains(name)) names.Add(name);
            }
            foreach (var ut in acq.UnlockTypes)
            {
                switch (ut)
                {
                    case UnlockType.Quest: Add("任务"); break;
                    case UnlockType.WeeklyMissions: Add("每周任务"); break;
                    case UnlockType.MonthlyLogin: Add("每月签到"); break;
                    case UnlockType.Returnee: Add("回归奖励"); break;
                    case UnlockType.Cruise: Add("巡游"); break;
                }
            }

            if (names.Count == 0) return null;
            var group = new AcqGroupView { Title = "任务" };
            group.Lines.Add(new AcqLineView { Text = "任务奖励：" + string.Join("、", names) });
            return group;
        }

        /// <summary>
        /// 打捞：按来源汇总获取点。数据来自 DB `map_drops`（一行 = 一张图，含来源与普通/困难），
        /// 旧缓存没有该结构时退回 `character_acquire` 的「章节-关卡」。
        /// 展示分档：≤5 全列、6~30 按来源折叠、&gt;30 只给摘要 + 检索提示。
        /// </summary>
        private static AcqGroupView? BuildDropGroup(AcquisitionData acq)
        {
            var points = new List<(string Source, string Label)>();

            if (acq.DropPoints.Count > 0)
            {
                foreach (var p in acq.DropPoints)
                {
                    string label = p.Map + (string.IsNullOrEmpty(p.Difficulty) ? "" : $"（{p.Difficulty}）");
                    if (p.IsBoss) label += "（Boss）";
                    points.Add((p.Source, label));
                }
            }
            else
            {
                foreach (var ch in acq.MapDrops)
                    foreach (var pt in ch.Points)
                        points.Add(("主线", $"{ch.Chapter}-{pt.Map}" + (pt.IsBoss ? "（Boss）" : "")));

                foreach (var wa in acq.ArchiveDrops)
                    foreach (var m in wa.Maps)
                        points.Add(("档案", m.Map + (m.IsBoss ? "（Boss）" : "")));
            }

            if (points.Count == 0)
            {
                // 退到装载层压缩好的行（无 map_drops 数据的旧库）
                var compressed = new List<string>();
                compressed.AddRange(acq.DropChapterLines);
                compressed.AddRange(acq.DropEventLines);
                if (acq.DropArchiveCount > 0) compressed.Add($"档案：{acq.DropArchiveCount} 处");
                if (compressed.Count == 0) return null;

                var compressedGroup = new AcqGroupView { Title = "打捞" };
                foreach (var line in compressed)
                    compressedGroup.Lines.Add(new AcqLineView { Text = line });
                return compressedGroup;
            }

            var group = new AcqGroupView { Title = "打捞" };

            // 来源固定顺序（主线 → 档案 → 活动 → 彩蛋），避免字典顺序漂移
            string[] sourceOrder = { "主线", "档案", "活动", "彩蛋" };
            var bySource = points
                .GroupBy(p => p.Source)
                .OrderBy(g => Array.IndexOf(sourceOrder, g.Key) is int i && i >= 0 ? i : sourceOrder.Length)
                .ToList();

            if (points.Count <= 5)
            {
                // 少：直接全列，不折叠
                foreach (var p in points)
                    group.Lines.Add(new AcqLineView { Text = $"{p.Source} {p.Label}" });
                return group;
            }

            group.Summary = string.Join(" / ", bySource.Select(g => $"{g.Key} {g.Count()}"));

            if (points.Count <= 30)
            {
                // 中：按来源分组折叠
                foreach (var g in bySource)
                {
                    var source = new AcqSourceView
                    {
                        Name = $"{g.Key}（{g.Count()} 处）",
                        Points = string.Join("、", g.Select(p => p.Label))
                    };
                    group.Sources.Add(source);
                }
                return group;
            }

            // 多：只给摘要 + 检索提示
            group.Hint = $"{points.Count} 处，数量较多，建议使用搜索进入完整页浏览";
            return group;
        }

        private static AcqGroupView? BuildInvitationGroup(AcquisitionData acq)
        {
            if (acq.InvitationItems.Count == 0) return null;
            var group = new AcqGroupView { Title = "邀请函" };
            group.Lines.Add(new AcqLineView { Text = "邀请函兑换：" + string.Join("、", acq.InvitationItems) });
            return group;
        }

        private static AcqGroupView? BuildPrayGroup(AcquisitionData acq)
        {
            if (!acq.PrayBuild) return null;
            var group = new AcqGroupView { Title = "祈愿" };
            group.Lines.Add(new AcqLineView
            {
                Text = "祈愿建造",
                Detail = acq.PrayPools.Count > 0 ? string.Join("、", acq.PrayPools) : ""
            });
            return group;
        }

        private static AcqGroupView? BuildSupportGroup(AcquisitionData acq)
        {
            if (!acq.UnlockTypes.Contains(UnlockType.Requisition)) return null;
            var group = new AcqGroupView { Title = "支援" };
            group.Lines.Add(new AcqLineView { Text = "勋章支援" });
            return group;
        }

        private static List<SlotView> BuildSlotViews(ShipViewModel ship)
        {
            var row = ship.SlotLevels != null && ship.SlotLevels.Count > 0 ? ship.SlotLevels[0] : new List<EquipmentSlotInfo>();
            var views = new List<SlotView>();
            for (int i = 0; i < row.Count; i++)
            {
                views.Add(new SlotView { SlotIndex = i + 1, EquipType = EquipTypesText(row[i].Types) });
            }
            return views;
        }

        private static List<InitEquipView> BuildInitEquipViews(ShipViewModel ship)
        {
            var views = new List<InitEquipView>();
            if (ship.SlotLevels == null || ship.SlotLevels.Count == 0) return views;
            var row = ship.SlotLevels[0];
            for (int i = 0; i < row.Count; i++)
            {
                if (row[i].DefaultEquipId <= 0) continue;
                views.Add(BuildInitEquipView(i + 1, row[i].DefaultEquipId));
            }
            return views;
        }

        /// <summary>
        /// 单条初始装备：名称/图标取装备目录（可能尚未装载，装载后由 RefreshInitEquipmentAsync 重刷），
        /// 7 项武器属性只显示库内真实提供的值，缺数据走「—」并在全部缺失时给一行说明。
        /// </summary>
        private static InitEquipView BuildInitEquipView(int slotIndex, int equipmentId)
        {
            var view = new InitEquipView { SlotIndex = slotIndex, EquipmentId = equipmentId };
            var entry = EquipmentCatalog.Find(equipmentId);
            view.Name = entry != null && !string.IsNullOrEmpty(entry.DisplayName) ? entry.DisplayName : $"装备 {equipmentId}";
            view.Sub = entry == null
                ? ""
                : $"{EquipmentTypeName(entry.Type)} · #{equipmentId}";
            view.IconUri = AssetUri($"icon/item/equips/{(entry != null && entry.IconId > 0 ? entry.IconId : equipmentId)}.png",
                                    "icon/item/unkown.png");

            var values = new (string Label, string Code, string? Text)[]
            {
                // 弹药：weapons.ammo_type 只有编号（1~24），库内没有弹种名称表 → 按缺数据显示
                ("弹药", "ammo", null),
                ("射角", "angle", NumberText(entry?.Angle)),
                ("伤害", "damage", NumberText(entry?.Damage)),
                ("射程", "range", NumberText(entry?.Range)),
                // weapons.reload 以 1/100 秒为单位（240 → 2.4 秒）
                ("装填", "reload", entry?.Reload is double reload ? (reload / 100.0).ToString("0.##") : null),
                ("散布", "scatter", NumberText(entry?.Scatter)),
                ("鱼雷", "torpedo", TorpedoText(entry?.Torpedo))
            };
            bool anyValue = false;
            foreach (var (label, code, text) in values)
            {
                bool has = !string.IsNullOrEmpty(text);
                anyValue |= has;
                view.Attributes.Add(new EquipAttrView
                {
                    Label = label,
                    Value = has ? text! : "—",
                    IconUri = AssetUri($"icon/equipmentattr/{code}.png", "icon/item/unkown.png")
                });
            }

            // 库里没有 weapons 表时 7 项全空，这里说明原因，避免用户以为是显示故障
            if (!anyValue)
                view.AttributeHint = "数据库未提供武器参数（伤害 / 射角 / 散布 / 射程 / 装填 / 弹药 / 鱼雷）";
            return view;
        }

        private static string? NumberText(int? value) => value.HasValue ? value.Value.ToString() : null;

        /// <summary>鱼雷数量（`weapons.torpedo`，主炮类为 0 → 视为不适用）。</summary>
        private static string? TorpedoText(int? value) => value is int n && n > 0 ? $"×{n}" : null;

        /// <summary>
        /// 装备目录可能尚未装载（首次进详情页时目录为空），装载后重刷一次。
        /// 用 token 校验，避免切换舰船后旧结果覆盖新页面；async void 内必须自己吞异常，否则会崩主线程。
        /// </summary>
        private async void RefreshInitEquipmentAsync(ShipViewModel ship)
        {
            if (EquipmentCatalog.All.Count > 0) return;
            int token = ++_initEquipToken;
            try
            {
                await EquipmentCatalog.EnsureLoadedAsync();
                if (token != _initEquipToken || _currentShip == null || _currentShip.Id != ship.Id) return;
                InitialEquipmentItemsControl.ItemsSource = BuildInitEquipViews(_currentShip);
            }
            catch (Exception ex)
            {
                LogService.Warning($"初始装备属性刷新失败：{ex.Message}", nameof(ShipDetailControl));
            }
        }

        /// <summary>
        /// 改造分步列表：按 `step` 分组竖排，步内按节点顺序（近似游戏 `transform_list[step][innerIndex]`）。
        /// 材料带品质底框；需要消耗本体的节点额外给一条「本体」芯片（船头像 + 原始稀有度框）。
        /// </summary>
        private static List<RetrofitStepView> BuildRetrofitNodeViews(ShipViewModel ship)
        {
            var steps = new List<RetrofitStepView>();
            var retro = ship.Retrofit;
            if (retro?.Nodes == null) return steps;

            string shipAvatar = ship.SkinAvatarUri(ship.CurrentSkinId);
            string shipBg = RarityBackground.ShipUri(ship.CategoryEnum, ship.RarityEnum);

            foreach (var group in retro.Nodes.GroupBy(n => n.Step).OrderBy(g => g.Key))
            {
                var stepView = new RetrofitStepView { Step = group.Key, Title = $"第 {group.Key} 步" };
                foreach (var node in group.OrderBy(n => n.IndexInStep))
                {
                    var cost = retro.CostByLetter != null && retro.CostByLetter.TryGetValue(node.Letter, out var c) ? c : null;
                    var chips = new List<ItemChipView>();
                    if (cost?.ItemsByRound != null)
                    {
                        foreach (var item in cost.ItemsByRound.SelectMany(r => r ?? new List<ItemRef>()))
                        {
                            if (item.Amount <= 0) continue;
                            string text = string.IsNullOrEmpty(item.Name) ? $"#{item.Id}×{item.Amount}" : $"{item.Name}×{item.Amount}";
                            chips.Add(new ItemChipView
                            {
                                Text = text,
                                IconUri = AssetUri(item.IconRel, "icon/item/unkown.png"),
                                BgUri = RarityBackground.EquipUri(item.Rarity),
                                HasBg = item.Rarity > 0
                            });
                        }
                    }
                    if (node.RequiresShip)
                    {
                        chips.Add(new ItemChipView
                        {
                            Text = $"{ship.DisplayName}（本体）×1",
                            IconUri = string.IsNullOrEmpty(shipAvatar) ? AssetUri(null, "icon/item/unkown.png") : shipAvatar,
                            BgUri = shipBg,
                            HasBg = true
                        });
                    }
                    if (chips.Count == 0)
                        chips.Add(new ItemChipView { Text = "—", IconUri = AssetUri(null, "icon/item/unkown.png") });

                    stepView.Nodes.Add(new RetrofitNodeView
                    {
                        Name = string.IsNullOrEmpty(node.Name?.GetLocalized())
                            ? node.Letter
                            : node.Name.GetLocalized(),
                        AttributeBonus = string.IsNullOrEmpty(node.EffectText) ? "—" : node.EffectText,
                        IconUri = AssetUri(node.Icon, null),
                        Items = chips,
                        RequiredCoins = cost?.Coins ?? node.Gold,
                        RequiredLevel = node.MinLevel,
                        // 数据库提供的是"最低突破档"（0~3）；沿用旧模板字段名展示
                        RequiredStars = node.MinLb
                    });
                }
                steps.Add(stepView);
            }
            return steps;
        }

        private static List<TaskView> BuildResearchTaskViews(ResearchData? research)
        {
            var views = new List<TaskView>();
            if (research == null) return views;
            // 开发里程碑（属性加成来自模型；数据库当前只给 id）
            if (research.Milestones.Count > 0)
            {
                foreach (var m in research.Milestones.Where(x => x.Bonus.Count > 0))
                {
                    views.Add(new TaskView
                    {
                        Name = $"开发{m.DevLevel}级",
                        Description = FormatStatDict(m.Bonus, ""),
                        Requirement = ""
                    });
                }
            }
            // 解锁任务链（DB unlock_task：只有任务 id 与开放间隔，数据库未提供任务名）
            if (research.UnlockTaskCount > 0)
            {
                views.Add(new TaskView
                {
                    Name = $"解锁任务链 {research.UnlockTaskCount} 个",
                    Description = "数据库未提供任务名称与要求",
                    Requirement = ""
                });
            }
            return views;
        }

        private static List<KeyView> BuildDevelopBonusViews(ResearchData? research)
        {
            var views = new List<KeyView>();
            if (research == null) return views;
            foreach (var m in research.Milestones.Where(x => x.Bonus.Count > 0))
                views.Add(new KeyView { Key = $"开发{m.DevLevel}级: {FormatStatDict(m.Bonus, "")}" });
            if (research.StrengthenEffectCount > 0 && research.Milestones.Count == 0)
                views.Add(new KeyView { Key = $"开发效果 {research.StrengthenEffectCount} 项（数据库未提供数值）" });
            if (research.FateStrengthenCount > 0 && research.Fate?.Stats.Count > 0 == false)
                views.Add(new KeyView { Key = $"天运拟合 {research.FateStrengthenCount} 级（数据库未提供数值）" });
            return views;
        }

        private static List<SkinView> BuildSkinViews(ShipViewModel ship)
        {
            var views = new List<SkinView>();
            if (ship.Skins == null) return views;
            foreach (var skin in ship.Skins)
            {
                views.Add(new SkinView
                {
                    Name = skin.Name?.GetLocalized() ?? skin.Id.ToString(),
                    ReleaseDate = "",
                    Type = SkinKindText(skin.Kind),
                    IsOathEnabled = skin.Kind == GameSkinKind.Oath,
                    AvatarUri = ship.SkinAvatarUri(skin.Id),
                    BgUri = RarityBackground.ShipUri(ship.CategoryEnum, ship.EffectiveRarityEnum)
                });
            }
            return views;
        }

        private static string SkinKindText(GameSkinKind kind) => kind switch
        {
            GameSkinKind.Default => "默认",
            GameSkinKind.Normal => "普通",
            GameSkinKind.Oath => "誓约",
            GameSkinKind.Retrofit => "改造",
            GameSkinKind.Original => "原设",
            GameSkinKind.Promo => "宣传",
            GameSkinKind.Valentine => "情人节",
            GameSkinKind.FormAlt => "形态",
            _ => kind.ToString()
        };

        /// <summary>
        /// 台词场景（DB `line_scenes.code`）→ 中文名。
        /// 库内 `name_key` 的 i18n 只覆盖 1/45 条，故此处内置全表兜底；补齐 i18n 后库内文案优先。
        /// </summary>
        private static string SceneNameZh(string? code) => code switch
        {
            "login_screen" => "登录界面",
            "ship_model" => "舰船模型",
            "self_intro" => "自我介绍",
            "acquire" => "获得",
            "login" => "登录",
            "login_ex" => "登录（追加）",
            "details" => "查看详情",
            "details_ex" => "查看详情（追加）",
            "home" => "主界面（待机）",
            "home_ex" => "主界面（追加）",
            "touch" => "触摸",
            "touch_ex" => "触摸（追加）",
            "special_touch" => "特殊触摸",
            "special_touch_ex" => "特殊触摸（追加）",
            "mission_reminder" => "任务提醒",
            "mission_reminder_ex" => "任务提醒（追加）",
            "mission_complete" => "任务完成",
            "mission_complete_ex" => "任务完成（追加）",
            "mail_reminder" => "邮件提醒",
            "mail_reminder_ex" => "邮件提醒（追加）",
            "return_to_port" => "回港",
            "return_to_port_ex" => "回港（追加）",
            "affinity_disappointed" => "好感度 · 失望",
            "affinity_stranger" => "好感度 · 陌生",
            "affinity_friendly" => "好感度 · 友好",
            "affinity_like" => "好感度 · 喜欢",
            "affinity_love" => "好感度 · 爱",
            "affinity_love_ex" => "好感度 · 爱（追加）",
            "oath" => "誓约",
            "gift" => "礼物",
            "commission_complete" => "委托完成",
            "commission_complete_ex" => "委托完成（追加）",
            "enhance_success" => "强化成功",
            "flagship_battle_start" => "旗舰开战",
            "flagship_battle_start_ex" => "旗舰开战（追加）",
            "victory" => "胜利",
            "victory_ex" => "胜利（追加）",
            "defeat" => "失败",
            "defeat_ex" => "失败（追加）",
            "skill" => "技能",
            "skill_ex" => "技能（追加）",
            "low_hp" => "血量告急",
            "low_hp_ex" => "血量告急（追加）",
            "easter_egg" => "彩蛋",
            "couple_encourage" => "组合鼓励",
            _ => string.IsNullOrEmpty(code) ? "其他" : code
        };

        /// <summary>场景键：新装载写 <see cref="VoiceLine.Scene"/>；旧静态缓存里只有 Key（同为场景代码）。</summary>
        private static string SceneKeyOf(VoiceLine line)
            => !string.IsNullOrEmpty(line.Scene) ? line.Scene : line.Key;

        /// <summary>场景显示名：库内文案优先，缺失时用内置中文表。</summary>
        private static string SceneDisplayName(VoiceLine line)
        {
            string localized = line.SceneName?.GetLocalized() ?? "";
            return string.IsNullOrEmpty(localized) ? SceneNameZh(SceneKeyOf(line)) : localized;
        }

        /// <summary>
        /// 按皮肤过滤后，把台词按场景分组（场景顺序取 DB sort_order，组内保持装载顺序）。
        /// 找不到该皮肤组时退回默认皮/唯一组，避免"皮肤存在但台词组 id 对不上"时整块空白。
        /// </summary>
        private static List<VoiceSceneView> BuildLineViews(ShipViewModel ship, int skinId)
        {
            var views = new List<VoiceSceneView>();
            var groups = ship.WordsBySkin;
            if (groups == null || groups.Count == 0) return views;

            var group = groups.FirstOrDefault(g => g.SkinId == skinId)
                        ?? groups.FirstOrDefault(g => g.SkinId <= 0)
                        ?? (groups.Count == 1 ? groups[0] : null);
            if (group == null) return views;

            var all = new List<VoiceLine>();
            all.AddRange(group.Lines ?? new List<VoiceLine>());
            all.AddRange(group.Extra ?? new List<VoiceLine>());

            foreach (var scene in all.GroupBy(SceneKeyOf).OrderBy(g => g.Min(l => l.SceneOrder)))
            {
                var sceneView = new VoiceSceneView { SceneName = SceneDisplayName(scene.First()) };
                foreach (var line in scene)
                {
                    sceneView.Lines.Add(new LineView
                    {
                        Name = sceneView.SceneName,
                        Content = line.Content?.GetLocalized() ?? "",
                        TriggerCondition = ConditionText(line.Conditions)
                    });
                }
                views.Add(sceneView);
            }
            return views;
        }

        /// <summary>
        /// 台词区皮肤下拉：列出该船**全部皮肤**（默认皮排最前），没台词的皮肤也保留，
        /// 选中后显示「该皮肤暂无可显示台词」。旧缓存里出现过的皮肤组也会补进列表。
        /// </summary>
        private void FillLineSkinComboBox(ShipViewModel ship)
        {
            var options = new List<LineSkinOption>();

            if (ship.Skins != null)
            {
                foreach (var skin in ship.Skins.OrderByDescending(s => s.Kind == GameSkinKind.Default))
                {
                    options.Add(new LineSkinOption
                    {
                        SkinId = skin.Id,
                        Display = VoiceSkinLabel(ship, skin.Id)
                    });
                }
            }

            // 兜底：台词组里的皮肤若不在 Skins（旧离线缓存可能缺），仍要能选
            foreach (var group in ship.WordsBySkin ?? new List<SkinVoiceGroup>())
            {
                if (options.Any(o => o.SkinId == group.SkinId)) continue;
                options.Add(new LineSkinOption
                {
                    SkinId = group.SkinId,
                    Display = VoiceSkinLabel(ship, group.SkinId)
                });
            }

            LogService.Info(
                $"台词：{ship.DisplayName}({ship.Id}) 皮肤 {options.Count} 项，台词组 {(ship.WordsBySkin?.Count ?? 0)} 组",
                nameof(ShipDetailControl));

            _lineSkinOptions = options;
            _isUpdating = true;
            try
            {
                LineSkinComboBox.ItemsSource = options;
                LineSkinComboBox.SelectedIndex = options.Count > 0 ? 0 : -1;
            }
            finally
            {
                _isUpdating = false;
            }

            ApplyLineSkinSelection(ship);
        }

        private void LineSkinComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            ApplyLineSkinSelection(_currentShip);
        }

        /// <summary>
        /// 渲染选中皮肤的台词。
        /// 坑：ItemsSource 与 SelectedIndex 同一拍设置时，ComboBox.SelectedItem 可能是 null，
        /// 直接用 SelectedItem 会永远落到空态，所以以 SelectedIndex 为准。
        /// </summary>
        private void ApplyLineSkinSelection(ShipViewModel ship)
        {
            int index = LineSkinComboBox.SelectedIndex;
            if (index < 0 || index >= _lineSkinOptions.Count)
            {
                LinesItemsControl.ItemsSource = null;
                LineEmptyText.Text = (ship.WordsBySkin?.Count ?? 0) == 0 ? "数据库未提供该舰船台词" : "请选择皮肤";
                LineEmptyText.Visibility = Visibility.Visible;
                return;
            }

            var views = BuildLineViews(ship, _lineSkinOptions[index].SkinId);
            LinesItemsControl.ItemsSource = views;
            LineEmptyText.Text = "该皮肤暂无可显示台词";
            LineEmptyText.Visibility = views.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string SkinIdLabel(ShipViewModel ship, int skinId)
        {
            var skin = ship.Skins?.FirstOrDefault(s => s.Id == skinId);
            return skin?.Name?.GetLocalized() ?? $"皮肤{skinId}";
        }

        /// <summary>台词皮肤名：默认皮（SkinId=-1）用数据库默认皮名，其余按皮肤 id 取名。</summary>
        private static string VoiceSkinLabel(ShipViewModel ship, int skinId)
        {
            if (skinId > 0) return SkinIdLabel(ship, skinId);
            var defaultSkin = ship.Skins?.FirstOrDefault(s => s.Kind == GameSkinKind.Default);
            string name = defaultSkin?.Name.GetLocalized() ?? "";
            return string.IsNullOrEmpty(name)
                ? ship.DisplayName + L("ShipDetail_SkinBaseSuffix", "（基础立绘）")
                : name;
        }

        private static string ConditionText(VoiceLineConditions? c)
        {
            if (c == null) return "";
            if (c.Oath == true) return "誓约后";
            if (c.Affinity.HasValue) return $"好感≥{c.Affinity.Value}";
            if (c.Impossible == true) return "暂不可触发";
            if (c.Amount.HasValue && c.Ships?.Count > 0) return $"编队含 {c.Amount.Value} 艘指定船";
            if (c.Ships?.Count > 0) return "编队含指定船";
            return "";
        }

        // ========== 头像加载相关 ==========

        /// <summary>已上报过缺失的「船:皮肤」组合，避免同一组合反复写日志。</summary>
        private readonly System.Collections.Generic.HashSet<string> _reportedMissingAvatars = new();

        /// <summary>头像候选全部失败：写一条可定位的日志并提示界面。</summary>
        private void ReportAvatarMissing()
        {
            var ship = _currentShip;
            if (ship == null) return;
            string key = ship.Id + ":" + ship.CurrentSkinId;
            if (_reportedMissingAvatars.Add(key))
            {
                LogService.Warning(
                    "头像缺失：ID " + ship.Id +
                    "｜global " + ship.RawName +
                    "｜名称 " + ship.DisplayName +
                    "｜皮肤 " + ship.CurrentSkinId +
                    "｜目录 " + ship.AvatarFolderRel +
                    "｜候选 " + string.Join(" / ", ship.AvatarFileBases.Select(b => b + ".png")),
                    nameof(ShipDetailControl));
            }
            AvatarStateText.Text = "未找到头像文件：" + ship.AvatarFolderRel + "/" + ship.AvatarFileName;
        }

        private void SetDefaultAvatar()
        {
            var defaultUri = new Uri("ms-appx:///Assets/icon/square/unknown.png");
            ShipAvatarImage.Source = new BitmapImage(defaultUri);
        }

        /// <summary>刷新头像外框与誓约边框（按当前船的有效稀有度 / 誓约状态）。</summary>
        private void UpdateAvatarFrames()
        {
            var ship = _currentShip;
            if (ship == null)
            {
                ShipAvatarBorderImage.Visibility = Visibility.Collapsed;
                ShipOathFrameImage.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                string border = ship.AvatarBorderUri;
                if (!string.IsNullOrEmpty(border))
                {
                    ShipAvatarBorderImage.Source = new BitmapImage(new Uri(border));
                    // 边框画布比例随档位不同（普通/META/科研金/科研彩），边距由 VM 按档位给
                    ShipAvatarBorderImage.Margin = ship.AvatarBorderMargin;
                    ShipAvatarBorderImage.Visibility = Visibility.Visible;
                }
                else
                {
                    ShipAvatarBorderImage.Visibility = Visibility.Collapsed;
                }

                if (ship.ShowOathFrame)
                {
                    string frame = ship.OathFrameUri;
                    if (!string.IsNullOrEmpty(frame))
                    {
                        ShipOathFrameImage.Source = new BitmapImage(new Uri(frame));
                        ShipOathFrameImage.Visibility = Visibility.Visible;
                    }
                    else ShipOathFrameImage.Visibility = Visibility.Collapsed;
                }
                else ShipOathFrameImage.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogService.Warning("头像边框加载失败：" + ex.Message, nameof(ShipDetailControl));
            }
        }

        private void StartAvatarFadeIn()
        {
            UpdateAvatarFrames();
            AvatarFadeInStoryboard.Stop();
            ShipAvatarImage.Opacity = 0;
            AvatarFadeInStoryboard.Begin();
        }

        private void TryLoadImage(string uriString, int token, Action<bool> callback)
        {
            try
            {
                var uri = new Uri(uriString);
                var bitmap = new BitmapImage();
                bitmap.ImageFailed += (s, e) =>
                {
                    if (token == _avatarLoadToken)
                        callback?.Invoke(false);
                };
                bitmap.ImageOpened += (s, e) =>
                {
                    if (token == _avatarLoadToken)
                        callback?.Invoke(true);
                };
                bitmap.UriSource = uri;
                ShipAvatarImage.Source = bitmap;
            }
            catch
            {
                if (token == _avatarLoadToken)
                    callback?.Invoke(false);
            }
        }

        private async void LoadAndFadeInAvatar(int token)
        {
            if (token != _avatarLoadToken) return;
            ShipAvatarImage.Opacity = 0;
            SetAvatarBackground();

            // 1. 尝试本地文件（新规则目录/旧目录，含皮肤候选名）
            if (_currentShip != null)
            {
                string localPath = _currentShip.LocalAvatarPath;
                if (File.Exists(localPath))
                {
                    try
                    {
                        if (App.IsPackaged)
                        {
                            // 打包环境：经 StorageFile 读取（避免 ms-appdata 路径问题）
                            var file = await StorageFile.GetFileFromPathAsync(localPath);
                            using (var stream = await file.OpenReadAsync())
                            {
                                var bitmap = new BitmapImage();
                                await bitmap.SetSourceAsync(stream);
                                if (token == _avatarLoadToken)
                                {
                                    ShipAvatarImage.Source = bitmap;
                                    StartAvatarFadeIn();
                                    return;
                                }
                            }
                        }
                        else
                        {
                            // 非打包环境没有包标识，StorageFile 可能抛“拒绝访问/类未注册”：直接按文件 URI 加载
                            var bitmap = new BitmapImage { UriSource = new Uri(localPath) };
                            if (token == _avatarLoadToken)
                            {
                                ShipAvatarImage.Source = bitmap;
                                StartAvatarFadeIn();
                                return;
                            }
                        }
                    }
                    catch { /* 忽略本地加载失败 */ }
                }
            }

            // 2. 内置资源候选（依次尝试）：squareicon/{类型}/{阵营}/{名}.png/.jpg → 占位图
            //    旧版 Assets/Ship 目录已废弃删除，不再作为候选
            var candidates = new List<string>();
            if (_currentShip != null)
            {
                foreach (var sqBase in _currentShip.AvatarSquareiconRelBases)
                {
                    candidates.Add($"ms-appx:///Assets/icon/square/{sqBase}.png");
                    candidates.Add($"ms-appx:///Assets/icon/square/{sqBase}.jpg");
                }
            }
            TryLoadPackageCandidates(candidates, 0, token);
        }

        /// <summary>按稀有度设置详情头像背景（Assets/background）。</summary>
        private void SetAvatarBackground()
        {
            try
            {
                if (_currentShip == null || string.IsNullOrEmpty(_currentShip.AvatarBgUri))
                {
                    ShipAvatarBgImage.Source = null;
                    return;
                }
                ShipAvatarBgImage.Source = new BitmapImage(new Uri(_currentShip.AvatarBgUri));
            }
            catch { /* 忽略背景加载失败 */ }
        }

        /// <summary>按顺序尝试内置资源候选；全部失败后显示占位图并记录日志。</summary>
        private void TryLoadPackageCandidates(IReadOnlyList<string> uris, int index, int token)
        {
            if (token != _avatarLoadToken) return;
            if (uris == null || index >= uris.Count)
            {
                SetDefaultAvatar();
                StartAvatarFadeIn();
                ReportAvatarMissing();
                return;
            }

            TryLoadImage(uris[index], token, success =>
            {
                if (token != _avatarLoadToken) return;
                if (success)
                {
                    StartAvatarFadeIn();
                }
                else
                {
                    TryLoadPackageCandidates(uris, index + 1, token);
                }
            });
        }

        // ========== 兵装图标加载 ==========
        /// <summary>
        /// 专属兵装图标背景（图标底色）：走稀有度底色体系 `RarityBackground.AugmentUri()`
        /// → `Assets/background/bg_5_2`，与装备图标（EquipPickerDialog）同一套 `Assets/background/bg_*.png`。
        /// 传空/null 表示清除背景（无专属兵装时）。
        /// </summary>
        private void SetGearIconBackground(string? uri = null)
        {
            if (string.IsNullOrEmpty(uri))
            {
                SpecialGearBgImage.Source = null;
                return;
            }
            try { SpecialGearBgImage.Source = new BitmapImage(new Uri(uri)); }
            catch { SpecialGearBgImage.Source = null; }
        }

        private void SetDefaultGearIcon()
        {
            var candidates = new List<string>();
            AddItemIconCandidates(candidates, "Assets/icon/item/default.png");
            try { SpecialGearImage.Source = new BitmapImage(new Uri(candidates[0])); }
            catch { SpecialGearImage.Source = null; }
        }

        private void StartGearFadeIn()
        {
            GearFadeInStoryboard.Stop();
            SpecialGearImage.Opacity = 0;
            GearFadeInStoryboard.Begin();
        }

        /// <summary>
        /// 专属兵装图标：唯一来源 `Assets/icon/item/spweapon/{兵装id}.png`（与界面语言无关）。
        /// 加载方式与头像一致——BitmapImage + UriSource，并**立即挂到 Image**（尚未挂载的 BitmapImage
        /// 不会开始解码，ImageOpened/ImageFailed 都不会触发）；ImageFailed 时试下一候选。
        /// 不使用 StorageFile.GetFileFromApplicationUriAsync：该 API 需要包标识，直接运行 exe 时必失败。
        /// </summary>
        private void LoadCustomGearIcon(int augmentId, int token)
        {
            if (token != _gearLoadToken) return;

            var candidates = new List<string>();
            if (augmentId > 0)
                AddItemIconCandidates(candidates, $"Assets/icon/item/spweapon/{augmentId}.png");

            if (candidates.Count == 0)
            {
                LogService.Warning("专属兵装 id 无效（<=0），无法定位图标，保留默认占位图", nameof(ShipDetailControl));
                return;
            }

            LogService.Info($"专属兵装图标加载：id={augmentId}，候选 {candidates.Count} 个，首选 {candidates[0]}", nameof(ShipDetailControl));
            TryLoadGearCandidate(candidates, 0, token);
        }

        /// <summary>
        /// 同一资源的候选 URI：包内 `ms-appx` 优先，磁盘 `file://`（存在时）兜底，
        /// 使已打包注册与直接运行 exe 两种方式都能取到图标。
        /// </summary>
        private static void AddItemIconCandidates(List<string> sink, string relPath)
        {
            sink.Add($"ms-appx:///{relPath}");
            try
            {
                string physical = Path.Combine(AppContext.BaseDirectory, relPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(physical))
                    sink.Add(new Uri(physical).AbsoluteUri);
            }
            catch { /* 路径异常忽略，仅用 ms-appx 候选 */ }
        }

        /// <summary>
        /// Assets 相对路径（如 `icon/refit/hp_1.png`）→ 可直接绑定的 URI。
        /// 文件不存在时返回 <paramref name="fallbackRel"/> 的 URI（也为空则返回空串，
        /// 模板用 HasIcon 之类开关隐藏，避免列表里出现空白图）。
        /// </summary>
        private static string AssetUri(string? relPath, string? fallbackRel)
        {
            string uri = TryAssetUri(relPath);
            if (uri.Length == 0 && !string.IsNullOrEmpty(fallbackRel)) uri = TryAssetUri(fallbackRel);
            return uri;
        }

        private static string TryAssetUri(string? relPath)
        {
            if (string.IsNullOrWhiteSpace(relPath)) return "";
            try
            {
                string physical = Path.Combine(AppContext.BaseDirectory, "Assets",
                    relPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(physical)) return "";
                return App.IsPackaged ? "ms-appx:///Assets/" + relPath : new Uri(physical).AbsoluteUri;
            }
            catch
            {
                return "";
            }
        }
        /// <summary>按顺序尝试兵装图标候选；全部失败则记录日志并保留默认占位图。</summary>
        private void TryLoadGearCandidate(IReadOnlyList<string> uris, int index, int token)
        {
            if (token != _gearLoadToken) return;
            if (uris == null || index >= uris.Count)
            {
                LogService.Warning($"专属兵装图标未找到（{uris?.Count ?? 0} 个候选均失败），保留默认占位图", nameof(ShipDetailControl));
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.ImageFailed += (s, e) =>
                {
                    if (token != _gearLoadToken) return;
                    LogService.Info($"专属兵装图标候选失败，尝试下一个：{uris[index]}", nameof(ShipDetailControl));
                    TryLoadGearCandidate(uris, index + 1, token);
                };
                bitmap.ImageOpened += (s, e) =>
                {
                    if (token != _gearLoadToken) return;
                    StartGearFadeIn();
                };
                bitmap.UriSource = new Uri(uris[index]);
                SpecialGearImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                LogService.Warning($"专属兵装图标 URI 无效：{uris[index]}（{ex.Message}）", nameof(ShipDetailControl));
                TryLoadGearCandidate(uris, index + 1, token);
            }
        }

        // ========== 清空显示 ==========
        private void ClearDisplay()
        {
            ++_avatarLoadToken;
            ++_gearLoadToken;
            _currentShip = null;

            // 只清空"动态值"TextBlock（按 x:Name 白名单），保留静态标签/卡片标题
            foreach (var child in FindVisualChildren<TextBlock>(this))
            {
                if (!string.IsNullOrEmpty(child.Name) && DynamicTextNames.Contains(child.Name))
                    child.Text = "";
            }

            // 清空列表
            AcquireGroupsItemsControl.ItemsSource = null;
            EquipmentSlotsItemsControl.ItemsSource = null;
            InitialEquipmentItemsControl.ItemsSource = null;
            SkillsHost.Children.Clear();
            RetrofitNodesItemsControl.ItemsSource = null;
            ResearchTasksItemsControl.ItemsSource = null;
            MetaBorder.Visibility = Visibility.Collapsed;
            DevelopBonusItemsControl.ItemsSource = null;
            SkinsItemsControl.ItemsSource = null;
            LinesItemsControl.ItemsSource = null;
            GiftPreferencesItemsControl.ItemsSource = null;
            FateSimPanel.Visibility = Visibility.Collapsed;

            // 重置状态控件
            OwnedCheckBox.IsChecked = false;
            BreakthroughSlider.Value = 0;
            BreakthroughValueText.Text = "0";
            OathCheckBox.IsChecked = false;
            Level120CheckBox.IsChecked = false;
            RemodeledCheckBox.IsChecked = false;
            RemodeledCheckBox.IsEnabled = false;
            SpecialGearObtainedCheckBox.IsChecked = false;
            SpecialGearObtainedCheckBox.Visibility = Visibility.Collapsed;

            // 重置头像/皮肤
            ShipAvatarBgImage.Source = null;
            SetDefaultAvatar();
            AvatarFadeInStoryboard.Stop();
            ShipAvatarImage.Opacity = 1;
            SkinComboBox.ItemsSource = null;
            SkinComboBox.Visibility = Visibility.Collapsed;
            SkinLabelText.Visibility = Visibility.Collapsed;
            AvatarStateText.Text = "";

            // 重置兵装
            SpecialGearImage.Source = null;
            SetGearIconBackground();
            SpecialGearExpander.Visibility = Visibility.Collapsed;
            SpecialGearBorder.Visibility = Visibility.Collapsed;
            GearFadeInStoryboard.Stop();

            // 条件卡片一并收起（清空后不该再占位置）
            RetrofitBorder.Visibility = Visibility.Collapsed;
            ResearchBorder.Visibility = Visibility.Collapsed;
            MetaBorder.Visibility = Visibility.Collapsed;

            // 重置台词皮肤下拉（台词区外观由 x:Name 白名单之外，需单独清）
            _lineSkinOptions = new List<LineSkinOption>();
            _isUpdating = true;
            try { LineSkinComboBox.ItemsSource = null; }
            finally { _isUpdating = false; }
            LineEmptyText.Visibility = Visibility.Collapsed;
        }

        // ========== 状态变更事件 ==========
        private void OnOwnedChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool newOwned = OwnedCheckBox.IsChecked ?? false;
                if (_currentShip.Owned != newOwned)
                {
                    _currentShip.Owned = newOwned;
                    if (!_currentShip.Owned)
                    {
                        _currentShip.Breakthrough = 0;
                        _currentShip.Oath = false;
                        _currentShip.Level120 = false;
                        _currentShip.Retrofitted = false;
                        _currentShip.SpecialGearObtained = false;
                        BreakthroughSlider.Value = 0;
                        OathCheckBox.IsChecked = false;
                        Level120CheckBox.IsChecked = false;
                        RemodeledCheckBox.IsChecked = false;
                        SpecialGearObtainedCheckBox.IsChecked = false;
                    }
                    RemodeledCheckBox.IsEnabled = CanRemodel(_currentShip) && _currentShip.Owned;
                    SaveShip();
                    UpdateControlStates();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 获得状态改为 {_currentShip.Owned}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnBreakthroughChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            if (IsSpecialBulin(_currentShip))
            {
                BreakthroughSlider.Value = 3;
                return;
            }
            _isUpdating = true;
            try
            {
                int newValue = (int)e.NewValue;
                if (_currentShip.Breakthrough != newValue)
                {
                    _currentShip.Breakthrough = newValue;
                    BreakthroughValueText.Text = newValue.ToString();
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 突破状态改为 {_currentShip.Breakthrough}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnOathChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool newOath = OathCheckBox.IsChecked ?? false;
                if (_currentShip.Oath != newOath)
                {
                    _currentShip.Oath = newOath;
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 誓约状态改为 {_currentShip.Oath}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnLevel120Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool newLevel120 = Level120CheckBox.IsChecked ?? false;
                if (_currentShip.Level120 != newLevel120)
                {
                    _currentShip.Level120 = newLevel120;
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 等级状态改为 {_currentShip.Level120}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnRemodeledChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool wasRemodeled = _currentShip.Retrofitted;
                bool newRemodeled = RemodeledCheckBox.IsChecked ?? false;
                if (wasRemodeled != newRemodeled)
                {
                    _currentShip.Retrofitted = newRemodeled;
                    SaveShip();
                    RefreshNameAndRarityDisplay();
                    RefreshAvatarForRemodel();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 改造状态改为 {_currentShip.Retrofitted}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnSpecialGearObtainedChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool newObtained = SpecialGearObtainedCheckBox.IsChecked ?? false;
                if (_currentShip.SpecialGearObtained != newObtained)
                {
                    _currentShip.SpecialGearObtained = newObtained;
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 专属兵装状态改为 {_currentShip.SpecialGearObtained}");
                }
            }
            finally { _isUpdating = false; }
        }

        // ========== 辅助方法 ==========
        private void RefreshNameAndRarityDisplay()
        {
            if (_currentShip == null) return;
            ShipNameText.Text = _currentShip.DisplayName;
            RarityText.Text = _currentShip.EffectiveRarity;
        }

        private void RefreshAvatarForRemodel()
        {
            if (_currentShip == null) return;
            int token = ++_avatarLoadToken;
            ShipAvatarImage.Opacity = 0;
            LoadAndFadeInAvatar(token);
        }

        // ================= 立绘/皮肤切换 =================

        /// <summary>资源键取词（缺失回退中文兜底）。</summary>
        private static string L(string key, string fallback) {
                string text = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse().GetString(key);
                return string.IsNullOrEmpty(text) ? fallback : text;
        }

        /// <summary>皮肤下拉项。</summary>
        public sealed class AvatarOption
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
        }

        /// <summary>
        /// 填充皮肤下拉：数据库每艘船都带 1 条默认皮（skin_type=default），
        /// 因此不再另加「（基础立绘）」条目，直接用该条的名称作为唯一默认项（Id=0），
        /// 其余皮肤按 id 排列。
        /// </summary>
        private void FillSkinComboBox(ShipViewModel ship)
        {
            try
            {
                var defaultSkin = ship.Skins?.FirstOrDefault(s => s.Kind == GameSkinKind.Default);
                string defaultName = defaultSkin?.Name.GetLocalized() ?? "";
                if (string.IsNullOrWhiteSpace(defaultName))
                    defaultName = ship.DisplayName + L("ShipDetail_SkinBaseSuffix", "（基础立绘）");

                var options = new List<AvatarOption> { new AvatarOption { Id = 0, Display = defaultName } };
                if (ship.Skins != null)
                {
                    foreach (var skin in ship.Skins)
                    {
                        if (skin.Id <= 0) continue;
                        if (skin.Kind == GameSkinKind.Default) continue;   // 默认皮已由 0 代表
                        string kindTag = skin.Kind switch
                        {
                            GameSkinKind.Oath => L("ShipDetail_SkinTagOath", " ·誓约"),
                            GameSkinKind.Retrofit => L("ShipDetail_SkinTagRetrofit", " ·改造"),
                            _ => ""
                        };
                        string name = string.IsNullOrEmpty(skin.Name.GetLocalized())
                            ? string.Format(L("ShipDetail_SkinFallbackName", "皮肤 {0}"), skin.Id)
                            : skin.Name.GetLocalized();
                        options.Add(new AvatarOption { Id = skin.Id, Display = name + kindTag });
                    }
                }

                bool hasSkinOptions = options.Count > 1;
                SkinComboBox.Visibility = hasSkinOptions ? Visibility.Visible : Visibility.Collapsed;
                SkinLabelText.Visibility = hasSkinOptions ? Visibility.Visible : Visibility.Collapsed;
                AvatarStateText.Text = "";
                SkinComboBox.ItemsSource = options;

                _isUpdating = true;
                var current = options.FirstOrDefault(o => o.Id == ship.CurrentSkinId) ?? options[0];
                SkinComboBox.SelectedItem = current;
            }
            finally { _isUpdating = false; }
        }

        private void SkinComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            if ((sender as ComboBox)?.SelectedItem is AvatarOption opt && opt.Id != _currentShip.CurrentSkinId)
            {
                _currentShip.SwitchSkin(opt.Id);
                int token = ++_avatarLoadToken;
                ShipAvatarImage.Opacity = 0;
                LoadAndFadeInAvatar(token);
                AvatarStateText.Text = _currentShip.IsAvatarDownloaded
                    ? ""
                    : L("ShipDetail_SkinNotDownloaded", "本地未下载该立绘，将显示默认图（设置页可更新头像包）");
            }
        }

        private bool IsSpecialBulin(ShipViewModel? ship)
        {
            if (ship == null) return false;
            return ship.IsBulin; // 数据位掩码 Flags.Bulin（替代旧硬编码中文名）
        }

        private void UpdateControlStates()
        {
            if (_currentShip == null) return;
            bool owned = _currentShip.Owned;
            bool isBulin = IsSpecialBulin(_currentShip);

            OwnedCheckBox.IsEnabled = true;
            BreakthroughSlider.IsEnabled = owned && !isBulin;
            OathCheckBox.IsEnabled = owned;
            Level120CheckBox.IsEnabled = owned;
            RemodeledCheckBox.IsEnabled = owned && CanRemodel(_currentShip);
            SpecialGearObtainedCheckBox.IsEnabled = owned && _currentShip.UniqueAugment != null;

            if (isBulin)
            {
                _currentShip.Breakthrough = 3;
                BreakthroughSlider.Value = 3;
                BreakthroughValueText.Text = "3";
            }
        }

        private void OnAffectionChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                _currentShip.AffectionMax = (sender as CheckBox)?.IsChecked ?? false;
                SaveShip();
            }
            finally { _isUpdating = false; }
        }

        private void OnLevel125Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                _currentShip.Level125 = (sender as CheckBox)?.IsChecked ?? false;
                SaveShip();
            }
            finally { _isUpdating = false; }
        }

        private void OnResearchLevelChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                int newValue = (int)e.NewValue;
                if (_currentShip.ResearchLevel != newValue)
                {
                    _currentShip.ResearchLevel = newValue;
                    ResearchLevelText.Text = newValue.ToString();
                    SaveShip();
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnFateLevelChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                int newValue = (int)e.NewValue;
                if (_currentShip.FateLevel != newValue)
                {
                    _currentShip.FateLevel = newValue;
                    FateLevelText.Text = newValue.ToString();
                    SaveShip();
                }
            }
            finally { _isUpdating = false; }
        }

        private async void SaveShip()
        {
            if (_currentShip == null) return;
            var app = Application.Current as App;
            await app?.ShipManager?.SaveAsync();
        }

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var sub in FindVisualChildren<T>(child))
                    yield return sub;
            }
        }

        // ============================================================
        // 展示行（View Row）：以数据行形态向既有 DataTemplate 暴露字段，
        // 使 XAML 模板（属性名不变）直接可用；值均来自新模型。
        // ============================================================

        /// <summary>100 级属性快照展示容器。</summary>
        public sealed class ShipStatsView
        {
            public int Hp { get; set; }
            public ArmorType Armor { get; set; }
            public int Fp { get; set; }
            public int Trp { get; set; }
            public int Aa { get; set; }
            public int Avi { get; set; }
            public int Rld { get; set; }
            public int Hit { get; set; }
            public int Eva { get; set; }
            public int Asw { get; set; }
            public int Luck { get; set; }
            public double Speed { get; set; }
        }

        /// <summary>获取方式大类分组（建造/商店/任务/打捞/邀请函/祈愿/支援）。</summary>
        public sealed class AcqGroupView
        {
            public string Title { get; set; } = "";
            /// <summary>摘要（打捞 &gt;5 处时给「主线 N / 档案 N」）。</summary>
            public string Summary { get; set; } = "";
            /// <summary>提示（打捞 &gt;30 处时给「数量较多，建议使用搜索…」）。</summary>
            public string Hint { get; set; } = "";
            /// <summary>平铺条目（非打捞类，一律不折叠）。</summary>
            public List<AcqLineView> Lines { get; set; } = new();
            /// <summary>按来源折叠的分组（打捞 6~30 处）。</summary>
            public List<AcqSourceView> Sources { get; set; } = new();
            public bool HasLines => Lines.Count > 0;
        }

        /// <summary>获取方式条目（一行主文本 + 可选说明）。</summary>
        public sealed class AcqLineView
        {
            public string Text { get; set; } = "";
            public string Detail { get; set; } = "";
        }

        /// <summary>打捞来源分组（主线/活动/档案/彩蛋）。</summary>
        public sealed class AcqSourceView
        {
            public string Name { get; set; } = "";
            /// <summary>该来源下的关卡，按「、」拼接。</summary>
            public string Points { get; set; } = "";
        }
        /// <summary>装备槽展示行。</summary>
        public sealed class SlotView
        {
            public int SlotIndex { get; set; }
            public string EquipType { get; set; } = "";
        }

        /// <summary>初始装备展示行（装备名/图标 + 武器属性 7 项）。</summary>
        public sealed class InitEquipView
        {
            public int SlotIndex { get; set; }
            public int EquipmentId { get; set; }
            public string Name { get; set; } = "";
            /// <summary>类型 · 编号。</summary>
            public string Sub { get; set; } = "";
            public string IconUri { get; set; } = "";
            public List<EquipAttrView> Attributes { get; set; } = new();
            /// <summary>武器参数全部缺失时的说明文字（否则为空）。</summary>
            public string AttributeHint { get; set; } = "";
        }

        /// <summary>技能展示行。</summary>
        public sealed class SkillView
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
        }


        // ================= 技能：图标 + 占位符数值（强调色）+ 等级滑条 =================

        /// <summary>技能等级记忆（config: skill_levels = JSON {"船id":{"技能id":等级}}）。</summary>
        private Dictionary<string, Dictionary<string, int>>? _skillLevels;

        private Dictionary<string, Dictionary<string, int>> SkillLevels
        {
            get
            {
                if (_skillLevels != null) return _skillLevels;
                _skillLevels = new Dictionary<string, Dictionary<string, int>>();
                try
                {
                    var config = (Application.Current as App)?.ShipManager?.Config;
                    if (config != null && config.TryGetValue("skill_levels", out var raw) &&
                        raw is string json && !string.IsNullOrWhiteSpace(json))
                    {
                        var parsed = System.Text.Json.JsonSerializer
                            .Deserialize<Dictionary<string, Dictionary<string, int>>>(json);
                        if (parsed != null) _skillLevels = parsed;
                    }
                }
                catch { /* 损坏则重建 */ }
                return _skillLevels;
            }
        }

        private int GetSkillLevel(int shipId, Skill skill)
        {
            string shipKey = shipId.ToString();
            if (SkillLevels.TryGetValue(shipKey, out var map) &&
                map.TryGetValue(skill.Id.ToString(), out int level) && level >= 1)
                return Math.Min(level, Math.Max(1, skill.Max));
            return 1;
        }

        private void SaveSkillLevel(int shipId, Skill skill, int level)
        {
            try
            {
                string shipKey = shipId.ToString();
                if (!SkillLevels.TryGetValue(shipKey, out var map))
                    SkillLevels[shipKey] = map = new Dictionary<string, int>();
                map[skill.Id.ToString()] = level;

                var app = Application.Current as App;
                if (app?.ShipManager == null) return;
                app.ShipManager.Config["skill_levels"] =
                    System.Text.Json.JsonSerializer.Serialize(SkillLevels);
                app.ShipManager.SaveConfig();
            }
            catch { /* 记忆失败不影响显示 */ }
        }

        private static Brush? AccentBrush
        {
            get
            {
                foreach (string key in new[] { "AccentTextFillColorPrimaryBrush", "AccentButtonBackground", "SystemControlHighlightAccentBrush" })
                {
                    try
                    {
                        if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
                            return brush;
                    }
                    catch { }
                }
                return null;
            }
        }

        /// <summary>构建技能列表：图标 + 名称 + 带等级数值的描述 + Ticks 等级滑条。</summary>
        private void BuildSkillCards(ShipViewModel ship)
        {
            SkillsHost.Children.Clear();
            if (ship?.Skills == null) return;

            foreach (var skill in ship.Skills)
            {
                var card = new Border
                {
                    Padding = new Thickness(4, 6, 4, 6),
                    BorderBrush = ResBrushOf("CardStrokeColorDefaultBrush"),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };

                var stack = new StackPanel { Spacing = 4 };

                var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var icon = new Image { Width = 36, Height = 36, Stretch = Stretch.Uniform };
                try
                {
                    if (!string.IsNullOrEmpty(skill.IconRel))
                        icon.Source = new BitmapImage(new Uri("ms-appx:///Assets/" + skill.IconRel));
                }
                catch { }
                head.Children.Add(icon);
                head.Children.Add(new TextBlock
                {
                    Text = skill.Name.GetLocalized(),
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                });
                stack.Children.Add(head);

                var description = new RichTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
                int level = GetSkillLevel(ship.Id, skill);
                RenderSkillDescription(description, skill, level);
                stack.Children.Add(description);

                if (skill.Max > 1 && skill.Values.Count > 0)
                {
                    var levelRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    var slider = new Slider
                    {
                        Minimum = 1,
                        Maximum = skill.Max,
                        Value = level,
                        StepFrequency = 1,
                        TickFrequency = 1,
                        TickPlacement = TickPlacement.BottomRight,
                        Width = 220,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var levelText = new TextBlock
                    {
                        Text = $"Lv {level} / {skill.Max}",
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = ResBrushOf("TextFillColorSecondaryBrush")
                    };
                    slider.ValueChanged += (sender, args) =>
                    {
                        int value = (int)Math.Round(args.NewValue);
                        if (value < 1) value = 1;
                        if (Math.Abs(slider.Value - value) > 0.01) slider.Value = value;   // 吸附到刻度
                        levelText.Text = $"Lv {value} / {skill.Max}";
                        RenderSkillDescription(description, skill, value);
                        SaveSkillLevel(ship.Id, skill, value);
                    };
                    levelRow.Children.Add(slider);
                    levelRow.Children.Add(levelText);
                    stack.Children.Add(levelRow);
                }

                card.Child = stack;
                SkillsHost.Children.Add(card);
            }
        }

        /// <summary>把描述里的 $1/$2… 替换为当前等级数值（强调色）；无数值时保留原占位符。</summary>
        private void RenderSkillDescription(RichTextBlock target, Skill skill, int level)
        {
            target.Blocks.Clear();
            var paragraph = new Microsoft.UI.Xaml.Documents.Paragraph();
            string text = skill.Description.GetLocalized();
            var accent = AccentBrush;

            int last = 0;
            foreach (System.Text.RegularExpressions.Match match in
                     System.Text.RegularExpressions.Regex.Matches(text, @"\$(\d+)"))
            {
                if (match.Index > last)
                    paragraph.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
                    {
                        Text = text.Substring(last, match.Index - last)
                    });

                int placeholder = int.Parse(match.Groups[1].Value);
                string value = skill.ValueAt(placeholder, level);
                var run = new Microsoft.UI.Xaml.Documents.Run
                {
                    Text = string.IsNullOrEmpty(value) ? match.Value : value,
                    FontWeight = string.IsNullOrEmpty(value)
                        ? Microsoft.UI.Text.FontWeights.Normal
                        : Microsoft.UI.Text.FontWeights.SemiBold
                };
                if (!string.IsNullOrEmpty(value) && accent != null) run.Foreground = accent;
                paragraph.Inlines.Add(run);
                last = match.Index + match.Length;
            }
            if (last < text.Length)
                paragraph.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = text.Substring(last) });

            target.Blocks.Add(paragraph);
        }

        private static Brush? ResBrushOf(string key)
        {
            try
            {
                if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush)
                    return brush;
            }
            catch { }
            return null;
        }

        /// <summary>改造分步展示行（一个 step 一组，组内按步内序号竖排）。</summary>
        public sealed class RetrofitStepView
        {
            public int Step { get; set; }
            public string Title { get; set; } = "";
            public List<RetrofitNodeView> Nodes { get; set; } = new();
        }

        /// <summary>改造节点展示行。</summary>
        public sealed class RetrofitNodeView
        {
            public string Name { get; set; } = "";
            public string AttributeBonus { get; set; } = "";
            /// <summary>节点图标（未找到图标文件时为空，模板用 HasIcon 隐藏）。</summary>
            public string IconUri { get; set; } = "";
            public bool HasIcon => !string.IsNullOrEmpty(IconUri);
            /// <summary>材料清单（图标 + 名称×数量 + 品质底框）；无材料时为一条「—」。</summary>
            public List<ItemChipView> Items { get; set; } = new();
            public int RequiredCoins { get; set; }
            public int RequiredLevel { get; set; }
            public int RequiredStars { get; set; }
        }

        /// <summary>物品展示芯片：品质底框 + 图标 + 文本。</summary>
        public sealed class ItemChipView
        {
            public string Text { get; set; } = "";
            public string IconUri { get; set; } = "";
            /// <summary>品质底框 URI（空/无品质时不显示）。</summary>
            public string BgUri { get; set; } = "";
            public bool HasBg { get; set; }
        }

        /// <summary>装备一条武器属性（图标 + 名称 + 数值）。</summary>
        public sealed class EquipAttrView
        {
            public string Label { get; set; } = "";
            public string Value { get; set; } = "";
            public string IconUri { get; set; } = "";
        }

        /// <summary>科研任务/里程碑展示行。</summary>
        public sealed class TaskView
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public string Requirement { get; set; } = "";
        }

        /// <summary>仅含 Key 的展示行（开发奖励模板绑定 Key）。</summary>
        public sealed class KeyView
        {
            public string Key { get; set; } = "";
        }

        /// <summary>皮肤展示行。</summary>
        public sealed class SkinView
        {
            public string Name { get; set; } = "";
            public string ReleaseDate { get; set; } = "";
            public string Type { get; set; } = "";
            public bool IsOathEnabled { get; set; }
            /// <summary>皮肤头像 URI（未找到头像文件时为空）。</summary>
            public string AvatarUri { get; set; } = "";
            /// <summary>头像稀有度背景 URI。</summary>
            public string BgUri { get; set; } = "";
            public bool HasAvatar => !string.IsNullOrEmpty(AvatarUri);
        }

        /// <summary>台词展示行（场景分组内的单条台词）。</summary>
        public sealed class LineView
        {
            public string Name { get; set; } = "";
            public string Content { get; set; } = "";
            public string TriggerCondition { get; set; } = "";
        }

        /// <summary>台词分组：一个场景下的全部台词。</summary>
        public sealed class VoiceSceneView
        {
            public string SceneName { get; set; } = "";
            public List<LineView> Lines { get; set; } = new();
        }

        /// <summary>台词区皮肤下拉项（只列有台词的皮肤）。</summary>
        public sealed class LineSkinOption
        {
            public int SkinId { get; set; }
            public string Display { get; set; } = "";
        }

        /// <summary>礼物偏好展示行。</summary>
        public sealed class GiftView
        {
            public int GiftId { get; set; }
            /// <summary>礼物名（含 id），如「古董摆件（180006）」。</summary>
            public string DisplayName { get; set; } = "";
            /// <summary>礼物图标（缺图时为物品占位图）。</summary>
            public string IconUri { get; set; } = "";
            /// <summary>品质底框 URI。</summary>
            public string BgUri { get; set; } = "";
            public bool HasBg { get; set; }
            public GiftPreferenceType Preference { get; set; } = GiftPreferenceType.Dislike;
        }
    }
}
