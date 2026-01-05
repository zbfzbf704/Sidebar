#region License Information (GPL v3)

/*
    Sidebar - 基于 ShareX 开发的侧边栏应用程序
    
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.

    ---
    
    Based on ShareX:
    Copyright (c) 2007-2025 ShareX Team
    Licensed under GPL v3
    
    ---
    
    Copyright (c) 2025 蝴蝶哥
    Email: your-email@example.com
    
    This code is part of the Sidebar application.
    All rights reserved.
*/

#endregion License Information (GPL v3)

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ShareX.HelpersLib;

namespace Sidebar
{
    /// <summary>
    /// API 请求结果
    /// </summary>
    public class APIResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public int StatusCode { get; set; }

        public APIResult()
        {
            Data = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 授权 API 客户端
    /// </summary>
    public class AuthAPIClient
    {
        private const string AUTH_SERVER = "https://your-api-server.com";
        private const string APP_KEY = "SideBar"; // 应用标识
        
        private const int TIMEOUT = 10; // 秒
        private const int MAX_RETRIES = 3;
        private const int RETRY_DELAY = 1000; // 毫秒
        
        private HttpClient httpClient;
        
        public string Phone { get; set; }
        public string Token { get; set; }
        public string Expires { get; set; }
        public string SubscriptionType { get; set; } // 订阅类型：plan1(年费会员)、plan2(永久会员)、trial(试用期)
        
        public bool IsLoggedIn => !string.IsNullOrEmpty(Phone) && !string.IsNullOrEmpty(Token);
        
        public AuthAPIClient()
        {
            httpClient = HttpClientFactory.Create();
            // 注意：不能修改共享 HttpClient 的 Timeout 属性，因为可能已经被使用过
            // 超时将通过 CancellationTokenSource 在请求时控制
            
            // 加载保存的 Token
            LoadSavedToken();
        }
        
        private void LoadSavedToken()
        {
            try
            {
                string tokenPath = GetTokenPath();
                if (File.Exists(tokenPath))
                {
                    string content = File.ReadAllText(tokenPath);
                    string json = null;
                    
                    // 尝试解密（新格式：加密存储）
                    try
                    {
                        json = DPAPI.Decrypt(content, "Sidebar-Auth-Token", DataProtectionScope.CurrentUser);
#if DEBUG
                        System.Diagnostics.Debug.WriteLine("已使用加密格式加载 Token");
#endif
                    }
                    catch
                    {
                        // 如果不是加密格式，按旧格式处理（向后兼容）
                        try
                        {
                            // 尝试直接解析 JSON（旧格式：明文存储）
                            var testData = JsonConvert.DeserializeObject<Dictionary<string, string>>(content);
                            if (testData != null)
                            {
                                json = content;
#if DEBUG
                                System.Diagnostics.Debug.WriteLine("检测到旧格式（明文），将自动迁移为加密格式");
#endif
                            }
                        }
                        catch
                        {
                            // 既不是加密格式，也不是有效的 JSON，可能是损坏的文件
#if DEBUG
                            System.Diagnostics.Debug.WriteLine("Token 文件格式无效，将忽略");
#endif
                            return;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(json))
                    {
                        var tokenData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (tokenData != null)
                        {
                            Phone = tokenData.ContainsKey("phone") ? tokenData["phone"] : null;
                            Token = tokenData.ContainsKey("token") ? tokenData["token"] : null;
                            Expires = tokenData.ContainsKey("expires") ? tokenData["expires"] : null;
                            SubscriptionType = tokenData.ContainsKey("subscription_type") ? tokenData["subscription_type"] : null;
                            
                            // 如果是从旧格式加载的，自动迁移为加密格式
                            if (json == content)
                            {
                                SaveToken(); // 重新保存为加密格式
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"加载 Token 失败: {ex.Message}");
#endif
            }
        }
        
        private void SaveToken()
        {
            try
            {
                if (!string.IsNullOrEmpty(Phone) && !string.IsNullOrEmpty(Token))
                {
                    string tokenPath = GetTokenPath();
                    string tokenDir = Path.GetDirectoryName(tokenPath);
                    if (!string.IsNullOrEmpty(tokenDir) && !Directory.Exists(tokenDir))
                    {
                        Directory.CreateDirectory(tokenDir);
                    }
                    
                    var tokenData = new Dictionary<string, string>
                    {
                        { "phone", Phone },
                        { "token", Token },
                        { "expires", Expires ?? "" },
                        { "subscription_type", SubscriptionType ?? "" }
                    };
                    
                    string json = JsonConvert.SerializeObject(tokenData);
                    
                    // 使用 DPAPI 加密存储（使用应用标识作为额外熵，增强安全性）
                    string encryptedJson = DPAPI.Encrypt(json, "Sidebar-Auth-Token", DataProtectionScope.CurrentUser);
                    
                    File.WriteAllText(tokenPath, encryptedJson);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Token 已加密保存");
#endif
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"保存 Token 失败: {ex.Message}");
#endif
            }
        }
        
        private void ClearToken()
        {
            try
            {
                string tokenPath = GetTokenPath();
                if (File.Exists(tokenPath))
                {
                    File.Delete(tokenPath);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"清除 Token 失败: {ex.Message}");
#endif
            }
        }
        
        private string GetTokenPath()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sidebar");
            return Path.Combine(appDataPath, "auth_token.json");
        }
        
        private string GetDeviceId()
        {
            try
            {
                // 使用机器名和用户名生成设备 ID
                string machineName = Environment.MachineName;
                string userName = Environment.UserName;
                string deviceId = $"{machineName}_{userName}";
                
                // 计算 MD5 哈希
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(deviceId));
                    StringBuilder sb = new StringBuilder();
                    foreach (byte b in hashBytes)
                    {
                        sb.Append(b.ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
            catch
            {
                return Guid.NewGuid().ToString("N");
            }
        }
        
        private string GetDeviceName()
        {
            try
            {
                return $"{Environment.MachineName} - {Environment.UserName}";
            }
            catch
            {
                return "Unknown Device";
            }
        }
        
        private async Task<APIResult> RequestAsync(string method, string endpoint, Dictionary<string, object> data = null, Dictionary<string, string> queryParams = null)
        {
            string url = $"{AUTH_SERVER}/api/{endpoint}";
            
            // 添加查询参数
            if (queryParams != null && queryParams.Count > 0)
            {
                var queryString = new StringBuilder();
                foreach (var param in queryParams)
                {
                    if (queryString.Length > 0) queryString.Append("&");
                    queryString.Append($"{Uri.EscapeDataString(param.Key)}={Uri.EscapeDataString(param.Value)}");
                }
                url += "?" + queryString.ToString();
            }
            
            string lastError = null;
            int delay = RETRY_DELAY;
            
            for (int attempt = 0; attempt < MAX_RETRIES; attempt++)
            {
                try
                {
                    HttpRequestMessage request;
                    
                    if (method.ToUpper() == "GET")
                    {
                        request = new HttpRequestMessage(HttpMethod.Get, url);
                    }
                    else
                    {
                        request = new HttpRequestMessage(HttpMethod.Post, url);
                        
                        if (data != null)
                        {
                            string json = JsonConvert.SerializeObject(data);
                            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                        }
                    }
                    
                    // 添加 Token 到 Header
                    if (!string.IsNullOrEmpty(Token))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
                    }
                    
                    // 使用 CancellationTokenSource 来控制超时，而不是修改共享 HttpClient 的 Timeout 属性
                    using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(TIMEOUT)))
                    {
                        HttpResponseMessage response = await httpClient.SendAsync(request, cts.Token);
                        string responseText = await response.Content.ReadAsStringAsync();
                        
#if DEBUG
                        // 记录响应内容（用于调试）
                        System.Diagnostics.Debug.WriteLine($"API Response [{endpoint}]: Status={response.StatusCode}, Body={responseText.Substring(0, Math.Min(500, responseText.Length))}");
#endif
                        
                        try
                        {
                            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                            
                            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                // verify 接口特殊处理
                                if (endpoint == "verify" && result.ContainsKey("status"))
                                {
                                    string status = result["status"]?.ToString();
                                    return new APIResult
                                    {
                                        Success = true,
                                        Message = result.ContainsKey("message") ? result["message"]?.ToString() : "",
                                        Data = result,
                                        StatusCode = (int)response.StatusCode
                                    };
                                }
                                
                                // 其他接口使用 success 字段
                                bool success = result.ContainsKey("success") && Convert.ToBoolean(result["success"]);
                                string message = result.ContainsKey("message") ? result["message"]?.ToString() : "";
                                
                                // 如果服务器返回了 error 字段，也添加到消息中
                                if (!success && result.ContainsKey("error"))
                                {
                                    string error = result["error"]?.ToString();
                                    if (!string.IsNullOrEmpty(error))
                                    {
                                        message = string.IsNullOrEmpty(message) ? error : $"{message} ({error})";
                                    }
                                }
                                
                                var dataDict = new Dictionary<string, object>();
                                foreach (var kvp in result)
                                {
                                    if (kvp.Key != "success" && kvp.Key != "message" && kvp.Key != "error")
                                    {
                                        dataDict[kvp.Key] = kvp.Value;
                                    }
                                }
                                
                                return new APIResult
                                {
                                    Success = success,
                                    Message = message,
                                    Data = dataDict,
                                    StatusCode = (int)response.StatusCode
                                };
                            }
                            else
                            {
                                string message = result.ContainsKey("message") ? result["message"]?.ToString() : $"HTTP错误 {response.StatusCode}";
                                if (result.ContainsKey("error"))
                                {
                                    string error = result["error"]?.ToString();
                                    if (!string.IsNullOrEmpty(error))
                                    {
                                        message = string.IsNullOrEmpty(message) ? error : $"{message} ({error})";
                                    }
                                }
                                return new APIResult
                                {
                                    Success = false,
                                    Message = message,
                                    Data = result,
                                    StatusCode = (int)response.StatusCode
                                };
                            }
                        }
                        catch (JsonException)
                        {
                            // 尝试解析原始响应文本，可能包含错误信息
                            string errorMessage = $"服务器响应格式错误";
                            if (!string.IsNullOrEmpty(responseText))
                            {
                                if (responseText.Length > 500)
                                {
                                    errorMessage += $": {responseText.Substring(0, 500)}...";
                                }
                                else
                                {
                                    errorMessage += $": {responseText}";
                                }
                            }
                            else
                            {
                                errorMessage += $": HTTP {response.StatusCode}";
                            }
                            
                            return new APIResult
                            {
                                Success = false,
                                Message = errorMessage,
                                StatusCode = (int)response.StatusCode
                            };
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    lastError = "请求超时，请检查网络";
                    if (attempt < MAX_RETRIES - 1)
                    {
                        await Task.Delay(delay);
                        delay *= 2;
                    }
                }
                catch (HttpRequestException ex)
                {
                    lastError = $"网络连接失败: {ex.Message}";
                    if (attempt < MAX_RETRIES - 1)
                    {
                        await Task.Delay(delay);
                        delay *= 2;
                    }
                }
                catch (Exception ex)
                {
                    lastError = $"请求异常: {ex.Message}";
                    break; // 其他异常不重试
                }
            }
            
            return new APIResult
            {
                Success = false,
                Message = lastError ?? "请求失败",
                StatusCode = 0
            };
        }
        
        // ==================== 用户认证 ====================
        
        public async Task<APIResult> RegisterAsync(string phone, string password)
        {
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", phone },
                { "password", password }
            };
            
            var result = await RequestAsync("POST", "register", data);
            
            if (result.Success && result.Data.ContainsKey("token"))
            {
                Phone = phone;
                Token = result.Data["token"]?.ToString();
                Expires = result.Data.ContainsKey("expires_at") ? result.Data["expires_at"]?.ToString() : null;
                
                // 尝试从 subscription 中获取订阅类型
                if (result.Data.ContainsKey("subscription"))
                {
                    try
                    {
                        var subscriptionJson = JsonConvert.SerializeObject(result.Data["subscription"]);
                        var subscription = JsonConvert.DeserializeObject<Dictionary<string, object>>(subscriptionJson);
                        if (subscription != null && subscription.ContainsKey("type"))
                        {
                            SubscriptionType = subscription["type"]?.ToString();
                        }
                    }
                    catch
                    {
                        // 解析失败时忽略
                    }
                }
                
                SaveToken();
            }
            
            return result;
        }
        
        public async Task<APIResult> LoginAsync(string phone, string password)
        {
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", phone },
                { "password", password },
                { "device_id", GetDeviceId() },
                { "device_name", GetDeviceName() }
            };
            
            var result = await RequestAsync("POST", "login", data);
            
            if (result.Success && result.Data.ContainsKey("token"))
            {
                Phone = phone;
                Token = result.Data["token"]?.ToString();
                
                // 尝试从 subscription 中获取过期时间和订阅类型
                if (result.Data.ContainsKey("subscription"))
                {
                    try
                    {
                        var subscriptionJson = JsonConvert.SerializeObject(result.Data["subscription"]);
                        var subscription = JsonConvert.DeserializeObject<Dictionary<string, object>>(subscriptionJson);
                        if (subscription != null)
                        {
                            if (subscription.ContainsKey("expire_date"))
                            {
                                Expires = subscription["expire_date"]?.ToString();
                            }
                            if (subscription.ContainsKey("type"))
                            {
                                SubscriptionType = subscription["type"]?.ToString();
                            }
                        }
                    }
                    catch
                    {
                        // 如果解析失败，尝试直接从 expires_at 获取
                        Expires = result.Data.ContainsKey("expires_at") ? result.Data["expires_at"]?.ToString() : null;
                    }
                }
                else
                {
                    Expires = result.Data.ContainsKey("expires_at") ? result.Data["expires_at"]?.ToString() : null;
                }
                
                SaveToken();
            }
            
            return result;
        }
        
        public async Task<APIResult> VerifyAsync()
        {
            if (string.IsNullOrEmpty(Phone))
            {
                return new APIResult { Success = false, Message = "未登录" };
            }
            
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", Phone },
                { "device_id", GetDeviceId() }
            };
            
            var result = await RequestAsync("POST", "verify", data);
            
            if (result.Success)
            {
                if (result.Data.ContainsKey("expire_date"))
                {
                    Expires = result.Data["expire_date"]?.ToString();
                }
                
                // 尝试从 subscription 中获取订阅类型
                if (result.Data.ContainsKey("subscription"))
                {
                    try
                    {
                        var subscriptionJson = JsonConvert.SerializeObject(result.Data["subscription"]);
                        var subscription = JsonConvert.DeserializeObject<Dictionary<string, object>>(subscriptionJson);
                        if (subscription != null && subscription.ContainsKey("type"))
                        {
                            SubscriptionType = subscription["type"]?.ToString();
                        }
                    }
                    catch
                    {
                        // 解析失败时忽略
                    }
                }
                
                SaveToken();
            }
            
            return result;
        }
        
        public void Logout()
        {
            Phone = null;
            Token = null;
            Expires = null;
            SubscriptionType = null;
            ClearToken();
        }
        
        /// <summary>
        /// 检查订阅是否有效（包括试用期检查）
        /// </summary>
        /// <returns>true 表示订阅有效，false 表示已过期或未登录</returns>
        public bool IsSubscriptionValid()
        {
            // 永久会员始终有效
            if (SubscriptionType == "plan2")
            {
                return true;
            }
            
            // 已登录用户：优先使用服务器返回的订阅信息
            if (IsLoggedIn)
            {
                // 如果有服务器返回的过期时间，使用服务器数据
                if (!string.IsNullOrEmpty(Expires))
                {
                    if (DateTime.TryParse(Expires, out DateTime expireDate))
                    {
                        bool isValid = DateTime.Now < expireDate;
                        
                        // 如果服务器订阅有效，清除本地试用期记录（避免混淆）
                        if (isValid && SubscriptionType != "trial")
                        {
                            ClearTrialPeriod();
                        }
                        
                        return isValid;
                    }
                }
                
                // 如果服务器没有过期时间，可能是试用期，检查本地试用期
                // 但只有在订阅类型为 trial 或为空时才使用本地试用期
                if (string.IsNullOrEmpty(SubscriptionType) || SubscriptionType == "trial")
                {
                    return CheckLocalTrialPeriod();
                }
                
                // 其他情况（如 plan1 但没有过期时间），认为无效
                return false;
            }
            
            // 未登录用户：使用本地试用期
            return CheckLocalTrialPeriod();
        }
        
        /// <summary>
        /// 检查本地试用期（60天）
        /// </summary>
        private bool CheckLocalTrialPeriod()
        {
            try
            {
                string trialPath = GetTrialPeriodPath();
                if (File.Exists(trialPath))
                {
                    string content = File.ReadAllText(trialPath);
                    string json = null;
                    
                    // 尝试解密（新格式：加密存储）
                    try
                    {
                        json = DPAPI.Decrypt(content, "Sidebar-Trial-Period", DataProtectionScope.CurrentUser);
                    }
                    catch
                    {
                        // 如果不是加密格式，按旧格式处理（向后兼容）
                        try
                        {
                            var testData = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                            if (testData != null)
                            {
                                json = content;
                                // 自动迁移为加密格式
                                SaveTrialPeriodStart();
                            }
                        }
                        catch
                        {
                            // 文件格式无效
                            return false;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(json))
                    {
                        var trialData = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (trialData != null && trialData.ContainsKey("start_date"))
                        {
                            if (DateTime.TryParse(trialData["start_date"]?.ToString(), out DateTime startDate))
                            {
                                DateTime endDate = startDate.AddDays(60); // 60天试用期
                                return DateTime.Now < endDate;
                            }
                        }
                    }
                }
                else
                {
                    // 首次使用，创建试用期记录
                    SaveTrialPeriodStart();
                    return true; // 首次使用，试用期有效
                }
            }
            catch
            {
                // 出错时默认允许使用（避免误锁）
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 保存试用期开始时间
        /// </summary>
        private void SaveTrialPeriodStart()
        {
            try
            {
                string trialPath = GetTrialPeriodPath();
                string trialDir = Path.GetDirectoryName(trialPath);
                if (!Directory.Exists(trialDir))
                {
                    Directory.CreateDirectory(trialDir);
                }
                
                var trialData = new Dictionary<string, string>
                {
                    { "start_date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
                };
                
                string json = JsonConvert.SerializeObject(trialData);
                
                // 使用 DPAPI 加密存储
                string encryptedJson = DPAPI.Encrypt(json, "Sidebar-Trial-Period", DataProtectionScope.CurrentUser);
                
                File.WriteAllText(trialPath, encryptedJson);
            }
            catch
            {
                // 保存失败时忽略
            }
        }
        
        /// <summary>
        /// 获取试用期文件路径
        /// </summary>
        private string GetTrialPeriodPath()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sidebar");
            return Path.Combine(appDataPath, "trial_period.json");
        }
        
        /// <summary>
        /// 获取剩余试用期天数
        /// </summary>
        public int GetRemainingTrialDays()
        {
            // 已登录用户：如果服务器有订阅信息，不使用本地试用期
            if (IsLoggedIn && !string.IsNullOrEmpty(Expires))
            {
                if (DateTime.TryParse(Expires, out DateTime expireDate))
                {
                    int remainingDays = (int)(expireDate - DateTime.Now).TotalDays;
                    return Math.Max(0, remainingDays);
                }
            }
            
            // 未登录或服务器无数据：使用本地试用期
            try
            {
                string trialPath = GetTrialPeriodPath();
                if (File.Exists(trialPath))
                {
                    string content = File.ReadAllText(trialPath);
                    string json = null;
                    
                    // 尝试解密（新格式：加密存储）
                    try
                    {
                        json = DPAPI.Decrypt(content, "Sidebar-Trial-Period", DataProtectionScope.CurrentUser);
                    }
                    catch
                    {
                        // 如果不是加密格式，按旧格式处理（向后兼容）
                        try
                        {
                            var testData = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                            if (testData != null)
                            {
                                json = content;
                            }
                        }
                        catch
                        {
                            // 文件格式无效
                            return 0;
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(json))
                    {
                        var trialData = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (trialData != null && trialData.ContainsKey("start_date"))
                        {
                            if (DateTime.TryParse(trialData["start_date"]?.ToString(), out DateTime startDate))
                            {
                                DateTime endDate = startDate.AddDays(60);
                                int remainingDays = (int)(endDate - DateTime.Now).TotalDays;
                                return Math.Max(0, remainingDays);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"获取剩余试用期失败：{ex.Message}");
#endif
            }
            
            return 0;
        }
        
        /// <summary>
        /// 清除本地试用期记录（购买套餐后调用）
        /// </summary>
        public void ClearTrialPeriod()
        {
            try
            {
                string trialPath = GetTrialPeriodPath();
                if (File.Exists(trialPath))
                {
                    File.Delete(trialPath);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("已清除本地试用期记录");
#endif
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"清除试用期记录失败：{ex.Message}");
#endif
            }
        }
        
        /// <summary>
        /// 刷新订阅状态（支付成功后调用）
        /// </summary>
        public async Task<bool> RefreshSubscriptionAsync()
        {
            if (!IsLoggedIn)
            {
                return false;
            }
            
            try
            {
                var result = await VerifyAsync();
                if (result.Success)
                {
                    // 如果订阅有效且不是试用期，清除本地试用期记录
                    if (IsSubscriptionValid() && SubscriptionType != "trial" && !string.IsNullOrEmpty(SubscriptionType))
                    {
                        ClearTrialPeriod();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"刷新订阅状态失败：{ex.Message}");
#endif
            }
            
            return false;
        }
        
        // ==================== 密码重置（通过已绑定设备） ====================
        
        /// <summary>
        /// 使用已绑定设备重置密码
        /// </summary>
        public async Task<APIResult> ResetPasswordAsync(string phone, string newPassword)
        {
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", phone },
                { "new_password", newPassword },
                { "device_id", GetDeviceId() }
            };
            
            return await RequestAsync("POST", "reset-password", data);
        }
        
        // ==================== 设备管理 ====================
        
        public async Task<APIResult> GetDevicesAsync()
        {
            if (!IsLoggedIn)
            {
                return new APIResult { Success = false, Message = "未登录" };
            }
            
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", Phone },
                { "device_id", GetDeviceId() }
            };
            
            return await RequestAsync("POST", "devices/list", data);
        }
        
        public async Task<APIResult> UnbindDeviceAsync(string targetDeviceId)
        {
            if (!IsLoggedIn)
            {
                return new APIResult { Success = false, Message = "未登录" };
            }
            
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", Phone },
                { "device_id", GetDeviceId() },
                { "target_device_id", targetDeviceId }
            };
            
            return await RequestAsync("POST", "devices/unbind", data);
        }
        
        // ==================== 应用商城 ====================
        
        public async Task<APIResult> BrowseAppsAsync()
        {
            return await RequestAsync("GET", "apps/browse", null, null);
        }
        
        // ==================== 支付管理 ====================
        
        public async Task<APIResult> GetPlansAsync()
        {
            var queryParams = new Dictionary<string, string>
            {
                { "app_key", APP_KEY }
            };
            
            return await RequestAsync("GET", "plans", null, queryParams);
        }
        
        public async Task<APIResult> CreatePaymentAsync(string planId, bool checkOnly = false)
        {
            if (!IsLoggedIn)
            {
                return new APIResult { Success = false, Message = "未登录" };
            }
            
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", Phone },
                { "plan_id", planId },
                { "check_only", checkOnly }
            };
            
            return await RequestAsync("POST", "payment/create", data);
        }
        
        public async Task<APIResult> ConfirmPaymentAsync(string orderId)
        {
            if (!IsLoggedIn)
            {
                return new APIResult { Success = false, Message = "未登录" };
            }
            
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", Phone },
                { "order_id", orderId }
            };
            
            var result = await RequestAsync("POST", "payment/confirm", data);
            
            if (result.Success)
            {
                // 更新过期时间
                if (result.Data.ContainsKey("expire_date"))
                {
                    Expires = result.Data["expire_date"]?.ToString();
                }
                
                // 尝试从 subscription 中获取订阅类型
                if (result.Data.ContainsKey("subscription"))
                {
                    try
                    {
                        var subscriptionJson = JsonConvert.SerializeObject(result.Data["subscription"]);
                        var subscription = JsonConvert.DeserializeObject<Dictionary<string, object>>(subscriptionJson);
                        if (subscription != null && subscription.ContainsKey("type"))
                        {
                            SubscriptionType = subscription["type"]?.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        System.Diagnostics.Debug.WriteLine($"解析订阅信息失败：{ex.Message}");
#endif
                    }
                }
                
                // 保存Token（包含新的订阅信息）
                SaveToken();
                
                // 如果购买成功且不是试用期，清除本地试用期记录
                if (SubscriptionType != "trial" && !string.IsNullOrEmpty(SubscriptionType))
                {
                    ClearTrialPeriod();
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"支付成功，订阅类型：{SubscriptionType}，已清除本地试用期记录");
#endif
                }
            }
            
            return result;
        }
        
        public async Task<APIResult> CheckPaymentStatusAsync(string orderId)
        {
            if (!IsLoggedIn)
            {
                return new APIResult { Success = false, Message = "未登录" };
            }
            
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", Phone },
                { "order_id", orderId }
            };
            
            return await RequestAsync("POST", "payment/status", data);
        }
        
        public async Task<APIResult> GetPaymentOrdersAsync()
        {
            if (!IsLoggedIn)
            {
                return new APIResult { Success = false, Message = "未登录" };
            }
            
            // 参考客户端：只发送 app_key 和 phone，token 是可选的
            // 服务端代码中 verify_user_token 函数不存在，会导致服务器内部错误
            // 根据参考客户端代码，只发送 app_key 和 phone，不发送 token
            var data = new Dictionary<string, object>
            {
                { "app_key", APP_KEY },
                { "phone", Phone }
            };
            
            // 参考客户端代码中，如果已登录会添加 token，但服务端代码中 verify_user_token 函数未定义
            // 会导致服务器内部错误。根据参考客户端，token 是可选的，暂时不发送
            // 如果服务端修复了 verify_user_token 函数，可以取消下面的注释
            // if (!string.IsNullOrEmpty(Token))
            // {
            //     data["token"] = Token;
            // }
            
            return await RequestAsync("POST", "payment/orders", data);
        }
        
        // ==================== 软件更新 ====================
        
        /// <summary>
        /// 检查软件更新（根据服务端实现：使用Header传递参数）
        /// </summary>
        /// <param name="currentVersion">当前版本号（如：1.0.0）</param>
        public async Task<APIResult> CheckUpdateAsync(string currentVersion)
        {
            // 服务端API: GET /api/update/check
            // 使用Header传递参数：X-App-Key, X-Version
            string url = $"{AUTH_SERVER}/api/update/check";
            
            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(TIMEOUT)))
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                    
                    // 添加Header参数（服务端要求）
                    request.Headers.Add("X-App-Key", APP_KEY);
                    request.Headers.Add("X-Version", currentVersion);
                    
                    // 添加Token到Header（如果已登录）
                    if (!string.IsNullOrEmpty(Token))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
                    }
                    
                    HttpResponseMessage response = await httpClient.SendAsync(request, cts.Token);
                    string responseText = await response.Content.ReadAsStringAsync();
                    
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Update Check Response: Status={response.StatusCode}, Body={responseText.Substring(0, Math.Min(500, responseText.Length))}");
#endif
                    
                    try
                    {
                        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            bool success = result.ContainsKey("success") && Convert.ToBoolean(result["success"]);
                            string message = result.ContainsKey("message") ? result["message"]?.ToString() : "";
                            
                            var dataDict = new Dictionary<string, object>();
                            if (result.ContainsKey("data") && result["data"] != null)
                            {
                                var dataJson = JsonConvert.SerializeObject(result["data"]);
                                var dataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                                if (dataObj != null)
                                {
                                    dataDict = dataObj;
                                }
                            }
                            
                            return new APIResult
                            {
                                Success = success,
                                Message = message,
                                Data = dataDict,
                                StatusCode = (int)response.StatusCode
                            };
                        }
                        else
                        {
                            string errorMessage = result.ContainsKey("message") ? result["message"]?.ToString() : $"HTTP错误 {response.StatusCode}";
                            return new APIResult
                            {
                                Success = false,
                                Message = errorMessage,
                                Data = result,
                                StatusCode = (int)response.StatusCode
                            };
                        }
                    }
                    catch (JsonException)
                    {
                        return new APIResult
                        {
                            Success = false,
                            Message = $"服务器响应格式错误: {responseText.Substring(0, Math.Min(500, responseText.Length))}",
                            StatusCode = (int)response.StatusCode
                        };
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return new APIResult { Success = false, Message = "请求超时，请检查网络", StatusCode = 0 };
            }
            catch (Exception ex)
            {
                return new APIResult { Success = false, Message = $"请求异常: {ex.Message}", StatusCode = 0 };
            }
        }
        
        // ==================== 通知管理 ====================
        
        /// <summary>
        /// 获取通知（根据服务端实现：使用Header传递参数，返回单个通知）
        /// </summary>
        public async Task<APIResult> GetNotificationsAsync()
        {
            // 服务端API: GET /api/notification/get
            // 使用Header传递参数：X-App-Key, X-Version, X-User-Type
            string url = $"{AUTH_SERVER}/api/notification/get";
            
            try
            {
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(TIMEOUT)))
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                    
                    // 添加Header参数（服务端要求）
                    request.Headers.Add("X-App-Key", APP_KEY);
                    
                    // 获取当前版本
                    string currentVersion = ShareX.HelpersLib.Helpers.GetApplicationVersion();
                    request.Headers.Add("X-Version", currentVersion);
                    
                    // 确定用户类型
                    string userType = "trial";
                    if (SubscriptionType == "plan1")
                    {
                        userType = "yearly";
                    }
                    else if (SubscriptionType == "plan2")
                    {
                        userType = "lifetime";
                    }
                    request.Headers.Add("X-User-Type", userType);
                    
                    // 添加Token到Header（如果已登录）
                    if (!string.IsNullOrEmpty(Token))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Token);
                    }
                    
                    HttpResponseMessage response = await httpClient.SendAsync(request, cts.Token);
                    string responseText = await response.Content.ReadAsStringAsync();
                    
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"Notification Response: Status={response.StatusCode}, Body={responseText.Substring(0, Math.Min(500, responseText.Length))}");
#endif
                    
                    try
                    {
                        var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseText);
                        
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            bool success = result.ContainsKey("success") && Convert.ToBoolean(result["success"]);
                            string message = result.ContainsKey("message") ? result["message"]?.ToString() : "";
                            
                            var dataDict = new Dictionary<string, object>();
                            if (result.ContainsKey("data") && result["data"] != null)
                            {
                                // 服务端可能返回 data: null（没有通知）
                                var dataValue = result["data"];
                                if (dataValue != null)
                                {
                                    var dataJson = JsonConvert.SerializeObject(dataValue);
                                    var dataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                                    if (dataObj != null)
                                    {
                                        dataDict = dataObj;
                                    }
                                }
                            }
                            
                            return new APIResult
                            {
                                Success = success,
                                Message = message,
                                Data = dataDict,
                                StatusCode = (int)response.StatusCode
                            };
                        }
                        else
                        {
                            string errorMessage = result.ContainsKey("message") ? result["message"]?.ToString() : $"HTTP错误 {response.StatusCode}";
                            return new APIResult
                            {
                                Success = false,
                                Message = errorMessage,
                                Data = result,
                                StatusCode = (int)response.StatusCode
                            };
                        }
                    }
                    catch (JsonException)
                    {
                        return new APIResult
                        {
                            Success = false,
                            Message = $"服务器响应格式错误: {responseText.Substring(0, Math.Min(500, responseText.Length))}",
                            StatusCode = (int)response.StatusCode
                        };
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return new APIResult { Success = false, Message = "请求超时，请检查网络", StatusCode = 0 };
            }
            catch (Exception ex)
            {
                return new APIResult { Success = false, Message = $"请求异常: {ex.Message}", StatusCode = 0 };
            }
        }
    }
}

