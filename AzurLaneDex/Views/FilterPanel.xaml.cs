using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using AzurLaneDex.Models;

namespace AzurLaneDex.Views
{
    public sealed partial class FilterPanel : UserControl
    {
        public FilterPanel()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// 根据舰船类别切换阵营面板
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

        /// <summary>
        /// 获取用户当前选择的筛选条件（枚举类型）
        /// </summary>
        public FilterCriteria GetFilterCriteria()
        {
            var criteria = new FilterCriteria();

            // 舰种（ShipType）
            var shipTypes = new List<ShipType>();
            if (ShipTypeDD.IsChecked == true) shipTypes.Add(ShipType.DD);
            if (ShipTypeCL.IsChecked == true) shipTypes.Add(ShipType.CL);
            if (ShipTypeCA.IsChecked == true) shipTypes.Add(ShipType.CA);
            if (ShipTypeCB.IsChecked == true) shipTypes.Add(ShipType.CB);
            if (ShipTypeBM.IsChecked == true) shipTypes.Add(ShipType.BM);
            if (ShipTypeBC.IsChecked == true) shipTypes.Add(ShipType.BC);
            if (ShipTypeBB.IsChecked == true) shipTypes.Add(ShipType.BB);
            if (ShipTypeBBV.IsChecked == true) shipTypes.Add(ShipType.BBV);
            if (ShipTypeCV.IsChecked == true) shipTypes.Add(ShipType.CV);
            if (ShipTypeCVL.IsChecked == true) shipTypes.Add(ShipType.CVL);
            if (ShipTypeAR.IsChecked == true) shipTypes.Add(ShipType.AR);
            if (ShipTypeSS.IsChecked == true) shipTypes.Add(ShipType.SS);
            if (ShipTypeSSV.IsChecked == true) shipTypes.Add(ShipType.SSV);
            if (ShipTypeAE.IsChecked == true) shipTypes.Add(ShipType.AE);
            if (ShipTypeSail.IsChecked == true) shipTypes.Add(ShipType.Sail);
            criteria.ShipTypes = shipTypes;

            // 阵营（Faction）
            var factions = new List<Faction>();
            if (NormalFactionPanel.Visibility == Visibility.Visible)
            {
                if (FactionEU.IsChecked == true) factions.Add(Faction.EagleUnion);
                if (FactionRN.IsChecked == true) factions.Add(Faction.RoyalNavy);
                if (FactionIJN.IsChecked == true) factions.Add(Faction.SakuraEmpire);
                if (FactionKMS.IsChecked == true) factions.Add(Faction.IronBlood);
                if (FactionDragon.IsChecked == true) factions.Add(Faction.DragonEmpery);
                if (FactionSN.IsChecked == true) factions.Add(Faction.NorthernUnion);
                if (FactionFFNF.IsChecked == true) factions.Add(Faction.FreeFrench);
                if (FactionMNF.IsChecked == true) factions.Add(Faction.Vichya);
                if (FactionSardegna.IsChecked == true) factions.Add(Faction.Sardegna);
                if (FactionTulip.IsChecked == true) factions.Add(Faction.Tulip);
                if (FactionCrystalLeague.IsChecked == true) factions.Add(Faction.CrystalLeague);
                if (FactionMETA.IsChecked == true) factions.Add(Faction.META);
                if (FactionTempesta.IsChecked == true) factions.Add(Faction.Tempesta);
                if (FactionOther.IsChecked == true) factions.Add(Faction.Other);
            }
            else if (CollabFactionPanel.Visibility == Visibility.Visible)
            {
                if (FactionCollab_Nep.IsChecked == true) factions.Add(Faction.Collab_Nep);
                if (FactionCollab_Bilibili.IsChecked == true) factions.Add(Faction.Collab_Bilibili);
                if (FactionCollab_Utawarerumono.IsChecked == true) factions.Add(Faction.Collab_Utawarerumono);
                if (FactionCollab_KizunaAI.IsChecked == true) factions.Add(Faction.Collab_KizunaAI);
                if (FactionCollab_Hololive.IsChecked == true) factions.Add(Faction.Collab_Hololive);
                if (FactionCollab_DoAXVV.IsChecked == true) factions.Add(Faction.Collab_DoAXVV);
                if (FactionCollab_Idolmaster.IsChecked == true) factions.Add(Faction.Collab_Idolmaster);
                if (FactionCollab_SSSS.IsChecked == true) factions.Add(Faction.Collab_SSSS);
                if (FactionCollab_Ryza.IsChecked == true) factions.Add(Faction.Collab_Ryza);
                if (FactionCollab_Senran.IsChecked == true) factions.Add(Faction.Collab_Senran);
                if (FactionCollab_Toloveru.IsChecked == true) factions.Add(Faction.Collab_Toloveru);
                if (FactionCollab_BRS.IsChecked == true) factions.Add(Faction.Collab_BRS);
                if (FactionCollab_Danmachi.IsChecked == true) factions.Add(Faction.Collab_Danmachi);
                if (FactionCollab_Yumia.IsChecked == true) factions.Add(Faction.Collab_Yumia);
                if (FactionCollab_DAL.IsChecked == true) factions.Add(Faction.Collab_DAL);
                if (FactionCollab_NieR.IsChecked == true) factions.Add(Faction.Collab_NieR);
            }
            else if (MetaFactionPanel.Visibility == Visibility.Visible)
            {
                if (FactionMeta_Flame.IsChecked == true) factions.Add(Faction.Meta_Flame);
                if (FactionMeta_Core.IsChecked == true) factions.Add(Faction.Meta_Core);
                if (FactionMeta_Reason.IsChecked == true) factions.Add(Faction.Meta_Reason);
                if (FactionMeta_Light.IsChecked == true) factions.Add(Faction.Meta_Light);
                if (FactionMeta_Fire.IsChecked == true) factions.Add(Faction.Meta_Fire);
            }
            criteria.Factions = factions;

            // 稀有度（Rarity）
            var rarities = new List<Rarity>();
            if (RarityNormal.IsChecked == true) rarities.Add(Rarity.N);
            if (RarityRare.IsChecked == true) rarities.Add(Rarity.R);
            if (RarityElite.IsChecked == true) rarities.Add(Rarity.SR);
            if (RaritySuperRare.IsChecked == true) rarities.Add(Rarity.SSR);
            if (RarityLegendary.IsChecked == true) rarities.Add(Rarity.UR);
            if (RarityDecisive.IsChecked == true) rarities.Add(Rarity.Decisive);
            if (RarityUltimate.IsChecked == true) rarities.Add(Rarity.Ultimate);
            criteria.Rarities = rarities;

            // 附加状态（布尔值）
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

            // 属性加成（AttributeType）
            var attrTypes = new List<AttributeType>();
            if (AttrFirepower.IsChecked == true) attrTypes.Add(AttributeType.FP);
            if (AttrAviation.IsChecked == true) attrTypes.Add(AttributeType.AVI);
            if (AttrMobility.IsChecked == true) attrTypes.Add(AttributeType.EVA);
            if (AttrAA.IsChecked == true) attrTypes.Add(AttributeType.AA);
            if (AttrTorpedo.IsChecked == true) attrTypes.Add(AttributeType.TRP);
            if (AttrReload.IsChecked == true) attrTypes.Add(AttributeType.RLD);
            if (AttrDurability.IsChecked == true) attrTypes.Add(AttributeType.HP);
            if (AttrAntiSub.IsChecked == true) attrTypes.Add(AttributeType.ASW);
            criteria.AttributeBonuses = attrTypes;

            return criteria;
        }

        /// <summary>
        /// 从已有的筛选条件恢复UI状态
        /// </summary>
        public void SetCriteria(FilterCriteria criteria)
        {
            if (criteria == null) return;

            // 舰种
            ShipTypeDD.IsChecked = criteria.ShipTypes.Contains(ShipType.DD);
            ShipTypeCL.IsChecked = criteria.ShipTypes.Contains(ShipType.CL);
            ShipTypeCA.IsChecked = criteria.ShipTypes.Contains(ShipType.CA);
            ShipTypeCB.IsChecked = criteria.ShipTypes.Contains(ShipType.CB);
            ShipTypeBM.IsChecked = criteria.ShipTypes.Contains(ShipType.BM);
            ShipTypeBC.IsChecked = criteria.ShipTypes.Contains(ShipType.BC);
            ShipTypeBB.IsChecked = criteria.ShipTypes.Contains(ShipType.BB);
            ShipTypeBBV.IsChecked = criteria.ShipTypes.Contains(ShipType.BBV);
            ShipTypeCV.IsChecked = criteria.ShipTypes.Contains(ShipType.CV);
            ShipTypeCVL.IsChecked = criteria.ShipTypes.Contains(ShipType.CVL);
            ShipTypeAR.IsChecked = criteria.ShipTypes.Contains(ShipType.AR);
            ShipTypeSS.IsChecked = criteria.ShipTypes.Contains(ShipType.SS);
            ShipTypeSSV.IsChecked = criteria.ShipTypes.Contains(ShipType.SSV);
            ShipTypeAE.IsChecked = criteria.ShipTypes.Contains(ShipType.AE);
            ShipTypeSail.IsChecked = criteria.ShipTypes.Contains(ShipType.Sail);

            // 阵营
            if (NormalFactionPanel.Visibility == Visibility.Visible)
            {
                FactionEU.IsChecked = criteria.Factions.Contains(Faction.EagleUnion);
                FactionRN.IsChecked = criteria.Factions.Contains(Faction.RoyalNavy);
                FactionIJN.IsChecked = criteria.Factions.Contains(Faction.SakuraEmpire);
                FactionKMS.IsChecked = criteria.Factions.Contains(Faction.IronBlood);
                FactionDragon.IsChecked = criteria.Factions.Contains(Faction.DragonEmpery);
                FactionSN.IsChecked = criteria.Factions.Contains(Faction.NorthernUnion);
                FactionFFNF.IsChecked = criteria.Factions.Contains(Faction.FreeFrench);
                FactionMNF.IsChecked = criteria.Factions.Contains(Faction.Vichya);
                FactionSardegna.IsChecked = criteria.Factions.Contains(Faction.Sardegna);
                FactionTulip.IsChecked = criteria.Factions.Contains(Faction.Tulip);
                FactionCrystalLeague.IsChecked = criteria.Factions.Contains(Faction.CrystalLeague);
                FactionMETA.IsChecked = criteria.Factions.Contains(Faction.META);
                FactionTempesta.IsChecked = criteria.Factions.Contains(Faction.Tempesta);
                FactionOther.IsChecked = criteria.Factions.Contains(Faction.Other);
            }
            else if (CollabFactionPanel.Visibility == Visibility.Visible)
            {
                FactionCollab_Nep.IsChecked = criteria.Factions.Contains(Faction.Collab_Nep);
                FactionCollab_Bilibili.IsChecked = criteria.Factions.Contains(Faction.Collab_Bilibili);
                FactionCollab_Utawarerumono.IsChecked = criteria.Factions.Contains(Faction.Collab_Utawarerumono);
                FactionCollab_KizunaAI.IsChecked = criteria.Factions.Contains(Faction.Collab_KizunaAI);
                FactionCollab_Hololive.IsChecked = criteria.Factions.Contains(Faction.Collab_Hololive);
                FactionCollab_DoAXVV.IsChecked = criteria.Factions.Contains(Faction.Collab_DoAXVV);
                FactionCollab_Idolmaster.IsChecked = criteria.Factions.Contains(Faction.Collab_Idolmaster);
                FactionCollab_SSSS.IsChecked = criteria.Factions.Contains(Faction.Collab_SSSS);
                FactionCollab_Ryza.IsChecked = criteria.Factions.Contains(Faction.Collab_Ryza);
                FactionCollab_Senran.IsChecked = criteria.Factions.Contains(Faction.Collab_Senran);
                FactionCollab_Toloveru.IsChecked = criteria.Factions.Contains(Faction.Collab_Toloveru);
                FactionCollab_BRS.IsChecked = criteria.Factions.Contains(Faction.Collab_BRS);
                FactionCollab_Danmachi.IsChecked = criteria.Factions.Contains(Faction.Collab_Danmachi);
                FactionCollab_Yumia.IsChecked = criteria.Factions.Contains(Faction.Collab_Yumia);
                FactionCollab_DAL.IsChecked = criteria.Factions.Contains(Faction.Collab_DAL);
                FactionCollab_NieR.IsChecked = criteria.Factions.Contains(Faction.Collab_NieR);
            }
            else if (MetaFactionPanel.Visibility == Visibility.Visible)
            {
                FactionMeta_Flame.IsChecked = criteria.Factions.Contains(Faction.Meta_Flame);
                FactionMeta_Core.IsChecked = criteria.Factions.Contains(Faction.Meta_Core);
                FactionMeta_Reason.IsChecked = criteria.Factions.Contains(Faction.Meta_Reason);
                FactionMeta_Light.IsChecked = criteria.Factions.Contains(Faction.Meta_Light);
                FactionMeta_Fire.IsChecked = criteria.Factions.Contains(Faction.Meta_Fire);
            }

            // 稀有度
            RarityNormal.IsChecked = criteria.Rarities.Contains(Rarity.N);
            RarityRare.IsChecked = criteria.Rarities.Contains(Rarity.R);
            RarityElite.IsChecked = criteria.Rarities.Contains(Rarity.SR);
            RaritySuperRare.IsChecked = criteria.Rarities.Contains(Rarity.SSR);
            RarityLegendary.IsChecked = criteria.Rarities.Contains(Rarity.UR);
            RarityDecisive.IsChecked = criteria.Rarities.Contains(Rarity.Decisive);
            RarityUltimate.IsChecked = criteria.Rarities.Contains(Rarity.Ultimate);

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
            AttrFirepower.IsChecked = criteria.AttributeBonuses.Contains(AttributeType.FP);
            AttrAviation.IsChecked = criteria.AttributeBonuses.Contains(AttributeType.AVI);
            AttrMobility.IsChecked = criteria.AttributeBonuses.Contains(AttributeType.EVA);
            AttrAA.IsChecked = criteria.AttributeBonuses.Contains(AttributeType.AA);
            AttrTorpedo.IsChecked = criteria.AttributeBonuses.Contains(AttributeType.TRP);
            AttrReload.IsChecked = criteria.AttributeBonuses.Contains(AttributeType.RLD);
            AttrDurability.IsChecked = criteria.AttributeBonuses.Contains(AttributeType.HP);
            AttrAntiSub.IsChecked = criteria.AttributeBonuses.Contains(AttributeType.ASW);
        }
    }

    /// 筛选条件类（使用枚举类型）
    public class FilterCriteria
    {
        public List<ShipType> ShipTypes { get; set; } = new();
        public List<Faction> Factions { get; set; } = new();
        public List<Rarity> Rarities { get; set; } = new();
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
        public List<AttributeType> AttributeBonuses { get; set; } = new();
    }
}