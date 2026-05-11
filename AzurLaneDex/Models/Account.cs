using System;
using System.Collections.Generic;

namespace AzurLaneDex.Models
{
    public class Account
    {
        public string Name { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string AvatarPath { get; set; } = "";
        public bool IsDeveloper { get; set; }
        public string SecurityQuestion { get; set; } = "";
        public string SecurityAnswerHash { get; set; } = "";
        public string Created { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        public string LastLogin { get; set; } = "";
        public bool IsSystem { get; set; }  // 系统账户（如 developer）不可删除
    }

    public class AccountsData
    {
        public List<Account> Accounts { get; set; } = new();
        public string CurrentAccount { get; set; } = "";
    }
}
