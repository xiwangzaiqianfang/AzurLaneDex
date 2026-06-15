using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AzurLaneDex.Models
{
    public class TagDefinition
    {
        public string Tag { get; set; } = "";
        public string Category { get; set; } = "";
        public int ParamCount { get; set; } = 0;
        public List<string> ParamTypes { get; set; } = new();
        public Regex? MatchRegex { get; set; }
        public string Example { get; set; } = "";
        public string DisplayName
        {
            get
            {
                var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
                string resourceKey = $"{Tag}";
                string localized = loader.GetString(resourceKey);
                if (string.IsNullOrEmpty(localized))
                {
                    // 降级：使用 Category + Example
                    return $"{Category}：{Example}";
                }
                return localized;
            }
        }
        public string LocalizedCategory
        {
            get
            {
                var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
                string key = $"Category_{Category}";
                string localized = loader.GetString(key);
                return string.IsNullOrEmpty(localized) ? Category : localized;
            }
        }
    }



    public static class TagLibrary
    {
        public static List<TagDefinition> GetAllTags()
        {
            return new List<TagDefinition>
        {
            // ========== 建造类 ==========
            new TagDefinition { Tag = "acquire_7", Category = "建造", ParamCount = 0,
                MatchRegex = new Regex(@"轻型池建造|轻型建造", RegexOptions.Compiled), Example = "轻型池建造" },
            new TagDefinition { Tag = "acquire_8", Category = "建造", ParamCount = 0,
                MatchRegex = new Regex(@"重型池建造|重型建造", RegexOptions.Compiled), Example = "重型池建造" },
            new TagDefinition { Tag = "acquire_9", Category = "建造", ParamCount = 0,
                MatchRegex = new Regex(@"特(?:型|种)池建造|特(?:型|种)建造", RegexOptions.Compiled), Example = "特种池建造" },
            new TagDefinition { Tag = "acquire_10", Category = "建造", ParamCount = 0,
                MatchRegex = new Regex(@"期间限定建造|限定池建造", RegexOptions.Compiled), Example = "期间限定建造" },
            new TagDefinition { Tag = "acquire_11", Category = "建造", ParamCount = 0,
                MatchRegex = new Regex(@"无法建造", RegexOptions.Compiled), Example = "无法建造" },
            new TagDefinition { Tag = "acquire_52", Category = "建造", ParamCount = 0,
                MatchRegex = new Regex(@"^建造$|建造获得|建造获取", RegexOptions.Compiled), Example = "建造" },

            // ========== 打捞类 ==========
            new TagDefinition { Tag = "acquire_5", Category = "打捞", ParamCount = 0,
                MatchRegex = new Regex(@"仅限打捞", RegexOptions.Compiled), Example = "仅限打捞" },
            new TagDefinition { Tag = "acquire_46", Category = "打捞", ParamCount = 2,
                MatchRegex = new Regex(@"作战档案(.+?)：(.+)", RegexOptions.Compiled), Example = "作战档案箱庭疗法：B1/B2/B3/D1/D2/D3" },
            new TagDefinition { Tag = "acquire_61", Category = "打捞", ParamCount = 2,
                MatchRegex = new Regex(@"作战档案(.+?)(?:掉落|打捞|获得)(.+)", RegexOptions.Compiled), Example = "作战档案箱庭疗法掉落B1/B2/B3/D1/D2/D3" },
            new TagDefinition { Tag = "acquire_47", Category = "作战档案通关60次指定关卡获取", ParamCount = 2,
                MatchRegex = new Regex(@"作战档案通关60次(.+?)(?:获得|：)(.+)", RegexOptions.Compiled), Example = "作战档案通关60次【红染的参访者】A3获得" },
            new TagDefinition { Tag = "acquire_2", Category = "打捞", ParamCount = 0,
                MatchRegex = new Regex(@"普通掉落点", RegexOptions.Compiled), Example = "普通掉落点" },
            new TagDefinition { Tag = "acquire_3", Category = "打捞", ParamCount = 0,
                MatchRegex = new Regex(@"档案掉落点", RegexOptions.Compiled), Example = "档案掉落点" },
            new TagDefinition { Tag = "acquire_4", Category = "打捞", ParamCount = 0,
                MatchRegex = new Regex(@"活动掉落点", RegexOptions.Compiled), Example = "活动掉落点" },
            new TagDefinition { Tag = "acquire_66", Category = "打捞", ParamCount = 0,
                MatchRegex = new Regex(@"下方(?:仅列出|请列出)常驻掉落点|下方仅列出普通掉落点", RegexOptions.Compiled),
                Example = "下方仅列出普通掉落点" },
            new TagDefinition { Tag = "acquire_50", Category = "打捞", ParamCount = 0,
                MatchRegex = new Regex(@"无法打捞|不可打捞", RegexOptions.Compiled), Example = "无法打捞" },
            new TagDefinition { Tag = "acquire_62", Category = "打捞", ParamCount = 0,
                MatchRegex = new Regex(@"^打捞$|打捞获得", RegexOptions.Compiled), Example = "打捞" },

            // ========== 勋章支援 ==========
            new TagDefinition { Tag = "acquire_12", Category = "勋章支援", ParamCount = 0,
                MatchRegex = new Regex(@"勋章支援(?:概率)?获得", RegexOptions.Compiled), Example = "勋章支援概率获得" },

            // ========== 商店兑换类 ==========
            new TagDefinition { Tag = "acquire_14", Category = "商店兑换", ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"舰队商店(\d+)舰队币兑换|商店", RegexOptions.Compiled), Example = "舰队商店2000舰队币兑换获得" },
            new TagDefinition { Tag = "acquire_15", Category = "商店兑换", ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"军需商店(\d+)功勋兑换", RegexOptions.Compiled), Example = "军需商店8000功勋兑换" },
            new TagDefinition { Tag = "acquire_16", Category = "商店兑换", ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"META商店(\d+)破碎结晶兑换", RegexOptions.Compiled), Example = "META商店800破碎结晶兑换" },
            new TagDefinition { Tag = "acquire_60", Category = "商店兑换", ParamCount = 2,
                ParamTypes = new List<string> { "int", "string" },
                MatchRegex = new Regex(@"META商店(\d+)破碎结晶兑换结晶：(.+)|结晶[：:](.+)", RegexOptions.Compiled), Example = "META商店800破碎结晶兑换结晶：飞龙·META" },
            new TagDefinition { Tag = "acquire_17", Category = "商店兑换", ParamCount = 1,
                MatchRegex = new Regex(@"核心(?:月度兑换)?(?:商店)?(\d+)核心数据兑换", RegexOptions.Compiled), Example = "核心月度兑换商店100核心数据兑换" },
            new TagDefinition { Tag = "acquire_18", Category = "商店兑换", ParamCount = 1,
                MatchRegex = new Regex(@"勋章商店(\d+)荣誉勋章兑换", RegexOptions.Compiled), Example = "勋章商店100荣誉勋章兑换" },
            new TagDefinition { Tag = "acquire_19", Category = "商店兑换", ParamCount = 0,
                MatchRegex = new Regex(@"原型商店4000特装原型兑换", RegexOptions.Compiled), Example = "原型商店4000特装原型兑换" },
            new TagDefinition { Tag = "acquire_20", Category = "商店兑换", ParamCount = 3,
                ParamTypes = new List<string> { "string", "string", "string" },
                MatchRegex = new Regex(@"(.+?)活动商店(.+?)(?:兑换|获得)", RegexOptions.Compiled), Example = "【复刻】微层混合活动商店「荣誉之章」兑换获得" },
            new TagDefinition { Tag = "acquire_21", Category = "兑换", ParamCount = 1,
                MatchRegex = new Regex(@"(.+?)礼包购买(?:获得)?", RegexOptions.Compiled), Example = "新春福袋礼包购买" },
            new TagDefinition { Tag = "acquire_22", Category = "科研", ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"科研([一二三四五六七八九十]|\d+)期获得", RegexOptions.Compiled), Example = "科研4期获得" },
            new TagDefinition { Tag = "acquire_23", Category = "兑换", ParamCount = 0,
                MatchRegex = new Regex(@"作战补给(?:兑换)?", RegexOptions.Compiled), Example = "作战补给兑换" },
            new TagDefinition { Tag = "acquire_24", Category = "兑换", ParamCount = 0,
                MatchRegex = new Regex(@"^兑换$|商店兑换", RegexOptions.Compiled), Example = "商店兑换" },
            new TagDefinition { Tag = "acquire_51", Category = "兑换", ParamCount = 0,
                MatchRegex = new Regex(@"无法兑换", RegexOptions.Compiled), Example = "无法兑换" },
            new TagDefinition { Tag = "acquire_13", Category = "兑换", ParamCount = 0,
                MatchRegex = new Regex(@"商店兑换", RegexOptions.Compiled), Example = "商店兑换" },

            // ========== 贺年卡、邀请函类 ==========
            new TagDefinition { Tag = "acquire_25", Category = "兑换", ParamCount = 3,
                ParamTypes = new List<string> { "string", "string", "string" },
                MatchRegex = new Regex(@"贺年卡（([甲乙丙丁戊己庚辛壬癸])([子丑寅卯辰巳午未申酉戌亥])）(?:\s*（(\d{4})）)?", RegexOptions.Compiled), Example = "贺年卡（辛丑）（2021）可选" },
            new TagDefinition { Tag = "acquire_63", Category = "兑换", ParamCount = 0,
                MatchRegex = new Regex(@"贺年卡(?:可选|兑换)", RegexOptions.Compiled), Example = "贺年卡可选" },
            new TagDefinition { Tag = "acquire_26", Category = "兑换", ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"(\d{4})年年贺状可选|年贺状(\d{4})可选", RegexOptions.Compiled), Example = "2024年年贺状可选" },
            new TagDefinition { Tag = "acquire_27", Category = "兑换", ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"([一二三四五六七八九十]|\d+)周年邀请函可选", RegexOptions.Compiled), Example = "三周年邀请函可选" },
            new TagDefinition { Tag = "acquire_28", Category = "兑换", ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"(\d{4})年庆典邀请函可选|庆典邀请函(\d{4})可选", RegexOptions.Compiled), Example = "2021年庆典邀请函可选" },
            new TagDefinition { Tag = "acquire_29", Category = "兑换", ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"(\d{4})年宴会邀请函可选|宴会邀请函(\d{4})可选", RegexOptions.Compiled), Example = "2018年宴会邀请函可选" },
            new TagDefinition { Tag = "acquire_64", Category = "兑换", ParamCount = 0,
                MatchRegex = new Regex(@"邀请函(?:可选|兑换)", RegexOptions.Compiled), Example = "邀请函可选" },
            new TagDefinition { Tag = "acquire_30", Category = "兑换", ParamCount = 0,
                MatchRegex = new Regex(@"圣夜的赠礼", RegexOptions.Compiled), Example = "圣夜的赠礼" },
            new TagDefinition { Tag = "acquire_31", Category = "兑换", ParamCount = 1,
                ParamTypes = new List<string> { "string" },
                MatchRegex = new Regex(@"「(.+?)」角色自选可选", RegexOptions.Compiled), Example = "「复刻：假日航程」角色自选可选" },
            new TagDefinition { Tag = "acquire_32", Category = "兑换", ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"(\d+)\s*th庆典邀请函", RegexOptions.Compiled), Example = "4th庆典邀请函" },
            new TagDefinition { Tag = "acquire_33", Category = "兑换", ParamCount = 0,
                MatchRegex = new Regex(@"心愿卡", RegexOptions.Compiled), Example = "心愿卡" },
            new TagDefinition { Tag = "acquire_34", Category = "兑换", ParamCount = 2,
                ParamTypes = new List<string> { "int", "int" },
                MatchRegex = new Regex(@"(\d{4})年(\d+)月新晋指挥官·新服开服庆典活动-新服庆典PT任务", RegexOptions.Compiled), Example = "2023年3月新晋指挥官·新服开服庆典活动-新服庆典PT任务" },

            // ========== META 相关 ==========
            new TagDefinition { Tag = "acquire_35", Category = "META", ParamCount = 0,
                MatchRegex = new Regex(@"META[\s]*信标档案信标解析", RegexOptions.Compiled), Example = "META信标档案信标解析" },
            new TagDefinition { Tag = "acquire_36", Category = "META", ParamCount = 0,
                MatchRegex = new Regex(@"META[\s]*研究室资讯同步(?:解析)?", RegexOptions.Compiled), Example = "META研究室资讯同步解析" },

            // ========== 任务奖励类 ==========
            new TagDefinition { Tag = "acquire_37", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"世界巡游赠送", RegexOptions.Compiled), Example = "世界巡游赠送" },
            new TagDefinition { Tag = "acquire_38", Category = "兑换", ParamCount = 0,
                MatchRegex = new Regex(@"推出海上传奇品质舰船的大型EX活动累计PT获取", RegexOptions.Compiled), Example = "大型EX活动累计PT获取" },
            new TagDefinition { Tag = "acquire_54", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"累计PT(?:获取|奖励)|PT累计|活动累计PT", RegexOptions.Compiled), Example = "累计PT获取" },
            new TagDefinition { Tag = "acquire_39", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"布里支援计划", RegexOptions.Compiled), Example = "布里支援计划" },
            new TagDefinition { Tag = "acquire_41", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"日常/周常任务(?:奖励)?", RegexOptions.Compiled), Example = "日常/周常任务奖励" },
            new TagDefinition { Tag = "acquire_42", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"月度签到(?:奖励)?", RegexOptions.Compiled), Example = "月度签到奖励" },
            new TagDefinition { Tag = "acquire_43", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"活动任务奖励", RegexOptions.Compiled), Example = "活动任务奖励" },
            new TagDefinition { Tag = "acquire_44", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"主线普通关卡三星奖励", RegexOptions.Compiled), Example = "主线普通关卡三星奖励" },
            new TagDefinition { Tag = "acquire_45", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"大型EX和中型SP活动关卡三星奖励", RegexOptions.Compiled), Example = "大型EX和中型SP活动关卡三星奖励" },
            new TagDefinition { Tag = "acquire_48", Category = "任务奖励", ParamCount = 2,
                ParamTypes = new List<string> { "string", "string" },
                MatchRegex = new Regex(@"在(.+?)[、\n\r\t]+(.*?)活动中作为临时NPC[，；、]累计1000点友好度获得", RegexOptions.Compiled), Example = "在「微层混合」、「微层混合·复刻」活动中作为临时NPC，累计1000点友好度获得" },
            new TagDefinition { Tag = "acquire_49", Category = "任务奖励", ParamCount = 2,
                ParamTypes = new List<string> { "string", "int" },
                MatchRegex = new Regex(@"限时活动(.+?)；累计登录(\d+)获取", RegexOptions.Compiled), Example = "限时活动「新春福袋」；累计登录7获取" },
            new TagDefinition { Tag = "acquire_55", Category = "活动赠送", ParamCount = 0,
                MatchRegex = new Regex(@"活动赠送|活动(?:免费)?获取|特别奖励登陆活动|赠送", RegexOptions.Compiled), Example = "活动赠送" },
            new TagDefinition { Tag = "acquire_56", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"新兵训练(?:任务)?", RegexOptions.Compiled), Example = "新兵训练任务" },
            new TagDefinition { Tag = "acquire_57", Category = "收藏奖励", ParamCount = 0,
                MatchRegex = new Regex(@"收藏奖励", RegexOptions.Compiled), Example = "收藏奖励" },
            new TagDefinition { Tag = "acquire_58", Category = "初始", ParamCount = 0,
                MatchRegex = new Regex(@"初始船|初始选择|初始舰", RegexOptions.Compiled), Example = "初始船" },
            new TagDefinition {
                Tag = "acquire_59",
                Category = "任务奖励",
                ParamCount = 1,
                ParamTypes = new List<string> { "int" },
                MatchRegex = new Regex(@"指挥官等级达到(\d+)级(?:获得|奖励)", RegexOptions.Compiled),
                Example = "指挥官等级达到15级获得"
            },
            // ========== 兜底 ==========
            new TagDefinition { Tag = "acquire_custom", Category = "其他", ParamCount = 1,
                ParamTypes = new List<string> { "string" }, MatchRegex = null, Example = "自定义文本" }
        };
        }
        public static List<TagDefinition> GetAllGearTags()
        {
            return new List<TagDefinition>
        {
            new TagDefinition { Tag = "gear_1", Category = "兵装制造", ParamCount = 0,
                MatchRegex = new Regex(@"兵装制造|无", RegexOptions.Compiled), Example = "兵装制造" },
            new TagDefinition { Tag = "gear_2", Category = "活动获取", ParamCount = 1,
                ParamTypes = new List<string> { "string" },
                MatchRegex = new Regex(@"(.+?)活动获取", RegexOptions.Compiled), Example = "「复刻：微层混合」活动获取" },
            new TagDefinition { Tag = "gear_3", Category = "限时建造", ParamCount = 0,
                MatchRegex = new Regex(@"限时兵装建造", RegexOptions.Compiled), Example = "限时兵装建造" },
            new TagDefinition { Tag = "gear_4", Category = "商店兑换", ParamCount = 2,
                ParamTypes = new List<string> { "string", "string" },
                MatchRegex = new Regex(@"(.+?)商店(\d+)代币兑换", RegexOptions.Compiled), Example = "核心商店200核心数据兑换" },
            new TagDefinition { Tag = "gear_5", Category = "任务奖励", ParamCount = 0,
                MatchRegex = new Regex(@"活动任务奖励", RegexOptions.Compiled), Example = "活动任务奖励" },
            new TagDefinition { Tag = "gear_custom", Category = "其他", ParamCount = 1,
                ParamTypes = new List<string> { "string" }, MatchRegex = null, Example = "自定义文本" }
        };
        }
    }
}