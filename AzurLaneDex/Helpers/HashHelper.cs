using System;
using System.Security.Cryptography;
using System.Text;

namespace AzurLaneDex.Helpers
{
    public static class HashHelper
    {
        private const string Salt = "AzurLaneDex_Salt_2025";

        public static string Hash(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            var salted = input + Salt;
            var bytes = Encoding.UTF8.GetBytes(salted);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLower();
        }
    }
}
