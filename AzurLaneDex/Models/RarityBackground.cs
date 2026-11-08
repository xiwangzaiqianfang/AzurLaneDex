namespace AzurLaneDex.Models
{
    /// <summary>
    /// 图标稀有度背景（Assets/background/bg_*.png）：
    /// bg_1=T1/N、bg_2=T2/R、bg_3=T3/SR、bg_3_1=META SR、
    /// bg_4=T4/SSR（专属兵装同款）、bg_4_0=科研SSR（最高方案）、bg_4_1=META SSR、
    /// bg_5=T5/UR、bg_5_0=科研UR（决战方案）、bg_5_1=META UR。
    /// 船/装备/兵装图标统一经此生成背景 URI。
    /// </summary>
    public static class RarityBackground
    {
        private const string Base = "ms-appx:///Assets/background/";

        /// <summary>舰船头像背景：普通/联动按稀有度；科研/最高方案与决战方案；META 特殊。</summary>
        public static string ShipUri(ShipCategory category, Rarity rarity)
        {
            if (category == ShipCategory.Research)
                return Full(IsTopTier(rarity) ? "bg_5_0" : "bg_4_0");
            if (category == ShipCategory.META)
            {
                if (IsTopTier(rarity)) return Full("bg_5_1");
                return rarity switch
                {
                    Rarity.SSR => Full("bg_4_1"),
                    Rarity.SR => Full("bg_3_1"),
                    _ => Full(BaseRarity(rarity))
                };
            }
            return Full(BaseRarity(rarity));
        }

        /// <summary>
        /// 头像外框（background/{数字}.png）：
        /// N→1、R→2、SR→3、SSR→4、UR→5；科研 SSR→4_0、科研 UR→5_0；META SR/SSR→4_1；装备不套边框。
        /// </summary>
        public static string BorderUri(ShipCategory category, Rarity rarity)
        {
            if (category == ShipCategory.Research)
                return Full(IsTopTier(rarity) ? "5_0" : "4_0");
            if (category == ShipCategory.META)
                return Full("4_1");
            return Full(rarity switch
            {
                Rarity.T1 or Rarity.N => "1",
                Rarity.R => "2",
                Rarity.SR => "3",
                Rarity.SSR => "4",
                Rarity.UR or Rarity.Ultimate or Rarity.Decisive => "5",
                _ => ""
            });
        }

        /// <summary>
        /// 誓约边框（background/prop*.png）：默认 prop、SSR 用 prop4_0、META 用 prop4_1、UR 用 prop5_0。
        /// </summary>
        public static string OathFrameUri(ShipCategory category, Rarity rarity)
        {
            if (IsTopTier(rarity)) return Full("prop5_0");
            if (category == ShipCategory.META) return Full("prop4_1");
            if (rarity == Rarity.SSR) return Full("prop4_0");
            return Full("prop");
        }

        /// <summary>装备图标背景：按装备稀有度数字（T1..T5 = 1..5；6+ 按 UR）。</summary>
        public static string EquipUri(int rarityCode)
        {
            string name = rarityCode switch
            {
                1 => "bg_2_2", // ？？？
                2 => "bg_2_2", // 白
                3 => "bg_3_2", // 蓝
                4 => "bg_4_2", // 紫
                5 => "bg_5_2", // 金
                6 => "bg_6_2", // 彩
                _ => ""
            };
            return name.Length == 0 ? "" : Full(name);
        }

        /// <summary>专属兵装图标背景（固定 bg_4）。</summary>
        public static string AugmentUri() => Full("bg_5_2");

        /// <summary>普通稀有度背景名。</summary>
        private static string BaseRarity(Rarity rarity) => rarity switch
        {
            Rarity.T1 or Rarity.N => "bg_1",
            Rarity.R => "bg_2",
            Rarity.SR => "bg_3",
            Rarity.SSR => "bg_4",
            Rarity.UR or Rarity.Ultimate or Rarity.Decisive => "bg_5",
            _ => ""
        };

        /// <summary>顶层稀有度：UR / 决战方案（DR=Ultimate）/ 最高方案（PRY=Decisive）统一按最高档。</summary>
        private static bool IsTopTier(Rarity rarity) =>
            rarity is Rarity.UR or Rarity.Ultimate or Rarity.Decisive;

        private static string Full(string name) => string.IsNullOrEmpty(name) ? "" : Base + name + ".png";
    }
}
