using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AzurLaneDex.Services;

public class LanzouFileItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string Time { get; set; } = "";
    public string DirectLink { get; set; } = "";
}

public class LanzouService
{
    private readonly HttpClient _client;

    private static readonly string MobileUserAgent =
        "Mozilla/5.0 (Linux; Android 7.1.2; PCT-AL10 Build/N2G47H; wv) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 " +
        "Chrome/111.0.5563.116 Mobile Safari/537.36";

    public LanzouService(string? proxy = null)
    {
        _client = CreateClient(proxy);
    }

    private static HttpClient CreateClient(string? proxy)
    {
        if (string.IsNullOrEmpty(proxy))
            return new HttpClient();
        var handler = new HttpClientHandler
        {
            Proxy = new System.Net.WebProxy(proxy),
            UseProxy = true
        };
        return new HttpClient(handler);
    }

    /// <summary>
    /// 获取蓝奏云文件夹内所有文件列表
    /// </summary>
    /// <param name="folderUrl">文件夹分享链接，如 https://xxx.lanzouo.com/b00xxxxx</param>
    /// <param name="password">文件夹密码（可为空）</param>
    public async Task<List<LanzouFileItem>> GetFileListAsync(string folderUrl, string? password = null, int page = 1)
    {
        var files = new List<LanzouFileItem>();

        // 1. GET 文件夹页面，提取关键参数
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("User-Agent", MobileUserAgent);
        _client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
        _client.DefaultRequestHeaders.Add("Referer", "https://www.lanzouo.com/");

        string html = await _client.GetStringAsync(folderUrl);

        // 提取 folder_id
        var folderIdMatch = Regex.Match(html, @"var\s+lanmuczfolder_id\s*=\s*'(\d+)'");
        string folderId = folderIdMatch.Success ? folderIdMatch.Groups[1].Value : "";

        // 提取参数
        var paramMatches = Regex.Matches(html, @"data\s*:\s*'([^']+)'");
        var paramsList = new List<string>();
        foreach (Match m in paramMatches)
            paramsList.Add(m.Groups[1].Value);

        if (paramsList.Count < 4 || string.IsNullOrEmpty(folderId))
        {
            // 如果有密码，先提交密码
            var signMatch = Regex.Match(html, @"var\s+skdklds\s*=\s*'([^']+)'");
            if (!string.IsNullOrEmpty(password) && signMatch.Success)
            {
                string sign = signMatch.Groups[1].Value;
                var pwContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["lx"] = "2",
                    ["pwd"] = password,
                    ["sign"] = sign,
                    ["folder_id"] = folderId
                });
                var pwResponse = await _client.PostAsync(folderUrl, pwContent);
                html = await pwResponse.Content.ReadAsStringAsync();

                folderIdMatch = Regex.Match(html, @"var\s+lanmuczfolder_id\s*=\s*'(\d+)'");
                folderId = folderIdMatch.Success ? folderIdMatch.Groups[1].Value : "";
                paramMatches = Regex.Matches(html, @"data\s*:\s*'([^']+)'");
                paramsList.Clear();
                foreach (Match m in paramMatches)
                    paramsList.Add(m.Groups[1].Value);
            }
        }

        if (paramsList.Count < 4 || string.IsNullOrEmpty(folderId))
            return files;

        // 2. POST 获取文件列表
        string ajaxUrl = "https://www.lanzouo.com/filemoreajax.php";
        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["lx"] = "2",
            ["folder_id"] = folderId,
            ["pg"] = page.ToString(),
            ["t"] = paramsList[0],
            ["k"] = paramsList[1],
            ["uid"] = paramsList[2],
            ["pwd"] = paramsList.Count > 3 ? paramsList[3] : ""
        });

        _client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var response = await _client.PostAsync(ajaxUrl, formData);
        string json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("zt", out var zt) && zt.GetInt32() == 1 &&
            root.TryGetProperty("text", out var textArray) && textArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in textArray.EnumerateArray())
            {
                files.Add(new LanzouFileItem
                {
                    Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                    Name = item.TryGetProperty("name_all", out var name) ? name.GetString() ?? "" : "",
                    Size = item.TryGetProperty("size", out var size) ? size.GetString() ?? "" : "",
                    Time = item.TryGetProperty("time", out var time) ? time.GetString() ?? "" : ""
                });
            }
        }

        return files;
    }

    /// <summary>
    /// 获取单个文件的下载直链
    /// </summary>
    public async Task<string?> GetDirectLinkAsync(string fileUrl, string fileId, string? password = null)
    {
        _client.DefaultRequestHeaders.Clear();
        _client.DefaultRequestHeaders.Add("User-Agent", MobileUserAgent);
        _client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
        _client.DefaultRequestHeaders.Add("Referer", "https://www.lanzouo.com/");

        string html = await _client.GetStringAsync(fileUrl);

        // 提取签名参数
        var signMatch = Regex.Match(html, @"var\s+ajaxdata\s*=\s*'([^']+)'");
        var sign = signMatch.Success ? signMatch.Groups[1].Value : "";

        // POST 获取直链
        string ajaxUrl = "https://www.lanzouo.com/ajaxm.php";
        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["action"] = "downprocess",
            ["sign"] = sign,
            ["file_id"] = fileId,
            ["p"] = password ?? ""
        });

        _client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var response = await _client.PostAsync(ajaxUrl, formData);
        string resultJson = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("zt", out var zt) && zt.GetInt32() == 1 &&
            root.TryGetProperty("dom", out var dom) && dom.ValueKind == JsonValueKind.String)
        {
            string domStr = dom.GetString() ?? "";
            // 提取直链 URL
            var urlMatch = Regex.Match(domStr, @"https?://[^""']+");
            if (urlMatch.Success)
                return urlMatch.Value;
        }

        return null;
    }

    /// <summary>
    /// 判断是否为蓝奏云链接
    /// </summary>
    public static bool IsLanzouUrl(string url)
    {
        return url.Contains("lanzouo.com") || url.Contains("lanzouy.com") ||
               url.Contains("lanzous.com") || url.Contains("lanzoux.com") ||
               url.Contains("lanzouw.com") || url.Contains("lanzoui.com") ||
               url.Contains("lanzouq.com") || url.Contains("lanzoup.com");
    }
}