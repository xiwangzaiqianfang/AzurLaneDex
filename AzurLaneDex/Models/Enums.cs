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
        Tempesta,
        Other,
        CrystalLeague,
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
        Meta_Flame = 200,
        Meta_Core,
        Meta_Reason,
        Meta_Light,
        Meta_Fire
    }

    public enum ShipClass
    {
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
        Sail
    }

    public enum Rarity
    {
        N = 1,
        R,
        SR,
        SSR,
        UR,
        Decisive,
        Ultimate
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
        EVA,
        ASW
    }

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