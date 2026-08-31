using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.Resources;

namespace AzurLaneDex.ViewModels
{
    public class ShipViewModel : INotifyPropertyChanged
    {
        private readonly ShipStatic _static;
        private ShipState _state;
        private bool _isSelected;
        private readonly ResourceLoader _loader = ResourceLoader.GetForViewIndependentUse();

        public ShipViewModel(ShipStatic staticShip, ShipState state)
        {
            _static = staticShip;
            _state = state;
        }

        // ========== 基础标识 ==========
        public int Id => _static.Id;
        public int GameOrder => _static.GameOrder;
        public string RawName => _static.GlobalName;

        public string DisplayId
        {
            get
            {
                // 可根据 Category 自定义显示格式
                return $"NO.{Id:D3}";
            }
        }

        // ========== 多语言显示属性 ==========
        public string DisplayName
        {
            get
            {
                string baseName = _static.Name.GetLocalized();
                if (Retrofitted && Retrofit.CanRetrofit && !baseName.EndsWith(_loader.GetString("ShipName_RemodelSuffix")))
                {
                    return baseName + _loader.GetString("ShipName_RemodelSuffix");
                }
                return baseName;
            }
        }

        public string AltName => _static.AltName.GetLocalized();
        public string ClassName => _static.Class.GetLocalized();
        public string Alias => _static.Alias;
        public string CV => _static.CV.GetLocalized();

        public string ShipType => LocalizationHelper.GetEnumString("ShipType", (int)_static.Type);
        public string Faction => LocalizationHelper.GetEnumString("Faction", (int)_static.Faction);
        public string Rarity => LocalizationHelper.GetEnumString("Rarity", (int)_static.Rarity);
        public string Category => LocalizationHelper.GetEnumString("ShipCategory", (int)_static.Category);

        // ========== 枚举属性（用于筛选/排序） ==========
        public ShipCategory CategoryEnum => _static.Category;
        public ShipType ShipTypeEnum => _static.Type;
        public Faction FactionEnum => _static.Faction;
        public Rarity RarityEnum => _static.Rarity;

        // ========== 静态数据对象（用于详情页） ==========
        public ShipStats Stats => _static.Stats;
        public PerformanceRating Performance => _static.Performance;
        public FleetTech FleetTech => _static.FleetTech;
        public AcquisitionData Acquisition => _static.Acquisition;
        public SpecialGear SpecialGear => _static.SpecialGear;
        public List<EquipmentSlot> EquipmentSlots => _static.EquipmentSlots;
        public List<InitialEquipment> InitialEquipment => _static.InitialEquipment;
        public List<Skill> Skills => _static.Skills;
        public RetrofitData Retrofit => _static.Retrofit;
        public ResearchData Research => _static.Research;
        public List<Skin> Skins => _static.Skins;
        public List<VoiceLine> Lines => _static.Lines;
        public List<GiftPreference> GiftPreferences => _static.GiftPreferences;

        // ========== 强化/退役 ==========
        public bool CanBeEnhanceMaterial => _static.CanBeEnhanceMaterial;
        public int EnhanceValue => _static.EnhanceValue;
        public bool CanRetire => _static.CanRetire;
        public string RetirementReward => _static.RetirementReward;
        public EnhanceExp EnhanceExp => _static.EnhanceExp;
        public List<int> EnhanceItems => _static.EnhanceItems;
        public string ExtraEnhance => _static.ExtraEnhance;

        // ========== 其他静态属性 ==========
        public string ReleaseDate => _static.ReleaseDate;
        public bool IsPermanent => _static.IsPermanent;
        public bool CanSpecialGear => SpecialGear != null;
        public string Artist => _static.Artist;
        public string Remarks => _static.Remarks;
        public string Notes => _static.Notes;
        public string RelatedEvent => _static.RelatedEvent;
        public string ReferenceMarkdown => _static.ReferenceMarkdown;

        // ========== 活动相关（用于搜索） ==========
        public string DebutEvent => _static.RelatedEvent;

        // ========== 属性加成（用于筛选） ==========
        public TechBonusDetail ObtainBonus => _static.FleetTechBonus?.Obtain ?? new TechBonusDetail();
        public TechBonusDetail Level120Bonus => _static.FleetTechBonus?.Level120 ?? new TechBonusDetail();
        public AttributeType ObtainBonusAttrEnum
        {
            get
            {
                var obtain = _static.FleetTechBonus?.Obtain;
                if (obtain == null) return AttributeType.None;
                if (obtain.Hp != 0) return AttributeType.HP;
                if (obtain.Fp != 0) return AttributeType.FP;
                if (obtain.Trp != 0) return AttributeType.TRP;
                if (obtain.Avi != 0) return AttributeType.AVI;
                if (obtain.Aa != 0) return AttributeType.AA;
                if (obtain.Hit != 0) return AttributeType.ACC;
                if (obtain.Eva != 0) return AttributeType.EVA;
                if (obtain.Asw != 0) return AttributeType.ASW;
                return AttributeType.None;
            }
        }

        public AttributeType Level120BonusAttrEnum
        {
            get
            {
                var level120 = _static.FleetTechBonus?.Level120;
                if (level120 == null) return AttributeType.None;
                if (level120.Hp != 0) return AttributeType.HP;
                if (level120.Fp != 0) return AttributeType.FP;
                if (level120.Trp != 0) return AttributeType.TRP;
                if (level120.Avi != 0) return AttributeType.AVI;
                if (level120.Aa != 0) return AttributeType.AA;
                if (level120.Hit != 0) return AttributeType.ACC;
                if (level120.Eva != 0) return AttributeType.EVA;
                if (level120.Asw != 0) return AttributeType.ASW;
                return AttributeType.None;
            }
        }

        // 用于判断是否有加成（方便 UI）
        public bool HasObtainBonus => ObtainBonus.Hp != 0 || ObtainBonus.Fp != 0 || ObtainBonus.Trp != 0 || ObtainBonus.Avi != 0 || ObtainBonus.Aa != 0 || ObtainBonus.Hit != 0 || ObtainBonus.Eva != 0 || ObtainBonus.Asw != 0;
        public bool HasLevel120Bonus => Level120Bonus.Hp != 0 || Level120Bonus.Fp != 0 || Level120Bonus.Trp != 0 || Level120Bonus.Avi != 0 || Level120Bonus.Aa != 0 || Level120Bonus.Hit != 0 || Level120Bonus.Eva != 0 || Level120Bonus.Asw != 0;

        // 用于筛选的枚举（从 TargetTypes 判断是否存在）
        public List<ShipType> ObtainBonusTargetTypes => ObtainBonus.TargetTypes;
        public List<ShipType> Level120BonusTargetTypes => Level120Bonus.TargetTypes;

        // ========== 头像 ==========
        public string AvatarUri
        {
            get
            {
                string baseName = _static.GlobalName;
                string suffix = Retrofitted ? "_g" : "";
                return $"ms-appx:///Assets/Ship/{baseName}{suffix}.png";
            }
        }

        public string LocalAvatarPath
        {
            get
            {
                string factionId = ((int)_static.Faction).ToString();
                string avatarFileName = Retrofitted ? $"{_static.GlobalName}_g.png" : $"{_static.GlobalName}.png";
                string localPath = Path.Combine(App.DataRoot, "avatars", factionId, avatarFileName);
                if (File.Exists(localPath))
                    return localPath;
                return string.Empty;
            }
        }

        // ========== 有效稀有度（改造后提升） ==========
        public string EffectiveRarity
        {
            get
            {
                if (Retrofitted && Retrofit.CanRetrofit)
                {
                    int idx = (int)_static.Rarity;
                    if (idx >= 1 && idx <= 5)
                        return LocalizationHelper.GetEnumString("Rarity", idx + 1);
                }
                return Rarity;
            }
        }

        // ========== 动态状态（从 ShipState 读取） ==========
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
            set { _state.Oath = value; OnPropertyChanged(); }
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
                    OnPropertyChanged(nameof(EffectiveRarity));
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

        // ========== 计算属性 ==========
        public bool IsMaxBreakthrough => Owned && Breakthrough >= 3;

        public string BreakthroughDisplay
        {
            get
            {
                if (!Owned) return "0";
                return Breakthrough >= 3 ? "⭐" : Breakthrough.ToString();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        // ========== 方法 ==========
        public ShipState GetState() => _state;

        public ShipStatic GetStaticCopy() => _static;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}