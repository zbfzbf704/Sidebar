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
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ShareX.HelpersLib;

namespace Sidebar
{
    internal static class Program
    {
        public const string AppName = "SideBar";
        public const string MutexName = "SideBar-SingleInstance-82E6AC09-0FEF-4390-AD9F-0DD3F5561EFC";
        public static readonly string PipeName = $"{Environment.MachineName}-{Environment.UserName}-{AppName}";
        
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // 启用视觉样式
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 高 DPI 支持
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            
            // 单实例检查：如果已有实例运行，静默退出
            using (SingleInstanceManager singleInstanceManager = new SingleInstanceManager(MutexName, PipeName, true, args))
            {
                // 如果不是第一个实例，SingleInstanceManager 会自动将参数发送给第一个实例并退出
                if (!singleInstanceManager.IsFirstInstance)
                {
                    // 静默退出，不显示任何消息
                    return;
                }
            
            // 加载自定义图标并替换 ShareX 默认图标
            LoadCustomIcon();
            
            // 创建并显示侧边栏
            SidebarForm sidebar = new SidebarForm();
            sidebar.Show();
            
            Application.Run();
            }
        }
        
        /// <summary>
        /// 加载自定义图标并设置为 ShareX 全局图标
        /// </summary>
        private static void LoadCustomIcon()
        {
            try
            {
                // 尝试多个可能的路径
                string[] possiblePaths = new string[]
                {
                    Path.Combine(Application.StartupPath, "icons", "ico.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", "ico.png"),
                    Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "icons", "ico.png"),
                    @"C:\Users\zbfzb\Documents\projects\Sidebar\icons\ico.png" // 开发时的绝对路径
                };
                
                string iconPath = null;
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        iconPath = path;
                        break;
                    }
                }
                
                if (!string.IsNullOrEmpty(iconPath))
                {
                    // 从 PNG 文件创建 Icon
                    using (Bitmap bitmap = new Bitmap(iconPath))
                    {
                        // 创建多个尺寸的图标（Windows 图标通常需要多个尺寸）
                        // 使用 32x32 作为主要尺寸，并创建 16x16 版本
                        Icon customIcon = null;
                        
                        try
                        {
                            // 方法1：尝试直接使用 GetHicon（适用于简单情况）
                            IntPtr hIcon = bitmap.GetHicon();
                            Icon tempIcon = Icon.FromHandle(hIcon);
                            
                            // 创建新的 Icon 对象以避免句柄问题
                            using (MemoryStream ms = new MemoryStream())
                            {
                                // 保存为 ICO 格式
                                tempIcon.Save(ms);
                                ms.Position = 0;
                                customIcon = new Icon(ms);
                            }
                            
                            // 清理临时图标
                            tempIcon.Dispose();
                            DeleteObject(hIcon); // 删除 GDI 对象
                        }
                        catch
                        {
                            // 如果方法1失败，尝试创建简单的图标
                            try
                            {
                                // 调整到标准图标尺寸（32x32）
                                Bitmap resizedBitmap = new Bitmap(bitmap, 32, 32);
                                IntPtr hIcon = resizedBitmap.GetHicon();
                                customIcon = Icon.FromHandle(hIcon);
                                
                                // 克隆以避免句柄问题
                                Icon clonedIcon = (Icon)customIcon.Clone();
                                customIcon.Dispose();
                                customIcon = clonedIcon;
                                
                                resizedBitmap.Dispose();
                                DeleteObject(hIcon);
                            }
                            catch
                            {
                                // 如果都失败，使用默认图标
                            }
                        }
                        
                        if (customIcon != null)
                        {
                            // 设置为 ShareX 全局图标（所有使用 ShareXResources.ApplyTheme 的窗口都会使用此图标）
                            ShareXResources.Icon = customIcon;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果加载失败，使用默认的 ShareX 图标（静默失败）
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"加载自定义图标失败: {ex.Message}");
#endif
            }
        }
        
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}

