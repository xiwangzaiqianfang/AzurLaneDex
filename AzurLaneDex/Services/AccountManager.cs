using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using AzurLaneDex.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace AzurLaneDex.Services
{
    public class AccountManager
    {
        private readonly string _accountsFile;
        public List<Account> Accounts { get; private set; } = new();
        public string CurrentAccount { get; set; } = "";

        private string _defaultAccount = "";

        public string GetDefaultAccount() => _defaultAccount;
        public Account? GetCurrentAccount() => Accounts.FirstOrDefault(a => a.Name == CurrentAccount);


        public AccountManager()
        {
            // 使用 App.DataRoot，确保已初始化
            string dataRoot = App.DataRoot;
            if (string.IsNullOrEmpty(App.DataRoot))
                throw new InvalidOperationException("App.DataRoot not initialized");
            string accountsFile = Path.Combine(App.DataRoot, "users", "accounts.json");
            if (string.IsNullOrEmpty(dataRoot))
            {
                // 后备：使用 LocalApplicationData
                dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzurLaneDex", "data");
                Directory.CreateDirectory(dataRoot);
            }
            var usersDir = Path.Combine(dataRoot, "users");
            Directory.CreateDirectory(usersDir);
            _accountsFile = Path.Combine(usersDir, "accounts.json");
            Load();
        }

        private void Load()
        {
            if (File.Exists(_accountsFile))
            {
                var json = File.ReadAllText(_accountsFile);
                var data = JsonSerializer.Deserialize<AccountsData>(json);
                if (data != null)
                {
                    Accounts = data.Accounts ?? new List<Account>();
                    CurrentAccount = data.CurrentAccount ?? "";
                }
            }
            if (Accounts.Count == 0)
                CreateDefaultDeveloper();
        }

        private void CreateDefaultDeveloper()
        {
            var dev = new Account
            {
                Name = "developer",
                PasswordHash = HashHelper.Hash("1029384756"),
                AvatarPath = "",
                IsDeveloper = true,
                IsSystem = true,
                Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                LastLogin = ""
            };
            Accounts.Add(dev);
            CurrentAccount = "developer";
            Save();
        }

        public void Save()
        {
            var data = new AccountsData { Accounts = Accounts, CurrentAccount = CurrentAccount, DefaultAccount = _defaultAccount }; var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_accountsFile, json);
        }

        public bool VerifyPassword(string name, string password)
        {
            var acc = Accounts.Find(a => a.Name == name);
            return acc != null && acc.PasswordHash == HashHelper.Hash(password);
        }

        public bool AddAccount(string name, string password, string avatarPath = "", bool isDeveloper = false)
        {
            if (Accounts.Exists(a => a.Name == name)) return false;
            var acc = new Account
            {
                Name = name,
                PasswordHash = HashHelper.Hash(password),
                AvatarPath = avatarPath,
                IsDeveloper = isDeveloper
            };
            Accounts.Add(acc);
            Save();
            LogService.Operation("账户操作", $"添加账户 {name}");
            return true;
        }
        public bool IsSystemAccount(string name) => Accounts.FirstOrDefault(a => a.Name == name)?.IsSystem ?? false;

        public bool DeleteAccount(string accountName)
        {
            var acc = Accounts.FirstOrDefault(a => a.Name == accountName);
            if (acc == null) return false;
            if (acc.IsSystem) return false; // 不能删除系统账户

            Accounts.Remove(acc);
            if (CurrentAccount == accountName)
            {
                var fallback = Accounts.FirstOrDefault(a => !a.IsSystem) ?? Accounts.FirstOrDefault();
                CurrentAccount = fallback?.Name ?? "";
            }
            Save();
            LogService.Operation("账户操作", $"删除账户 {accountName}");
            return true;
        }

        public void SetCurrentAccount(string accountName)
        {
            var acc = Accounts.FirstOrDefault(a => a.Name == accountName);
            System.Diagnostics.Debug.WriteLine($"SetCurrentAccount called for {accountName}, found: {acc != null}");
            if (acc == null) return;
            CurrentAccount = accountName;
            acc.LastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Save();
            LogService.Operation("账户操作", $"默认账户 {accountName}");
        }
        public bool IsDeveloper(string? name = null)
        {
            string accountName = name ?? CurrentAccount;
            var acc = Accounts.FirstOrDefault(a => a.Name == accountName);
            return acc?.IsDeveloper ?? false;
        }
        /// <summary>
        /// 获取所有普通账户名称列表（排除系统账户）
        /// </summary>
        public List<string> GetAccountList()
        {
            return Accounts.Where(a => !a.IsSystem).Select(a => a.Name).ToList();
        }
        /// <summary>
        /// 获取账户信息（包含 is_system, is_developer 等）
        /// </summary>
        public Dictionary<string, object> GetAccountInfo(string accountName)
        {
            var acc = Accounts.FirstOrDefault(a => a.Name == accountName);
            if (acc == null) return new Dictionary<string, object>();
            return new Dictionary<string, object>
            {
                ["name"] = acc.Name,
                ["password_hash"] = acc.PasswordHash,
                ["avatar"] = acc.AvatarPath,
                ["is_developer"] = acc.IsDeveloper,
                ["is_system"] = acc.IsSystem,
                ["security_question"] = acc.SecurityQuestion,
                ["security_answer_hash"] = acc.SecurityAnswerHash,
                ["created"] = acc.Created,
                ["last_login"] = acc.LastLogin
            };
        }
        public void SetDefaultAccount(string accountName)
        {
            if (Accounts.Any(a => a.Name == accountName))
            {
                _defaultAccount = accountName;
                Save();
            }
        }
        public int GetRegularAccountCount() => Accounts.Count(a => !a.IsSystem);
        public bool CanModifyDeveloperFlag()
        {
            var current = Accounts.FirstOrDefault(a => a.Name == CurrentAccount);
            if (current == null) return false;
            // 系统账户或已有开发者权限的账户可以修改
            return current.IsSystem || current.IsDeveloper;
        }
        public bool SetDeveloperFlag(string accountName, bool isDeveloper)
        {
            // 权限检查
            var current = Accounts.FirstOrDefault(a => a.Name == CurrentAccount);
            if (current == null || (!current.IsDeveloper && !current.IsSystem))
                throw new UnauthorizedAccessException("只有管理员才能修改开发者标志");

            var acc = Accounts.FirstOrDefault(a => a.Name == accountName);
            if (acc == null) return false;

            // 不允许修改系统账户的开发者标志（可选项）
            if (acc.IsSystem) return false;

            acc.IsDeveloper = isDeveloper;
            Save();
            LogService.Operation("账户操作", $"{accountName} 帐户权限变更");
            return true;
        }
        /// <summary>
        /// 修改指定账户的密码
        /// </summary>
        /// <param name="accountName">要修改密码的账户名（如果为 null 或空，则修改当前登录账户）</param>
        /// <param name="oldPassword">旧密码（明文字符串）</param>
        /// <param name="newPassword">新密码（明文字符串）</param>
        /// <returns>是否修改成功</returns>
        public bool ChangePassword(string? accountName, string oldPassword, string newPassword)
        {
            // 确定要修改的账户名
            string targetName = string.IsNullOrEmpty(accountName) ? CurrentAccount : accountName;
            var account = Accounts.FirstOrDefault(a => a.Name == targetName);
            if (account == null) return false;

            // 验证旧密码（如果账户有密码）
            if (!string.IsNullOrEmpty(account.PasswordHash))
            {
                if (HashHelper.Hash(oldPassword) != account.PasswordHash)
                    return false;
            }
            else
            {
                // 如果账户原本没有密码，那么旧密码可以为空（或者要求旧密码为空，视业务而定）
                if (!string.IsNullOrEmpty(oldPassword))
                    return false;
            }

            // 设置新密码（如果新密码为空，则清空密码）
            account.PasswordHash = string.IsNullOrEmpty(newPassword) ? "" : HashHelper.Hash(newPassword);
            Save();
            LogService.Operation("账户操作", $"账户 {accountName} 密码变更");
            return true;
        }
        public string GetSecurityQuestion(string accountName)
        {
            var acc = Accounts.FirstOrDefault(a => a.Name == accountName);
            return acc?.SecurityQuestion ?? "";
        }

        public bool ResetPasswordBySecurity(string accountName, string answer, string newPassword)
        {
            var acc = Accounts.FirstOrDefault(a => a.Name == accountName);
            if (acc == null) return false;
            if (acc.SecurityAnswerHash != HashHelper.Hash(answer)) return false;
            acc.PasswordHash = HashHelper.Hash(newPassword);
            Save();
            LogService.Operation("账户操作", $"账户 {accountName} 密码重置");
            return true;
        }
        public void SetSecurityQuestion(string accountName, string question, string answer)
        {
            var acc = Accounts.FirstOrDefault(a => a.Name == accountName);
            if (acc == null) return;
            acc.SecurityQuestion = question;
            acc.SecurityAnswerHash = HashHelper.Hash(answer);
            Save();
            LogService.Operation("账户操作", $"账户 {accountName} 密码保护设置");
        }
        public bool AdminSetPassword(string accountName, string newPassword)
        {
            var acc = Accounts.FirstOrDefault(a => a.Name == accountName);
            if (acc == null) return false;
            acc.PasswordHash = string.IsNullOrEmpty(newPassword) ? "" : HashHelper.Hash(newPassword);
            Save();
            return true;
        }
    }
    public class AccountsData
    {
        public List<Account> Accounts { get; set; } = new();
        public string CurrentAccount { get; set; } = "";
        public string DefaultAccount { get; set; } = "";
        
    }
}
