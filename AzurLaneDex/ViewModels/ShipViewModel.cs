using AzurLaneDex.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static AzurLaneDex.Models.ShipStatic;

namespace AzurLaneDex.ViewModels;

public class ShipViewModel : INotifyPropertyChanged
{
    private readonly ShipStatic _static;
    private ShipState _state;
    private bool _isSelected;

    public ShipViewModel(ShipStatic staticShip, ShipState state)
    {
        _static = staticShip;
        _state = state;
    }

    // 静态属性（只读）
    public int Id => _static.Id;
    public string Name => _static.Name;
    public string AltName => _static.AltName;
    public string Faction => _static.Faction;
    public string ShipClass => _static.ShipClass;
    public string Rarity => _static.Rarity;
    public int GameOrder => _static.GameOrder;
    public ShipCategory Category => _static.Category;
    public int CategoryOrder => _static.CategoryOrder;
    public string AcquireMain => _static.AcquireMain;
    public string AcquireDetail => _static.AcquireDetail;
    public string BuildTime => _static.BuildTime;
    public List<string> DropLocations => _static.DropLocations;
    public string ShopExchange => _static.ShopExchange;
    public bool IsPermanent => _static.IsPermanent;
    public string DebutEvent => _static.DebutEvent;
    public string ReleaseDate => _static.ReleaseDate;
    public string Notes => _static.Notes;
    public bool CanRemodel => _static.CanRemodel;
    public string RemodelDate => _static.RemodelDate;
    public bool CanSpecialGear => _static.CanSpecialGear;
    public string SpecialGearName => _static.SpecialGearName;
    public string SpecialGearDate => _static.SpecialGearDate;
    public string SpecialGearAcquire => _static.SpecialGearAcquire;
    public string ImagePath => _static.ImagePath;

    // 属性加成信息
    public string ObtainBonusAttr => _static.ObtainBonusAttr;
    public int ObtainBonusValue => _static.ObtainBonusValue;
    public List<string> ObtainAffects => _static.ObtainAffects;

    public string Level120BonusAttr => _static.Level120BonusAttr;
    public int Level120BonusValue => _static.Level120BonusValue;
    public List<string> Level120Affects => _static.Level120Affects;

    public int TechPointsObtain => _static.TechPointsObtain;
    public int TechPointsMax => _static.TechPointsMax;
    public int TechPoints120 => _static.TechPoints120;

    // 动态属性（可写，触发 PropertyChanged）
    private static readonly string[] RarityOrder = { "普通", "稀有", "精锐", "超稀有", "海上传奇" };
    public bool Owned
    {
        get => _state.Owned;
        set
        {
            if (_state.Owned != value)
            {
                _state.Owned = value;
                OnPropertyChanged();
                // 若需要其他派生属性可在此添加
            }
        }
    }

    public int Breakthrough
    {
        get => _state.Breakthrough;
        set
        {
            if (_state.Breakthrough != value)
            {
                _state.Breakthrough = value;
                OnPropertyChanged();
                // 通知依赖属性
                OnPropertyChanged(nameof(BreakthroughDisplay));
                OnPropertyChanged(nameof(IsMaxBreakthrough));
            }
        }
    }

    public bool Remodeled
    {
        get => _state.Remodeled;
        set
        {
            if (_state.Remodeled != value)
            {
                _state.Remodeled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(EffectiveRarity));  // 新增
            }
        }
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

    // 多选用
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    // 辅助属性
    public bool IsMaxBreakthrough => Breakthrough == 3;
    public string BreakthroughDisplay => Breakthrough == 3 ? "满破" : Breakthrough.ToString();
    public string DisplayId
    {
        get
        {
            switch (Category)
            {
                case ShipCategory.META:
                    int metaSeq = Id - ShipIdRanges.MetaStart + 1;
                    return $"NO.META{metaSeq:D3}";
                case ShipCategory.Collab:
                    int collabSeq = Id - ShipIdRanges.CollabStart + 1;
                    return $"NO.Collab{collabSeq:D3}";
                case ShipCategory.Research:
                    int researchSeq = Id - ShipIdRanges.ResearchStart + 1;
                    return $"NO.Plan{researchSeq:D3}";
                default:
                    return $"NO.{Id:D3}";
            }
        }
    }
    public string DisplayName
    {
        get
        {
            string baseName = string.IsNullOrEmpty(AltName) ? Name : $"{Name}（{AltName}）";
            if (Remodeled && CanRemodel)
            {
                if (string.IsNullOrEmpty(AltName))
                {
                    baseName = Name + "改";
                }
                else
                {
                    baseName = $"{Name}改 ({AltName}改)";
                }
            }
            return baseName;
        }
    }

    public string EffectiveRarity
    {
        get
        {
            if (Remodeled && CanRemodel)
            {
                var idx = Array.IndexOf(RarityOrder, Rarity);
                if (idx >= 0 && idx < RarityOrder.Length - 1)
                    return RarityOrder[idx + 1];
            }
            return Rarity;
        }
    }

    // 获取当前状态对象（用于保存）
    public ShipState GetState() => _state;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    public ShipStatic GetStaticCopy()
    {
        return new ShipStatic
        {
            Id = _static.Id,
            Name = _static.Name,
            AltName = _static.AltName,
            Faction = _static.Faction,
            ShipClass = _static.ShipClass,
            Rarity = _static.Rarity,
            GameOrder = _static.GameOrder,
            Category = _static.Category,
            CategoryOrder = _static.CategoryOrder,
            AcquireMain = _static.AcquireMain,
            AcquireDetail = _static.AcquireDetail,
            BuildTime = _static.BuildTime,
            DropLocations = new List<string>(_static.DropLocations),
            ShopExchange = _static.ShopExchange,
            IsPermanent = _static.IsPermanent,
            DebutEvent = _static.DebutEvent,
            ReleaseDate = _static.ReleaseDate,
            Notes = _static.Notes,
            CanRemodel = _static.CanRemodel,
            RemodelDate = _static.RemodelDate,
            CanSpecialGear = _static.CanSpecialGear,
            SpecialGearName = _static.SpecialGearName,
            SpecialGearDate = _static.SpecialGearDate,
            SpecialGearAcquire = _static.SpecialGearAcquire,
            ImagePath = _static.ImagePath,
            ObtainBonusAttr = _static.ObtainBonusAttr,
            ObtainBonusValue = _static.ObtainBonusValue,
            ObtainAffects = new List<string>(_static.ObtainAffects),
            Level120BonusAttr = _static.Level120BonusAttr,
            Level120BonusValue = _static.Level120BonusValue,
            Level120Affects = new List<string>(_static.Level120Affects),
            TechPointsObtain = _static.TechPointsObtain,
            TechPointsMax = _static.TechPointsMax,
            TechPoints120 = _static.TechPoints120,
        };
    }
}