namespace AzurLaneDex.Models
{
    public enum ShipCategory
    {
        Normal = 0,
        Collab = 1,
        Research = 2,
        META = 3
    }

    public enum Faction
    {
        Universal = 0,
        EagleUnion = 1,
        RoyalNavy,
        SakuraEmpire,
        IronBlood,
        DragonEmpery,
        Sardegna,
        NorthernUnion,
        FreeFrench,
        Vichya,
        Tulip,
        CrystalLeague,
        French = 97,
        Tempesta = 98,
        Other = 99,
        Collab_Nep = 100,
        Collab_Bilibili,
        Collab_Utawarerumono,
        Collab_KizunaAI,
        Collab_Hololive,
        Collab_DoAXVV,
        Collab_Idolmaster,
        Collab_SSSS,
        Collab_Ryza,
        Collab_Senran,
        Collab_Toloveru,
        Collab_BRS,
        Collab_Danmachi,
        Collab_Yumia,
        Collab_DAL,
        Collab_NieR,
        Meta_Flame = 200,
        Meta_Core,
        Meta_Reason,
        Meta_Light,
        Meta_Fire,
        META = 299,
        Council = 300,
        X = 400,
        Siren = 500,
        Unknown = 999
    }

    public enum ShipType
    {
        UNKNOWN = 0,
        DD = 1,
        CL,
        CA,
        CB,
        BM,
        BC,
        BB,
        BBV,
        CV,
        CVL,
        AR,
        SS,
        SSV,
        AE,
        Sail,
        CAV,
        CT,
        TRP,
        CARGO,
        BOMB,
        DDG,
        IX,
        SP
    }

    public enum Rarity
    {
        T1 = 0,
        N = 1,
        R,
        SR,
        SSR,
        UR,
        Decisive,
        Ultimate,
        Unknown
    }

    public enum AttributeType
    {
        None = 0,
        HP = 1,
        FP,
        TRP,
        AA,
        AVI,
        ACC,
        RLD,
        HIT,
        EVA,
        SPD,
        LUCK,
        ASW,
        OIL
    }

    public enum ArmorType
    {
        Light = 0,
        Medium,
        Heavy
    }

    public enum ItemType
    {
        Unknown = 0,
        GeneralPartT1 = 101,
        GeneralPartT2,
        GeneralPartT3,
        GeneralPartT4,
        MainGunPartT1 = 111,
        MainGunPartT2,
        MainGunPartT3,
        MainGunPartT4,
        TorpedoPartT1 = 121,
        TorpedoPartT2,
        TorpedoPartT3,
        TorpedoPartT4,
        AAGunPartT1 = 131,
        AAGunPartT2,
        AAGunPartT3,
        AAGunPartT4,
        AircraftPartT1 = 141,
        AircraftPartT2,
        AircraftPartT3,
        AircraftPartT4,
        DDRetrofitBlueprintT1 = 201,
        DDRetrofitBlueprintT2,
        DDRetrofitBlueprintT3,
        CLRetrofitBlueprintT1 = 211,
        CLRetrofitBlueprintT2,
        CLRetrofitBlueprintT3,
        BBRetrofitBlueprintT1 = 221,
        BBRetrofitBlueprintT2,
        BBRetrofitBlueprintT3,
        CVRetrofitBlueprintT1 = 231,
        CVRetrofitBlueprintT2,
        CVRetrofitBlueprintT3,
    }

    public enum SkillType
    { 
        Attack = 1,
        Defense,
        Support,
        special
    }

    public enum PerformanceGrade
    {
        D,
        C,
        B,
        A,
        S
    }
    public enum AcquisitionMethodType
    {
        Construction,
        Drop,
        Exchange,
        Research,
        Other
    }
    public enum ConstructionPool
    { 
        Light, 
        Heavy, 
        Special, 
        Limited, 
        Wish, 
        None 
    }
    public enum ExchangeShop
    {
        Guild,      // 军需商店
        Merit,      // 演习商店
        Event,      // 活动商店
        Medal,      // 勋章兑换
        Core,       // 核心数据
        Other,
        Custom = 999
    }
    public enum SkinType { Static, Dynamic, MultiDynamic, Live2D, Live2DPlus }
    public enum GiftPreferenceType { Like, Normal, Dislike }

    public enum UpdateChannel
    {
        Stable = 0,   // 正式版
        Preview = 1,   // 预览版
        Dev = 2
    }

    public enum UpdateSource
    {
        GitHub,
        Gitee
    }
}