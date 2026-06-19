using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using AzurLaneDex.Services;
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
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

        // 本地化显示属性
        public string DisplayName
        {
            get
            {
                string baseName = _static.Name.GetLocalized(_static.Name.GetValueOrDefault("zh-Hans"));
                if (Remodeled && CanRemodel && !baseName.EndsWith(_loader.GetString("ShipName_RemodelSuffix")))
                {
                    return baseName + _loader.GetString("ShipName_RemodelSuffix");
                }
                return baseName;
            }
        }
        public string DisplayAltName => _static.AltName.GetLocalized();
        public string Faction => LocalizationHelper.GetEnumString("Faction", _static.FactionId);
        public string ShipClass => LocalizationHelper.GetEnumString("ShipClass", _static.ShipClassId);
        public string Rarity => LocalizationHelper.GetEnumString("Rarity", _static.RarityId);
        public string ObtainBonusAttr => LocalizationHelper.GetEnumString("Attr", _static.ObtainBonusAttrId);
        public string Level120BonusAttr => LocalizationHelper.GetEnumString("Attr", _static.Level120BonusAttrId);

        public string DebutEvent => _static.DebutEvent.GetLocalized();
        public string Notes => _static.Notes.GetLocalized();

        public string ObtainAffectsDisplay => string.Join(", ", _static.ObtainAffectClassIds.Select(id => LocalizationHelper.GetEnumString("ShipClass", id)));
        public string Level120AffectsDisplay => string.Join(", ", _static.Level120AffectClassIds.Select(id => LocalizationHelper.GetEnumString("ShipClass", id)));

        // ID 属性（用于排序）
        public int RarityId => _static.RarityId;
        public int FactionId => _static.FactionId;
        public int ShipClassId => _static.ShipClassId;

        // 原始名称（用于头像文件名、布里判断）
        public string RawName => _static.Name.GetValueOrDefault("zh-Hans");
        public string RawAltName => _static.AltName.GetValueOrDefault("zh-Hans");

        // 其他静态属性
        public int Id => _static.Id;
        public int GameOrder => _static.GameOrder;
        public ShipCategory Category => _static.Category;
        public int CategoryOrder => _static.CategoryOrder;
        public List<AcquireEntry> AcquireEntries => _static.AcquireEntries;

        // 兼容旧数据的 Legacy 字段（LocalizedString 类型）
        public LocalizedString? AcquireMainLegacy => _static.AcquireMainLegacy;
        public LocalizedString? AcquireDetailLegacy => _static.AcquireDetailLegacy;
        public string AcquireMain => _static.AcquireMainLegacy?.GetValueOrDefault("zh-Hans") ?? "";
        public string AcquireDetail => _static.AcquireDetailLegacy?.GetValueOrDefault("zh-Hans") ?? "";
        public string BuildTime => _static.BuildTime;
        public List<string> DropLocations => _static.DropLocations;
        public string ShopExchange => _static.ShopExchange;
        public bool IsPermanent => _static.IsPermanent;
        public string ReleaseDate => _static.ReleaseDate;
        public bool CanRemodel => _static.CanRemodel;
        public string RemodelDate => _static.RemodelDate;
        public bool CanSpecialGear => _static.CanSpecialGear;
        public LocalizedString SpecialGearName => _static.SpecialGearName;
        public List<SpecialGearEntry> SpecialGearEntries => _static.SpecialGearEntries;
        public string SpecialGearDate => _static.SpecialGearDate;
        public string SpecialGearAcquireText => _static.SpecialGearAcquire?.GetLocalized() ?? "";        // public string SpecialGearAcquire => _static.SpecialGearAcquire;
        public int ObtainBonusValue => _static.ObtainBonusValue;
        public int Level120BonusValue => _static.Level120BonusValue;
        public int TechPointsObtain => _static.TechPointsObtain;
        public int TechPointsMax => _static.TechPointsMax;
        public int TechPoints120 => _static.TechPoints120;

        // 动态状态
        private static readonly string[] RarityOrder = { "普通", "稀有", "精锐", "超稀有", "海上传奇" };

        public bool Owned
        {
            get => _state.Owned;
            set { if (_state.Owned != value) { _state.Owned = value; OnPropertyChanged(); OnPropertyChanged(nameof(BreakthroughDisplay));  } }
        }

        public int Breakthrough
        {
            get => _state.Breakthrough;
            set { if (_state.Breakthrough != value) { _state.Breakthrough = value; OnPropertyChanged(); OnPropertyChanged(nameof(BreakthroughDisplay)); OnPropertyChanged(nameof(IsMaxBreakthrough)); } }
        }

        public bool Remodeled
        {
            get => _state.Remodeled;
            set { if (_state.Remodeled != value) { _state.Remodeled = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(EffectiveRarity)); } }
        }

        public bool Oath
        {
            get => _state.Oath;
            set { if (_state.Oath != value) { _state.Oath = value; OnPropertyChanged(); } }
        }

        public bool Level120
        {
            get => _state.Level120;
            set { if (_state.Level120 != value) { _state.Level120 = value; OnPropertyChanged(); } }
        }

        public bool SpecialGearObtained
        {
            get => _state.SpecialGearObtained;
            set { if (_state.SpecialGearObtained != value) { _state.SpecialGearObtained = value; OnPropertyChanged(); } }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        // 辅助属性
        public string AcquireMainLegacyText => _static.AcquireMainLegacy?.GetValueOrDefault("zh-Hans") ?? "";
        public string AcquireDetailLegacyText => _static.AcquireDetailLegacy?.GetValueOrDefault("zh-Hans") ?? "";
        public bool IsMaxBreakthrough => Owned && Breakthrough >= 3;
        // public string BreakthroughDisplay => Breakthrough == 3 ? _loader.GetString("MaxBreak") : Breakthrough.ToString();
        public string BreakthroughDisplay
        {
            get
            {
                // 未获得时显示 0
                if (!Owned) return "0";
                // 满破显示实心星，否则显示数字
                return Breakthrough >= 3 ? "⭐" : Breakthrough.ToString();
            }
        }

        public string DisplayId
        {
            get
            {
                switch (Category)
                {
                    case ShipCategory.META: return $"NO.META{Id - ShipIdRanges.MetaStart + 1:D3}";
                    case ShipCategory.Collab: return $"NO.Collab{Id - ShipIdRanges.CollabStart + 1:D3}";
                    case ShipCategory.Research: return $"NO.Plan{Id - ShipIdRanges.ResearchStart + 1:D3}";
                    default: return $"NO.{Id:D3}";
                }
            }
        }

        public string EffectiveRarity
        {
            get
            {
                if (Remodeled && CanRemodel)
                {
                    int idx = _static.RarityId - 1;
                    if (idx >= 0 && idx < RarityOrder.Length - 1)
                        return LocalizationHelper.GetEnumString("Rarity", idx + 2);
                }
                return Rarity;
            }
        }

        // 获取状态（用于保存）
        public ShipState GetState() => _state;

        // 深拷贝（用于编辑）
        public ShipStatic GetStaticCopy()
        {
            System.Diagnostics.Debug.WriteLine($"GetStaticCopy: _static.AcquireEntries count = {_static.AcquireEntries?.Count ?? 0}");
            return new ShipStatic
            {
                Id = _static.Id,
                Name = new LocalizedString(_static.Name),
                AltName = new LocalizedString(_static.AltName),
                FactionId = _static.FactionId,
                ShipClassId = _static.ShipClassId,
                RarityId = _static.RarityId,
                GameOrder = _static.GameOrder,
                Category = _static.Category,
                CategoryOrder = _static.CategoryOrder,
                AcquireEntries = _static.AcquireEntries?.Select(e => new AcquireEntry
                {
                    Tag = e.Tag,
                    Parameters = new List<string>(e.Parameters),
                    CustomText = new LocalizedString(e.CustomText) // 假设 LocalizedString 支持拷贝构造
                }).ToList() ?? new List<AcquireEntry>(),
                AcquireMainLegacy = _static.AcquireMainLegacy != null ? new LocalizedString(_static.AcquireMainLegacy) : null,
                AcquireDetailLegacy = _static.AcquireDetailLegacy != null ? new LocalizedString(_static.AcquireDetailLegacy) : null,
                BuildTime = _static.BuildTime,
                DropLocations = new List<string>(_static.DropLocations),
                ShopExchange = _static.ShopExchange,
                IsPermanent = _static.IsPermanent,
                DebutEvent = new LocalizedString(_static.DebutEvent),
                ReleaseDate = _static.ReleaseDate,
                Notes = new LocalizedString(_static.Notes),
                CanRemodel = _static.CanRemodel,
                RemodelDate = _static.RemodelDate,
                CanSpecialGear = _static.CanSpecialGear,
                SpecialGearName = _static.SpecialGearName,
                SpecialGearDate = _static.SpecialGearDate,
                SpecialGearAcquire = _static.SpecialGearAcquire,
                ObtainBonusAttrId = _static.ObtainBonusAttrId,
                ObtainBonusValue = _static.ObtainBonusValue,
                ObtainAffectClassIds = new List<int>(_static.ObtainAffectClassIds),
                Level120BonusAttrId = _static.Level120BonusAttrId,
                Level120BonusValue = _static.Level120BonusValue,
                Level120AffectClassIds = new List<int>(_static.Level120AffectClassIds),
                TechPointsObtain = _static.TechPointsObtain,
                TechPointsMax = _static.TechPointsMax,
                TechPoints120 = _static.TechPoints120
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string LocalAvatarPath
        {
            get
            {
                string factionId = this.Faction ?? "other";
                string avatarFileName = this.Remodeled && this.CanRemodel ? $"{this.RawName}改.jpg" : $"{this.RawName}.jpg";
                string localPath = Path.Combine(App.DataRoot, "avatars", factionId, avatarFileName);
                if (File.Exists(localPath))
                    return localPath;
                // 后备：返回空字符串或默认路径（如 ms-appx 路径）
                return string.Empty;
            }
        }
    }
}