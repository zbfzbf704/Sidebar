#region Copyright Information

/*
    Copyright (c) 2025 蝴蝶哥
    Email: your-email@example.com
    
    This code is part of the Sidebar application.
    All rights reserved.
*/

#endregion Copyright Information

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShareX;
using ShareX.HelpersLib;
using Newtonsoft.Json;

namespace Sidebar
{
    public partial class DesktopForm : Form
    {
        #region Windows API Declarations
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT Point);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int ULW_ALPHA = 0x00000002;
        private const uint WM_ACTIVATE = 0x0006;
        private const uint WA_INACTIVE = 0;
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;

            public POINT(int x, int y)
            {
                this.x = x;
                this.y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;

            public SIZE(int cx, int cy)
            {
                this.cx = cx;
                this.cy = cy;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        #endregion

        // 常量
        private const int INITIAL_WIDTH = 460;
        private const int INITIAL_HEIGHT = 280;
        private const int MAX_HEIGHT = 600; // 最大高度，超过后显示滚动条
        private const int CORNER_RADIUS = 16;
        private const int SHADOW_SIZE = 8;
        private const int ICON_SIZE = 48;
        private const int PADDING = 20;
        private const int CATEGORY_ICON_GAP = 15; // 分类栏和图标区域之间的间距
        private const int TEXT_HEIGHT = 42; // 文本区域高度（支持双排，增加高度以更好显示中文）
        private const int TEXT_WIDTH = 70; // 文本区域宽度（超出图标宽度，参考 Windows 显示更多文字）
        // 增大图标间距：图标之间、图标和分类之间都有足够间隔
        private const int ICON_HORIZONTAL_SPACING = 30; // 图标水平间距（图标之间）
        private const int ICON_VERTICAL_SPACING = 15; // 图标垂直间距（图标和文本之间）
        private const int ROW_SPACING = 20; // 行间距（图标行之间的间距）

        // 数据
        private Dictionary<string, List<DesktopItem>> categories = new Dictionary<string, List<DesktopItem>>(); // 分类数据
        private string currentCategory = "桌面"; // 当前分类
        private const string DEFAULT_CATEGORY = "桌面"; // 桌面分类名称（不能删除）
        private List<DesktopItem> items => categories.ContainsKey(currentCategory) ? categories[currentCategory] : new List<DesktopItem>(); // 当前分类的图标
        private string configPath;
        private string storagePath; // 存储桌面文件的目录
        private Color backgroundColor = Color.FromArgb(5, 30, 30, 30); // 与侧边栏一致
        private Font textFont; // 文本字体
        private DesktopItem selectedItem = null; // 当前选中的项目
        private DesktopItem hoveredItem = null; // 当前悬停的项目
        private string selectedCategory = null; // 当前右键选中的分类
        private Dictionary<DesktopItem, float> itemScales = new Dictionary<DesktopItem, float>(); // 图标缩放值
        private Timer animationTimer; // 动画定时器
        private Timer autoCloseTimer; // 自动关闭定时器（鼠标离开窗口后5秒关闭，操作中延长至10秒）
        private Timer dragDetectionTimer; // 拖拽检测定时器（用于延迟关闭窗口，检测是否真的在拖拽）
        private bool isSidebarLeft = false; // 侧边栏是否在左侧
        private bool isMouseInside = false; // 鼠标是否在窗口内（初始为false，因为窗口刚显示时鼠标可能不在窗口内）
        private bool isOperationInProgress = false; // 是否有正在进行的操作（拖拽、添加分类、重命名、删除等）

        // 布局
        private int currentRowCount = 0;
        private int currentColCount = 0;
        private const int CATEGORY_BAR_WIDTH = 60; // 分类栏宽度（竖排）
        private int scrollOffsetY = 0; // 垂直滚动偏移量
        private int maxScrollOffset = 0; // 最大滚动偏移量
        private Point lastMousePos; // 上次鼠标位置（用于滚动）

        public DesktopForm()
        {
            InitializeComponent();
            
            // 初始化字体（使用系统默认字体，支持中文，避免重影）
            try
            {
                // 使用系统默认字体，通常能很好地支持中文
                textFont = new Font(SystemFonts.DefaultFont.FontFamily, 9F, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                // 如果失败，使用微软雅黑
                textFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            }
            
            // 初始化动画定时器
            animationTimer = new Timer();
            animationTimer.Interval = 16; // ~60fps
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
            
            // 初始化自动关闭定时器（鼠标无操作5秒后关闭，操作中延长至10秒）
            autoCloseTimer = new Timer();
            autoCloseTimer.Interval = 5000; // 5秒
            autoCloseTimer.Tick += AutoCloseTimer_Tick;
            
            // 初始化拖拽检测定时器（用于延迟关闭窗口，检测是否真的在拖拽）
            dragDetectionTimer = new Timer();
            dragDetectionTimer.Interval = 100; // 100ms周期性检查
            dragDetectionTimer.Tick += DragDetectionTimer_Tick;
            
            // 初始化分类
            if (!categories.ContainsKey(DEFAULT_CATEGORY))
            {
                categories[DEFAULT_CATEGORY] = new List<DesktopItem>();
            }
            
            LoadItems();
            UpdateWindowSize();
            
            // 确保窗口句柄创建后再显示
            this.HandleCreated += DesktopForm_HandleCreated;
        }
        
        /// <summary>
        /// 自动关闭定时器事件：鼠标离开窗口5秒后关闭窗口（操作中不关闭）
        /// </summary>
        private void AutoCloseTimer_Tick(object sender, EventArgs e)
        {
            // 如果鼠标在窗口内或有操作进行中，停止定时器，不关闭窗口
            if (isMouseInside || isOperationInProgress)
            {
                StopAutoCloseTimer();
                return;
            }
            
            // 只有在鼠标不在窗口内且没有操作进行时才关闭
            StopAutoCloseTimer();
            this.Hide();
        }
        
        /// <summary>
        /// 拖拽检测定时器事件：周期性检查，延迟关闭窗口，检测是否真的在拖拽
        /// </summary>
        private void DragDetectionTimer_Tick(object sender, EventArgs e)
        {
            // 如果已经有操作进行中（比如已经触发了DragEnter），停止定时器，不关闭窗口
            if (isOperationInProgress)
            {
                dragDetectionTimer?.Stop();
                return;
            }
            
            // 检查鼠标是否进入窗口区域
            Point mousePos = Control.MousePosition;
            Rectangle windowRect = new Rectangle(this.Location, this.Size);
            
            if (windowRect.Contains(mousePos))
            {
                // 鼠标进入窗口区域，可能是拖拽操作，停止定时器，不关闭窗口
                dragDetectionTimer?.Stop();
                return;
            }
            
            // 检查鼠标按键是否仍然被按下
            bool mouseButtonPressed = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 || 
                                     (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
            
            if (!mouseButtonPressed)
            {
                // 鼠标按键已释放，且没有操作进行中，说明不是拖拽，关闭窗口
                dragDetectionTimer?.Stop();
                StopAutoCloseTimer();
                this.Hide();
            }
            // 如果鼠标按键仍然被按下，继续等待（定时器会继续触发）
        }
        
        /// <summary>
        /// 停止自动关闭定时器
        /// </summary>
        private void StopAutoCloseTimer()
        {
            autoCloseTimer?.Stop();
        }
        
        /// <summary>
        /// 启动自动关闭定时器（只有在鼠标不在窗口内且没有操作进行时才启动）
        /// </summary>
        private void StartAutoCloseTimer(int interval)
        {
            if (autoCloseTimer != null && this.Visible && !isMouseInside && !isOperationInProgress)
            {
                StopAutoCloseTimer();
                autoCloseTimer.Interval = interval;
                autoCloseTimer.Start();
            }
        }
        
        /// <summary>
        /// 重置自动关闭定时器（鼠标在窗口内有操作时调用，停止定时器）
        /// </summary>
        private void ResetAutoCloseTimer()
        {
            StopAutoCloseTimer();
        }
        
        /// <summary>
        /// 开始操作（拖拽、添加分类、重命名、删除等）
        /// </summary>
        private void StartOperation()
        {
            isOperationInProgress = true;
            StopAutoCloseTimer();
        }
        
        /// <summary>
        /// 结束操作（拖拽、添加分类、重命名、删除等）
        /// </summary>
        private void EndOperation()
        {
            isOperationInProgress = false;
            if (!isMouseInside)
            {
                StartAutoCloseTimer(5000);
            }
            else
            {
                StopAutoCloseTimer();
            }
        }
        
        /// <summary>
        /// 检查自动关闭定时器是否正在运行
        /// </summary>
        private bool IsAutoCloseTimerRunning()
        {
            return autoCloseTimer != null && autoCloseTimer.Enabled;
        }
        
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            bool needsUpdate = false;
            
            // 更新所有图标的缩放动画
            foreach (var item in items)
            {
                float currentScale = itemScales.ContainsKey(item) ? itemScales[item] : 1.0f;
                float targetScale = (item == hoveredItem) ? 1.15f : 1.0f; // 悬停时放大到1.15倍
                
                if (Math.Abs(currentScale - targetScale) > 0.01f)
                {
                    // 平滑过渡
                    float step = 0.1f;
                    if (currentScale < targetScale)
                    {
                        currentScale = Math.Min(currentScale + step, targetScale);
                    }
                    else
                    {
                        currentScale = Math.Max(currentScale - step, targetScale);
                    }
                    
                    itemScales[item] = currentScale;
                    needsUpdate = true;
                }
            }
            
            if (needsUpdate && IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }
        
        private void DesktopForm_HandleCreated(object sender, EventArgs e)
        {
            // 窗口句柄创建后，立即更新一次显示
            if (IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x80000;   // WS_EX_LAYERED
                return cp;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "桌面图标管理";
            this.Size = new Size(INITIAL_WIDTH, INITIAL_HEIGHT);
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.AllowDrop = true;

            // 获取配置文件路径和存储路径
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sidebar");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            configPath = Path.Combine(appDataPath, "desktop_items.json");
            
            // 创建存储桌面文件的目录
            storagePath = Path.Combine(appDataPath, "DesktopFiles");
            if (!Directory.Exists(storagePath))
            {
                Directory.CreateDirectory(storagePath);
            }

            // 事件
            this.DragEnter += DesktopForm_DragEnter;
            this.DragOver += DesktopForm_DragOver;
            this.DragDrop += DesktopForm_DragDrop;
            this.DragLeave += DesktopForm_DragLeave;
            this.Paint += DesktopForm_Paint;
            this.MouseClick += DesktopForm_MouseClick;
            this.MouseMove += DesktopForm_MouseMove;
            this.MouseDown += DesktopForm_MouseDown;
            this.MouseEnter += DesktopForm_MouseEnter;
            this.MouseLeave += DesktopForm_MouseLeave;
            this.MouseWheel += DesktopForm_MouseWheel;
        }
        
        /// <summary>
        /// 鼠标进入窗口时停止自动关闭定时器
        /// </summary>
        private void DesktopForm_MouseEnter(object sender, EventArgs e)
        {
            isMouseInside = true;
            StopAutoCloseTimer();
        }
        
        /// <summary>
        /// 鼠标离开窗口时启动自动关闭定时器（从离开开始计算5秒后关闭）
        /// </summary>
        private void DesktopForm_MouseLeave(object sender, EventArgs e)
        {
            // 如果有操作进行中，不更新状态，也不启动定时器
            if (!isOperationInProgress)
            {
                isMouseInside = false;
                StartAutoCloseTimer(5000);
            }
        }

        // 设置窗口位置（紧贴侧边栏顶部图标）
        public void SetPosition(Point sidebarLocation, int sidebarTopIconY, bool sidebarIsLeft)
        {
            isSidebarLeft = sidebarIsLeft;
            
            // 计算窗口位置：根据侧边栏位置决定
            int x, y;
            if (sidebarIsLeft)
            {
                // 侧边栏在左侧，桌面窗口在右侧
                x = sidebarLocation.X + 70 + 10; // 侧边栏宽度 + 间距
            }
            else
            {
                // 侧边栏在右侧，桌面窗口在左侧
                x = sidebarLocation.X - this.Width - 10; // 侧边栏左侧
            }
            y = sidebarLocation.Y + sidebarTopIconY;
            
            // 确保窗口在屏幕内
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            if (x + this.Width > screenBounds.Right)
            {
                x = screenBounds.Right - this.Width;
            }
            if (x < screenBounds.Left)
            {
                x = screenBounds.Left;
            }
            if (y + this.Height > screenBounds.Bottom)
            {
                y = screenBounds.Bottom - this.Height;
            }
            if (y < screenBounds.Top)
            {
                y = screenBounds.Top;
            }
            
            this.Location = new Point(x, y);
        }

        private void DesktopForm_DragEnter(object sender, DragEventArgs e)
        {
            // 停止拖拽检测定时器（如果正在运行），因为已经确认是拖拽操作
            dragDetectionTimer?.Stop();
            
            // 开始拖拽操作
            StartOperation();
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        /// <summary>
        /// 拖拽悬停事件：持续检测拖拽操作，确保窗口不关闭
        /// </summary>
        private void DesktopForm_DragOver(object sender, DragEventArgs e)
        {
            // 拖拽操作进行中，停止拖拽检测定时器（如果正在运行）
            dragDetectionTimer?.Stop();
            
            // 确保操作状态已设置（作为双重保险）
            if (!isOperationInProgress)
            {
            StartOperation();
            }
            
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void DesktopForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string filePath in files)
                {
                    if (File.Exists(filePath) || Directory.Exists(filePath))
                    {
                        // 检查拖拽规则：
                        // 1. 快捷方式（.lnk 文件）可以拖拽到任何分类
                        // 2. 桌面上的真实文件/文件夹只能拖拽到"桌面"分类
                        bool isShortcut = IsShortcutFile(filePath);
                        bool isFromDesktop = IsFromDesktop(filePath);
                        
                        if (isFromDesktop && !isShortcut && currentCategory != DEFAULT_CATEGORY)
                        {
                            // 桌面上的真实文件/文件夹，只能添加到"桌面"分类
                            ShowNotification(
                                $"桌面上的真实文件/文件夹只能添加到\"{DEFAULT_CATEGORY}\"分类中。\n\n" +
                                $"💡 提示：桌面上的快捷方式可以添加到任何分类。\n\n" +
                                $"请先切换到\"{DEFAULT_CATEGORY}\"分类，然后再拖入此文件。",
                                "提示",
                                4000,
                                MessageBoxIcon.Information
                            );
                            continue;
                        }
                        
                        AddItem(filePath);
                    }
                }
                SaveItems();
                UpdateWindowSize();
                
                // 使用 UpdateLayeredWindow 的窗口需要直接调用 UpdateLayeredWindowBitmap
                if (IsHandleCreated)
                {
                    UpdateLayeredWindowBitmap();
                }
            }
            
            // 拖拽操作结束
            EndOperation();
        }
        
        /// <summary>
        /// 拖拽离开窗口事件
        /// </summary>
        private void DesktopForm_DragLeave(object sender, EventArgs e)
        {
            // 拖拽离开窗口，结束操作
            EndOperation();
        }
        
        /// <summary>
        /// 检查文件是否来自桌面
        /// </summary>
        private bool IsFromDesktop(string filePath)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string normalizedDesktopPath = Path.GetFullPath(desktopPath).TrimEnd('\\');
                string normalizedFilePath = Path.GetFullPath(filePath);
                
                // 检查文件路径是否以桌面路径开头
                return normalizedFilePath.StartsWith(normalizedDesktopPath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 检查文件是否是快捷方式
        /// </summary>
        private bool IsShortcutFile(string filePath)
        {
            try
            {
                // 检查文件扩展名是否为 .lnk（Windows 快捷方式）
                string extension = Path.GetExtension(filePath);
                if (string.IsNullOrEmpty(extension))
                    return false;
                    
                return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 复制文件或文件夹到存储目录
        /// </summary>
        private string CopyToStorage(string sourcePath, string displayName)
        {
            try
            {
                string fileName = Path.GetFileName(sourcePath);
                string extension = Path.GetExtension(fileName);
                string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                
                // 生成唯一文件名（如果已存在则添加序号）
                string destPath = Path.Combine(storagePath, fileName);
                int counter = 1;
                while (File.Exists(destPath) || Directory.Exists(destPath))
                {
                    string newFileName = $"{nameWithoutExt}_{counter}{extension}";
                    destPath = Path.Combine(storagePath, newFileName);
                    counter++;
                }
                
                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destPath, false);
                }
                else if (Directory.Exists(sourcePath))
                {
                    // 复制文件夹
                    CopyDirectory(sourcePath, destPath);
                }
                
                return destPath;
            }
            catch (Exception ex)
            {
                ShowNotification($"复制文件失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                return null;
            }
        }
        
        /// <summary>
        /// 计算文件夹内的文件数量（递归）
        /// </summary>
        private int CountFilesInDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                return 0;
            
            try
            {
                int count = 0;
                // 计算当前目录下的文件数
                count += Directory.GetFiles(directory).Length;
                
                // 递归计算子目录下的文件数
                foreach (string subDir in Directory.GetDirectories(directory))
                {
                    count += CountFilesInDirectory(subDir);
                }
                
                return count;
            }
            catch
            {
                return 0;
            }
        }
        
        /// <summary>
        /// 递归复制目录
        /// </summary>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(destDir))
                return;
                
            if (!Directory.Exists(sourceDir))
                return;
            
            try
            {
                Directory.CreateDirectory(destDir);
                
                foreach (string file in Directory.GetFiles(sourceDir))
                {
                    try
                    {
                        string fileName = Path.GetFileName(file);
                        if (string.IsNullOrEmpty(fileName)) continue;
                        
                        string destFile = Path.Combine(destDir, fileName);
                        File.Copy(file, destFile, false);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"复制文件失败: {file}, 错误: {ex.Message}");
                        // 继续复制其他文件
                    }
                }
                
                foreach (string subDir in Directory.GetDirectories(sourceDir))
                {
                    try
                    {
                        string dirName = Path.GetFileName(subDir);
                        if (string.IsNullOrEmpty(dirName)) continue;
                        
                        string destSubDir = Path.Combine(destDir, dirName);
                        CopyDirectory(subDir, destSubDir);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"复制目录失败: {subDir}, 错误: {ex.Message}");
                        // 继续复制其他目录
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制目录失败: {sourceDir} -> {destDir}, 错误: {ex.Message}");
                throw;
            }
        }

        private void DesktopForm_MouseDown(object sender, MouseEventArgs e)
        {
            ResetAutoCloseTimer(); // 重置自动关闭定时器
            
            if (e.Button == MouseButtons.Right)
            {
                // 检查是否点击了分类栏（使用 IsCategoryBarPoint 方法）
                if (IsCategoryBarPoint(e.Location))
                {
                    // 右键点击分类栏，显示分类菜单
                    HandleCategoryBarRightClick(e.Location);
                    return;
                }
                
                DesktopItem item = GetItemAtPoint(e.Location);
                selectedItem = item;
                
                if (item != null)
                {
                    ShowContextMenu(e.Location);
                }
                else
                {
                    // 空白区域右键，显示备份/还原菜单
                    ShowEmptyAreaContextMenu(e.Location);
                }
            }
        }

        private void DesktopForm_MouseClick(object sender, MouseEventArgs e)
        {
            ResetAutoCloseTimer(); // 重置自动关闭定时器
            
            if (e.Button == MouseButtons.Left)
            {
                // 检查是否点击了"➕"按钮
                if (IsCategoryBarPoint(e.Location))
                {
                    int barWidth = CATEGORY_BAR_WIDTH - 5;
                    int buttonSpacing = 5;
                    int buttonWidth = barWidth - 10;
                    int buttonHeight = 25;
                    int barX, barY;
                    
                    if (isSidebarLeft)
                    {
                        barX = PADDING + SHADOW_SIZE;
                    }
                    else
                    {
                        barX = Width - (PADDING + SHADOW_SIZE) - barWidth;
                    }
                    barY = PADDING + SHADOW_SIZE;
                    
                    EnsureDesktopCategoryFirst();
                    var categoryList = categories.Keys.ToList();
                    int startY = barY + 5;
                    
                    // 检查是否点击了添加按钮
                    int addBtnHeight = 30;
                    int addBtnX = barX + 5;
                    int addBtnY = startY + categoryList.Count * (buttonHeight + buttonSpacing);
                    
                    Rectangle addBtnRect = new Rectangle(addBtnX, addBtnY, buttonWidth, addBtnHeight);
                    if (addBtnRect.Contains(e.Location))
                    {
                        AddNewCategory();
                        return;
                    }
                }
                
                DesktopItem item = GetItemAtPoint(e.Location);
                if (item != null)
                {
                    try
                    {
                        // 根据存储方式决定打开哪个文件
                        string pathToOpen = item.IsRealFile ? item.FilePath : item.OriginalPath ?? item.FilePath;
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = pathToOpen,
                            UseShellExecute = true
                        });
                        // 启动程序后立即关闭窗口
                        this.Hide();
                        StopAutoCloseTimer();
                    }
                    catch (Exception ex)
                    {
                        ShowNotification($"无法打开文件：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                    }
                }
            }
        }
        
        private void ShowContextMenu(Point location)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            
            // 重命名
            ToolStripMenuItem renameItem = new ToolStripMenuItem("重命名");
            renameItem.Click += (s, e) => RenameItem();
            menu.Items.Add(renameItem);
            
            // 复制
            ToolStripMenuItem copyItem = new ToolStripMenuItem("复制");
            copyItem.Click += (s, e) => CopyItem();
            menu.Items.Add(copyItem);
            
            menu.Items.Add(new ToolStripSeparator());
            
            // 移动到其他分类（只有多个分类时才显示）
            if (selectedItem != null && categories.Count > 1)
            {
                ToolStripMenuItem moveToItem = new ToolStripMenuItem("移动到分类");
                
                // 获取当前项目的源文件路径
                string itemSourcePath = selectedItem.OriginalPath ?? selectedItem.FilePath;
                bool isShortcut = IsShortcutFile(itemSourcePath);
                bool isFromDesktop = IsFromDesktop(itemSourcePath);
                bool isDesktopRealFile = isFromDesktop && !isShortcut && selectedItem.IsRealFile;
                
                foreach (var category in categories.Keys)
                {
                    if (category == currentCategory) continue; // 跳过当前分类
                    
                    ToolStripMenuItem categoryItem = new ToolStripMenuItem(category);
                    string targetCategory = category; // 捕获变量
                    
                    // 如果是桌面真实文件，只能移动到"桌面"分类
                    if (isDesktopRealFile && targetCategory != DEFAULT_CATEGORY)
                    {
                        categoryItem.Enabled = false;
                        categoryItem.ToolTipText = "桌面真实文件只能在\"桌面\"分类中";
                    }
                    else
                    {
                        categoryItem.Click += (s, e) => MoveItemToCategory(selectedItem, targetCategory);
                    }
                    
                    moveToItem.DropDownItems.Add(categoryItem);
                }
                
                if (moveToItem.DropDownItems.Count > 0)
                {
                    menu.Items.Add(moveToItem);
                    menu.Items.Add(new ToolStripSeparator());
                }
            }
            
            // 如果是文件夹，添加备份选项
            if (selectedItem != null)
            {
                string itemPath = selectedItem.IsRealFile ? selectedItem.FilePath : (selectedItem.OriginalPath ?? selectedItem.FilePath);
                if (Directory.Exists(itemPath))
                {
                    ToolStripMenuItem backupItem = new ToolStripMenuItem("备份为ZIP");
                    backupItem.Click += (s, e) => BackupFolder(itemPath);
                    menu.Items.Add(backupItem);
                    menu.Items.Add(new ToolStripSeparator());
                }
            }
            
            // 打开文件所在位置
            ToolStripMenuItem openLocationItem = new ToolStripMenuItem("打开文件所在位置");
            openLocationItem.Click += (s, e) => OpenItemLocation();
            menu.Items.Add(openLocationItem);
            
            menu.Items.Add(new ToolStripSeparator());
            
            // 删除
            ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除");
            deleteItem.Click += (s, e) => DeleteItem();
            menu.Items.Add(deleteItem);
            
            menu.Show(this, location);
        }
        
        private void ShowEmptyAreaContextMenu(Point location)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            
            // 备份
            ToolStripMenuItem backupItem = new ToolStripMenuItem("备份");
            backupItem.Click += (s, e) => BackupData();
            menu.Items.Add(backupItem);
            
            // 还原
            ToolStripMenuItem restoreItem = new ToolStripMenuItem("还原");
            restoreItem.Click += (s, e) => RestoreData();
            menu.Items.Add(restoreItem);
            
            menu.Show(this, location);
        }
        
        /// <summary>
        /// 移动图标到指定分类
        /// </summary>
        private void MoveItemToCategory(DesktopItem item, string targetCategory)
        {
            if (item == null || string.IsNullOrEmpty(targetCategory))
                return;
                
            // 开始移动操作
            StartOperation();
            
            try
            {
                // 检查目标分类是否存在
                if (!categories.ContainsKey(targetCategory))
                {
                    categories[targetCategory] = new List<DesktopItem>();
                }
                
                // 从当前分类移除
                if (categories.ContainsKey(currentCategory))
                {
                    categories[currentCategory].Remove(item);
                }
                
                // 添加到目标分类
                categories[targetCategory].Add(item);
                
                // 保存数据
                SaveItems();
                
                // 更新界面
                UpdateWindowSize();
                if (IsHandleCreated)
                {
                    UpdateLayeredWindowBitmap();
                }
                
                ShowNotification($"已将 \"{item.DisplayName}\" 移动到 \"{targetCategory}\" 分类", "成功", 2000);
            }
            catch (Exception ex)
            {
                ShowNotification($"移动失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }
        
        private void BackupData()
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "ZIP 文件 (*.zip)|*.zip",
                    FileName = $"桌面备份_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
                    Title = "选择备份文件保存位置"
                };
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    string zipPath = saveDialog.FileName;
                    
                    // 创建进度窗口
                    ProgressForm progressForm = new ProgressForm("正在备份...");
                    progressForm.Show();
                    Application.DoEvents();
                    
                    // 使用Task异步执行备份
                    Task.Run(() =>
                    {
                        try
                        {
                            string tempDir = Path.Combine(Path.GetTempPath(), $"DesktopBackup_{Guid.NewGuid()}");
                            Directory.CreateDirectory(tempDir);
                            
                            try
                            {
                                // 计算总文件数
                                int totalFiles = 0;
                                foreach (var category in categories)
                                {
                                    foreach (var item in category.Value)
                                    {
                                        if (item.IsRealFile && !string.IsNullOrEmpty(item.FilePath))
                                        {
                                            totalFiles++;
                                        }
                                    }
                                }
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(0, totalFiles + 2, "正在保存分类信息...");
                                }));
                                
                                // 保存分类和文件信息到JSON
                                string dataJson = JsonConvert.SerializeObject(categories, Formatting.Indented);
                                string dataPath = Path.Combine(tempDir, "desktop_data.json");
                                File.WriteAllText(dataPath, dataJson);
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(1, totalFiles + 2, "正在复制文件...");
                                }));
                                
                                // 复制所有真实文件到临时目录
                                string filesDir = Path.Combine(tempDir, "files");
                                Directory.CreateDirectory(filesDir);
                                
                                int currentFile = 1;
                                foreach (var category in categories)
                                {
                                    foreach (var item in category.Value)
                                    {
                                        if (item.IsRealFile && !string.IsNullOrEmpty(item.FilePath))
                                        {
                                            string fileName = Path.GetFileName(item.FilePath);
                                            progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                            {
                                                progressForm.SetProgress(currentFile + 1, totalFiles + 2, $"正在复制：{fileName}");
                                            }));
                                            
                                            // 真实文件：复制文件本身
                                            if (File.Exists(item.FilePath))
                                            {
                                                string relativePath = Path.Combine("files", category.Key, fileName);
                                                string destPath = Path.Combine(tempDir, relativePath);
                                                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                                File.Copy(item.FilePath, destPath, true);
                                            }
                                            else if (Directory.Exists(item.FilePath))
                                            {
                                                string relativePath = Path.Combine("files", category.Key, fileName);
                                                string destPath = Path.Combine(tempDir, relativePath);
                                                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                                                CopyDirectory(item.FilePath, destPath);
                                            }
                                            
                                            currentFile++;
                                        }
                                    }
                                }
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(totalFiles + 1, totalFiles + 2, "正在创建压缩包...");
                                }));
                                
                                // 创建ZIP文件
                                if (File.Exists(zipPath))
                                {
                                    File.Delete(zipPath);
                                }
                                ZipFile.CreateFromDirectory(tempDir, zipPath);
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(totalFiles + 2, totalFiles + 2, "备份完成");
                                }));
                                
                                // 清理临时目录
                                if (Directory.Exists(tempDir))
                                {
                                    try
                                    {
                                        Directory.Delete(tempDir, true);
                                    }
                                    catch { }
                                }
                                
                                // 关闭进度窗口并显示成功消息
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.Close();
                                    ShowNotification(
                                        $"备份成功！\n\n备份文件：{zipPath}\n\n包含 {categories.Count} 个分类，共 {categories.Values.Sum(list => list.Count)} 个文件。",
                                        "备份完成",
                                        4000,
                                        MessageBoxIcon.Information
                                    );
                                }));
                            }
                            catch (Exception ex)
                            {
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.Close();
                                    ShowNotification($"备份失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                            progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                            {
                                progressForm.Close();
                                ShowNotification($"备份失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                            }));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"备份失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// 备份文件夹为ZIP文件
        /// </summary>
        private void BackupFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                ShowNotification("文件夹不存在或路径无效", "错误", 3000, MessageBoxIcon.Error);
                return;
            }
            
            try
            {
                string folderName = Path.GetFileName(folderPath);
                if (string.IsNullOrEmpty(folderName))
                {
                    folderName = "文件夹";
                }
                
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "ZIP 文件 (*.zip)|*.zip",
                    FileName = $"{folderName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
                    Title = "选择备份文件保存位置"
                };
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    string zipPath = saveDialog.FileName;
                    
                    // 创建进度窗口
                    ProgressForm progressForm = new ProgressForm("正在备份文件夹...");
                    progressForm.Show();
                    Application.DoEvents();
                    
                    // 使用Task异步执行备份
                    Task.Run(() =>
                    {
                        try
                        {
                            // 计算总文件数
                            progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                            {
                                progressForm.SetProgress(0, 100, "正在计算文件数量...");
                            }));
                            
                            int totalFiles = CountFilesInDirectory(folderPath);
                            
                            if (totalFiles == 0)
                            {
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.Close();
                                    ShowNotification("文件夹为空，无法备份", "提示", 3000, MessageBoxIcon.Information);
                                }));
                                return;
                            }
                            
                            // 创建临时目录
                            string tempDir = Path.Combine(Path.GetTempPath(), $"FolderBackup_{Guid.NewGuid()}");
                            Directory.CreateDirectory(tempDir);
                            
                            try
                            {
                                // 复制文件夹到临时目录
                                string tempFolderPath = Path.Combine(tempDir, folderName);
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(0, totalFiles + 2, "正在复制文件...");
                                }));
                                
                                // 递归复制文件并更新进度
                                int currentFile = CopyDirectoryWithProgress(folderPath, tempFolderPath, progressForm, 0, totalFiles);
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(totalFiles + 1, totalFiles + 2, "正在创建压缩包...");
                                }));
                                
                                // 创建ZIP文件
                                if (File.Exists(zipPath))
                                {
                                    File.Delete(zipPath);
                                }
                                
                                ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(totalFiles + 2, totalFiles + 2, "备份完成");
                                }));
                                
                                // 清理临时目录
                                if (Directory.Exists(tempDir))
                                {
                                    try
                                    {
                                        Directory.Delete(tempDir, true);
                                    }
                                    catch { }
                                }
                                
                                // 关闭进度窗口并显示成功消息
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.Close();
                                    ShowNotification(
                                        $"备份成功！\n\n备份文件：{zipPath}\n\n包含 {totalFiles} 个文件。",
                                        "备份完成",
                                        4000,
                                        MessageBoxIcon.Information
                                    );
                                }));
                            }
                            catch (Exception ex)
                            {
                                // 清理临时目录
                                if (Directory.Exists(tempDir))
                                {
                                    try
                                    {
                                        Directory.Delete(tempDir, true);
                                    }
                                    catch { }
                                }
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.Close();
                                    ShowNotification($"备份失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                            progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                            {
                                progressForm.Close();
                                ShowNotification($"备份失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                            }));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"备份失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// 递归复制目录并更新进度
        /// </summary>
        private int CopyDirectoryWithProgress(string sourceDir, string destDir, ProgressForm progressForm, int currentFile, int totalFiles)
        {
            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(destDir))
                return currentFile;
                
            if (!Directory.Exists(sourceDir))
                return currentFile;
            
            try
            {
                Directory.CreateDirectory(destDir);
                
                // 复制文件
                string[] files = Directory.GetFiles(sourceDir);
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(destDir, fileName);
                    File.Copy(file, destFile, true);
                    
                    currentFile++;
                    if (progressForm != null && !progressForm.IsDisposed)
                    {
                        progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                        {
                            progressForm.SetProgress(currentFile, totalFiles + 2, $"正在复制：{fileName}");
                        }));
                    }
                }
                
                // 递归复制子目录
                string[] dirs = Directory.GetDirectories(sourceDir);
                foreach (string dir in dirs)
                {
                    string dirName = Path.GetFileName(dir);
                    string destSubDir = Path.Combine(destDir, dirName);
                    currentFile = CopyDirectoryWithProgress(dir, destSubDir, progressForm, currentFile, totalFiles);
                }
                
                return currentFile;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"复制目录失败: {sourceDir} -> {destDir}, 错误: {ex.Message}");
                throw;
            }
        }
        
        private void RestoreData()
        {
            try
            {
                OpenFileDialog openDialog = new OpenFileDialog
                {
                    Filter = "ZIP 文件 (*.zip)|*.zip",
                    Title = "选择要还原的备份文件"
                };
                
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    string zipPath = openDialog.FileName;
                    
                    DialogResult confirm = MessageBox.Show(
                        "还原操作将替换当前所有分类和文件，是否继续？",
                        "确认还原",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2
                    );
                    
                    if (confirm != DialogResult.Yes)
                    {
                        return;
                    }
                    
                    // 创建进度窗口
                    ProgressForm progressForm = new ProgressForm("正在还原...");
                    progressForm.Show();
                    Application.DoEvents();
                    
                    // 使用Task异步执行还原
                    Task.Run(() =>
                    {
                        try
                        {
                            string tempDir = Path.Combine(Path.GetTempPath(), $"DesktopRestore_{Guid.NewGuid()}");
                            Directory.CreateDirectory(tempDir);
                            
                            try
                            {
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(0, 100, "正在解压备份文件...");
                                }));
                                
                                // 解压ZIP文件
                                ZipFile.ExtractToDirectory(zipPath, tempDir);
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(10, 100, "正在读取分类信息...");
                                }));
                                
                                // 读取分类和文件信息
                                string dataPath = Path.Combine(tempDir, "desktop_data.json");
                                if (!File.Exists(dataPath))
                                {
                                    progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                    {
                                        progressForm.Close();
                                        ShowNotification("备份文件格式不正确，缺少数据文件。", "错误", 3000, MessageBoxIcon.Error);
                                    }));
                                    return;
                                }
                                
                                string dataJson = File.ReadAllText(dataPath);
                                var restoredCategories = JsonConvert.DeserializeObject<Dictionary<string, List<DesktopItem>>>(dataJson);
                                
                                if (restoredCategories == null)
                                {
                                    progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                    {
                                        progressForm.Close();
                                        ShowNotification("备份文件格式不正确，无法读取数据。", "错误", 3000, MessageBoxIcon.Error);
                                    }));
                                    return;
                                }
                                
                                // 计算需要恢复的文件数
                                int totalFiles = 0;
                                foreach (var category in restoredCategories)
                                {
                                    foreach (var item in category.Value)
                                    {
                                        if (item.IsRealFile && !string.IsNullOrEmpty(item.FilePath))
                                        {
                                            totalFiles++;
                                        }
                                    }
                                }
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(20, 100, "正在恢复文件...");
                                }));
                                
                                // 恢复真实文件
                                string filesDir = Path.Combine(tempDir, "files");
                                int currentFile = 0;
                                if (Directory.Exists(filesDir))
                                {
                                    foreach (var category in restoredCategories)
                                    {
                                        string categoryFilesDir = Path.Combine(filesDir, category.Key);
                                        if (Directory.Exists(categoryFilesDir))
                                        {
                                            foreach (var item in category.Value)
                                            {
                                                if (item.IsRealFile && !string.IsNullOrEmpty(item.FilePath))
                                                {
                                                    string fileName = Path.GetFileName(item.FilePath);
                                                    int progress = 20 + (int)((double)currentFile / totalFiles * 70);
                                                    progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                                    {
                                                        progressForm.SetProgress(progress, 100, $"正在恢复：{fileName}");
                                                    }));
                                                    
                                                    string sourcePath = Path.Combine(categoryFilesDir, fileName);
                                                    
                                                    if (File.Exists(sourcePath))
                                                    {
                                                        // 恢复文件到存储目录
                                                        string restoredPath = CopyToStorage(sourcePath, fileName);
                                                        if (restoredPath != null)
                                                        {
                                                            item.FilePath = restoredPath;
                                                        }
                                                    }
                                                    else if (Directory.Exists(sourcePath))
                                                    {
                                                        // 恢复文件夹到存储目录
                                                        string restoredPath = CopyToStorage(sourcePath, fileName);
                                                        if (restoredPath != null)
                                                        {
                                                            item.FilePath = restoredPath;
                                                        }
                                                    }
                                                    
                                                    currentFile++;
                                                }
                                            }
                                        }
                                    }
                                }
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(90, 100, "正在应用更改...");
                                }));
                                
                                // 替换当前分类数据
                                categories = restoredCategories;
                                currentCategory = DEFAULT_CATEGORY;
                                if (!categories.ContainsKey(DEFAULT_CATEGORY))
                                {
                                    categories[DEFAULT_CATEGORY] = new List<DesktopItem>();
                                }
                                
                                // 初始化图标缩放值
                                itemScales.Clear();
                                foreach (var category in categories.Values)
                                {
                                    foreach (var item in category)
                                    {
                                        itemScales[item] = 1.0f;
                                    }
                                }
                                
                                // 保存还原后的数据
                                SaveItems();
                                UpdateWindowSize();
                                
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.SetProgress(100, 100, "还原完成");
                                }));
                                
                                if (IsHandleCreated)
                                {
                                    this.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                    {
                                        UpdateLayeredWindowBitmap();
                                    }));
                                }
                                
                                // 关闭进度窗口并显示成功消息
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.Close();
                                    ShowNotification(
                                        $"还原成功！\n\n已恢复 {categories.Count} 个分类，共 {categories.Values.Sum(list => list.Count)} 个文件。",
                                        "还原完成",
                                        4000,
                                        MessageBoxIcon.Information
                                    );
                                }));
                                
                                // 清理临时目录
                                if (Directory.Exists(tempDir))
                                {
                                    try
                                    {
                                        Directory.Delete(tempDir, true);
                                    }
                                    catch { }
                                }
                            }
                            catch (Exception ex)
                            {
                                progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                                {
                                    progressForm.Close();
                                    ShowNotification($"还原失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                            progressForm.Invoke((System.Windows.Forms.MethodInvoker)(() =>
                            {
                                progressForm.Close();
                                ShowNotification($"还原失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                            }));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"还原失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        private void RenameItem()
        {
            if (selectedItem == null) return;
            
            // 开始重命名操作
            StartOperation();
            
            // 创建自定义的小型输入框
            Form inputForm = new Form
            {
                Text = "重命名",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.Manual,
                TopMost = true,
                ShowInTaskbar = false,
                Size = new Size(320, 150), // 增加宽度确保按钮完整显示
                // 定位在桌面窗口附近（右侧）
                Location = new Point(
                    this.Right + 10,
                    this.Top + 50
                )
            };
            
            Label label = new Label
            {
                Text = "请输入新名称：",
                Location = new Point(10, 15),
                Size = new Size(300, 20),
                AutoSize = false
            };
            inputForm.Controls.Add(label);
            
            TextBox txtInput = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(300, 23),
                Text = Path.GetFileNameWithoutExtension(selectedItem.DisplayName)
            };
            txtInput.SelectAll();
            inputForm.Controls.Add(txtInput);
            
            // 应用 ShareX 主题（需要在计算按钮位置之前应用，因为主题可能影响窗口大小）
            ShareXResources.ApplyTheme(inputForm, true);
            
            // 按钮布局：确定和取消按钮，右对齐，间距10像素
            int buttonWidth = 75;
            int buttonHeight = 25;
            int buttonY = 75;
            int buttonSpacing = 10;
            int rightMargin = 10; // 右边距
            
            Button btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Size = new Size(buttonWidth, buttonHeight)
            };
            inputForm.Controls.Add(btnCancel);
            
            Button btnOK = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Size = new Size(buttonWidth, buttonHeight)
            };
            inputForm.Controls.Add(btnOK);
            
            inputForm.AcceptButton = btnOK;
            inputForm.CancelButton = btnCancel;
            
            // 在窗口显示后重新计算按钮位置（确保使用正确的客户区宽度）
            inputForm.Shown += (s, e) =>
            {
                // 使用 ClientSize.Width 而不是 Width，因为 Width 包括边框
                int clientWidth = inputForm.ClientSize.Width;
                int cancelX = clientWidth - rightMargin - buttonWidth;
                int okX = cancelX - buttonSpacing - buttonWidth;
                
                btnCancel.Location = new Point(cancelX, buttonY);
                btnOK.Location = new Point(okX, buttonY);
            };
            
            // 确保窗口在屏幕内
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            if (inputForm.Right > screenBounds.Right)
            {
                // 如果右侧超出，显示在左侧
                inputForm.Left = this.Left - inputForm.Width - 10;
            }
            if (inputForm.Bottom > screenBounds.Bottom)
            {
                inputForm.Top = screenBounds.Bottom - inputForm.Height;
            }
            if (inputForm.Left < screenBounds.Left)
            {
                inputForm.Left = screenBounds.Left;
            }
            if (inputForm.Top < screenBounds.Top)
            {
                inputForm.Top = screenBounds.Top;
            }
            
            DialogResult result = inputForm.ShowDialog(this);
            
            // 重命名操作结束
            EndOperation();
            
            if (result == DialogResult.OK)
            {
                string newName = txtInput.Text.Trim();
                
                if (!string.IsNullOrEmpty(newName) && newName != Path.GetFileNameWithoutExtension(selectedItem.DisplayName))
                {
                    try
                    {
                        // 只对真实文件进行重命名
                        if (selectedItem.IsRealFile)
                        {
                            string directory = Path.GetDirectoryName(selectedItem.FilePath);
                            string extension = Path.GetExtension(selectedItem.FilePath);
                            string newPath = Path.Combine(directory, newName + extension);
                            
                            if (File.Exists(selectedItem.FilePath))
                            {
                                File.Move(selectedItem.FilePath, newPath);
                            }
                            else if (Directory.Exists(selectedItem.FilePath))
                            {
                                Directory.Move(selectedItem.FilePath, newPath);
                            }
                            
                            selectedItem.FilePath = newPath;
                            selectedItem.DisplayName = newName; // 不显示扩展名，仅显示名称
                        }
                        else
                        {
                            // 路径引用类型只更新显示名称（不包含扩展名）
                            selectedItem.DisplayName = newName;
                        }
                        
                        SaveItems();
                        
                        if (IsHandleCreated)
                        {
                            UpdateLayeredWindowBitmap();
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowNotification($"重命名失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                    }
                }
            }
        }
        
        private void CopyItem()
        {
            if (selectedItem == null) return;
            
            try
            {
                // 确定要复制的文件路径
                string pathToCopy = selectedItem.IsRealFile ? selectedItem.FilePath : (selectedItem.OriginalPath ?? selectedItem.FilePath);
                
                // 检查文件或文件夹是否存在
                if (!File.Exists(pathToCopy) && !Directory.Exists(pathToCopy))
                {
                    ShowNotification(
                        $"文件或文件夹不存在：\n{pathToCopy}",
                        "错误",
                        3000,
                        MessageBoxIcon.Error
                    );
                    return;
                }
                
                // 使用文件拖放列表格式复制到剪贴板，这样可以在其他地方粘贴文件
                StringCollection filePaths = new StringCollection();
                filePaths.Add(pathToCopy);
                Clipboard.SetFileDropList(filePaths);
            }
            catch (Exception ex)
            {
                ShowNotification($"复制失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        private void OpenItemLocation()
        {
            if (selectedItem == null) return;
            
            try
            {
                // 根据存储方式决定打开哪个路径
                string pathToOpen = selectedItem.IsRealFile ? selectedItem.FilePath : (selectedItem.OriginalPath ?? selectedItem.FilePath);
                
                if (File.Exists(pathToOpen))
                {
                    NativeMethods.OpenFolderAndSelectFile(pathToOpen);
                }
                else if (Directory.Exists(pathToOpen))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = pathToOpen,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"打开位置失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        private void DeleteItem()
        {
            if (selectedItem == null) return;
            
            // 开始删除操作
            StartOperation();
            
            // 根据文件类型生成不同的提示信息
            string messageText;
            
            if (selectedItem.IsRealFile)
            {
                // 真实文件（桌面文件）：会删除复制的文件
                messageText = $"确定要删除 \"{selectedItem.DisplayName}\" 吗？\n\n" +
                             $"⚠️ 这是真实文件（从桌面复制存储）\n" +
                             $"删除操作将永久删除该文件，无法恢复！\n\n" +
                             $"文件位置：\n{selectedItem.FilePath}";
            }
            else
            {
                // 路径引用（其他位置文件）：只从列表中移除
                string originalPath = selectedItem.OriginalPath ?? selectedItem.FilePath;
                messageText = $"确定要从桌面窗口中移除 \"{selectedItem.DisplayName}\" 吗？\n\n" +
                             $"ℹ️ 这是路径引用（快捷方式）\n" +
                             $"删除操作只会从桌面窗口中移除，不会删除原始文件。\n\n" +
                             $"原始文件位置：\n{originalPath}";
            }
            
            // 创建自定义 MessageBox，设置 TopMost
            // 增加窗口高度，确保文字和按钮都有足够空间
            int messageBoxWidth = 500;
            int messageBoxHeight = 250; // 增加高度，确保按钮区域不被文字覆盖
            
            Form messageBox = new Form
            {
                Text = "确认删除",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                TopMost = true,
                ShowInTaskbar = false,
                Size = new Size(messageBoxWidth, messageBoxHeight)
            };
            
            // 应用 ShareX 主题（需要在计算控件位置之前应用）
            ShareXResources.ApplyTheme(messageBox, true);
            
            // 按钮布局参数
            int buttonWidth = 75;
            int buttonHeight = 25;
            int buttonSpacing = 10;
            int rightMargin = 10; // 右边距
            int bottomMargin = 10; // 底边距
            int topMargin = 20; // 顶部边距
            int labelSpacing = 15; // Label 和按钮之间的间距
            
            Button btnNo = new Button
            {
                Text = "否",
                DialogResult = DialogResult.No,
                Size = new Size(buttonWidth, buttonHeight)
            };
            messageBox.Controls.Add(btnNo);
            
            Button btnYes = new Button
            {
                Text = "是",
                DialogResult = DialogResult.Yes,
                Size = new Size(buttonWidth, buttonHeight)
            };
            messageBox.Controls.Add(btnYes);
            
            messageBox.AcceptButton = btnYes;
            messageBox.CancelButton = btnNo;
            
            // 在窗口显示后重新计算所有控件位置（确保使用正确的客户区尺寸）
            messageBox.Shown += (s, e) =>
            {
                // 使用 ClientSize 而不是窗口总尺寸，因为 ClientSize 不包括边框
                int clientWidth = messageBox.ClientSize.Width;
                int clientHeight = messageBox.ClientSize.Height;
                
                // 计算按钮Y位置（距离底部10像素）
                int buttonY = clientHeight - bottomMargin - buttonHeight;
                
                // 计算按钮X位置（右对齐）
                int noX = clientWidth - rightMargin - buttonWidth;
                int yesX = noX - buttonSpacing - buttonWidth;
                
                btnNo.Location = new Point(noX, buttonY);
                btnYes.Location = new Point(yesX, buttonY);
                
                // 计算 Label 的高度：从顶部到按钮上方，留出间距
                int labelHeight = buttonY - topMargin - labelSpacing;
                int labelWidth = clientWidth - (topMargin * 2); // 左右各留20像素边距
                
                // 创建或更新 Label
                Label label = new Label
                {
                    Text = messageText,
                    Location = new Point(topMargin, topMargin),
                    Size = new Size(labelWidth, labelHeight),
                    AutoSize = false
                };
                messageBox.Controls.Add(label);
            };
            
            DialogResult result = messageBox.ShowDialog(this);
            
            // 删除操作结束
            EndOperation();
            
            if (result == DialogResult.Yes)
            {
                // 如果是真实文件，删除复制的文件
                if (selectedItem.IsRealFile && !string.IsNullOrEmpty(selectedItem.FilePath))
                {
                    try
                    {
                        if (File.Exists(selectedItem.FilePath))
                        {
                            File.Delete(selectedItem.FilePath);
                        }
                        else if (Directory.Exists(selectedItem.FilePath))
                        {
                            Directory.Delete(selectedItem.FilePath, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 删除失败时提示用户
                        ShowNotification($"删除文件失败：{ex.Message}\n\n文件路径：{selectedItem.FilePath}", 
                            "删除失败", 3000, MessageBoxIcon.Warning);
                    }
                }
                
                // 从当前分类中移除
                if (categories.ContainsKey(currentCategory))
                {
                    categories[currentCategory].Remove(selectedItem);
                }
                selectedItem = null;
                SaveItems();
                UpdateWindowSize();
                
                if (IsHandleCreated)
                {
                    UpdateLayeredWindowBitmap();
                }
            }
        }

        private void DesktopForm_MouseMove(object sender, MouseEventArgs e)
        {
            ResetAutoCloseTimer(); // 重置自动关闭定时器
            
            // 检查是否在分类栏区域（排除"➕"按钮）
            string hoveredCategory = GetHoveredCategory(e.Location);
            if (hoveredCategory != null && hoveredCategory != currentCategory)
            {
                // 鼠标悬停在分类按钮上，自动切换分类
                currentCategory = hoveredCategory;
                // 初始化新分类的图标缩放值
                foreach (var desktopItem in items)
                {
                    if (!itemScales.ContainsKey(desktopItem))
                    {
                        itemScales[desktopItem] = 1.0f;
                    }
                }
                UpdateWindowSize();
                if (IsHandleCreated)
                {
                    UpdateLayeredWindowBitmap();
                }
                this.Cursor = Cursors.Hand;
                return;
            }
            
            DesktopItem item = GetItemAtPoint(e.Location);
            this.Cursor = item != null ? Cursors.Hand : Cursors.Default;
            
            // 更新悬停状态
            if (hoveredItem != item)
            {
                hoveredItem = item;
                // 动画定时器会自动更新显示
            }
            
            lastMousePos = e.Location;
        }
        
        /// <summary>
        /// 鼠标滚轮事件：实现滚动功能
        /// </summary>
        private void DesktopForm_MouseWheel(object sender, MouseEventArgs e)
        {
            if (maxScrollOffset <= 0) return; // 不需要滚动
            
            int delta = e.Delta;
            int scrollStep = 30; // 每次滚动的像素数
            
            if (delta > 0)
            {
                // 向上滚动
                scrollOffsetY = Math.Max(0, scrollOffsetY - scrollStep);
            }
            else
            {
                // 向下滚动
                scrollOffsetY = Math.Min(maxScrollOffset, scrollOffsetY + scrollStep);
            }
            
            if (IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }
        
        private string GetHoveredCategory(Point point)
        {
            // 检查是否在分类栏区域
            if (!IsCategoryBarPoint(point))
            {
                return null;
            }
            
            int barWidth = CATEGORY_BAR_WIDTH - 5;
            int buttonSpacing = 5;
            int buttonWidth = barWidth - 10;
            int buttonHeight = 25;
            int barX, barY;
            
            if (isSidebarLeft)
            {
                barX = PADDING + SHADOW_SIZE;
            }
            else
            {
                barX = Width - (PADDING + SHADOW_SIZE) - barWidth;
            }
            barY = PADDING + SHADOW_SIZE;
            
            EnsureDesktopCategoryFirst();
            var categoryList = categories.Keys.ToList();
            int startY = barY + 5;
            
            // 检查鼠标悬停在哪个分类按钮上（排除"➕"按钮）
            for (int i = 0; i < categoryList.Count; i++)
            {
                string category = categoryList[i];
                int btnX = barX + 5;
                int btnY = startY + i * (buttonHeight + buttonSpacing);
                
                Rectangle btnRect = new Rectangle(btnX, btnY, buttonWidth, buttonHeight);
                if (btnRect.Contains(point))
                {
                    return category;
                }
            }
            
            return null; // 不在任何分类按钮上（可能在"➕"按钮上）
        }

        private DesktopItem GetItemAtPoint(Point point)
        {
            // 根据侧边栏位置决定图标区域起始位置
            int x, y;
            if (isSidebarLeft)
            {
                // 侧边栏在左侧，分类栏在左侧，图标区域从右侧开始（增加间距）
                x = PADDING + SHADOW_SIZE + CATEGORY_BAR_WIDTH + CATEGORY_ICON_GAP;
            }
            else
            {
                // 侧边栏在右侧，分类栏在右侧，图标区域从左侧开始
                x = PADDING + SHADOW_SIZE;
            }
            y = PADDING + SHADOW_SIZE - scrollOffsetY; // 应用滚动偏移

            // 计算每行图标数量（与 UpdateWindowSize 和 DrawIcons 中的计算保持一致）
            int iconAreaAvailableWidth = Width - CATEGORY_BAR_WIDTH - CATEGORY_ICON_GAP - (PADDING + SHADOW_SIZE) * 2;
            int iconsPerRow = Math.Max(1, (iconAreaAvailableWidth + ICON_HORIZONTAL_SPACING) / (ICON_SIZE + ICON_HORIZONTAL_SPACING));
            
            // 计算每行高度
            int rowHeight = ICON_SIZE + TEXT_HEIGHT + ICON_VERTICAL_SPACING;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int col = i % iconsPerRow;
                int row = i / iconsPerRow;

                int itemX = x + col * (ICON_SIZE + ICON_HORIZONTAL_SPACING);
                int itemY = y + row * (rowHeight + ROW_SPACING);

                Rectangle itemRect = new Rectangle(itemX, itemY, ICON_SIZE, ICON_SIZE + TEXT_HEIGHT);

                if (itemRect.Contains(point))
                {
                    return item;
                }
            }

            return null;
        }
        
        private bool IsCategoryBarPoint(Point point)
        {
            int barWidth = CATEGORY_BAR_WIDTH - 5;
            int barHeight = Height - (PADDING + SHADOW_SIZE) * 2;
            int barX, barY;
            
            if (isSidebarLeft)
            {
                // 侧边栏在左侧，分类栏靠左
                barX = PADDING + SHADOW_SIZE;
            }
            else
            {
                // 侧边栏在右侧，分类栏靠右
                barX = Width - (PADDING + SHADOW_SIZE) - barWidth;
            }
            barY = PADDING + SHADOW_SIZE;
            
            Rectangle barRect = new Rectangle(barX, barY, barWidth, barHeight);
            return barRect.Contains(point);
        }
        
        private void HandleCategoryBarClick(Point point)
        {
            int barWidth = CATEGORY_BAR_WIDTH - 5;
            int buttonSpacing = 5;
            int buttonWidth = barWidth - 10;
            int buttonHeight = 25;
            int barX, barY;
            
            if (isSidebarLeft)
            {
                barX = PADDING + SHADOW_SIZE;
            }
            else
            {
                barX = Width - (PADDING + SHADOW_SIZE) - barWidth;
            }
            barY = PADDING + SHADOW_SIZE;
            
            EnsureDesktopCategoryFirst();
            var categoryList = categories.Keys.ToList();
            int startY = barY + 5;
            
            // 检查点击了哪个分类按钮（竖排）
            for (int i = 0; i < categoryList.Count; i++)
            {
                string category = categoryList[i];
                int btnX = barX + 5;
                int btnY = startY + i * (buttonHeight + buttonSpacing);
                
                Rectangle btnRect = new Rectangle(btnX, btnY, buttonWidth, buttonHeight);
                if (btnRect.Contains(point))
                {
                    // 切换分类
                    currentCategory = category;
                    // 初始化新分类的图标缩放值
                    foreach (var item in items)
                    {
                        if (!itemScales.ContainsKey(item))
                        {
                            itemScales[item] = 1.0f;
                        }
                    }
                    UpdateWindowSize();
                    if (IsHandleCreated)
                    {
                        UpdateLayeredWindowBitmap();
                    }
                    return;
                }
            }
            
            // 检查是否点击了添加按钮
            int addBtnHeight = 30;
            int addBtnX = barX + 5;
            int addBtnY = startY + categoryList.Count * (buttonHeight + buttonSpacing);
            
            Rectangle addBtnRect = new Rectangle(addBtnX, addBtnY, buttonWidth, addBtnHeight);
            if (addBtnRect.Contains(point))
            {
                AddNewCategory();
            }
        }
        
        private void HandleCategoryBarRightClick(Point point)
        {
            int barWidth = CATEGORY_BAR_WIDTH - 5;
            int buttonSpacing = 5;
            int buttonWidth = barWidth - 10;
            int buttonHeight = 25;
            int barX, barY;
            
            if (isSidebarLeft)
            {
                barX = PADDING + SHADOW_SIZE;
            }
            else
            {
                barX = Width - (PADDING + SHADOW_SIZE) - barWidth;
            }
            barY = PADDING + SHADOW_SIZE;
            
            EnsureDesktopCategoryFirst();
            var categoryList = categories.Keys.ToList();
            int startY = barY + 5;
            
            // 检查右键点击了哪个分类按钮（竖排）
            for (int i = 0; i < categoryList.Count; i++)
            {
                string category = categoryList[i];
                int btnX = barX + 5;
                int btnY = startY + i * (buttonHeight + buttonSpacing);
                
                Rectangle btnRect = new Rectangle(btnX, btnY, buttonWidth, buttonHeight);
                if (btnRect.Contains(point))
                {
                    selectedCategory = category;
                    ShowCategoryContextMenu(point);
                    return;
                }
            }
        }
        
        private void ShowCategoryContextMenu(Point location)
        {
            if (selectedCategory == null) return;
            
            ContextMenuStrip menu = new ContextMenuStrip();
            
            // 只有非"桌面"分类才能重命名和删除
            if (selectedCategory != DEFAULT_CATEGORY)
            {
                // 重命名分类
                ToolStripMenuItem renameItem = new ToolStripMenuItem("重命名");
                renameItem.Click += (s, e) => RenameCategory();
                menu.Items.Add(renameItem);
                
                menu.Items.Add(new ToolStripSeparator());
                
                // 删除分类
                ToolStripMenuItem deleteItem = new ToolStripMenuItem("删除");
                deleteItem.Click += (s, e) => DeleteCategory();
                menu.Items.Add(deleteItem);
            }
            
            menu.Show(this, location);
        }
        
        private void RenameCategory()
        {
            if (selectedCategory == null) return;
            
            // "桌面"分类不能重命名
            if (selectedCategory == DEFAULT_CATEGORY)
            {
                ShowNotification(
                    $"\"{DEFAULT_CATEGORY}\"分类是桌面分类，不能重命名。",
                    "提示",
                    3000,
                    MessageBoxIcon.Information
                );
                return;
            }
            
            // 创建输入框重命名分类
            Form inputForm = new Form
            {
                Text = "重命名分类",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.Manual,
                TopMost = true,
                ShowInTaskbar = false,
                Size = new Size(320, 150), // 增加宽度确保按钮完整显示
                Location = new Point(
                    this.Right + 10,
                    this.Top + 50
                )
            };
            
            Label label = new Label
            {
                Text = "请输入新名称：",
                Location = new Point(10, 15),
                Size = new Size(300, 20),
                AutoSize = false
            };
            inputForm.Controls.Add(label);
            
            TextBox txtInput = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(300, 23),
                Text = selectedCategory
            };
            txtInput.SelectAll();
            inputForm.Controls.Add(txtInput);
            
            // 应用 ShareX 主题（需要在计算按钮位置之前应用，因为主题可能影响窗口大小）
            ShareXResources.ApplyTheme(inputForm, true);
            
            // 按钮布局：确定和取消按钮，右对齐，间距10像素
            int buttonWidth = 75;
            int buttonHeight = 25;
            int buttonY = 75;
            int buttonSpacing = 10;
            int rightMargin = 10; // 右边距
            
            Button btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Size = new Size(buttonWidth, buttonHeight)
            };
            inputForm.Controls.Add(btnCancel);
            
            Button btnOK = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Size = new Size(buttonWidth, buttonHeight)
            };
            inputForm.Controls.Add(btnOK);
            
            inputForm.AcceptButton = btnOK;
            inputForm.CancelButton = btnCancel;
            
            // 在窗口显示后重新计算按钮位置（确保使用正确的客户区宽度）
            inputForm.Shown += (s, e) =>
            {
                // 使用 ClientSize.Width 而不是 Width，因为 Width 包括边框
                int clientWidth = inputForm.ClientSize.Width;
                int cancelX = clientWidth - rightMargin - buttonWidth;
                int okX = cancelX - buttonSpacing - buttonWidth;
                
                btnCancel.Location = new Point(cancelX, buttonY);
                btnOK.Location = new Point(okX, buttonY);
            };
            
            // 确保窗口在屏幕内
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            if (inputForm.Right > screenBounds.Right)
            {
                inputForm.Left = this.Left - inputForm.Width - 10;
            }
            if (inputForm.Bottom > screenBounds.Bottom)
            {
                inputForm.Top = screenBounds.Bottom - inputForm.Height;
            }
            if (inputForm.Left < screenBounds.Left)
            {
                inputForm.Left = screenBounds.Left;
            }
            if (inputForm.Top < screenBounds.Top)
            {
                inputForm.Top = screenBounds.Top;
            }
            
            DialogResult result = inputForm.ShowDialog(this);
            
            // 重命名分类操作结束
            EndOperation();
            
            if (result == DialogResult.OK)
            {
                string newName = txtInput.Text.Trim();
                
                if (!string.IsNullOrEmpty(newName) && newName != selectedCategory && !categories.ContainsKey(newName))
                {
                    // 重命名分类
                    var items = categories[selectedCategory];
                    categories.Remove(selectedCategory);
                    categories[newName] = items;
                    
                    // 如果重命名的是当前分类，更新当前分类
                    if (currentCategory == selectedCategory)
                    {
                        currentCategory = newName;
                    }
                    
                    selectedCategory = null;
                    SaveItems();
                    UpdateWindowSize();
                    if (IsHandleCreated)
                    {
                        UpdateLayeredWindowBitmap();
                    }
                }
            }
        }
        
        private void DeleteCategory()
        {
            if (selectedCategory == null) return;
            
            // "桌面"分类不能删除
            if (selectedCategory == DEFAULT_CATEGORY)
            {
                ShowNotification(
                    $"\"{DEFAULT_CATEGORY}\"分类是桌面分类，不能删除。",
                    "提示",
                    3000,
                    MessageBoxIcon.Information
                );
                return;
            }
            
            // 如果只有一个分类，不能删除
            if (categories.Count <= 1)
            {
                ShowNotification("至少需要保留一个分类", "提示", 3000, MessageBoxIcon.Information);
                return;
            }
            
            // 开始删除分类操作
            StartOperation();
            
            DialogResult result = MessageBox.Show(
                $"确定要删除分类 \"{selectedCategory}\" 吗？\n\n注意：分类中的所有图标将被移动到\"{DEFAULT_CATEGORY}\"分类。",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2
            );
            
            // 删除分类操作结束
            EndOperation();
            
            if (result == DialogResult.Yes)
            {
                // 确保桌面分类存在
                if (!categories.ContainsKey(DEFAULT_CATEGORY))
                {
                    categories[DEFAULT_CATEGORY] = new List<DesktopItem>();
                }
                
                // 将删除分类的图标移动到桌面分类
                if (categories.ContainsKey(selectedCategory))
                {
                    categories[DEFAULT_CATEGORY].AddRange(categories[selectedCategory]);
                    categories.Remove(selectedCategory);
                }
                
                // 如果删除的是当前分类，切换到桌面分类
                if (currentCategory == selectedCategory)
                {
                    currentCategory = DEFAULT_CATEGORY;
                }
                
                selectedCategory = null;
                SaveItems();
                UpdateWindowSize();
                if (IsHandleCreated)
                {
                    UpdateLayeredWindowBitmap();
                }
            }
        }
        
        /// <summary>
        /// 在当前分类下方插入新分类
        /// </summary>
        private void InsertCategoryAfterCurrent(string newCategoryName)
        {
            // 创建新的字典，保持顺序
            Dictionary<string, List<DesktopItem>> newCategories = new Dictionary<string, List<DesktopItem>>();
            
            // 先添加"桌面"分类（如果存在）
            bool desktopAdded = false;
            if (categories.ContainsKey(DEFAULT_CATEGORY))
            {
                newCategories[DEFAULT_CATEGORY] = categories[DEFAULT_CATEGORY];
                desktopAdded = true;
            }
            
            // 遍历原分类，插入新分类到当前分类下方
            bool inserted = false;
            foreach (var kvp in categories)
            {
                // 跳过"桌面"分类（已经添加）
                if (kvp.Key == DEFAULT_CATEGORY)
                {
                    continue;
                }
                
                // 添加当前分类
                newCategories[kvp.Key] = kvp.Value;
                
                // 如果当前分类是选中的分类，在其后插入新分类
                if (kvp.Key == currentCategory && !inserted)
                {
                    newCategories[newCategoryName] = new List<DesktopItem>();
                    inserted = true;
                }
            }
            
            // 如果"桌面"分类不存在，确保它被添加
            if (!desktopAdded)
            {
                // 如果当前分类是"桌面"，在其后插入
                if (currentCategory == DEFAULT_CATEGORY)
                {
                    Dictionary<string, List<DesktopItem>> tempCategories = new Dictionary<string, List<DesktopItem>>();
                    tempCategories[DEFAULT_CATEGORY] = newCategories.ContainsKey(DEFAULT_CATEGORY) ? newCategories[DEFAULT_CATEGORY] : new List<DesktopItem>();
                    tempCategories[newCategoryName] = new List<DesktopItem>();
                    foreach (var kvp in newCategories)
                    {
                        if (kvp.Key != DEFAULT_CATEGORY)
                        {
                            tempCategories[kvp.Key] = kvp.Value;
                        }
                    }
                    newCategories = tempCategories;
                }
                else
                {
                    // 否则在最前面添加"桌面"分类
                    Dictionary<string, List<DesktopItem>> tempCategories = new Dictionary<string, List<DesktopItem>>();
                    tempCategories[DEFAULT_CATEGORY] = new List<DesktopItem>();
                    foreach (var kvp in newCategories)
                    {
                        tempCategories[kvp.Key] = kvp.Value;
                    }
                    newCategories = tempCategories;
                }
            }
            else if (!inserted)
            {
                // 如果新分类还没有被插入，添加到末尾
                newCategories[newCategoryName] = new List<DesktopItem>();
            }
            
            categories = newCategories;
        }
        
        /// <summary>
        /// 确保"桌面"分类在最顶部，但保持其他分类的顺序
        /// </summary>
        private void EnsureDesktopCategoryFirst()
        {
            if (!categories.ContainsKey(DEFAULT_CATEGORY))
            {
                // 如果"桌面"分类不存在，创建它
                Dictionary<string, List<DesktopItem>> newCategories = new Dictionary<string, List<DesktopItem>>();
                newCategories[DEFAULT_CATEGORY] = new List<DesktopItem>();
                foreach (var kvp in categories)
                {
                    newCategories[kvp.Key] = kvp.Value;
                }
                categories = newCategories;
            }
            else
            {
                // 如果"桌面"分类存在但不是第一个，将其移到第一个
                var categoryList = categories.Keys.ToList();
                if (categoryList.Count > 0 && categoryList[0] != DEFAULT_CATEGORY)
                {
                    Dictionary<string, List<DesktopItem>> newCategories = new Dictionary<string, List<DesktopItem>>();
                    newCategories[DEFAULT_CATEGORY] = categories[DEFAULT_CATEGORY];
                    foreach (var kvp in categories)
                    {
                        if (kvp.Key != DEFAULT_CATEGORY)
                        {
                            newCategories[kvp.Key] = kvp.Value;
                        }
                    }
                    categories = newCategories;
                }
            }
        }
        
        private void AddNewCategory()
        {
            // 开始添加分类操作
            StartOperation();
            
            // 创建输入框添加新分类
            Form inputForm = new Form
            {
                Text = "新建分类",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.Manual,
                TopMost = true,
                ShowInTaskbar = false,
                Size = new Size(320, 150), // 增加宽度确保按钮完整显示
                Location = new Point(
                    this.Right + 10,
                    this.Top + 50
                )
            };
            
            Label label = new Label
            {
                Text = "请输入分类名称：",
                Location = new Point(10, 15),
                Size = new Size(300, 20),
                AutoSize = false
            };
            inputForm.Controls.Add(label);
            
            TextBox txtInput = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(300, 23),
                Text = ""
            };
            inputForm.Controls.Add(txtInput);
            
            // 应用 ShareX 主题（需要在计算按钮位置之前应用，因为主题可能影响窗口大小）
            ShareXResources.ApplyTheme(inputForm, true);
            
            // 按钮布局：确定和取消按钮，右对齐，间距10像素
            int buttonWidth = 75;
            int buttonHeight = 25;
            int buttonY = 75;
            int buttonSpacing = 10;
            int rightMargin = 10; // 右边距
            
            Button btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Size = new Size(buttonWidth, buttonHeight)
            };
            inputForm.Controls.Add(btnCancel);
            
            Button btnOK = new Button
            {
                Text = "确定",
                DialogResult = DialogResult.OK,
                Size = new Size(buttonWidth, buttonHeight)
            };
            inputForm.Controls.Add(btnOK);
            
            inputForm.AcceptButton = btnOK;
            inputForm.CancelButton = btnCancel;
            
            // 在窗口显示后重新计算按钮位置（确保使用正确的客户区宽度）
            inputForm.Shown += (s, e) =>
            {
                // 使用 ClientSize.Width 而不是 Width，因为 Width 包括边框
                int clientWidth = inputForm.ClientSize.Width;
                int cancelX = clientWidth - rightMargin - buttonWidth;
                int okX = cancelX - buttonSpacing - buttonWidth;
                
                btnCancel.Location = new Point(cancelX, buttonY);
                btnOK.Location = new Point(okX, buttonY);
            };
            
            // 确保窗口在屏幕内
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            if (inputForm.Right > screenBounds.Right)
            {
                inputForm.Left = this.Left - inputForm.Width - 10;
            }
            if (inputForm.Bottom > screenBounds.Bottom)
            {
                inputForm.Top = screenBounds.Bottom - inputForm.Height;
            }
            if (inputForm.Left < screenBounds.Left)
            {
                inputForm.Left = screenBounds.Left;
            }
            if (inputForm.Top < screenBounds.Top)
            {
                inputForm.Top = screenBounds.Top;
            }
            
            DialogResult result = inputForm.ShowDialog(this);
            
            // 添加分类操作结束
            EndOperation();
            
            if (result == DialogResult.OK)
            {
                string categoryName = txtInput.Text.Trim();
                if (!string.IsNullOrEmpty(categoryName) && !categories.ContainsKey(categoryName))
                {
                    // 在当前分类下方插入新分类
                    InsertCategoryAfterCurrent(categoryName);
                    currentCategory = categoryName;
                    SaveItems();
                    UpdateWindowSize();
                    if (IsHandleCreated)
                    {
                        UpdateLayeredWindowBitmap();
                    }
                }
            }
        }

        private void AddItem(string filePath)
        {
            // 确保当前分类存在
            if (!categories.ContainsKey(currentCategory))
            {
                categories[currentCategory] = new List<DesktopItem>();
            }
            
            // 检查是否已存在（检查原始路径）
            if (items.Any(i => 
                (i.IsRealFile && i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)) ||
                (!i.IsRealFile && i.OriginalPath != null && i.OriginalPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))))
            {
                return;
            }

            DesktopItem item = new DesktopItem
            {
                DisplayName = Path.GetFileNameWithoutExtension(filePath) // 不显示扩展名，仅显示名称
            };
            
            // 判断文件类型
            bool isShortcut = IsShortcutFile(filePath);
            bool isFromDesktopLocation = IsFromDesktop(filePath);
            
            if (isFromDesktopLocation)
            {
                if (isShortcut)
                {
                    // 桌面上的快捷方式：可以添加到任何分类，保存为路径引用
                    item.FilePath = filePath;
                    item.OriginalPath = filePath;
                    item.IsRealFile = false;
                }
                else if (currentCategory == DEFAULT_CATEGORY)
                {
                    // 桌面上的真实文件/文件夹，且在"桌面"分类：复制为真实文件
                    string copiedPath = CopyToStorage(filePath, item.DisplayName);
                    if (copiedPath != null)
                    {
                        item.FilePath = copiedPath;
                        item.OriginalPath = filePath; // 保存原始路径用于显示
                        item.IsRealFile = true;
                        
                        // 删除桌面上的原始文件
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }
                            else if (Directory.Exists(filePath))
                            {
                                Directory.Delete(filePath, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 如果删除失败，不影响添加操作
                            System.Diagnostics.Debug.WriteLine($"删除桌面文件失败：{ex.Message}");
                        }
                    }
                    else
                    {
                        // 复制失败，不添加
                        return;
                    }
                }
                else
                {
                    // 桌面上的真实文件/文件夹，但不在"桌面"分类：不允许添加（已在DragDrop中检查，这里作为双重保险）
                    return;
                }
            }
            else
            {
                // 其他位置文件：所有分类都只保存路径引用
                item.FilePath = filePath;
                item.OriginalPath = filePath;
                item.IsRealFile = false;
            }

            categories[currentCategory].Add(item);
            
            // 初始化新图标的缩放值
            itemScales[item] = 1.0f;
            
            // 立即刷新显示（如果窗口已创建）
            if (IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }

        private void UpdateWindowSize()
        {
            // 宽度固定，包含分类栏和间距
            int fixedWidth = INITIAL_WIDTH + CATEGORY_BAR_WIDTH + CATEGORY_ICON_GAP;
            
            // 计算分类栏所需高度（分类按钮 + 添加按钮）
            int buttonSpacing = 5;
            int buttonHeight = 25;
            int addBtnHeight = 30;
            int categoryBarPadding = 5;
            int categoryBarMinHeight = categoryBarPadding * 2 + addBtnHeight; // 至少能显示添加按钮
            
            int categoryCount = categories.Count;
            int categoryBarHeight = categoryBarPadding * 2 + categoryCount * (buttonHeight + buttonSpacing) + addBtnHeight;
            categoryBarHeight = Math.Max(categoryBarHeight, categoryBarMinHeight);
            
            // 计算图标区域可用宽度（窗口宽度 - 分类栏 - 间距 - 边距）
            int iconAreaAvailableWidth = fixedWidth - CATEGORY_BAR_WIDTH - CATEGORY_ICON_GAP - (PADDING + SHADOW_SIZE) * 2;
            
            // 根据可用宽度计算每行图标数量（自适应）
            // 每个图标需要：ICON_SIZE + ICON_HORIZONTAL_SPACING
            int iconsPerRow = Math.Max(1, (iconAreaAvailableWidth + ICON_HORIZONTAL_SPACING) / (ICON_SIZE + ICON_HORIZONTAL_SPACING));
            currentColCount = iconsPerRow;
            
            // 计算图标区域所需高度
            int iconAreaHeight = INITIAL_HEIGHT - (PADDING + SHADOW_SIZE) * 2; // 初始图标区域高度
            
            if (items.Count > 0)
            {
                int rowCount = (int)Math.Ceiling((double)items.Count / iconsPerRow);
                // 每行高度 = 图标高度 + 文本高度 + 垂直间距
                int rowHeight = ICON_SIZE + TEXT_HEIGHT + ICON_VERTICAL_SPACING;
                iconAreaHeight = rowCount * rowHeight + (rowCount - 1) * ROW_SPACING;
                currentRowCount = rowCount;
            }
            else
            {
                currentRowCount = 0;
            }
            
            // 计算实际需要的总高度（分类栏高度和图标区域高度的较大值 + 上下边距和阴影）
            int requiredHeight = Math.Max(categoryBarHeight, iconAreaHeight) + (PADDING + SHADOW_SIZE) * 2;
            
            // 如果超过最大高度，使用最大高度并启用滚动
            int displayHeight = requiredHeight;
            if (requiredHeight > MAX_HEIGHT)
            {
                displayHeight = MAX_HEIGHT;
                maxScrollOffset = requiredHeight - MAX_HEIGHT + (PADDING + SHADOW_SIZE) * 2;
                // 确保滚动偏移量在有效范围内
                if (scrollOffsetY > maxScrollOffset)
                {
                    scrollOffsetY = maxScrollOffset;
                }
            }
            else
            {
                maxScrollOffset = 0;
                scrollOffsetY = 0;
            }
            
            // 最小高度
            displayHeight = Math.Max(displayHeight, INITIAL_HEIGHT);

            this.Size = new Size(fixedWidth, displayHeight);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不绘制背景，UpdateLayeredWindow 会处理
        }

        private void DesktopForm_Paint(object sender, PaintEventArgs e)
        {
            // Paint 事件不在这里处理，使用 UpdateLayeredWindowBitmap 直接更新
        }
        
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // 窗口显示后立即更新一次
            if (IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }

        private void UpdateLayeredWindowBitmap()
        {
            if (Width <= 0 || Height <= 0 || !IsHandleCreated) return;

            using (Bitmap bitmap = new Bitmap(Width, Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    // 清除背景为透明
                    g.Clear(Color.Transparent);

                    // 绘制阴影
                    DrawShadow(g);

                    // 创建圆角矩形路径
                    Rectangle rect = new Rectangle(SHADOW_SIZE, SHADOW_SIZE,
                        Width - SHADOW_SIZE * 2, Height - SHADOW_SIZE * 2);
                    GraphicsPath path = CreateRoundedRectangle(rect, CORNER_RADIUS);

                    // 绘制背景色
                    using (SolidBrush brush = new SolidBrush(backgroundColor))
                    {
                        g.FillPath(brush, path);
                    }

                    // 绘制分类栏
                    DrawCategoryBar(g);
                    
                    // 绘制图标
                    DrawIcons(g);
                    
                    // 绘制滚动条（如果需要滚动）
                    if (maxScrollOffset > 0)
                    {
                        DrawScrollBar(g);
                    }

                    path.Dispose();
                }

                // 使用 UpdateLayeredWindow 应用位图
                IntPtr screenDC = GetDC(IntPtr.Zero);
                IntPtr memDC = CreateCompatibleDC(screenDC);
                IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                IntPtr oldBitmap = SelectObject(memDC, hBitmap);

                SIZE size = new SIZE(Width, Height);
                POINT pointSource = new POINT(0, 0);
                POINT topPos = new POINT(Left, Top);
                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA
                };

                UpdateLayeredWindow(Handle, screenDC, ref topPos, ref size, memDC, ref pointSource, 0, ref blend, ULW_ALPHA);

                SelectObject(memDC, oldBitmap);
                DeleteObject(hBitmap);
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, screenDC);
            }
        }

        private void DrawShadow(Graphics g)
        {
            Rectangle shadowRect = new Rectangle(SHADOW_SIZE / 2, SHADOW_SIZE / 2,
                Width - SHADOW_SIZE, Height - SHADOW_SIZE);

            for (int i = SHADOW_SIZE; i > 0; i--)
            {
                float alpha = (float)(0.15 * (SHADOW_SIZE - i + 1) / SHADOW_SIZE);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb((int)(alpha * 255), Color.Black)))
                {
                    int offset = i / 2;
                    Rectangle layerRect = new Rectangle(
                        Math.Max(0, SHADOW_SIZE / 2 - offset),
                        Math.Max(0, SHADOW_SIZE / 2 - offset),
                        Math.Min(Width, shadowRect.Width + i),
                        Math.Min(Height, shadowRect.Height + i));
                    GraphicsPath layerPath = CreateRoundedRectangle(layerRect, CORNER_RADIUS);
                    g.FillPath(brush, layerPath);
                    layerPath.Dispose();
                }
            }
        }

        private GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            const int arcSegments = 12;

            // 左上圆角
            AddSmoothArc(path, rect.Left, rect.Top, radius * 2, radius * 2, 180, 90, arcSegments);
            // 右上圆角
            AddSmoothArc(path, rect.Right - radius * 2, rect.Top, radius * 2, radius * 2, 270, 90, arcSegments);
            // 右下圆角
            AddSmoothArc(path, rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90, arcSegments);
            // 左下圆角
            AddSmoothArc(path, rect.Left, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90, arcSegments);

            path.CloseFigure();
            return path;
        }

        private void AddSmoothArc(GraphicsPath path, float x, float y, float width, float height, float startAngle, float sweepAngle, int segments)
        {
            float angleStep = sweepAngle / segments;
            float angle = startAngle;

            for (int i = 0; i <= segments; i++)
            {
                float rad = (float)(angle * Math.PI / 180.0);
                float px = x + width / 2 + (float)(width / 2 * Math.Cos(rad));
                float py = y + height / 2 + (float)(height / 2 * Math.Sin(rad));

                if (i == 0)
                {
                    path.AddLine(px, py, px, py);
                }
                else
                {
                    path.AddLine(path.GetLastPoint(), new PointF(px, py));
                }

                angle += angleStep;
            }
        }

        private void DrawCategoryBar(Graphics g)
        {
            // 根据侧边栏位置决定分类栏位置
            int barX, barY;
            int barWidth = CATEGORY_BAR_WIDTH - 5;
            int barHeight = Height - (PADDING + SHADOW_SIZE) * 2;
            
            if (isSidebarLeft)
            {
                // 侧边栏在左侧，分类栏靠左
                barX = PADDING + SHADOW_SIZE;
            }
            else
            {
                // 侧边栏在右侧，分类栏靠右
                barX = Width - (PADDING + SHADOW_SIZE) - barWidth;
            }
            barY = PADDING + SHADOW_SIZE;
            
            // 绘制分类栏背景（完全透明，与背景融合）
            // 不绘制背景，直接跳过
            
            // 绘制分类按钮（竖排）
            int buttonSpacing = 5;
            int buttonWidth = barWidth - 10;
            int buttonHeight = 25;
            
            // 获取所有分类（保持顺序，但确保"桌面"在最前面）
            EnsureDesktopCategoryFirst();
            var categoryList = categories.Keys.ToList();
            
            int startY = barY + 5;
            for (int i = 0; i < categoryList.Count; i++)
            {
                string category = categoryList[i];
                bool isActive = category == currentCategory;
                
                int btnX = barX + 5;
                int btnY = startY + i * (buttonHeight + buttonSpacing);
                
                // 绘制按钮背景
                Color btnBgColor = isActive ? Color.FromArgb(50, 255, 255, 255) : Color.FromArgb(20, 255, 255, 255);
                using (SolidBrush brush = new SolidBrush(btnBgColor))
                {
                    Rectangle btnRect = new Rectangle(btnX, btnY, buttonWidth, buttonHeight);
                    g.FillRectangle(brush, btnRect);
                }
                
                // 绘制按钮边框
                using (Pen pen = new Pen(isActive ? Color.White : Color.FromArgb(100, 255, 255, 255), 1))
                {
                    Rectangle btnRect = new Rectangle(btnX, btnY, buttonWidth, buttonHeight);
                    g.DrawRectangle(pen, btnRect);
                }
                
                // 绘制按钮文字
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                {
                    StringFormat format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    Rectangle textRect = new Rectangle(btnX, btnY, buttonWidth, buttonHeight);
                    g.DrawString(category, textFont, textBrush, textRect, format);
                }
            }
            
            // 绘制添加分类按钮（只显示➕符号）
            int addBtnHeight = 30;
            int addBtnX = barX + 5;
            int addBtnY = startY + categoryList.Count * (buttonHeight + buttonSpacing);
            
            // 绘制添加按钮
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(20, 255, 255, 255)))
            {
                Rectangle addBtnRect = new Rectangle(addBtnX, addBtnY, buttonWidth, addBtnHeight);
                g.FillRectangle(brush, addBtnRect);
            }
            
            using (Pen pen = new Pen(Color.FromArgb(100, 255, 255, 255), 1))
            {
                Rectangle addBtnRect = new Rectangle(addBtnX, addBtnY, buttonWidth, addBtnHeight);
                g.DrawRectangle(pen, addBtnRect);
            }
            
            // 只绘制➕符号
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                Rectangle textRect = new Rectangle(addBtnX, addBtnY, buttonWidth, addBtnHeight);
                g.DrawString("➕", textFont, textBrush, textRect, format);
            }
        }
        
        private void DrawIcons(Graphics g)
        {
            // 根据侧边栏位置决定图标区域起始位置
            int x, y;
            if (isSidebarLeft)
            {
                // 侧边栏在左侧，分类栏在左侧，图标区域从右侧开始（增加间距）
                x = PADDING + SHADOW_SIZE + CATEGORY_BAR_WIDTH + CATEGORY_ICON_GAP;
            }
            else
            {
                // 侧边栏在右侧，分类栏在右侧，图标区域从左侧开始
                x = PADDING + SHADOW_SIZE;
            }
            y = PADDING + SHADOW_SIZE - scrollOffsetY; // 应用滚动偏移

            // 计算每行图标数量（与 UpdateWindowSize 中的计算保持一致）
            int iconAreaAvailableWidth = Width - CATEGORY_BAR_WIDTH - CATEGORY_ICON_GAP - (PADDING + SHADOW_SIZE) * 2;
            int iconsPerRow = Math.Max(1, (iconAreaAvailableWidth + ICON_HORIZONTAL_SPACING) / (ICON_SIZE + ICON_HORIZONTAL_SPACING));
            
            // 计算每行高度
            int rowHeight = ICON_SIZE + TEXT_HEIGHT + ICON_VERTICAL_SPACING;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int col = i % iconsPerRow;
                int row = i / iconsPerRow;

                int itemX = x + col * (ICON_SIZE + ICON_HORIZONTAL_SPACING);
                int itemY = y + row * (rowHeight + ROW_SPACING);

                // 检查图标是否在可见区域内（考虑滚动）
                int visibleTop = PADDING + SHADOW_SIZE;
                int visibleBottom = Height - (PADDING + SHADOW_SIZE);
                int itemBottom = itemY + ICON_SIZE + TEXT_HEIGHT;

                // 只绘制可见的图标（优化性能）
                if (itemBottom >= visibleTop && itemY <= visibleBottom)
                {
                // 获取图标的缩放值
                float scale = itemScales.ContainsKey(item) ? itemScales[item] : 1.0f;
                
                // 计算缩放后的位置和大小
                float scaledSize = ICON_SIZE * scale;
                float offset = (ICON_SIZE - scaledSize) / 2.0f;
                float scaledX = itemX + offset;
                float scaledY = itemY + offset;
                
                // 绘制图标（应用缩放）
                DrawIcon(g, item, (int)scaledX, (int)scaledY, (int)scaledSize);

                // 绘制文件名（支持双排显示）
                DrawItemText(g, item, itemX, itemY);
                }
            }
        }

        private void DrawIcon(Graphics g, DesktopItem item, int x, int y, int size = -1)
        {
            if (size < 0) size = ICON_SIZE;
            
            try
            {
                // 对于快捷方式文件，直接使用文件路径获取图标
                // GetFileIcon 会自动处理快捷方式
                // 根据存储方式决定使用哪个路径获取图标
                string iconPath = item.IsRealFile ? item.FilePath : (item.OriginalPath ?? item.FilePath);
                
                // 如果是快捷方式，尝试获取目标路径的图标
                if (Path.GetExtension(iconPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        // 尝试解析快捷方式目标路径
                        ShareX.HelpersLib.WshShell shell = new ShareX.HelpersLib.WshShell();
                        ShareX.HelpersLib.IWshShortcut shortcut = ((ShareX.HelpersLib.IWshShell)shell).CreateShortcut(iconPath);
                        if (!string.IsNullOrEmpty(shortcut.TargetPath) && (File.Exists(shortcut.TargetPath) || Directory.Exists(shortcut.TargetPath)))
                        {
                            iconPath = shortcut.TargetPath;
                        }
                    }
                    catch
                    {
                        // 解析失败，使用快捷方式文件本身
                    }
                }
                
                Icon icon = NativeMethods.GetFileIcon(iconPath, false);
                if (icon != null && icon.Width > 0 && icon.Height > 0)
                {
                    using (icon)
                    {
                        // 高质量缩放图标
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        
                        Rectangle iconRect = new Rectangle(x, y, size, size);
                        g.DrawIcon(icon, iconRect);
                    }
                }
                else
                {
                    // 使用默认图标
                    DrawDefaultIcon(g, x, y, size);
                }
            }
            catch (Exception ex)
            {
                // 获取图标失败，使用默认图标
                System.Diagnostics.Debug.WriteLine($"获取图标失败: {item.FilePath}, 错误: {ex.Message}");
                DrawDefaultIcon(g, x, y, size);
            }
        }

        private void DrawDefaultIcon(Graphics g, int x, int y, int size = -1)
        {
            if (size < 0) size = ICON_SIZE;
            
            // 绘制简单的默认图标（矩形）
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(100, 255, 255, 255)))
            {
                g.FillRectangle(brush, x, y, size, size);
            }
            using (Pen pen = new Pen(Color.White, 2))
            {
                g.DrawRectangle(pen, x + 2, y + 2, size - 4, size - 4);
            }
        }
        
        private void DrawItemText(Graphics g, DesktopItem item, int x, int y)
        {
            string displayName = item.DisplayName.TrimStart(' '); // 去除开头的空格
            
            // 设置文本绘制选项，避免中文重影，使用更好的渲染质量
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            
            using (SolidBrush textBrush = new SolidBrush(Color.White))
            {
                float lineHeight = textFont.GetHeight(g);
                float availableWidth = TEXT_WIDTH; // 使用扩展的文本宽度，可以显示更多文字
                
                // 创建左对齐格式（参考 Windows 方式）
                StringFormat format = new StringFormat
                {
                    Alignment = StringAlignment.Near, // 左对齐
                    LineAlignment = StringAlignment.Near,
                    Trimming = StringTrimming.None, // 不使用省略号
                    FormatFlags = StringFormatFlags.NoWrap // 禁用自动换行，手动处理
                };
                
                // 先测量实际文本宽度（不使用宽度限制，获取真实宽度）
                // 使用 StringFormat.GenericTypographic 获得更准确的测量
                StringFormat measureFormat = new StringFormat(StringFormat.GenericTypographic)
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Near,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                SizeF fullTextSize = g.MeasureString(displayName, textFont, int.MaxValue, measureFormat);
                
                // 如果文本宽度超过可用宽度，需要换行（参考 Windows：单行约4-5个汉字，两行约8-10个汉字）
                // 使用更宽松的阈值，避免过早换行
                if (fullTextSize.Width > availableWidth * 1.05f)
                {
                    // 参考 Windows 的换行方式：找到最佳换行点
                    string line1 = "";
                    string line2 = "";
                    
                    // 方法1：优先在空格处换行（Windows 的默认行为）
                    int spaceIndex = -1;
                    // 从中间位置向两边搜索空格
                    int midPos = displayName.Length / 2;
                    for (int i = 0; i < displayName.Length; i++)
                    {
                        int pos1 = midPos - i;
                        int pos2 = midPos + i;
                        if (pos1 > 0 && pos1 < displayName.Length && displayName[pos1] == ' ')
                        {
                            spaceIndex = pos1;
                            break;
                        }
                        if (pos2 > 0 && pos2 < displayName.Length && displayName[pos2] == ' ')
                        {
                            spaceIndex = pos2;
                            break;
                        }
                    }
                    
                    if (spaceIndex > 0 && spaceIndex < displayName.Length - 1)
                    {
                        // 在空格处换行
                        line1 = displayName.Substring(0, spaceIndex).TrimEnd(' ');
                        line2 = displayName.Substring(spaceIndex + 1).TrimStart(' ');
                    }
                    else
                    {
                        // 方法2：使用二分法找到最接近可用宽度的位置
                        int left = 1;
                        int right = displayName.Length - 1;
                        int bestBreakPoint = displayName.Length / 2;
                        
                        while (left <= right)
                        {
                            int mid = (left + right) / 2;
                            string testLine = displayName.Substring(0, mid);
                            // 测量实际宽度（使用 GenericTypographic 获得更准确的测量）
                            SizeF testSize = g.MeasureString(testLine, textFont, int.MaxValue, measureFormat);
                            
                            if (testSize.Width <= availableWidth)
                            {
                                bestBreakPoint = mid;
                                left = mid + 1;
                            }
                            else
                            {
                                right = mid - 1;
                            }
                        }
                        
                        // 尝试在最佳位置附近寻找更好的换行点（分隔符、中英文边界等）
                        int searchStart = Math.Max(1, bestBreakPoint - 8);
                        int searchEnd = Math.Min(displayName.Length - 1, bestBreakPoint + 8);
                        int finalBreakPoint = bestBreakPoint;
                        float bestFit = float.MaxValue;
                        
                        for (int i = searchStart; i <= searchEnd; i++)
                        {
                            if (i >= displayName.Length) break;
                            
                            char currentChar = displayName[i];
                            bool isGoodBreak = false;
                            
                            // 检查是否为好的换行点
                            if (currentChar == ' ' || currentChar == '_' || currentChar == '-' || currentChar == '.' ||
                                currentChar == '（' || currentChar == '(' || currentChar == '【' || currentChar == '[' ||
                                currentChar == '，' || currentChar == ',' || currentChar == '。' || currentChar == '、')
                            {
                                isGoodBreak = true;
                            }
                            else if (i > 0 && i < displayName.Length)
                            {
                                // 中英文边界
                                char prevChar = displayName[i - 1];
                                if (IsChinese(prevChar) != IsChinese(currentChar))
                                {
                                    isGoodBreak = true;
                                }
                            }
                            
                            if (i > 0)
                            {
                                string testLine = displayName.Substring(0, i);
                                // 使用 GenericTypographic 获得更准确的测量
                                SizeF testSize = g.MeasureString(testLine, textFont, int.MaxValue, measureFormat);
                                
                                if (testSize.Width <= availableWidth)
                                {
                                    float diff = availableWidth - testSize.Width;
                                    if (isGoodBreak || diff < bestFit)
                                    {
                                        if (isGoodBreak || diff < bestFit * 0.8f) // 优先选择好的换行点
                                        {
                                            bestFit = diff;
                                            finalBreakPoint = i;
                                            if (isGoodBreak) break; // 找到好的换行点就停止
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (finalBreakPoint > 0 && finalBreakPoint < displayName.Length)
                        {
                            line1 = displayName.Substring(0, finalBreakPoint);
                            // 如果换行点在分隔符处，跳过该字符
                            char breakChar = displayName[finalBreakPoint];
                            if (breakChar == ' ' || breakChar == '_' || breakChar == '-' || breakChar == '.' ||
                                breakChar == '，' || breakChar == ',' || breakChar == '。' || breakChar == '、')
                            {
                                line2 = displayName.Substring(finalBreakPoint + 1).TrimStart(' ');
                            }
                            else
                            {
                                line2 = displayName.Substring(finalBreakPoint).TrimStart(' ');
                            }
                        }
                        else
                        {
                            // 如果找不到合适的换行点，使用最佳位置
                            line1 = displayName.Substring(0, bestBreakPoint);
                            line2 = displayName.Substring(bestBreakPoint).TrimStart(' ');
                        }
                    }
                    
                    // 确保两行都不为空
                    if (string.IsNullOrEmpty(line1))
                    {
                        line1 = displayName;
                        line2 = "";
                    }
                    
                    // 绘制第一行（自适应对齐，使用扩展宽度）
                    if (!string.IsNullOrEmpty(line1))
                    {
                        // 文字区域居中于图标，但宽度更宽
                        int textX = x - (TEXT_WIDTH - ICON_SIZE) / 2;
                        Rectangle line1Rect = new Rectangle(textX, y + ICON_SIZE + 2, TEXT_WIDTH, (int)(lineHeight + 1));
                        
                        // 测量第一行文字宽度
                        SizeF line1Size = g.MeasureString(line1, textFont, int.MaxValue, measureFormat);
                        if (line1Size.Width < availableWidth * 0.8f)
                        {
                            format.Alignment = StringAlignment.Center; // 文字少时居中
                        }
                        else
                        {
                            format.Alignment = StringAlignment.Near; // 文字多时左对齐
                        }
                        
                        format.Trimming = StringTrimming.None; // 不使用省略号
                        g.DrawString(line1, textFont, textBrush, line1Rect, format);
                    }
                    
                    // 绘制第二行（自适应对齐，使用扩展宽度）
                    if (!string.IsNullOrEmpty(line2))
                    {
                        // 文字区域居中于图标，但宽度更宽
                        int textX = x - (TEXT_WIDTH - ICON_SIZE) / 2;
                        Rectangle line2Rect = new Rectangle(textX, y + ICON_SIZE + 2 + (int)(lineHeight + 2), TEXT_WIDTH, (int)(lineHeight + 1));
                        
                        // 测量第二行文字宽度
                        SizeF line2Size = g.MeasureString(line2, textFont, int.MaxValue, measureFormat);
                        if (line2Size.Width < availableWidth * 0.8f)
                        {
                            format.Alignment = StringAlignment.Center; // 文字少时居中
                        }
                        else
                        {
                            format.Alignment = StringAlignment.Near; // 文字多时左对齐
                        }
                        
                        format.Trimming = StringTrimming.None; // 不使用省略号
                        g.DrawString(line2, textFont, textBrush, line2Rect, format);
                    }
                }
                else
                {
                    // 单行显示：文字少时居中，文字多时左对齐
                    int textX = x - (TEXT_WIDTH - ICON_SIZE) / 2;
                    Rectangle textRect = new Rectangle(textX, y + ICON_SIZE + 2, TEXT_WIDTH, TEXT_HEIGHT - 2);
                    
                    // 如果文字宽度小于可用宽度的80%，则居中显示
                    if (fullTextSize.Width < availableWidth * 0.8f)
                    {
                        format.Alignment = StringAlignment.Center; // 居中
                    }
                    else
                    {
                        format.Alignment = StringAlignment.Near; // 左对齐
                    }
                    
                    format.Trimming = StringTrimming.None; // 不使用省略号
                    g.DrawString(displayName, textFont, textBrush, textRect, format);
                }
            }
        }
        
        /// <summary>
        /// 绘制滚动条
        /// </summary>
        private void DrawScrollBar(Graphics g)
        {
            const int SCROLLBAR_WIDTH = 8; // 滚动条宽度
            const int SCROLLBAR_MARGIN = 4; // 滚动条边距
            const int MIN_THUMB_HEIGHT = 20; // 滑块最小高度
            
            // 计算滚动条位置（在窗口右侧，与分类栏相对）
            int scrollbarX, scrollbarY, scrollbarHeight;
            
            if (isSidebarLeft)
            {
                // 侧边栏在左侧，滚动条在右侧
                scrollbarX = Width - (PADDING + SHADOW_SIZE) - SCROLLBAR_WIDTH - SCROLLBAR_MARGIN;
            }
            else
            {
                // 侧边栏在右侧，滚动条也在右侧（在分类栏左侧）
                scrollbarX = Width - (PADDING + SHADOW_SIZE) - CATEGORY_BAR_WIDTH - CATEGORY_ICON_GAP - SCROLLBAR_WIDTH - SCROLLBAR_MARGIN;
            }
            
            scrollbarY = PADDING + SHADOW_SIZE;
            scrollbarHeight = Height - (PADDING + SHADOW_SIZE) * 2;
            
            // 计算滚动条轨道区域
            Rectangle trackRect = new Rectangle(scrollbarX, scrollbarY, SCROLLBAR_WIDTH, scrollbarHeight);
            
            // 绘制滚动条轨道（半透明背景）
            using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
            {
                g.FillRectangle(trackBrush, trackRect);
            }
            
            // 计算滑块位置和大小
            int totalContentHeight = scrollbarHeight + maxScrollOffset; // 总内容高度
            int thumbHeight = Math.Max(MIN_THUMB_HEIGHT, (int)((double)scrollbarHeight * scrollbarHeight / totalContentHeight));
            
            // 计算滑块位置（根据当前滚动偏移量）
            int thumbY;
            if (maxScrollOffset > 0)
            {
                thumbY = scrollbarY + (int)((double)scrollOffsetY / maxScrollOffset * (scrollbarHeight - thumbHeight));
            }
            else
            {
                thumbY = scrollbarY;
            }
            
            // 确保滑块在轨道内
            thumbY = Math.Max(scrollbarY, Math.Min(thumbY, scrollbarY + scrollbarHeight - thumbHeight));
            
            // 绘制滑块
            Rectangle thumbRect = new Rectangle(scrollbarX, thumbY, SCROLLBAR_WIDTH, thumbHeight);
            using (SolidBrush thumbBrush = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
            {
                g.FillRectangle(thumbBrush, thumbRect);
            }
            
            // 绘制滑块边框
            using (Pen thumbPen = new Pen(Color.FromArgb(180, 255, 255, 255), 1))
            {
                g.DrawRectangle(thumbPen, thumbRect);
            }
        }
        
        /// <summary>
        /// 判断字符是否为中文字符
        /// </summary>
        private bool IsChinese(char c)
        {
            return c >= 0x4E00 && c <= 0x9FFF;
        }
        
        /// <summary>
        /// 判断是否为好的换行点
        /// </summary>
        private bool IsGoodBreakPoint(string text, int index)
        {
            if (index <= 0 || index >= text.Length) return false;
            char currentChar = text[index];
            return currentChar == ' ' || currentChar == '_' || currentChar == '-' || 
                   currentChar == '.' || currentChar == '（' || currentChar == '(' || 
                   currentChar == '【' || currentChar == '[';
        }

        private void LoadItems()
        {
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, List<DesktopItem>>>(json);
                    if (loaded != null)
                    {
                        categories = loaded;
                        // 验证文件是否存在并迁移旧数据
                        foreach (var category in categories.Keys.ToList())
                        {
                            categories[category] = categories[category]
                                .Select(i => 
                                {
                                    // 兼容旧数据：如果 OriginalPath 为空，说明是旧数据，设置为路径引用类型
                                    if (string.IsNullOrEmpty(i.OriginalPath))
                                    {
                                        i.OriginalPath = i.FilePath;
                                        i.IsRealFile = false; // 旧数据默认为路径引用
                                    }
                                    return i;
                                })
                                .Where(i => 
                                {
                                    // 对于真实文件，检查复制的文件是否存在
                                    // 对于路径引用，检查原始路径是否存在
                                    string pathToCheck = i.IsRealFile ? i.FilePath : (i.OriginalPath ?? i.FilePath);
                                    return File.Exists(pathToCheck) || Directory.Exists(pathToCheck);
                                })
                                .ToList();
                            
                            // 初始化所有图标的缩放值
                            foreach (var item in categories[category])
                            {
                                itemScales[item] = 1.0f;
                            }
                        }
                        
                        // 迁移旧的"默认"分类到"桌面"分类
                        if (categories.ContainsKey("默认"))
                        {
                            // 如果"桌面"分类不存在，创建它
                            if (!categories.ContainsKey(DEFAULT_CATEGORY))
                            {
                                categories[DEFAULT_CATEGORY] = new List<DesktopItem>();
                            }
                            
                            // 将"默认"分类的数据合并到"桌面"分类
                            categories[DEFAULT_CATEGORY].AddRange(categories["默认"]);
                            
                            // 如果当前分类是"默认"，切换到"桌面"
                            if (currentCategory == "默认")
                            {
                                currentCategory = DEFAULT_CATEGORY;
                            }
                            
                            // 删除"默认"分类
                            categories.Remove("默认");
                            
                            // 保存迁移后的数据
                            SaveItems();
                        }
                        
                        // 确保"桌面"分类在最顶部，但保持其他分类的顺序
                        EnsureDesktopCategoryFirst();
                        
                        // 如果当前分类不存在，使用第一个分类
                        if (!categories.ContainsKey(currentCategory) && categories.Count > 0)
                        {
                            currentCategory = categories.Keys.First();
                        }
                    }
                }
                catch
                {
                    // 如果加载失败，可能是旧格式，尝试兼容
                    try
                    {
                        string json = File.ReadAllText(configPath);
                        var loaded = JsonConvert.DeserializeObject<List<DesktopItem>>(json);
                        if (loaded != null)
                        {
                            // 迁移到新格式（旧数据默认为路径引用）
                            categories[DEFAULT_CATEGORY] = loaded
                                .Where(i => File.Exists(i.FilePath) || Directory.Exists(i.FilePath))
                                .Select(i => 
                                {
                                    // 设置旧数据为路径引用类型
                                    if (string.IsNullOrEmpty(i.OriginalPath))
                                    {
                                        i.OriginalPath = i.FilePath;
                                    }
                                    i.IsRealFile = false;
                                    return i;
                                })
                                .ToList();
                            
                            foreach (var item in categories[DEFAULT_CATEGORY])
                            {
                                itemScales[item] = 1.0f;
                            }
                            
                            SaveItems(); // 保存为新格式
                        }
                    }
                    catch { }
                }
            }
        }

        private void SaveItems()
        {
            if (string.IsNullOrEmpty(configPath))
            {
                return;
            }
            
            try
            {
                if (categories == null || categories.Count == 0)
                {
                    return;
                }
                
                string json = JsonConvert.SerializeObject(categories, Formatting.Indented);
                if (!string.IsNullOrEmpty(json))
                {
                    // 确保目录存在
                    string directory = Path.GetDirectoryName(configPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    File.WriteAllText(configPath, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
                // 不显示错误通知，避免干扰用户
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            if (IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }
        
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (value && IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
                // 窗口显示时，如果鼠标不在窗口内且没有操作进行，启动5秒自动关闭定时器
                // 如果鼠标在窗口内或有操作进行中，不启动定时器
                if (!isMouseInside && !isOperationInProgress)
                {
                    StartAutoCloseTimer(5000);
                }
            }
            else
            {
                StopAutoCloseTimer();
                isMouseInside = false;
            }
        }
        
        /// <summary>
        /// 处理窗口消息，用于检测窗口失去激活状态（点击桌面或其他窗口时立即关闭）
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            // 处理 WM_ACTIVATE 消息
            if (m.Msg == WM_ACTIVATE)
            {
                // LOWORD(wParam) 表示激活状态：WA_INACTIVE (0) 表示窗口失去激活状态
                uint wParam = (uint)m.WParam.ToInt64();
                uint activateState = wParam & 0xFFFF;
                
                if (activateState == WA_INACTIVE)
                {
                    // 窗口失去激活状态（用户点击了其他地方，如桌面或其他窗口）
                    // 停止拖拽检测定时器（如果正在运行）
                    dragDetectionTimer?.Stop();
                    
                    // 如果已经有操作进行中（比如已经在拖拽），不关闭窗口
                    if (isOperationInProgress)
                    {
                        return; // 不关闭窗口
                    }
                    
                    // 检查鼠标按键是否被按下（可能是拖拽的开始）
                    bool mouseButtonPressed = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0 || 
                                             (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
                    
                    if (mouseButtonPressed)
                    {
                        // 鼠标按键被按下，可能是拖拽操作
                        // 使用周期性检查，延迟关闭，等待 DragEnter/DragOver 事件触发
                        // 定时器会周期性检查鼠标位置和按键状态
                        dragDetectionTimer?.Stop();
                        dragDetectionTimer?.Start();
                    }
                    else
                    {
                        // 鼠标按键没有被按下，只是点击，立即关闭窗口
                        StopAutoCloseTimer();
                        this.Hide();
                        return; // 不再传递消息
                    }
                }
            }
            
            base.WndProc(ref m);
        }
        
        #region 通知方法（ShareX风格）
        
        /// <summary>
        /// 调整通知位置（通过反射访问 NotificationForm 实例）
        /// </summary>
        private void AdjustNotificationPosition(int offsetX, int offsetY)
        {
            try
            {
                // 使用反射获取 NotificationForm 的静态 instance 字段
                Type notificationFormType = typeof(NotificationForm);
                FieldInfo instanceField = notificationFormType.GetField("instance", 
                    BindingFlags.NonPublic | BindingFlags.Static);
                
                if (instanceField != null)
                {
                    object instance = instanceField.GetValue(null);
                    if (instance != null && instance is Form form && !form.IsDisposed)
                    {
                        // 调整位置
                        form.Location = new Point(form.Location.X + offsetX, form.Location.Y + offsetY);
                    }
                }
            }
            catch
            {
                // 如果反射失败，忽略错误
            }
        }
        
        /// <summary>
        /// 显示通知（ShareX 风格，右下角渐变动画）
        /// </summary>
        private void ShowNotification(string text, string title = "通知", int duration = 2000, MessageBoxIcon icon = MessageBoxIcon.Information)
        {
            try
            {
                // 根据图标类型设置不同的背景色
                Color backgroundColor = Color.FromArgb(50, 50, 50); // 默认背景色
                Color borderColor = Color.FromArgb(40, 40, 40); // 默认边框色
                
                if (icon == MessageBoxIcon.Error || icon == MessageBoxIcon.Warning)
                {
                    // 错误或警告使用稍微不同的颜色
                    backgroundColor = Color.FromArgb(60, 40, 40);
                    borderColor = Color.FromArgb(50, 30, 30);
                }
                
                // 创建通知配置
                NotificationFormConfig config = new NotificationFormConfig
                {
                    Duration = duration, // 显示持续时间（毫秒）
                    FadeDuration = 500, // 渐变动画持续时间（毫秒）
                    Placement = ContentAlignment.BottomRight, // 右下角位置
                    Offset = 80, // 距离屏幕右侧80像素，避免和侧边栏重叠
                    Size = new Size(300, 80), // 通知窗口大小
                    Title = title,
                    Text = text,
                    BackgroundColor = backgroundColor,
                    BorderColor = borderColor,
                    TextColor = Color.FromArgb(210, 210, 210), // 文本颜色
                    TitleColor = Color.FromArgb(240, 240, 240) // 标题颜色
                };
                
                // 显示通知（在主线程中，使用 ShareX 风格）
                if (this.InvokeRequired)
                {
                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                    {
                        NotificationForm.Show(config);
                    });
                }
                else
                {
                    NotificationForm.Show(config);
                }
            }
            catch
            {
                // 如果通知显示失败，回退到 MessageBox（仅作为最后手段）
                try
                {
                    MessageBox.Show(text, title, MessageBoxButtons.OK, icon);
                }
                catch
                {
                    // 如果 MessageBox 也失败，忽略错误
                }
            }
        }
        
        #endregion
    }

    public class DesktopItem
    {
        public string FilePath { get; set; } // 对于桌面文件，这是复制后的路径；对于其他文件，这是原始路径
        public string DisplayName { get; set; }
        public string OriginalPath { get; set; } // 原始路径（用于路径引用类型）
        public bool IsRealFile { get; set; } // true=真实文件（复制），false=路径引用
    }
}

