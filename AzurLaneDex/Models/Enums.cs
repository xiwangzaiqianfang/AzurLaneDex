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

    // ==================== 数据驱动新增枚举（值来自 AzurLaneData，勿改动既有枚举值） ====================

    /// <summary>
    /// 舰船位掩码（ships.json flags，与数据源 SHIP_FLAGS 一致）。
    /// </summary>
    [System.Flags]
    public enum ShipFlags
    {
        None = 0,
        /// <summary>布里（如通用型布里）。</summary>
        Bulin = 1 << 0,
        /// <summary>可改造。</summary>
        Retro = 1 << 1,
        /// <summary>科研船（PR/DR）。</summary>
        Research = 1 << 2,
        /// <summary>有天运拟似。</summary>
        Fate = 1 << 3,
        /// <summary>META 船。</summary>
        Meta = 1 << 4
    }

    /// <summary>
    /// 技能种类（skills.json type）。
    /// </summary>
    public enum GameSkillKind
    {
        /// <summary>装备技能（不直接随舰展示）。</summary>
        Equip = 0,
        /// <summary>进攻（红）。</summary>
        Offensive = 1,
        /// <summary>防御（蓝）。</summary>
        Defensive = 2,
        /// <summary>支援（黄）。</summary>
        Support = 3
    }

    /// <summary>
    /// 皮肤种类（skins.json type；-1=默认皮，仅存在于台词侧）。
    /// </summary>
    public enum GameSkinKind
    {
        /// <summary>默认皮（ships_words 中以 id=-1 表达）。</summary>
        Default = -1,
        /// <summary>普通。</summary>
        Normal = 0,
        /// <summary>誓约。</summary>
        Oath = 1,
        /// <summary>改造。</summary>
        Retrofit = 2,
        /// <summary>原设。</summary>
        Original = 3,
        /// <summary>宣传。</summary>
        Promo = 4,
        /// <summary>情人节。</summary>
        Valentine = 5,
        /// <summary>形态切换。</summary>
        FormAlt = 6
    }

    /// <summary>
    /// 皮肤获取方式（skins.json obtain_type）。
    /// </summary>
    public enum SkinObtainType
    {
        /// <summary>不可获取。</summary>
        Unobtainable = -1,
        /// <summary>钻石购买。</summary>
        Gems = 0,
        /// <summary>核心数据兑换。</summary>
        CoreData = 1,
        /// <summary>活动赠送。</summary>
        Event = 2,
        /// <summary>巡航奖励。</summary>
        CruisePass = 3,
        /// <summary>任务奖励。</summary>
        Quest = 4,
        /// <summary>奖品商店。</summary>
        PrizeShop = 5,
        /// <summary>誓约赠送。</summary>
        Oath = 6,
        /// <summary>改造获得。</summary>
        Retrofit = 7,
        /// <summary>线下宣传。</summary>
        Promo = 8,
        /// <summary>皮肤切换（双形态）。</summary>
        SkinSwitch = 9,
        /// <summary>巡航商店。</summary>
        CruiseShop = 10
    }

    /// <summary>
    /// 常驻获取途径（ship_drops other[]，UnlockType 0~16）。
    /// </summary>
    public enum UnlockType
    {
        /// <summary>军需商店（Guild shop）。</summary>
        GuildShop = 0,
        /// <summary>勋章商店。</summary>
        MedalShop = 1,
        /// <summary>核心数据商店。</summary>
        CoreDataShop = 2,
        /// <summary>功勋商店（演习）。</summary>
        MeritShop = 3,
        /// <summary>建造征用（Requisition）。</summary>
        Requisition = 4,
        /// <summary>原型商店。</summary>
        PrototypeShop = 5,
        /// <summary>常驻 UR 保底。</summary>
        PermanentUrPity = 6,
        /// <summary>每周任务。</summary>
        WeeklyMissions = 7,
        /// <summary>每月签到。</summary>
        MonthlyLogin = 8,
        /// <summary>回归奖励。</summary>
        Returnee = 9,
        /// <summary>记忆晶片（档案）。</summary>
        Memento = 10,
        /// <summary>巡游。</summary>
        Cruise = 11,
        /// <summary>META 商店。</summary>
        MetaShop = 12,
        /// <summary>META 挑战。</summary>
        MetaShowdown = 13,
        /// <summary>机密档案。</summary>
        Dossier = 14,
        /// <summary>船坞（Shipyard）。</summary>
        Shipyard = 15,
        /// <summary>任务。</summary>
        Quest = 16
    }
}