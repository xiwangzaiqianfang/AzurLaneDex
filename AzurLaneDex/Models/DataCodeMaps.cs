using System;
using System.Collections.Generic;

namespace AzurLaneDex.Models
{
    /// <summary>
    /// 数据源数值代码 ↔ 项目枚举的映射表（依据 docs/data_mapping.md §2）。
    /// 装载器/统计/编辑回写统一经此类转换，禁止在业务代码中散落魔法数字。
    /// 注：项目枚举值自 Enums.cs 定义，与数据源数值不同，必须经此处转换。
    /// </summary>
    public static class DataCodeMaps
    {
        // ---------------- Nation → Faction ----------------

        /// <summary>数据源 nation 代码 → 项目 Faction。未知值返回 null。</summary>
        public static Faction? NationToFaction(int nation)
        {
            // 常规阵营：1 白鹰 / 2 皇家 / 3 重樱 / 4 铁血 / 5 东煌 / 6 撒丁 / 7 北联 /
            // 8 自由鸢尾 / 9 维希 / 10 鸢尾教廷(并入自由鸢尾) / 11 郁金香 / 12 佩德雷里亚
            switch (nation)
            {
                case 1: return Faction.EagleUnion;
                case 2: return Faction.RoyalNavy;
                case 3: return Faction.SakuraEmpire;
                case 4: return Faction.IronBlood;
                case 5: return Faction.DragonEmpery;
                case 6: return Faction.Sardegna;
                case 7: return Faction.NorthernUnion;
                case 8:
                case 10: return Faction.FreeFrench;
                case 9: return Faction.Vichya;
                case 11: return Faction.Tulip;
                case 12: return Faction.CrystalLeague;
                case 96: return Faction.Tempesta;
                case 97: return Faction.META;
                case 98: return Faction.Other;   // 布里
                case 99: return Faction.Siren;
                // 联动（数据 101 起，项目 Collab_Nep=100 起，一一对应）
                case 101: return Faction.Collab_Nep;
                case 102: return Faction.Collab_Bilibili;
                case 103: return Faction.Collab_Utawarerumono;
                case 104: return Faction.Collab_KizunaAI;
                case 105: return Faction.Collab_Hololive;
                case 106: return Faction.Collab_DoAXVV;
                case 107: return Faction.Collab_Idolmaster;
                case 108: return Faction.Collab_SSSS;
                case 109: return Faction.Collab_Ryza;
                case 110: return Faction.Collab_Senran;
                case 111: return Faction.Collab_Toloveru;
                case 112: return Faction.Collab_BRS;
                case 113: return Faction.Collab_Yumia;
                case 114: return Faction.Collab_Danmachi;
                case 115: return Faction.Collab_DAL;
                default: return null;
            }
        }

        // ---------------- Rarity ----------------

        /// <summary>数据源稀有度 2~6 → 项目 Rarity（2 白 / 3 蓝 / 4 紫 SR / 5 金 SSR / 6 彩 UR）。</summary>
        public static Rarity RarityFromCode(int code)
        {
            switch (code)
            {
                case 2: return Rarity.N;
                case 3: return Rarity.R;
                case 4: return Rarity.SR;
                case 5: return Rarity.SSR;
                case 6: return Rarity.UR;
                default: return Rarity.Unknown;
            }
        }

        // ---------------- Hull → ShipType ----------------

        /// <summary>数据源 hull 代码 → 项目 ShipType。未知返回 null。</summary>
        public static ShipType? HullToShipType(int hull)
        {
            switch (hull)
            {
                case 1: return ShipType.DD;
                case 2: return ShipType.CL;
                case 3: return ShipType.CA;
                case 4: return ShipType.BC;
                case 5: return ShipType.BB;
                case 6: return ShipType.CVL;
                case 7: return ShipType.CV;
                case 8: return ShipType.SS;
                case 9: return ShipType.CAV;
                case 10: return ShipType.BBV;
                case 11: return ShipType.CT;
                case 12: return ShipType.AR;
                case 13: return ShipType.BM;
                case 17: return ShipType.SSV;
                case 18: return ShipType.CB;
                case 19: return ShipType.AE;
                case 20:
                case 21: return ShipType.DDG; // 导弹驱逐（21=主队形态）
                case 22:
                case 23:
                case 24: return ShipType.IX;
                case 25: return ShipType.SP;
                default: return null;
            }
        }

        /// <summary>项目 ShipType → 数据源 hull 代码（编辑回写用）。未知返回 null。</summary>
        public static int? ShipTypeToHull(ShipType type)
        {
            switch (type)
            {
                case ShipType.DD: return 1;
                case ShipType.CL: return 2;
                case ShipType.CA: return 3;
                case ShipType.BC: return 4;
                case ShipType.BB: return 5;
                case ShipType.CVL: return 6;
                case ShipType.CV: return 7;
                case ShipType.SS: return 8;
                case ShipType.CAV: return 9;
                case ShipType.BBV: return 10;
                case ShipType.CT: return 11;
                case ShipType.AR: return 12;
                case ShipType.BM: return 13;
                case ShipType.SSV: return 17;
                case ShipType.CB: return 18;
                case ShipType.AE: return 19;
                case ShipType.DDG: return 20;
                case ShipType.IX: return 22;
                case ShipType.SP: return 25;
                default: return null;
            }
        }

        // ---------------- 技能种类 / 装甲 ----------------

        /// <summary>技能描述中的 stat 键（如 "fp"/"aa"）→ AttributeType。</summary>
        public static AttributeType StatKeyToAttribute(string stat)
        {
            switch (stat?.ToLowerInvariant())
            {
                case "hp": return AttributeType.HP;
                case "fp": return AttributeType.FP;
                case "trp": return AttributeType.TRP;
                case "avi": return AttributeType.AVI;
                case "aa": return AttributeType.AA;
                case "rld": return AttributeType.RLD;
                case "hit": return AttributeType.ACC;
                case "eva": return AttributeType.EVA;
                case "asw": return AttributeType.ASW;
                case "spd": return AttributeType.SPD;
                case "luck": return AttributeType.LUCK;
                default: return AttributeType.None;
            }
        }

        /// <summary>数据源装甲 0/1/2 → 轻/中/重。</summary>
        public static ArmorType ArmorFromCode(int code) => code switch
        {
            0 => ArmorType.Light,
            1 => ArmorType.Medium,
            _ => ArmorType.Heavy
        };

        // ---------------- 分类（id 区间） ----------------

        /// <summary>按数据源 id 区间推导大分类（30000+ META / 20000+ 科研 / 10000+ 联动 / 其余常规）。</summary>
        public static ShipCategory CategoryFromId(int id)
        {
            if (id >= 30000) return ShipCategory.META;
            if (id >= 20000) return ShipCategory.Research;
            if (id >= 10000) return ShipCategory.Collab;
            return ShipCategory.Normal;
        }

        // ---------------- 技能种类 ----------------

        /// <summary>数据源技能 type 0~3 → GameSkillKind（0 装备 /1 进攻 /2 防御 /3 支援）。</summary>
        public static GameSkillKind SkillKindFromCode(int code) => (GameSkillKind)code;

        // ---------------- 头像目录规则（第七阶段；avatar 包内目录须与之一致） ----------------

        /// <summary>
        /// 常规阵营 → icon/square 头像目录名（与实际资源目录一致，勿随意修改）：
        /// 注意资源侧拼写（Sakura Islands / Sardegna Empire / Kindom of Tulipa 等）。
        /// </summary>
        public static string FactionAvatarDirName(Faction faction)
        {
            switch (faction)
            {
                case Faction.EagleUnion: return "Eagle Union";
                case Faction.RoyalNavy: return "Royal Navy";
                case Faction.SakuraEmpire: return "Sakura Islands";
                case Faction.IronBlood: return "Iron Blood";
                case Faction.DragonEmpery: return "Dragon Empery";
                case Faction.Sardegna: return "Sardegna Empire";
                case Faction.NorthernUnion: return "Northern Parliament";
                case Faction.FreeFrench: return "Iris Libre";
                case Faction.Vichya: return "Vichya Dominion";
                case Faction.Tulip: return "Kindom of Tulipa";
                case Faction.CrystalLeague: return "Liga de Pedreria";
                case Faction.Tempesta: return "Tempesta";
                case Faction.META: return "META";
                // 通用/布里/其它 → 资源统一置于 Common/Universal
                case Faction.Universal:
                case Faction.Other:
                case Faction.Siren:
                case Faction.Council:
                case Faction.X:
                case Faction.Unknown:
                    return "Universal";
                default: return faction.ToString();
            }
        }

        /// <summary>
        /// 联动阵营 → icon/square/Collab 下的作品名目录（与阵营代码同名，与实际资源目录一致）：
        /// 如 Collab/KizunaAI、Collab/Neptunia、Collab/Atelier Ryza。
        /// <para>
        /// 历史说明：旧版数据库里联动阵营 code 与实际作品整体错位（如 collab_nier 名下是《约会大作战》），
        /// 当时曾按实测资源目录做过错位映射；**使用者已修正数据库阵营**（并改为 collab_hyperdimension /
        /// collab_atelier 等语义代码），故此处恢复为「code 同名目录」。
        /// 仍保留一层兜底：<c>AvatarFiles</c> 在目录未命中时按文件名在 icon/square 全树再找一次。
        /// </para>
        /// </summary>
        public static string CollabAvatarDirName(Faction faction)
        {
            switch (faction)
            {
                case Faction.Collab_Nep: return "Neptunia";
                case Faction.Collab_Bilibili: return "Bili";
                case Faction.Collab_Utawarerumono: return "Utawarerumono";
                case Faction.Collab_KizunaAI: return "KizunaAI";
                case Faction.Collab_Hololive: return "Hololive";
                case Faction.Collab_DoAXVV: return "Venus Vacation";
                case Faction.Collab_Idolmaster: return "The Idolmaster";
                case Faction.Collab_SSSS: return "SSSS";
                case Faction.Collab_Ryza: return "Atelier Ryza";
                case Faction.Collab_Senran: return "Senran Kagura";
                case Faction.Collab_Toloveru: return "To-Love Ru";
                case Faction.Collab_BRS: return "BLACK★ROCK SHOOTER";
                case Faction.Collab_Danmachi: return "Danmachi";
                case Faction.Collab_Yumia: return "Atelier Yumia";
                case Faction.Collab_DAL: return "Date A Live";
                case Faction.Collab_NieR: return "NieR Automata";
                default: return faction.ToString();
            }
        }

        /// <summary>
        /// 头像文件名前缀候选（资源侧命名规范，实测自 Assets/icon/square），按优先级排列，
        /// 末尾总带一个空串兜底（联动/META/东煌及未加前缀的资源）：
        /// 白鹰 USS / 皇家 HMS / 重樱 IJN / 铁血 KMS·SMS / 撒丁 RN / 北联 SN /
        /// 自由鸢尾 FFNF / 维希 MNF / 风帆 MOT / 郁金香·佩德雷里亚 HNLMS / 布里 UNIV。
        /// </summary>
        public static IReadOnlyList<string> AvatarFilePrefixes(ShipCategory category, Faction faction, bool isBulin = false)
        {
            var list = new List<string>();
            if (isBulin)
            {
                list.Add("UNIV ");
                return list;
            }
            if (category == ShipCategory.Collab)
            {
                // 尼普顿系资源带 HDN 前缀（HDN Neptune 等）
                if (faction == Faction.Collab_Nep)
                {
                    list.Add("HDN ");
                    list.Add("");
                }
                return list;
            }
            if (category == ShipCategory.META) return list; // 无前缀

            switch (faction)
            {
                case Faction.EagleUnion: list.Add("USS "); break;
                case Faction.RoyalNavy: list.Add("HMS "); break;
                case Faction.SakuraEmpire: list.Add("IJN "); break;
                case Faction.IronBlood: list.Add("KMS "); list.Add("SMS "); break; // 现代 KMS / 一战 SMS
                case Faction.Sardegna: list.Add("RN "); break;
                case Faction.NorthernUnion: list.Add("SN "); break;
                case Faction.FreeFrench: list.Add("FFNF "); break;
                case Faction.Vichya: list.Add("MNF "); break;
                case Faction.Tempesta: list.Add("MOT "); break;
                case Faction.Tulip:
                case Faction.CrystalLeague: list.Add("HNLMS "); break;
                case Faction.Universal:
                case Faction.Other:
                case Faction.Unknown: list.Add("UNIV "); break;
                // 东煌（An Shan 等）无前缀
            }
            list.Add("");   // 兜底：无前缀
            return list;
        }

        /// <summary>
        /// 头像"类型"顶层目录（Assets/icon/square/{类型}/{阵营}/…）：
        /// 联动 → Collab；META → META；普通/科研/其它阵营 → Common
        /// （通用/布里等阵营落在 Common/Universal 子目录）。
        /// </summary>
        public static string AvatarTypeRoot(ShipCategory category)
        {
            if (category == ShipCategory.Collab) return "Collab";
            if (category == ShipCategory.META) return "META";
            return "Common"; // Normal / Research / 其它阵营
        }

        /// <summary>
        /// 头像相对目录（相对 icon/square 根）：例如
        /// 普通 → Common/Eagle Union；科研 → Common/Iron Blood；联动 → Collab/KizunaAI；
        /// META → META（不分类）；布里 → Common/Universal。
        /// </summary>
        public static string AvatarRelativeFolder(ShipCategory category, Faction faction, bool isBulin = false)
        {
            if (isBulin) return "Common/Universal";
            string root = AvatarTypeRoot(category);
            if (category == ShipCategory.META)
                return root; // META 不分类，直接放在 META/ 下
            string sub = category == ShipCategory.Collab
                ? CollabAvatarDirName(faction)
                : FactionAvatarDirName(faction);
            return root + "/" + sub;
        }
    }
}
