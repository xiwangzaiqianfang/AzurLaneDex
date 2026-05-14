using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using static AzurLaneDex.Models.ShipStatic;

namespace AzurLaneDex.Views
{
    public sealed partial class FilterPanel : UserControl
    {
        public FilterPanel()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// 获取当前用户选择的筛选条件
        /// </summary>
        public void SetCategory(ShipCategory category)
        {
            switch (category)
            {
                case ShipCategory.Normal:
                case ShipCategory.Research:
                    NormalFactionPanel.Visibility = Visibility.Visible;
                    CollabFactionPanel.Visibility = Visibility.Collapsed;
                    MetaFactionPanel.Visibility = Visibility.Collapsed;
                    break;
                case ShipCategory.Collab:
                    NormalFactionPanel.Visibility = Visibility.Collapsed;
                    CollabFactionPanel.Visibility = Visibility.Visible;
                    MetaFactionPanel.Visibility = Visibility.Collapsed;
                    break;
                case ShipCategory.META:
                    NormalFactionPanel.Visibility = Visibility.Collapsed;
                    CollabFactionPanel.Visibility = Visibility.Collapsed;
                    MetaFactionPanel.Visibility = Visibility.Visible;
                    break;
            }
        }
        public FilterCriteria GetFilterCriteria()
        {
            var criteria = new FilterCriteria();

            // 舰种
            var shipClasses = new List<string>();
            if (ClassDD.IsChecked == true) shipClasses.Add("驱逐");
            if (ClassCL.IsChecked == true) shipClasses.Add("轻巡");
            if (ClassCA.IsChecked == true) shipClasses.Add("重巡");
            if (ClassCB.IsChecked == true) shipClasses.Add("超巡");
            if (ClassBC.IsChecked == true) shipClasses.Add("战巡");
            if (ClassBB.IsChecked == true) shipClasses.Add("战列");
            if (ClassBBV.IsChecked == true) shipClasses.Add("航战");
            if (ClassCV.IsChecked == true) shipClasses.Add("航母");
            if (ClassCVL.IsChecked == true) shipClasses.Add("轻航");
            if (ClassAR.IsChecked == true) shipClasses.Add("维修");
            if (ClassSS.IsChecked == true) shipClasses.Add("潜艇");
            if (ClassSSV.IsChecked == true) shipClasses.Add("潜母");
            if (ClassAE.IsChecked == true) shipClasses.Add("运输");
            if (ClassSail.IsChecked == true) shipClasses.Add("风帆");
            if (ClassOther.IsChecked == true) shipClasses.Add("其他");
            criteria.ShipClasses = shipClasses;

            // 阵营
            var factions = new List<string>();
            if (NormalFactionPanel.Visibility == Visibility.Visible)
            {
                FactionEU.IsChecked = criteria.Factions.Contains("白鹰");
                FactionRN.IsChecked = criteria.Factions.Contains("皇家");
                FactionIJN.IsChecked = criteria.Factions.Contains("重樱");
                FactionKMS.IsChecked = criteria.Factions.Contains("铁血");
                FactionDragon.IsChecked = criteria.Factions.Contains("东煌");
                FactionSN.IsChecked = criteria.Factions.Contains("北方联合");
                FactionFFNF.IsChecked = criteria.Factions.Contains("自由鸢尾");
                FactionMNF.IsChecked = criteria.Factions.Contains("维希教廷");
                FactionSardegna.IsChecked = criteria.Factions.Contains("撒丁帝国");
                FactionMETA.IsChecked = criteria.Factions.Contains("META");
                FactionTempesta.IsChecked = criteria.Factions.Contains("飓风");
                FactionOther.IsChecked = criteria.Factions.Contains("其他");
            }
            else if (CollabFactionPanel.Visibility == Visibility.Visible)
            {
                FactionCollab_Nep.IsChecked = criteria.Factions.Contains("超次元游戏海王星");
                FactionCollab_Bilibili.IsChecked = criteria.Factions.Contains("哔哩哔哩");
                FactionCollab_Utawarerumono.IsChecked = criteria.Factions.Contains("传颂之物");
                FactionCollab_KizunaAI.IsChecked = criteria.Factions.Contains("绊爱");
                FactionCollab_Hololive.IsChecked = criteria.Factions.Contains("Hololive");
                FactionCollab_DoAXVV.IsChecked = criteria.Factions.Contains("死或生沙滩排球");
                FactionCollab_Idolmaster.IsChecked = criteria.Factions.Contains("偶像大师");
                FactionCollab_SSSS.IsChecked = criteria.Factions.Contains("SSSS");
                FactionCollab_Ryza.IsChecked = criteria.Factions.Contains("莱莎的炼金工房");
                FactionCollab_Senran.IsChecked = criteria.Factions.Contains("闪乱神乐");
                FactionCollab_Toloveru.IsChecked = criteria.Factions.Contains("出包王女");
                FactionCollab_BRS.IsChecked = criteria.Factions.Contains("黑岩射手");
                FactionCollab_Danmachi.IsChecked = criteria.Factions.Contains("地城邂逅");
                FactionCollab_Yumia.IsChecked = criteria.Factions.Contains("优米雅的炼金工房");
                FactionCollab_DAL.IsChecked = criteria.Factions.Contains("约会大作战V");

            }
            else if (MetaFactionPanel.Visibility == Visibility.Visible)
            {
                FactionMeta_Flame.IsChecked = criteria.Factions.Contains("破敌之炬");
                FactionMeta_Core.IsChecked = criteria.Factions.Contains("湮烬之核");
                FactionMeta_Reason.IsChecked = criteria.Factions.Contains("构造之理");
                FactionMeta_Light.IsChecked = criteria.Factions.Contains("逐光之焰");
                FactionMeta_Fire.IsChecked = criteria.Factions.Contains("摇曳之火");
            }
            criteria.Factions = factions;

            // 稀有度
            var rarities = new List<string>();
            if (RarityNormal.IsChecked == true) rarities.Add("普通");
            if (RarityRare.IsChecked == true) rarities.Add("稀有");
            if (RarityElite.IsChecked == true) rarities.Add("精锐");
            if (RaritySuperRare.IsChecked == true) rarities.Add("超稀有");
            if (RarityLegendary.IsChecked == true) rarities.Add("海上传奇");
            if (RarityDecisive.IsChecked == true) rarities.Add("最高方案");
            if (RarityUltimate.IsChecked == true) rarities.Add("决战方案");
            criteria.Rarities = rarities;

            // 附加状态
            criteria.CanRemodel = ExtraCanRemodel.IsChecked == true;
            criteria.Remodeled = ExtraRemodeled.IsChecked == true;
            criteria.MaxBreakthrough = ExtraMaxBreak.IsChecked == true;
            criteria.NotMaxBreakthrough = ExtraNotMaxBreak.IsChecked == true;
            criteria.Level120 = ExtraLevel120.IsChecked == true;
            criteria.NotLevel120 = ExtraNotLevel120.IsChecked == true;
            criteria.Oath = ExtraOath.IsChecked == true;
            criteria.NotOath = ExtraNotOath.IsChecked == true;
            criteria.CanSpecialGear = ExtraCanSpecial.IsChecked == true;
            criteria.SpecialGearObtained = ExtraSpecialObtained.IsChecked == true;

            // 属性加成
            var attrs = new List<string>();
            if (AttrFirepower.IsChecked == true) attrs.Add("炮击");
            if (AttrAviation.IsChecked == true) attrs.Add("航空");
            if (AttrMobility.IsChecked == true) attrs.Add("机动");
            if (AttrAA.IsChecked == true) attrs.Add("防空");
            if (AttrTorpedo.IsChecked == true) attrs.Add("雷击");
            if (AttrReload.IsChecked == true) attrs.Add("装填");
            if (AttrDurability.IsChecked == true) attrs.Add("耐久");
            if (AttrAntiSub.IsChecked == true) attrs.Add("反潜");
            criteria.AttributeBonuses = attrs;

            return criteria;
        }

        public void SetCriteria(FilterCriteria criteria)
        {
            if (criteria == null) return;

            // 舰种
            ClassDD.IsChecked = criteria.ShipClasses.Contains("驱逐");
            ClassCL.IsChecked = criteria.ShipClasses.Contains("轻巡");
            ClassCA.IsChecked = criteria.ShipClasses.Contains("重巡");
            ClassCB.IsChecked = criteria.ShipClasses.Contains("超巡");
            ClassBC.IsChecked = criteria.ShipClasses.Contains("战巡");
            ClassBB.IsChecked = criteria.ShipClasses.Contains("战列");
            ClassBBV.IsChecked = criteria.ShipClasses.Contains("航战");
            ClassCV.IsChecked = criteria.ShipClasses.Contains("航母");
            ClassCVL.IsChecked = criteria.ShipClasses.Contains("轻航");
            ClassAR.IsChecked = criteria.ShipClasses.Contains("维修");
            ClassSS.IsChecked = criteria.ShipClasses.Contains("潜艇");
            ClassSSV.IsChecked = criteria.ShipClasses.Contains("潜母");
            ClassAE.IsChecked = criteria.ShipClasses.Contains("运输");
            ClassSail.IsChecked = criteria.ShipClasses.Contains("风帆");
            ClassOther.IsChecked = criteria.ShipClasses.Contains("其他");

            // 阵营
            if (NormalFactionPanel.Visibility == Visibility.Visible)
            {
                FactionEU.IsChecked = criteria.Factions.Contains("白鹰");
                FactionRN.IsChecked = criteria.Factions.Contains("皇家");
                FactionIJN.IsChecked = criteria.Factions.Contains("重樱");
                FactionKMS.IsChecked = criteria.Factions.Contains("铁血");
                FactionDragon.IsChecked = criteria.Factions.Contains("东煌");
                FactionSN.IsChecked = criteria.Factions.Contains("北方联合");
                FactionFFNF.IsChecked = criteria.Factions.Contains("自由鸢尾");
                FactionMNF.IsChecked = criteria.Factions.Contains("维希教廷");
                FactionSardegna.IsChecked = criteria.Factions.Contains("撒丁帝国");
                FactionMETA.IsChecked = criteria.Factions.Contains("META");
                FactionTempesta.IsChecked = criteria.Factions.Contains("飓风");
                FactionOther.IsChecked = criteria.Factions.Contains("其他");
            }
            else if (CollabFactionPanel.Visibility == Visibility.Visible)
            {
                FactionCollab_Nep.IsChecked = criteria.Factions.Contains("超次元游戏海王星");
                FactionCollab_Bilibili.IsChecked = criteria.Factions.Contains("哔哩哔哩");
                FactionCollab_Utawarerumono.IsChecked = criteria.Factions.Contains("传颂之物");
                FactionCollab_KizunaAI.IsChecked = criteria.Factions.Contains("绊爱");
                FactionCollab_Hololive.IsChecked = criteria.Factions.Contains("Hololive");
                FactionCollab_DoAXVV.IsChecked = criteria.Factions.Contains("死或生沙滩排球");
                FactionCollab_Idolmaster.IsChecked = criteria.Factions.Contains("偶像大师");
                FactionCollab_SSSS.IsChecked = criteria.Factions.Contains("SSSS");
                FactionCollab_Ryza.IsChecked = criteria.Factions.Contains("莱莎的炼金工房");
                FactionCollab_Senran.IsChecked = criteria.Factions.Contains("闪乱神乐");
                FactionCollab_Toloveru.IsChecked = criteria.Factions.Contains("出包王女");
                FactionCollab_BRS.IsChecked = criteria.Factions.Contains("黑岩射手");
                FactionCollab_Danmachi.IsChecked = criteria.Factions.Contains("地城邂逅");
                FactionCollab_Yumia.IsChecked = criteria.Factions.Contains("优米雅的炼金工房");
                FactionCollab_DAL.IsChecked = criteria.Factions.Contains("约会大作战V");

            }
            else if (MetaFactionPanel.Visibility == Visibility.Visible)
            {
                FactionMeta_Flame.IsChecked = criteria.Factions.Contains("破敌之炬");
                FactionMeta_Core.IsChecked = criteria.Factions.Contains("湮烬之核");
                FactionMeta_Reason.IsChecked = criteria.Factions.Contains("构造之理");
                FactionMeta_Light.IsChecked = criteria.Factions.Contains("逐光之焰");
                FactionMeta_Fire.IsChecked = criteria.Factions.Contains("摇曳之火");
            }

            // 稀有度
            RarityNormal.IsChecked = criteria.Rarities.Contains("普通");
            RarityRare.IsChecked = criteria.Rarities.Contains("稀有");
            RarityElite.IsChecked = criteria.Rarities.Contains("精锐");
            RaritySuperRare.IsChecked = criteria.Rarities.Contains("超稀有");
            RarityLegendary.IsChecked = criteria.Rarities.Contains("海上传奇");
            RarityDecisive.IsChecked = criteria.Rarities.Contains("最高方案");
            RarityUltimate.IsChecked = criteria.Rarities.Contains("决战方案");

            // 附加状态
            ExtraCanRemodel.IsChecked = criteria.CanRemodel;
            ExtraRemodeled.IsChecked = criteria.Remodeled;
            ExtraMaxBreak.IsChecked = criteria.MaxBreakthrough;
            ExtraNotMaxBreak.IsChecked = criteria.NotMaxBreakthrough;
            ExtraLevel120.IsChecked = criteria.Level120;
            ExtraNotLevel120.IsChecked = criteria.NotLevel120;
            ExtraOath.IsChecked = criteria.Oath;
            ExtraNotOath.IsChecked = criteria.NotOath;
            ExtraCanSpecial.IsChecked = criteria.CanSpecialGear;
            ExtraSpecialObtained.IsChecked = criteria.SpecialGearObtained;

            // 属性加成
            AttrFirepower.IsChecked = criteria.AttributeBonuses.Contains("炮击");
            AttrAviation.IsChecked = criteria.AttributeBonuses.Contains("航空");
            AttrMobility.IsChecked = criteria.AttributeBonuses.Contains("机动");
            AttrAA.IsChecked = criteria.AttributeBonuses.Contains("防空");
            AttrTorpedo.IsChecked = criteria.AttributeBonuses.Contains("雷击");
            AttrReload.IsChecked = criteria.AttributeBonuses.Contains("装填");
            AttrDurability.IsChecked = criteria.AttributeBonuses.Contains("耐久");
            AttrAntiSub.IsChecked = criteria.AttributeBonuses.Contains("反潜");
        }
    }

    public class FilterCriteria
    {
        public List<string> ShipClasses { get; set; } = new();
        public List<string> Factions { get; set; } = new();
        public List<string> Rarities { get; set; } = new();
        public bool CanRemodel { get; set; }
        public bool Remodeled { get; set; }
        public bool MaxBreakthrough { get; set; }
        public bool NotMaxBreakthrough { get; set; }
        public bool Level120 { get; set; }
        public bool NotLevel120 { get; set; }
        public bool Oath { get; set; }
        public bool NotOath { get; set; }
        public bool CanSpecialGear { get; set; }
        public bool SpecialGearObtained { get; set; }
        public List<string> AttributeBonuses { get; set; } = new();
    }
}