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
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShareX.ScreenCaptureLib;
using ShareX.HelpersLib;
using ShareX.MediaLib;
using Newtonsoft.Json;
using ShareX.ImageEffectsLib;
using ShareX;
using Newtonsoft.Json.Serialization;

namespace Sidebar
{
    public partial class SidebarForm : Form
    {
        #region Constants and Configuration
        
        // 调试模式：设置为 false 以移除所有调试输出（商业发布）
#if DEBUG
        private const bool ENABLE_DEBUG_LOGGING = true;
#else
        private const bool ENABLE_DEBUG_LOGGING = false;
#endif
        
        #endregion Constants and Configuration
        
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowDisplayAffinity(IntPtr hwnd, out uint dwAffinity);

        // SetWindowDisplayAffinity 常量
        private const uint WDA_NONE = 0x00000000;           // 正常显示，可以被截图
        private const uint WDA_MONITOR = 0x00000001;        // 仅在指定显示器上显示，可以被截图
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011; // 从屏幕捕获中排除（Windows 10 1903+）

        private const int ULW_ALPHA = 0x00000002;
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

        // 侧边栏宽度
        private const int SIDEBAR_WIDTH = 70;
        // 圆角半径
        private const int CORNER_RADIUS = 16;
        // 吸附距离（像素）
        private const int DOCK_THRESHOLD = 50;
        // 阴影大小
        private const int SHADOW_SIZE = 8;
        // 图标大小
        private const int ICON_SIZE = 40;
        // 图标间距
        private const int ICON_SPACING = 10;
        // 顶部边距
        private const int TOP_MARGIN = 20;
        // 顶部偏移（为最大化窗口的程序让开关闭按钮留出空间）
        private const int TOP_OFFSET = 40;
        // 自动收缩相关
        private const int COLLAPSED_WIDTH = 0; // 收缩后的宽度（像素，0表示完全隐藏）
        private const int EDGE_DETECTION_WIDTH = 5; // 边缘检测宽度（像素）
        
        // 锁定按钮相关
        private const int LOCK_BUTTON_SIZE = 15; // 锁定按钮大小（像素）
        private const int LOCK_BUTTON_BOTTOM_MARGIN = 15; // 锁定按钮底部边距（像素）
        private const int LOCK_BUTTON_RIGHT_MARGIN = 3; // 锁定按钮右侧边距（像素）
        private Color lockButtonColorDefault = Color.FromArgb(255, 80, 80, 80); // #505050
        private Color lockButtonColorActive = Color.FromArgb(255, 0, 225, 16); // #00E110
        private bool isAutoHideLocked = true; // 自动隐藏是否锁定（默认锁定，不自动收缩）
        
        // 停靠位置
        private DockSide dockSide = DockSide.Right;
        
        // 拖拽相关
        private bool isDragging = false;
        private Point dragStartPoint;
        private Point formStartLocation;
        
        // 动画相关
        private Timer animationTimer;
        private Point animationStartPos;
        private Point animationTargetPos;
        private int animationSteps = 20;
        private int currentAnimationStep = 0;
        private bool isAnimating = false;
        
        // 背景颜色（Alpha = 5）
        private Color backgroundColor = Color.FromArgb(5, 255, 255, 255); // Alpha = 5
        private Color hoverColor = Color.FromArgb(80, 255, 255, 255); // 悬停时轻微不透明（已禁用）
        
        // 图标按钮列表
        private List<SidebarButton> buttons = new List<SidebarButton>();
        private SidebarButton hoveredButton = null;
        
        // 工具提示相关
        private TooltipForm tooltipForm = null;
        private Timer tooltipTimer; // 延迟显示工具提示的定时器
        private const int TOOLTIP_DELAY = 500; // 工具提示延迟显示时间（毫秒）
        
        // 图标模式：true = 使用 PNG 图片，false = 使用 Emoji
        private bool usePngIcons = false; // 默认使用 Emoji，可以改为 true 使用 PNG
        
        // 图标缩放动画相关
        private Timer iconScaleTimer;
        private Dictionary<SidebarButton, float> buttonScales = new Dictionary<SidebarButton, float>();
        private const float TARGET_SCALE = 1.3f; // 目标放大倍数（30%）
        private const float ANIMATION_DURATION = 200f; // 动画持续时间（毫秒）
        private Dictionary<SidebarButton, long> animationStartTimes = new Dictionary<SidebarButton, long>();
        
        // 自动收缩相关
        private Timer autoHideTimer; // 自动隐藏定时器
        private Timer collapseAnimationTimer; // 收缩动画定时器
        private bool isCollapsed = false; // 是否已收缩
        private bool isCollapsing = false; // 是否正在收缩/展开
        private float currentWidth = SIDEBAR_WIDTH; // 当前宽度（用于动画）
        private float targetWidth = SIDEBAR_WIDTH; // 目标宽度
        
        // 录制相关
        private RecordSettingsForm recordSettingsForm;
        private LoadingForm loadingForm;
        private ScreenRecorder currentRecorder;
        private bool isRecording = false;
        private RecordType currentRecordType;
        private int gifFPS = 10;
        private FFmpegOptions ffmpegOptions;
        private string tempRecordPath;
        private Timer escKeyTimer; // 用于监听 Ctrl+ESC 键（停止录制）
        
        // 快捷键相关
        private HotkeySettingsForm hotkeySettingsForm;
        private List<ToolButtonInfo> toolButtonInfos = new List<ToolButtonInfo>();
        private HotkeyForm globalHotkeyForm; // 全局快捷键管理器
        private Dictionary<string, HotkeyInfo> registeredHotkeys = new Dictionary<string, HotkeyInfo>();
        private Dictionary<string, HotkeyConfig> hotkeyConfigs = new Dictionary<string, HotkeyConfig>();
        
        public SidebarForm()
        {
            InitializeComponent();
            InitializeGlobalHotkeys(); // 先初始化全局快捷键管理器
            InitializeSidebar(); // 然后初始化侧边栏（会注册快捷键）
        }
        
        private void InitializeComponent()
        {
            SuspendLayout();
            
            // 窗体属性
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            // 使用 UpdateLayeredWindow 实现每像素透明，背景完全透明
            
            // 启用双缓冲和自定义绘制
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer | 
                     ControlStyles.UserPaint | 
                     ControlStyles.ResizeRedraw, true);
            
            // 动画定时器
            animationTimer = new Timer();
            animationTimer.Interval = 16; // ~60fps
            animationTimer.Tick += AnimationTimer_Tick;
            
            // 图标缩放动画定时器
            iconScaleTimer = new Timer();
            iconScaleTimer.Interval = 16; // ~60fps
            iconScaleTimer.Tick += IconScaleTimer_Tick;
            
            // 自动隐藏定时器（提高检测频率以提升响应速度）
            autoHideTimer = new Timer();
            autoHideTimer.Interval = 16; // ~60fps，提高检测频率以提升鼠标操控精准度
            autoHideTimer.Tick += AutoHideTimer_Tick;
            autoHideTimer.Start();
            
            // 收缩动画定时器
            collapseAnimationTimer = new Timer();
            collapseAnimationTimer.Interval = 16; // ~60fps
            collapseAnimationTimer.Tick += CollapseAnimationTimer_Tick;
            
            // Ctrl+ESC 键监听定时器（用于结束录制）
            escKeyTimer = new Timer();
            escKeyTimer.Interval = 50; // 50ms 检查一次
            escKeyTimer.Tick += EscKeyTimer_Tick;
            
            // 事件处理
            Load += SidebarForm_Load;
            MouseDown += SidebarForm_MouseDown;
            MouseMove += SidebarForm_MouseMove;
            MouseUp += SidebarForm_MouseUp;
            Paint += SidebarForm_Paint;
            MouseLeave += SidebarForm_MouseLeave;
            
            ResumeLayout(false);
        }
        
        private void SidebarForm_Load(object sender, EventArgs e)
        {
            // 窗口加载后，设置排除属性并更新分层窗口
            if (IsHandleCreated)
            {
                SetWindowExcludeFromCapture();
                if (Visible)
                {
                    UpdateLayeredWindowBitmap();
                }
            }
        }
        
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // 窗口句柄创建后，立即设置排除属性
            SetWindowExcludeFromCapture();
        }
        
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // 确保窗口始终显示在前端
            TopMost = true;
            // 窗口显示后，确保排除属性已设置并更新分层窗口
            if (IsHandleCreated)
            {
                SetWindowExcludeFromCapture();
                if (Visible)
                {
                    UpdateLayeredWindowBitmap();
                }
            }
            
            // 窗口显示后，确保快捷键已注册（如果之前注册失败）
            // 这解决了启动时快捷键延迟生效的问题
            if (globalHotkeyForm != null && globalHotkeyForm.IsHandleCreated)
            {
                // 如果快捷键配置已加载但未注册，重新注册
                if (hotkeyConfigs != null && hotkeyConfigs.Count > 0 && 
                    (registeredHotkeys == null || registeredHotkeys.Count == 0))
                {
                    LoadAndRegisterHotkeys();
                }
            }
        }
        
        // 设置窗口从屏幕捕获中排除（适用于截图和录屏）
        private void SetWindowExcludeFromCapture()
        {
            if (IsHandleCreated && Handle != IntPtr.Zero)
            {
                try
                {
                    // 使用 WDA_EXCLUDEFROMCAPTURE 让窗口在屏幕捕获时被排除
                    // 这适用于 Windows 10 1903 及更高版本
                    bool success = SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE);
                    if (!success)
                    {
                        // 如果设置失败（可能是旧版本 Windows），尝试使用 WDA_MONITOR
                        // 或者记录错误但不影响程序运行
                        int error = Marshal.GetLastWin32Error();
                        // 静默失败，不影响程序运行
                    }
                }
                catch
                {
                    // 如果 API 不存在（旧版本 Windows），静默失败
                }
            }
        }
        
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            // 确保窗口始终显示在前端
            if (value)
            {
                TopMost = true;
            }
            // 窗口变为可见时，更新分层窗口
            if (value && IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }
        
        private void InitializeSidebar()
        {
            // 初始位置：右侧
            DockToRight();
            
            // 添加常用工具图标按钮
            // 使用方式：
            // 1. 只使用 Emoji：AddToolButton("名称", "📷", () => { 功能代码 });
            // 2. 使用 PNG 图片：AddToolButton("名称", "📷", () => { 功能代码 }, "icons/icon.png");
            // 注意：需要将 usePngIcons 设置为 true 才会使用 PNG 图片
            
            // 桌面图标（最顶部）
            AddToolButton("桌面", "🖥️", () => {
                OpenDesktop();
            });
            
            AddToolButton("截图", "📷", () => {
                CaptureRegionAndSave();
            }); // 可以添加第四个参数，如 "icons/screenshot.png"
            
            AddToolButton("滚动截图", "📜", () => {
                CaptureScrollingAndSave();
            }); // 滚动截图功能
            
            AddToolButton("录制", "🎬", () => {
                ShowRecordSettings();
            }); // 屏幕录制功能
            
            AddToolButton("Pin", "📌", () => {
                PinToScreenFromScreen();
            }); // Pin to Screen 功能
            
            AddToolButton("颜色选择器", "🎨", () => {
                OpenScreenColorPicker();
            }); // 屏幕拾色器功能
            
            AddToolButton("尺子", "📏", () => {
                OpenScreenRuler();
            }); // 屏幕尺子功能
            
            AddToolButton("图像美化", "✨", () => {
                OpenImageBeautifier();
            }); // 图像美化功能
            
            AddToolButton("图片特效", "🎭", () => {
                OpenImageEffects();
            }); // 图片特效功能
            
            AddToolButton("图像编辑器", "✏️", () => {
                OpenImageEditor();
            }); // 图像编辑器功能
            
            AddToolButton("图像分割器", "✂️", () => {
                OpenImageSplitter();
            }); // 图像分割器功能
            
            AddToolButton("图像合并", "🔗", () => {
                OpenImageCombiner();
            }); // 图像合并功能
            
            AddToolButton("图像缩略图", "🖼️", () => {
                OpenImageThumbnailer();
            }); // 图像缩略图功能
            
            AddToolButton("视频转换器", "🎥", () => {
                OpenVideoConverter();
            }); // 视频转换器功能
            
            AddToolButton("文件重命名", "📝", () => {
                OpenFileRenamer();
            }); // 文件重命名功能
            
            AddToolButton("系统清理", "🧹", () => {
                OpenSystemCleaner();
            }); // 系统清理功能
            
            AddToolButton("设置", "⚙️", () => {
                OpenHotkeySettings();
            }); // 设置窗口
            
            // 初始化全局快捷键（确保 globalHotkeyForm 句柄已创建）
            if (globalHotkeyForm != null)
            {
                // 强制创建句柄（如果尚未创建）
                if (!globalHotkeyForm.IsHandleCreated)
                {
                    // 通过访问 Handle 属性强制创建句柄
                    IntPtr handle = globalHotkeyForm.Handle;
                }
                // 现在可以安全地注册快捷键
                LoadAndRegisterHotkeys();
            }
            
            // 初始化工具提示定时器
            tooltipTimer = new Timer();
            tooltipTimer.Interval = TOOLTIP_DELAY;
            tooltipTimer.Tick += TooltipTimer_Tick;
        }
        
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (不在任务栏显示)
                cp.ExStyle |= 0x80000;   // WS_EX_LAYERED (支持透明)
                return cp;
            }
        }
        
        private void SidebarForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 隐藏工具提示
                HideTooltip();
                tooltipTimer.Stop();
                
                // 检查是否点击了锁定按钮
                if (IsLockButtonClicked(e.Location))
                {
                    ToggleAutoHideLock();
                    return;
                }
                
                // 检查是否点击了图标按钮
                SidebarButton clickedButton = GetButtonAtPoint(e.Location);
                if (clickedButton != null)
                {
                    clickedButton.OnClick();
                    return;
                }
                
                // 否则开始拖拽
                isDragging = true;
                dragStartPoint = e.Location;
                formStartLocation = Location;
                Cursor = Cursors.SizeAll;
            }
        }
        
        private void SidebarForm_MouseMove(object sender, MouseEventArgs e)
        {
            // 如果侧边栏是收缩状态，检查鼠标是否在边缘区域
            if (isCollapsed)
            {
                // 将窗体内部坐标转换为屏幕坐标（优化：直接使用屏幕坐标提升精准度）
                Point screenPos = PointToScreen(e.Location);
                // 检查鼠标是否在边缘区域（使用屏幕坐标）
                if (IsMouseInEdgeArea(screenPos))
                {
                    // 展开侧边栏
                    ExpandSidebar();
                }
            }
            // 如果侧边栏是展开状态，鼠标在区域内，定时器会持续检查
            // 如果鼠标离开区域会自动收缩（无需额外处理）
            
            if (isDragging)
            {
                int deltaX = e.X - dragStartPoint.X;
                int deltaY = e.Y - dragStartPoint.Y;
                
                Point newLocation = new Point(
                    formStartLocation.X + deltaX,
                    formStartLocation.Y + deltaY
                );
                
            // 限制在屏幕范围内（优化：使用更精确的边界计算）
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            int currentSidebarWidth = (int)currentWidth;
            int minX = screenBounds.Left - SHADOW_SIZE;
            int maxX = screenBounds.Right - currentSidebarWidth - SHADOW_SIZE;
            int minY = screenBounds.Top + TOP_OFFSET;
            int maxY = screenBounds.Bottom - Height;
            
            newLocation.X = Math.Max(minX, Math.Min(newLocation.X, maxX));
            newLocation.Y = Math.Max(minY, Math.Min(newLocation.Y, maxY));
                
                Location = newLocation;
            }
            else if (!isCollapsed)
            {
                // 检查鼠标悬停（只在展开状态下，优化：提前判断避免嵌套）
                SidebarButton button = GetButtonAtPoint(e.Location);
                if (button != hoveredButton)
                {
                    // 如果之前有悬停的按钮，重置其动画时间
                    if (hoveredButton != null && animationStartTimes.ContainsKey(hoveredButton))
                    {
                        animationStartTimes[hoveredButton] = DateTime.Now.Ticks / 10000;
                    }
                    
                    hoveredButton = button;
                    
                    // 重置当前按钮的动画开始时间，确保立即开始动画
                    if (button != null)
                    {
                        animationStartTimes[button] = DateTime.Now.Ticks / 10000;
                    }
                    
                    // 立即启动图标缩放动画，无延迟
                    iconScaleTimer.Start();
                    
                    // 处理工具提示
                    if (button != null)
                    {
                        // 停止之前的定时器
                        tooltipTimer.Stop();
                        // 隐藏之前的工具提示
                        HideTooltip();
                        // 启动新的定时器
                        tooltipTimer.Start();
                    }
                    else
                    {
                        // 鼠标不在按钮上，隐藏工具提示
                        tooltipTimer.Stop();
                        HideTooltip();
                    }
                    
                    // 使用 UpdateLayeredWindow 时，需要直接调用更新方法
                    if (IsHandleCreated)
                    {
                        UpdateLayeredWindowBitmap();
                    }
                }
            }
        }
        
        private void SidebarForm_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && isDragging)
            {
                isDragging = false;
                Cursor = Cursors.Default;
                
                // 检查并执行吸附动画
                CheckAndDock();
            }
        }
        
        private void SidebarForm_MouseLeave(object sender, EventArgs e)
        {
            hoveredButton = null;
            // 鼠标离开时，图标缩放动画会继续运行直到回到原始大小
            // 使用 UpdateLayeredWindow 时，需要直接调用更新方法
            if (IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }
        
        // 图标缩放动画定时器事件 - 使用基于时间的平滑缓动函数（消除抖动）
        private void IconScaleTimer_Tick(object sender, EventArgs e)
        {
            long currentTime = DateTime.Now.Ticks / 10000; // 转换为毫秒
            bool needUpdate = false;
            bool allAtTarget = true;
            
            foreach (var button in buttons)
            {
                // 初始化缩放值
                if (!buttonScales.ContainsKey(button))
                {
                    buttonScales[button] = 1.0f;
                }
                
                float currentScale = buttonScales[button];
                float targetScale = (button == hoveredButton) ? TARGET_SCALE : 1.0f;
                
                // 如果目标值改变，重置动画开始时间
                if (!animationStartTimes.ContainsKey(button) || 
                    Math.Abs(currentScale - targetScale) > 0.01f)
                {
                    // 检查是否需要重置动画（目标改变时）
                    float lastTarget = (button == hoveredButton) ? TARGET_SCALE : 1.0f;
                    if (!animationStartTimes.ContainsKey(button) || 
                        Math.Abs(currentScale - lastTarget) < 0.01f)
                    {
                        animationStartTimes[button] = currentTime;
                    }
                }
                
                // 计算基于时间的缓动值
                long startTime = animationStartTimes.ContainsKey(button) ? animationStartTimes[button] : currentTime;
                long elapsed = currentTime - startTime;
                float progress = Math.Min(1.0f, elapsed / ANIMATION_DURATION);
                
                // 使用 ease-out 缓动函数实现平滑过渡（消除抖动）
                float easedProgress = 1.0f - (float)Math.Pow(1.0f - progress, 3); // cubic ease-out
                
                // 计算起始值和目标值
                float startScale = 1.0f; // 总是从1.0开始
                if (progress < 0.01f && Math.Abs(currentScale - 1.0f) > 0.01f && Math.Abs(currentScale - TARGET_SCALE) > 0.01f)
                {
                    // 如果动画刚开始且当前值不在起始或目标值，从当前值开始
                    startScale = currentScale;
                }
                
                // 计算新的缩放值
                float newScale = startScale + (targetScale - startScale) * easedProgress;
                
                // 只有当变化足够大时才更新，避免微小抖动
                if (Math.Abs(newScale - currentScale) > 0.0001f)
                {
                    buttonScales[button] = newScale;
                    needUpdate = true;
                }
                
                // 检查是否到达目标
                if (progress < 0.99f || Math.Abs(newScale - targetScale) > 0.01f)
                {
                    allAtTarget = false;
                }
            }
            
            // 如果所有按钮都达到目标值，停止定时器
            if (allAtTarget)
            {
                iconScaleTimer.Stop();
            }
            
            // 如果需要更新，刷新窗口
            if (needUpdate && IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }
        
        private void CheckAndDock()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            
            // 判断更靠近左边还是右边
            int distanceToLeft = Math.Abs(Location.X - screenBounds.Left);
            int distanceToRight = Math.Abs((Location.X + Width) - screenBounds.Right);
            
            DockSide newDockSide = dockSide;
            
            if (distanceToLeft < DOCK_THRESHOLD || (distanceToLeft < distanceToRight && distanceToLeft < DOCK_THRESHOLD * 2))
            {
                newDockSide = DockSide.Left;
            }
            else if (distanceToRight < DOCK_THRESHOLD)
            {
                newDockSide = DockSide.Right;
            }
            
            // 如果需要改变停靠位置，执行动画
            if (newDockSide != dockSide)
            {
                StartDockAnimation(newDockSide);
            }
            else
            {
                // 即使不改变位置，也确保完全对齐
                if (newDockSide == DockSide.Left)
                {
                    DockToLeft();
                }
                else
                {
                    DockToRight();
                }
            }
        }
        
        private void StartDockAnimation(DockSide targetSide)
        {
            if (isAnimating) return;
            
            animationStartPos = Location;
            dockSide = targetSide;
            
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            int currentSidebarWidth = (int)currentWidth;
            if (targetSide == DockSide.Left)
            {
                animationTargetPos = new Point(screenBounds.Left, screenBounds.Top + TOP_OFFSET);
            }
            else
            {
                animationTargetPos = new Point(screenBounds.Right - currentSidebarWidth - SHADOW_SIZE * 2, screenBounds.Top + TOP_OFFSET);
            }
            
            isAnimating = true;
            currentAnimationStep = 0;
            animationTimer.Start();
        }
        
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            currentAnimationStep++;
            
            if (currentAnimationStep >= animationSteps)
            {
                // 动画完成
                Location = animationTargetPos;
                animationTimer.Stop();
                isAnimating = false;
                UpdateSize();
                Invalidate();
            }
            else
            {
                // 使用缓动函数（ease-out）
                double progress = (double)currentAnimationStep / animationSteps;
                progress = 1 - Math.Pow(1 - progress, 3); // cubic ease-out
                
                int x = (int)(animationStartPos.X + (animationTargetPos.X - animationStartPos.X) * progress);
                int y = (int)(animationStartPos.Y + (animationTargetPos.Y - animationStartPos.Y) * progress);
                
                Location = new Point(x, y);
                Invalidate();
            }
        }
        
        private void DockToLeft()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            // 左侧停靠时，阴影在右边，所以位置就是屏幕左边缘
            // 顶部向下偏移 TOP_OFFSET 像素，为最大化窗口的程序让开关闭按钮留出空间
            Location = new Point(screenBounds.Left, screenBounds.Top + TOP_OFFSET);
            dockSide = DockSide.Left;
            UpdateSize();
            Invalidate();
        }
        
        private void DockToRight()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            // 位置需要考虑阴影偏移和当前宽度（支持收缩）
            // 顶部向下偏移 TOP_OFFSET 像素，为最大化窗口的程序让开关闭按钮留出空间
            int currentSidebarWidth = (int)currentWidth;
            Location = new Point(screenBounds.Right - currentSidebarWidth - SHADOW_SIZE * 2, screenBounds.Top + TOP_OFFSET);
            dockSide = DockSide.Right;
            UpdateSize();
            Invalidate();
        }
        
        private void UpdateSize()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            // 窗体大小需要包含阴影区域，高度减少 TOP_OFFSET（因为顶部向下移动了，但底部不变）
            // 宽度使用当前宽度（支持收缩动画）
            int width = (int)currentWidth;
            Size = new Size(width + SHADOW_SIZE * 2, screenBounds.Height - TOP_OFFSET);
            Invalidate();
        }
        
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不绘制背景，UpdateLayeredWindow 会处理
        }
        
        private void SidebarForm_Paint(object sender, PaintEventArgs e)
        {
            // 使用 UpdateLayeredWindow 实现每像素透明
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
            
                    // 清除整个背景为透明
                    g.Clear(Color.Transparent);
                    
                    // 绘制阴影（阴影会覆盖边缘区域）
            DrawShadow(g);
            
            // 创建圆角矩形路径
                    // 顶部向下偏移 TOP_OFFSET 像素，为最大化窗口的程序让开关闭按钮留出空间
                    // 宽度使用当前宽度（支持收缩）
                    int sidebarWidth = (int)currentWidth;
            Rectangle rect = new Rectangle(SHADOW_SIZE, SHADOW_SIZE, 
                        sidebarWidth, Height - SHADOW_SIZE * 2 - 1);
            GraphicsPath path = CreateRoundedRectangle(rect, CORNER_RADIUS);
            
                    // 绘制背景色（Alpha = 5）
            using (SolidBrush brush = new SolidBrush(backgroundColor))
            {
                g.FillPath(brush, path);
            }
            
            // 绘制图标按钮
            DrawButtons(g);
                    
                    // 绘制锁定按钮（在底部右侧）
                    DrawLockButton(g);
            
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
            // 阴影的基础矩形
            Rectangle shadowRect = new Rectangle(SHADOW_SIZE / 2, SHADOW_SIZE / 2,
                Width - SHADOW_SIZE, Height - SHADOW_SIZE);
            
            // 创建多层阴影效果
            for (int i = SHADOW_SIZE; i > 0; i--)
            {
                float alpha = (float)(0.15 * (SHADOW_SIZE - i + 1) / SHADOW_SIZE);
                using (SolidBrush brush = new SolidBrush(Color.FromArgb((int)(alpha * 255), Color.Black)))
                {
                    // 确保最外层阴影覆盖整个窗体边缘
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
            
            // 使用更精细的圆角绘制，提高边缘细腻度
            // 通过使用更小的角度步进，让圆角更平滑
            const int arcSegments = 12; // 每个90度圆角使用12个线段，提高精细度
            
            if (dockSide == DockSide.Left)
            {
                // 左侧停靠：右边圆角
                // 右上圆角 - 使用多个小线段代替单个弧线，提高精细度
                AddSmoothArc(path, 
                    rect.Right - radius * 2, rect.Top, radius * 2, radius * 2, 
                    270, 90, arcSegments);
                // 右下圆角
                AddSmoothArc(path, 
                    rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 
                    0, 90, arcSegments);
                path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom); // 底边
                path.AddLine(rect.Left, rect.Bottom, rect.Left, rect.Top); // 左边（直边）
                path.AddLine(rect.Left, rect.Top, rect.Right - radius * 2, rect.Top); // 顶边
            }
            else
            {
                // 右侧停靠：左边圆角
                path.AddLine(rect.Right, rect.Top, rect.Right, rect.Bottom); // 右边（直边）
                path.AddLine(rect.Right, rect.Bottom, rect.Left + radius * 2, rect.Bottom); // 底边
                // 左下圆角 - 使用多个小线段代替单个弧线，提高精细度
                AddSmoothArc(path, 
                    rect.Left, rect.Bottom - radius * 2, radius * 2, radius * 2, 
                    90, 90, arcSegments);
                // 左上圆角
                AddSmoothArc(path, 
                    rect.Left, rect.Top, radius * 2, radius * 2, 
                    180, 90, arcSegments);
                path.AddLine(rect.Left + radius * 2, rect.Top, rect.Right, rect.Top); // 顶边
            }
            
            path.CloseFigure();
            return path;
        }
        
        // 添加平滑的圆弧（使用多个小线段代替单个弧线，提高精细度）
        private void AddSmoothArc(GraphicsPath path, int x, int y, int width, int height, 
            float startAngle, float sweepAngle, int segments)
        {
            if (segments <= 1)
            {
                // 如果分段数太少，使用原始方法
                path.AddArc(x, y, width, height, startAngle, sweepAngle);
                return;
            }
            
            // 计算圆弧的中心点和半径
            float centerX = x + width / 2.0f;
            float centerY = y + height / 2.0f;
            float radiusX = width / 2.0f;
            float radiusY = height / 2.0f;
            
            // 将角度转换为弧度
            float startRad = startAngle * (float)Math.PI / 180.0f;
            float sweepRad = sweepAngle * (float)Math.PI / 180.0f;
            float angleStep = sweepRad / segments;
            
            // 计算起始点
            float currentAngle = startRad;
            float startX = centerX + radiusX * (float)Math.Cos(currentAngle);
            float startY = centerY + radiusY * (float)Math.Sin(currentAngle);
            
            // 移动到起始点
            path.AddLine(startX, startY, startX, startY);
            
            // 添加多个小线段，形成平滑的圆弧
            for (int i = 1; i <= segments; i++)
            {
                currentAngle = startRad + angleStep * i;
                float pointX = centerX + radiusX * (float)Math.Cos(currentAngle);
                float pointY = centerY + radiusY * (float)Math.Sin(currentAngle);
                path.AddLine(startX, startY, pointX, pointY);
                startX = pointX;
                startY = pointY;
            }
        }
        
        // 设置窗体区域为圆角（包含阴影区域）
        private void UpdateRegion()
        {
            // 使用 UpdateLayeredWindow 时不需要设置 Region
            Region = null;
        }
        
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
            Invalidate();
        }
        
        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            Invalidate();
        }
        
        // 添加工具按钮
        // 添加工具按钮（支持 Emoji 和 PNG 图片）
        private void AddToolButton(string name, string icon, Action onClick, string iconPath = null)
        {
            var button = new SidebarButton
            {
                Name = name,
                Icon = icon,  // Emoji 字符
                IconPath = iconPath,  // PNG 图片路径（可选）
                OnClick = onClick
            };
            buttons.Add(button);
            // 初始化按钮的缩放值
            buttonScales[button] = 1.0f;
            
            // 保存工具按钮信息用于设置
            toolButtonInfos.Add(new ToolButtonInfo
            {
                Name = name,
                Icon = icon,
                OnClick = onClick
            });
        }
        
        // 打开设置窗口
        private void OpenHotkeySettings()
        {
            try
            {
                if (hotkeySettingsForm != null && !hotkeySettingsForm.IsDisposed)
                {
                    hotkeySettingsForm.BringToFront();
                    hotkeySettingsForm.Show();
                    return;
                }
                
                hotkeySettingsForm = new HotkeySettingsForm(toolButtonInfos);
                hotkeySettingsForm.HotkeysSaved += HotkeySettingsForm_HotkeysSaved;
                hotkeySettingsForm.Show();
            }
            catch (Exception ex)
            {
                ShowNotification($"打开设置失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 快捷键保存后重新注册
        private void HotkeySettingsForm_HotkeysSaved(object sender, EventArgs e)
        {
            LoadAndRegisterHotkeys();
        }
        
        // 初始化全局快捷键管理器
        private void InitializeGlobalHotkeys()
        {
            globalHotkeyForm = new HotkeyForm();
            globalHotkeyForm.HotkeyPress += GlobalHotkeyForm_HotkeyPress;
            globalHotkeyForm.ShowInTaskbar = false;
            globalHotkeyForm.WindowState = FormWindowState.Minimized;
            globalHotkeyForm.Show();
            
            // 强制创建窗口句柄并处理消息，确保窗口完全初始化
            if (!globalHotkeyForm.IsHandleCreated)
            {
                IntPtr handle = globalHotkeyForm.Handle; // 强制创建句柄
            }
            
            // 处理消息队列，确保窗口消息循环已启动
            Application.DoEvents();
            
            // 如果句柄未创建，等待句柄创建后再注册快捷键
            if (!globalHotkeyForm.IsHandleCreated)
            {
                globalHotkeyForm.HandleCreated += (s, e) =>
                {
                    // 句柄创建后，立即注册快捷键（如果配置已加载）
                    if (hotkeyConfigs != null && hotkeyConfigs.Count > 0)
                    {
                        RegisterAllHotkeys();
                    }
                };
            }
        }
        
        // 全局快捷键触发事件
        private void GlobalHotkeyForm_HotkeyPress(ushort id, Keys key, Modifiers modifier)
        {
            // 确保在主线程上执行，避免第一次触发不灵敏的问题
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<ushort, Keys, Modifiers>(GlobalHotkeyForm_HotkeyPress), id, key, modifier);
                return;
            }
            
            // 快捷键优先级最高，无论打开什么程序都能触发
            // 查找对应的快捷键
            if (registeredHotkeys == null || registeredHotkeys.Count == 0)
            {
                return;
            }
            
            foreach (var kvp in registeredHotkeys)
            {
                if (kvp.Value != null && kvp.Value.ID == id)
                {
                    TriggerToolButton(kvp.Key);
                    break;
                }
            }
        }
        
        // 触发工具按钮
        private void TriggerToolButton(string toolName)
        {
            if (string.IsNullOrEmpty(toolName))
            {
                return;
            }
            
            // 在主线程中执行
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(TriggerToolButton), toolName);
                return;
            }
            
            try
            {
                // 首先尝试从 toolButtonInfos 查找
                var button = toolButtonInfos?.FirstOrDefault(b => b != null && b.Name == toolName);
                if (button != null && button.OnClick != null)
                {
                    button.OnClick.Invoke();
                    return;
                }
                
                // 如果没找到，尝试从 buttons 列表查找（直接使用按钮名称）
                var sidebarButton = buttons?.FirstOrDefault(b => b != null && b.Name == toolName);
                if (sidebarButton != null && sidebarButton.OnClick != null)
                {
                    sidebarButton.OnClick.Invoke();
                    return;
                }
            }
            catch (Exception ex)
            {
                LogError($"触发工具按钮失败: {toolName}", ex);
            }
        }
        
        // 加载并注册快捷键
        private void LoadAndRegisterHotkeys()
        {
            LoadHotkeyConfigs();
            RegisterAllHotkeys();
        }
        
        // 加载快捷键配置
        private void LoadHotkeyConfigs()
        {
            hotkeyConfigs = new Dictionary<string, HotkeyConfig>();
            
            string configPath = GetHotkeyConfigPath();
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, HotkeyConfig>>(json);
                    if (loaded != null)
                    {
                        hotkeyConfigs = loaded;
                    }
                }
                catch (Exception ex)
                {
                    LogError("加载快捷键配置失败", ex);
                }
            }
        }
        
        // 获取快捷键配置文件路径
        private string GetHotkeyConfigPath()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sidebar");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            return Path.Combine(appDataPath, "hotkeys.json");
        }
        
        // 注册所有快捷键
        private void RegisterAllHotkeys()
        {
            // 先注销所有已注册的快捷键
            UnregisterAllHotkeys();
            
            if (globalHotkeyForm == null)
            {
                return;
            }
            
            // 确保窗口句柄已创建
            if (!globalHotkeyForm.IsHandleCreated)
            {
                IntPtr handle = globalHotkeyForm.Handle; // 强制创建句柄
            }
            
            if (hotkeyConfigs == null || hotkeyConfigs.Count == 0)
            {
                return;
            }
            
            foreach (var kvp in hotkeyConfigs)
            {
                if (kvp.Value?.Hotkey != null && kvp.Value.Hotkey.IsValidHotkey)
                {
                    try
                    {
                        globalHotkeyForm.RegisterHotkey(kvp.Value.Hotkey);
                        if (kvp.Value.Hotkey.Status == HotkeyStatus.Registered)
                        {
                            registeredHotkeys[kvp.Key] = kvp.Value.Hotkey;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"注册快捷键失败: {kvp.Key}", ex);
                    }
                }
            }
            
            // 处理消息队列，确保快捷键注册完成并立即生效
            // 这解决了第一次使用快捷键不灵敏的问题
            Application.DoEvents();
        }
        
        // 注销所有快捷键
        private void UnregisterAllHotkeys()
        {
            if (globalHotkeyForm == null)
            {
                registeredHotkeys.Clear();
                return;
            }
            
            foreach (var kvp in registeredHotkeys.ToList())
            {
                try
                {
                    globalHotkeyForm.UnregisterHotkey(kvp.Value);
                }
                catch (Exception ex)
                {
                    LogError($"注销快捷键失败: {kvp.Key}", ex);
                }
            }
            registeredHotkeys.Clear();
        }
        
        #region 日志和错误处理
        
        /// <summary>
        /// 记录错误日志（仅在调试模式下输出）
        /// </summary>
        private void LogError(string message, Exception ex = null)
        {
#if DEBUG
            if (!ENABLE_DEBUG_LOGGING) return;
            
            try
            {
                string logMessage = ex != null 
                    ? $"{message}: {ex.Message}" 
                    : message;
                System.Diagnostics.Debug.WriteLine($"[Sidebar] {logMessage}");
                
                if (ex != null)
                {
                    System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                }
            }
            catch
            {
                // 忽略日志记录失败
            }
#endif
        }
        
        /// <summary>
        /// 记录调试信息（仅在调试模式下输出）
        /// </summary>
        private void LogDebug(string message)
        {
#if DEBUG
            try
            {
                System.Diagnostics.Debug.WriteLine($"[Sidebar] {message}");
            }
            catch
            {
                // 忽略日志记录失败
            }
#endif
        }
        
        #endregion 日志和错误处理
        
        // 获取指定位置的按钮
        private SidebarButton GetButtonAtPoint(Point point)
        {
            int y = TOP_MARGIN + SHADOW_SIZE;
            foreach (var button in buttons)
            {
                Rectangle buttonRect = new Rectangle(
                    SHADOW_SIZE + (SIDEBAR_WIDTH - ICON_SIZE) / 2,
                    y,
                    ICON_SIZE,
                    ICON_SIZE
                );
                
                if (buttonRect.Contains(point))
                {
                    return button;
                }
                
                y += ICON_SIZE + ICON_SPACING;
            }
            return null;
        }
        
        // 绘制按钮
        private void DrawButtons(Graphics g)
        {
            // 如果侧边栏是收缩状态，不绘制按钮
            if (isCollapsed)
            {
                return;
            }
            
            // 顶部向下偏移 TOP_OFFSET 像素，为最大化窗口的程序让开关闭按钮留出空间
            int y = TOP_MARGIN + SHADOW_SIZE;
            
            int buttonIndex = 0;
            foreach (var button in buttons)
            {
                Rectangle buttonRect = new Rectangle(
                    SHADOW_SIZE + (SIDEBAR_WIDTH - ICON_SIZE) / 2,
                    y,
                    ICON_SIZE,
                    ICON_SIZE
                );
                
                // 不绘制背景色，只保留放大效果，避免颜色反差
                // 如果需要背景色，可以取消注释下面的代码
                // if (button == hoveredButton)
                // {
                //     using (SolidBrush brush = new SolidBrush(hoverColor))
                //     {
                //         GraphicsPath buttonPath = CreateRoundedRectangle(buttonRect, 8);
                //         g.FillPath(brush, buttonPath);
                //         buttonPath.Dispose();
                //     }
                // }
                
                // 绘制图标（支持 PNG 图片和 Emoji）
                // 使用动画缩放值，实现平滑过渡
                float scale = buttonScales.ContainsKey(button) ? buttonScales[button] : 1.0f;
                
                // 使用浮点数计算，避免整数截断导致的抖动
                float scaledSize = ICON_SIZE * scale;
                float offset = (ICON_SIZE - scaledSize) / 2.0f; // 居中偏移
                
                // 使用浮点数矩形，绘制时自动处理像素对齐
                RectangleF iconRect = new RectangleF(
                    buttonRect.X + offset,
                    buttonRect.Y + offset,
                    scaledSize,
                    scaledSize
                );
                
                // 根据配置决定使用 PNG 还是 Emoji
                bool usePng = usePngIcons && !string.IsNullOrEmpty(button.IconPath) && File.Exists(button.IconPath);
                
                if (usePng)
                {
                    // 绘制 PNG 图片
                    try
                    {
                        using (Image iconImage = Image.FromFile(button.IconPath))
                        {
                            // 高质量缩放
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(iconImage, iconRect);
                        }
                    }
                    catch
                    {
                        // 如果图片加载失败，回退到 Emoji
                        DrawEmojiIcon(g, button.Icon, iconRect, scale);
                    }
                }
                else
                {
                    // 绘制 Emoji 图标
                    DrawEmojiIcon(g, button.Icon, iconRect, scale);
                }
                
                // 在第三个图标（索引2）和第四个图标（索引3）之间绘制分隔线
                // 在第六个图标（索引5）和第七个图标（索引6）之间绘制分隔线
                // 在第九个图标（索引8）和第十个图标（索引9）之间绘制分隔线
                // 需要在更新y之前计算分割线位置
                if (buttonIndex == 2 || buttonIndex == 5 || buttonIndex == 8)
                {
                    // 当前y是图标顶部，计算图标底部
                    int iconBottom = y + ICON_SIZE;
                    // 分割线应该在图标下方10像素，上下各10像素间距
                    // 总间距为20像素（10 + 10），分割线在中间
                    int separatorY = iconBottom + 10;
                    int separatorLeft = SHADOW_SIZE + 10; // 左边距
                    int separatorRight = SHADOW_SIZE + SIDEBAR_WIDTH - 10; // 右边距
                    
                    // 绘制分隔线（半透明，与侧边栏风格一致）
                    using (Pen separatorPen = new Pen(Color.FromArgb(30, 255, 255, 255), 1f))
                    {
                        g.DrawLine(separatorPen, separatorLeft, separatorY, separatorRight, separatorY);
                    }
                    
                    // 有分割线的图标之间使用20像素间距（上下各10像素）
                    y += ICON_SIZE + 20;
                }
                else
                {
                    // 其他图标之间使用正常的间距
                    y += ICON_SIZE + ICON_SPACING;
                }
                
                buttonIndex++;
            }
        }
        
        // 获取锁定按钮位置（优化：提取重复计算）
        private Rectangle GetLockButtonRect()
        {
            int buttonX = Width - SHADOW_SIZE - LOCK_BUTTON_RIGHT_MARGIN - LOCK_BUTTON_SIZE;
            int buttonY = Height - SHADOW_SIZE - LOCK_BUTTON_BOTTOM_MARGIN - LOCK_BUTTON_SIZE;
            return new Rectangle(buttonX, buttonY, LOCK_BUTTON_SIZE, LOCK_BUTTON_SIZE);
        }
        
        // 绘制锁定按钮（在底部右侧，带立体效果）
        private void DrawLockButton(Graphics g)
        {
            if (isCollapsed) return;
            
            Rectangle buttonRect = GetLockButtonRect();
            SmoothingMode oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            
            if (isAutoHideLocked)
            {
                // 锁定状态：绿色，带阴影和边框
                Rectangle shadowRect = new Rectangle(buttonRect.X + 1, buttonRect.Y + 1, buttonRect.Width, buttonRect.Height);
                using (SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(80, Color.Black)))
                    g.FillEllipse(shadowBrush, shadowRect);
                
                using (SolidBrush brush = new SolidBrush(lockButtonColorActive))
                    g.FillEllipse(brush, buttonRect);
                
                using (Pen borderPen = new Pen(Color.FromArgb(150, Color.Black), 0.5f))
                    g.DrawEllipse(borderPen, buttonRect);
            }
            else
            {
                // 默认状态：与背景色一致，无边框
                using (SolidBrush brush = new SolidBrush(backgroundColor))
                    g.FillEllipse(brush, buttonRect);
            }
            
            g.SmoothingMode = oldSmoothing;
        }
        
        // 检查是否点击了锁定按钮
        private bool IsLockButtonClicked(Point point)
        {
            if (isCollapsed) return false;
            
            Rectangle buttonRect = GetLockButtonRect();
            int centerX = buttonRect.X + LOCK_BUTTON_SIZE / 2;
            int centerY = buttonRect.Y + LOCK_BUTTON_SIZE / 2;
            int radius = LOCK_BUTTON_SIZE / 2;
            
            int dx = point.X - centerX;
            int dy = point.Y - centerY;
            return dx * dx + dy * dy <= radius * radius;
        }
        
        // 按钮功能描述字典
        private Dictionary<string, string> buttonDescriptions = new Dictionary<string, string>
        {
            { "桌面", "桌面图标管理" },
            { "截图", "区域截图并保存" },
            { "滚动截图", "滚动窗口截图" },
            { "录制", "屏幕录制" },
            { "Pin", "固定到屏幕" },
            { "颜色选择器", "屏幕颜色拾取器" },
            { "尺子", "屏幕测量尺" },
            { "图像美化", "图像美化处理" },
            { "图片特效", "图片特效处理" },
            { "图像编辑器", "图像编辑器" },
            { "图像分割器", "图像分割工具" },
            { "图像合并", "图像合并工具" },
            { "图像缩略图", "生成图像缩略图" },
            { "视频转换器", "视频格式转换" },
            { "文件重命名", "批量文件重命名" },
            { "系统清理", "系统清理工具" },
            { "设置", "设置" }
        };
        
        // 获取按钮的快捷键
        private string GetButtonHotkey(string buttonName)
        {
            if (hotkeyConfigs.ContainsKey(buttonName))
            {
                var config = hotkeyConfigs[buttonName];
                if (config?.Hotkey != null && config.Hotkey.Hotkey != Keys.None)
                {
                    return config.Hotkey.ToString();
                }
            }
            return null;
        }
        
        // 工具提示定时器事件
        private void TooltipTimer_Tick(object sender, EventArgs e)
        {
            tooltipTimer.Stop();
            
            if (hoveredButton != null && !isCollapsed)
            {
                ShowTooltip(hoveredButton);
            }
        }
        
        // 显示工具提示
        private void ShowTooltip(SidebarButton button)
        {
            if (button == null || isCollapsed) return;
            
            // 确保在 UI 线程上执行
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<SidebarButton>(ShowTooltip), button);
                return;
            }
            
            // 隐藏之前的工具提示
            HideTooltip();
            
            // 获取功能描述
            string description = buttonDescriptions.ContainsKey(button.Name) 
                ? buttonDescriptions[button.Name] 
                : button.Name;
            
            // 获取快捷键
            string hotkey = GetButtonHotkey(button.Name);
            
            // 构建工具提示文本
            string tooltipText = description;
            if (!string.IsNullOrEmpty(hotkey))
            {
                tooltipText += $"\n快捷键: {hotkey}";
            }
            
            // 计算按钮的 Y 位置
            int y = TOP_MARGIN + SHADOW_SIZE;
            foreach (var btn in buttons)
            {
                if (btn == button)
                {
                    break;
                }
                y += ICON_SIZE + ICON_SPACING;
            }
            
            try
            {
                // 创建工具提示窗口
                tooltipForm = new TooltipForm(tooltipText);
                
                // 确保窗口已创建并计算好大小
                tooltipForm.CreateControl();
                Application.DoEvents(); // 确保窗口大小已计算
                
                // 根据侧边栏位置决定工具提示显示在哪一侧
                Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
                Point tooltipLocation;
                int tooltipWidth = tooltipForm.Width;
                int tooltipHeight = tooltipForm.Height;
                
                if (dockSide == DockSide.Right)
                {
                    // 侧边栏在右侧，工具提示显示在左侧（侧边栏的左边）
                    Point buttonScreenPos = PointToScreen(new Point(
                        SHADOW_SIZE,
                        y + ICON_SIZE / 2
                    ));
                    tooltipLocation = new Point(
                        buttonScreenPos.X - tooltipWidth - 10, // 在按钮左侧
                        buttonScreenPos.Y - tooltipHeight / 2
                    );
                }
                else
                {
                    // 侧边栏在左侧，工具提示显示在右侧（侧边栏的右边）
                    Point buttonScreenPos = PointToScreen(new Point(
                        SHADOW_SIZE + SIDEBAR_WIDTH,
                        y + ICON_SIZE / 2
                    ));
                    tooltipLocation = new Point(
                        buttonScreenPos.X + 10, // 在按钮右侧
                        buttonScreenPos.Y - tooltipHeight / 2
                    );
                }
                
                // 确保窗口在屏幕范围内
                if (tooltipLocation.X + tooltipWidth > screenBounds.Right)
                {
                    tooltipLocation = new Point(
                        screenBounds.Right - tooltipWidth - 10,
                        tooltipLocation.Y
                    );
                }
                if (tooltipLocation.X < screenBounds.Left)
                {
                    tooltipLocation = new Point(
                        screenBounds.Left + 10,
                        tooltipLocation.Y
                    );
                }
                if (tooltipLocation.Y + tooltipHeight > screenBounds.Bottom)
                {
                    tooltipLocation = new Point(
                        tooltipLocation.X,
                        screenBounds.Bottom - tooltipHeight - 10
                    );
                }
                if (tooltipLocation.Y < screenBounds.Top)
                {
                    tooltipLocation = new Point(
                        tooltipLocation.X,
                        screenBounds.Top + 10
                    );
                }
                
                // 设置位置并显示
                tooltipForm.Location = tooltipLocation;
                tooltipForm.Show();
            }
            catch (Exception ex)
            {
                LogError("显示工具提示失败", ex);
                SafeDisposeTooltip();
            }
        }
        
        // 隐藏工具提示
        private void HideTooltip()
        {
            // 确保在 UI 线程上执行
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(HideTooltip));
                return;
            }
            
            SafeDisposeTooltip();
        }
        
        // 安全释放工具提示资源
        private void SafeDisposeTooltip()
        {
            if (tooltipForm != null && !tooltipForm.IsDisposed)
            {
                try
                {
                    tooltipForm.Close();
                    tooltipForm.Dispose();
                }
                catch (Exception ex)
                {
                    LogError("释放工具提示资源失败", ex);
                }
                finally
                {
                    tooltipForm = null;
                }
            }
        }
        
        // 切换自动隐藏锁定状态
        private void ToggleAutoHideLock()
        {
            isAutoHideLocked = !isAutoHideLocked;
            
            // 如果锁定，确保侧边栏展开
            if (isAutoHideLocked && isCollapsed)
            {
                ExpandSidebar();
            }
            
            // 更新界面
            if (IsHandleCreated)
            {
                UpdateLayeredWindowBitmap();
            }
        }
        
        // 绘制 Emoji 图标
        private void DrawEmojiIcon(Graphics g, string emoji, RectangleF iconRect, float scale)
        {
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    
                // 根据缩放调整字体大小
                float fontSize = 24 * scale;
                // 图标颜色保持白色，不因悬停而改变透明度，避免颜色反差
                using (Font font = new Font("Segoe UI Emoji", fontSize))
                    using (SolidBrush brush = new SolidBrush(Color.White))
                    {
                    g.DrawString(emoji, font, brush, iconRect, sf);
                }
            }
        }
        
        // 自动隐藏定时器事件（优化：提高检测频率，提升鼠标操控精准度）
        private void AutoHideTimer_Tick(object sender, EventArgs e)
        {
            // 如果自动隐藏已锁定，不执行收缩逻辑
            if (isAutoHideLocked)
            {
                return;
            }
            
            // 检查鼠标位置（屏幕坐标）
            Point mousePos = Control.MousePosition;
            
            // 如果侧边栏是收缩状态，检查鼠标是否在边缘区域
            if (isCollapsed)
            {
                // 检查鼠标是否在边缘区域（使用屏幕坐标）
                if (IsMouseInEdgeArea(mousePos))
                {
                    // 展开侧边栏
                    ExpandSidebar();
                }
            }
            // 如果侧边栏是展开状态，检查鼠标是否在区域内
            else
            {
                // 使用更精确的区域检测（考虑阴影区域）
                Rectangle sidebarRect = new Rectangle(Location, Size);
                
                // 如果鼠标不在侧边栏区域，立即收缩
                if (!sidebarRect.Contains(mousePos))
                {
                    CollapseSidebar();
                }
            }
        }
        
        // 收缩动画定时器事件（优化：移除未使用变量，提升性能）
        private void CollapseAnimationTimer_Tick(object sender, EventArgs e)
        {
            const float animationSpeed = 0.15f; // 动画速度
            const float threshold = 0.5f; // 动画完成阈值
            
            // 检查是否达到目标值
            if (Math.Abs(currentWidth - targetWidth) < threshold)
            {
                currentWidth = targetWidth;
                collapseAnimationTimer.Stop();
                isCollapsing = false;
                UpdateSize();
                UpdatePosition(); // 更新位置，确保向屏幕边缘对齐
                if (IsHandleCreated)
                {
                    UpdateLayeredWindowBitmap();
                }
            }
            else
            {
                // 平滑插值到目标宽度
                currentWidth += (targetWidth - currentWidth) * animationSpeed;
                UpdateSize();
                UpdatePosition(); // 更新位置，确保向屏幕边缘收缩
                if (IsHandleCreated)
                {
                    UpdateLayeredWindowBitmap();
                }
            }
        }
        
        // 更新位置，确保侧边栏向屏幕边缘收缩
        private void UpdatePosition()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            int width = (int)currentWidth;
            
            if (dockSide == DockSide.Left)
            {
                // 左侧停靠：保持左边缘位置不变（向屏幕左边缘收缩）
                Location = new Point(screenBounds.Left, screenBounds.Top + TOP_OFFSET);
            }
            else
            {
                // 右侧停靠：保持右边缘位置不变（向屏幕右边缘收缩）
                Location = new Point(screenBounds.Right - width - SHADOW_SIZE * 2, screenBounds.Top + TOP_OFFSET);
            }
        }
        
        // 收缩侧边栏（优化：添加状态检查，避免重复操作）
        private void CollapseSidebar()
        {
            if (isCollapsed || isCollapsing) return;
            
            isCollapsing = true;
            targetWidth = COLLAPSED_WIDTH;
            isCollapsed = true;
            collapseAnimationTimer.Start();
        }
        
        // 展开侧边栏（优化：添加状态检查，避免重复操作）
        private void ExpandSidebar()
        {
            if (!isCollapsed || isCollapsing) return;
            
            isCollapsing = true;
            targetWidth = SIDEBAR_WIDTH;
            isCollapsed = false;
            collapseAnimationTimer.Start();
        }
        
        // 检查鼠标是否在边缘区域（使用屏幕坐标，优化：提升检测精准度）
        private bool IsMouseInEdgeArea(Point mousePos)
        {
            Rectangle screenBounds = Screen.PrimaryScreen.WorkingArea;
            int edgeLeft = screenBounds.Left;
            int edgeRight = screenBounds.Right;
            int sidebarTop = Location.Y;
            int sidebarBottom = Location.Y + Height;
            
            // 检查Y坐标是否在侧边栏垂直范围内
            bool inVerticalRange = mousePos.Y >= sidebarTop && mousePos.Y <= sidebarBottom;
            
            if (!inVerticalRange)
            {
                return false;
            }
            
            if (dockSide == DockSide.Left)
            {
                // 左侧停靠：检查鼠标是否在屏幕左边缘检测宽度内
                return mousePos.X >= edgeLeft && mousePos.X <= edgeLeft + EDGE_DETECTION_WIDTH;
            }
            else
            {
                // 右侧停靠：检查鼠标是否在屏幕右边缘检测宽度内
                return mousePos.X >= edgeRight - EDGE_DETECTION_WIDTH && mousePos.X <= edgeRight;
            }
        }
        
        // 以最高质量保存图片（无压缩）
        private void SaveImageWithHighestQuality(Bitmap image, string filePath, string extension)
        {
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    // JPEG 格式：使用 100% 质量（无压缩）
                    SaveJpegWithQuality(image, filePath, 100);
                    break;
                case ".bmp":
                    // BMP 格式：本身就是无压缩的
                    image.Save(filePath, ImageFormat.Bmp);
                    break;
                case ".png":
                default:
                    // PNG 格式：使用无压缩或最高压缩级别
                    SavePngWithNoCompression(image, filePath);
                    break;
            }
        }
        
        // 保存 PNG 图片（无压缩）
        private void SavePngWithNoCompression(Bitmap image, string filePath)
        {
            // 获取 PNG 编码器
            ImageCodecInfo pngEncoder = GetEncoder(ImageFormat.Png);
            if (pngEncoder != null)
            {
                // 创建编码器参数
                using (EncoderParameters encoderParams = new EncoderParameters(1))
                {
                    // 设置压缩级别为 0（无压缩，最高质量）
                    // PNG 压缩级别范围：0-9，0 表示无压缩
                    encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Compression, (long)0);
                    
                    // 保存图片
                    image.Save(filePath, pngEncoder, encoderParams);
                }
            }
            else
            {
                // 如果无法获取编码器，使用默认方式保存
                image.Save(filePath, ImageFormat.Png);
            }
        }
        
        // 保存 JPEG 图片（指定质量）
        private void SaveJpegWithQuality(Bitmap image, string filePath, long quality)
        {
            // 获取 JPEG 编码器
            ImageCodecInfo jpegEncoder = GetEncoder(ImageFormat.Jpeg);
            if (jpegEncoder != null)
            {
                // 创建编码器参数
                using (EncoderParameters encoderParams = new EncoderParameters(1))
                {
                    // 设置质量级别（0-100，100 表示最高质量，无压缩）
                    encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                    
                    // 保存图片
                    image.Save(filePath, jpegEncoder, encoderParams);
                }
            }
            else
            {
                // 如果无法获取编码器，使用默认方式保存
                image.Save(filePath, ImageFormat.Jpeg);
            }
        }
        
        // 获取指定格式的图片编码器
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }
        
        // 隐藏侧边栏（用于截图时排除侧边栏）
        private bool HideSidebarForCapture()
        {
            bool wasVisible = Visible;
            if (wasVisible)
            {
                Hide();
                // 强制刷新，确保窗口立即隐藏
                Application.DoEvents();
            }
            return wasVisible;
        }
        
        // 恢复侧边栏显示（用于截图后恢复）
        private void RestoreSidebarAfterCapture(bool wasVisible)
        {
            if (wasVisible)
            {
                Show();
                // 确保窗口重新显示并更新
                if (IsHandleCreated)
                {
                    UpdateLayeredWindowBitmap();
                }
            }
        }
        
        // 显示保存对话框并保存图片（公共方法，避免重复代码）
        private bool ShowSaveDialogAndSave(Bitmap image, string defaultFileName = null)
        {
            if (image == null) return false;
            
            try
            {
                // 显示保存文件对话框
                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "PNG 图片|*.png|JPEG 图片|*.jpg|BMP 图片|*.bmp|所有文件|*.*";
                    saveDialog.FilterIndex = 1;
                    saveDialog.DefaultExt = "png";
                    saveDialog.FileName = defaultFileName ?? $"截图_{DateTime.Now:yyyyMMdd_HHmmss}";
                    
                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        // 根据文件扩展名选择保存格式并设置最高质量
                        string extension = Path.GetExtension(saveDialog.FileName).ToLower();
                        
                        // 保存图片（使用最高质量，无压缩）
                        SaveImageWithHighestQuality(image, saveDialog.FileName, extension);
                        
                        ShowNotification($"截图已保存到：\n{saveDialog.FileName}", "保存成功");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"保存失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
            
            return false;
        }
        
        // 截图区域并保存（使用 ShareX 的区域选择界面和截图功能）
        private void CaptureRegionAndSave()
        {
            bool wasVisible = HideSidebarForCapture();
            
            try
            {
                // 使用 ShareX 的区域截图功能获取截图
                Bitmap screenshot = RegionCaptureTasks.GetRegionImage();
                
                if (screenshot != null)
                {
                    ShowSaveDialogAndSave(screenshot, $"截图_{DateTime.Now:yyyyMMdd_HHmmss}");
                    screenshot.Dispose();
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"截图失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
            finally
            {
                RestoreSidebarAfterCapture(wasVisible);
            }
        }
        
        // Pin to Screen 功能
        private void PinToScreenFromScreen()
        {
            bool wasVisible = HideSidebarForCapture();
            
            try
            {
                // 使用 ShareX 的区域截图功能获取截图
                Bitmap screenshot = RegionCaptureTasks.GetRegionImage();
                
                if (screenshot != null)
                {
                    // 创建 PinToScreenOptions 并设置默认值
                    PinToScreenOptions options = new PinToScreenOptions
                    {
                        Placement = ContentAlignment.MiddleCenter, // 正中
                        PlacementOffset = 10, // 偏移10像素
                        TopMost = true, // 启用 top most
                        KeepCenterLocation = true, // 启用 keep center location
                        Shadow = true, // 启用 shadow
                        Border = true, // 启用 border
                        BorderSize = 1, // border size: 1
                        BorderColor = Color.Black, // border color: 黑色
                        MinimizeSize = new Size(3, 3) // minimize size: 3x3
                    };
                    
                    // 调用 ShareX 的 PinToScreen 功能
                    PinToScreenForm.PinToScreenAsync(screenshot, options, null);
                    
                    // 延迟隐藏设置按钮（因为 PinToScreenForm 是异步创建的）
                    Task.Delay(300).ContinueWith(t =>
                    {
                        try
                        {
                            // 使用反射获取所有 PinToScreenForm 实例并隐藏设置按钮
                            FieldInfo formsField = typeof(PinToScreenForm).GetField("forms", BindingFlags.NonPublic | BindingFlags.Static);
                            if (formsField != null)
                            {
                                System.Collections.IList forms = formsField.GetValue(null) as System.Collections.IList;
                                if (forms != null && forms.Count > 0)
                                {
                                    PinToScreenForm form = forms[forms.Count - 1] as PinToScreenForm;
                                    if (form != null && !form.IsDisposed)
                                    {
                                        // 使用 InvokeSafe 扩展方法在 UI 线程中执行
                                        form.InvokeSafe(() =>
                                        {
                                            try
                                            {
                                                // 使用反射获取 tsbOptions 按钮并隐藏
                                                FieldInfo tsbOptionsField = typeof(PinToScreenForm).GetField("tsbOptions", BindingFlags.NonPublic | BindingFlags.Instance);
                                                if (tsbOptionsField != null)
                                                {
                                                    ToolStripButton tsbOptions = tsbOptionsField.GetValue(form) as ToolStripButton;
                                                    if (tsbOptions != null)
                                                    {
                                                        tsbOptions.Visible = false;
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogError("隐藏 PinToScreenForm 按钮失败", ex);
                                            }
                                        });
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError("UI 线程调用失败", ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Pin to Screen 失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
            finally
            {
                RestoreSidebarAfterCapture(wasVisible);
            }
        }
        
        // 屏幕拾色器功能
        private void OpenScreenColorPicker()
        {
            bool wasVisible = HideSidebarForCapture();
            
            try
            {
                // 使用 ShareX 的屏幕拾色器功能
                // 创建默认的 RegionCaptureOptions
                RegionCaptureOptions options = new RegionCaptureOptions();
                
                // 获取点信息（包括颜色）
                PointInfo pointInfo = RegionCaptureTasks.GetPointInfo(options);
                
                if (pointInfo != null)
                {
                    // 使用默认格式：HEX（例如：#FF0000）
                    string colorFormat = "{0:HEX}";
                    
                    // 解析颜色格式并生成文本
                    string text = CodeMenuEntryPixelInfo.Parse(colorFormat, pointInfo.Color, pointInfo.Position);
                    
                    // 复制到剪贴板
                    ClipboardHelpers.CopyText(text);
                    
                    // 使用 ShareX 风格的右下角通知提示（带渐变动画）
                    ShowNotification($"颜色已复制到剪贴板：{text}", "屏幕拾色器");
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"拾色失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
            finally
            {
                RestoreSidebarAfterCapture(wasVisible);
            }
        }
        
        // 打开屏幕尺子功能
        private void OpenScreenRuler()
        {
            bool wasVisible = HideSidebarForCapture();
            
            try
            {
                // 使用 ShareX 的屏幕尺子功能
                // 创建默认的 RegionCaptureOptions
                RegionCaptureOptions options = new RegionCaptureOptions();
                
                // 显示屏幕尺子
                RegionCaptureTasks.ShowScreenRuler(options);
            }
            catch (Exception ex)
            {
                ShowNotification($"打开尺子失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
            finally
            {
                RestoreSidebarAfterCapture(wasVisible);
            }
        }
        
        // 打开图像编辑器
        private void OpenImageEditor()
        {
            try
            {
                // 显示文件选择对话框
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.ico|所有文件|*.*";
                    openFileDialog.Title = "选择要编辑的图片";
                    
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string filePath = openFileDialog.FileName;
                        
                        // 检查文件是否存在
                        if (!File.Exists(filePath))
                        {
                            ShowNotification("文件不存在或已被删除", "错误", 3000, MessageBoxIcon.Error);
                            return;
                        }
                        
                        // 隐藏侧边栏
                        bool wasVisible = Visible;
                        if (wasVisible)
                        {
                            Hide();
                        }
                        
                        // 直接使用 RegionCaptureForm 打开图像编辑器，避免 TaskHelpers 的错误处理机制
                        try
                        {
                            // 加载图片
                            Bitmap image = ImageHelpers.LoadImage(filePath);
                            if (image == null)
                            {
                                ShowNotification("无法加载图片文件", "错误", 3000, MessageBoxIcon.Error);
                                if (wasVisible)
                                {
                                    Show();
                                    if (IsHandleCreated)
                                    {
                                        UpdateLayeredWindowBitmap();
                                    }
                                }
                                return;
                            }
                            
                            // 转换为非索引位图
                            image = ImageHelpers.NonIndexedBitmap(image);
                            
                            // 创建默认的 RegionCaptureOptions
                            RegionCaptureOptions options = new RegionCaptureOptions();
                            
                            // 直接创建并显示 RegionCaptureForm
                            using (RegionCaptureForm editorForm = new RegionCaptureForm(RegionCaptureMode.Editor, options, image))
                            {
                                editorForm.ImageFilePath = filePath;
                                
                                // 禁用关闭时的保存提示，直接关闭
                                // 通过反射设置 forceClose 标志为 true，这样关闭时不会显示保存提示
                                try
                                {
                                    FieldInfo forceCloseField = typeof(RegionCaptureForm).GetField("forceClose", 
                                        BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (forceCloseField != null)
                                    {
                                        forceCloseField.SetValue(editorForm, true);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError("设置 forceClose 标志失败", ex);
                                }
                                
                                // 同时添加 FormClosing 事件处理器，确保直接关闭
                                editorForm.FormClosing += (sender, e) =>
                                {
                                    // 如果是用户关闭，直接允许关闭，不显示保存提示
                                    if (e.CloseReason == CloseReason.UserClosing)
                                    {
                                        e.Cancel = false;
                                        // 再次确保 forceClose 标志被设置
                                        try
                                        {
                                            FieldInfo forceCloseField = typeof(RegionCaptureForm).GetField("forceClose", 
                                                BindingFlags.NonPublic | BindingFlags.Instance);
                                            if (forceCloseField != null)
                                            {
                                                forceCloseField.SetValue(editorForm, true);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogError("设置 forceClose 标志失败", ex);
                                        }
                                    }
                                };
                                
                                // 设置保存图像事件处理器
                                editorForm.SaveImageRequested += (output, originalFilePath) =>
                                {
                                    try
                                    {
                                        using (output)
                                        {
                                            string savePath = originalFilePath;
                                            if (string.IsNullOrEmpty(savePath))
                                            {
                                                // 如果没有原始路径，使用默认保存路径
                                                string screenshotsFolder = Path.Combine(
                                                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                                                    "ShareX"
                                                );
                                                
                                                if (!Directory.Exists(screenshotsFolder))
                                                {
                                                    Directory.CreateDirectory(screenshotsFolder);
                                                }
                                                
                                                savePath = Path.Combine(
                                                    screenshotsFolder,
                                                    $"Screenshot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
                                                );
                                            }
                                            
                                            // 确保目录存在
                                            string directory = Path.GetDirectoryName(savePath);
                                            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                                            {
                                                Directory.CreateDirectory(directory);
                                            }
                                            
                                            // 保存图像
                                            ImageHelpers.SaveImage(output, savePath);
                                            
                                            // 保存成功后，异步关闭编辑器窗口
                                            editorForm.BeginInvoke((System.Windows.Forms.MethodInvoker)delegate
                                            {
                                                editorForm.DialogResult = DialogResult.OK;
                                                editorForm.Close();
                                            });
                                            
                                            return savePath;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogError("保存图像失败", ex);
                                        MessageBox.Show($"保存图像失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        return null;
                                    }
                                };
                                
                                // 设置另存为事件处理器
                                editorForm.SaveImageAsRequested += (output, originalFilePath) =>
                                {
                                    try
                                    {
                                        using (output)
                                        {
                                            using (SaveFileDialog saveDialog = new SaveFileDialog())
                                            {
                                                saveDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp|All Files|*.*";
                                                saveDialog.FilterIndex = 1;
                                                saveDialog.FileName = Path.GetFileNameWithoutExtension(originalFilePath ?? "Screenshot");
                                                
                                                if (saveDialog.ShowDialog(editorForm) == DialogResult.OK)
                                                {
                                                    ImageHelpers.SaveImage(output, saveDialog.FileName);
                                                    return saveDialog.FileName;
                                                }
                                            }
                                            return null;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogError("另存为图像失败", ex);
                                        MessageBox.Show($"另存为图像失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        return null;
                                    }
                                };
                                
                                // 在显示前隐藏工具栏中的"复制到剪贴板"和"上传图像"按钮
                                HideEditorToolbarButtons(editorForm);
                                
                                // 显示编辑器（模态对话框）
                                editorForm.ShowDialog();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError("打开图像编辑器失败", ex);
                            ShowNotification($"打开图像编辑器失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                        }
                        finally
                        {
                            // 恢复侧边栏显示
                            if (wasVisible)
                            {
                                Show();
                                if (IsHandleCreated)
                                {
                                    UpdateLayeredWindowBitmap();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"打开图像编辑器失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 隐藏图像编辑器工具栏中的"复制到剪贴板"和"上传图像"按钮
        private void HideEditorToolbarButtons(RegionCaptureForm editorForm)
        {
            try
            {
                // 递归查找所有 ToolStrip 控件
                Action<Control> findAndHideButtons = null;
                findAndHideButtons = (control) =>
                {
                    try
                    {
                        // 查找 ToolStrip 控件
                        if (control is ToolStrip toolStrip)
                        {
                            // 查找并隐藏"复制到剪贴板"和"上传图像"按钮
                            // 根据 CreateToolbar 代码，按钮顺序是：保存、另存为、复制、上传、打印
                            // 第 4 个按钮（索引 2）：复制到剪贴板
                            // 第 5 个按钮（索引 3）：上传图像
                            int buttonIndex = 0;
                            foreach (ToolStripItem item in toolStrip.Items)
                            {
                                // 只计算按钮，忽略分隔符等其他控件
                                if (item is ToolStripButton button)
                                {
                                    // 第 1 个按钮（索引 0）：完成捕捉任务后运行
                                    // 第 4 个按钮（索引 2）：保存图像为...
                                    // 第 5 个按钮（索引 3）：将图像复制到剪贴板
                                    // 第 6 个按钮（索引 4）：上传图像
                                    if (buttonIndex == 0 || buttonIndex == 2 || buttonIndex == 3 || buttonIndex == 4)
                                    {
                                        button.Visible = false;
                                    }
                                    buttonIndex++;
                                }
                            }
                        }
                        
                        // 递归查找子控件
                        foreach (Control child in control.Controls)
                        {
                            findAndHideButtons(child);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("查找和隐藏按钮失败", ex);
                    }
                };
                
                // 使用定时器定期检查，确保工具栏已创建
                Timer checkTimer = new Timer();
                checkTimer.Interval = 50; // 每50ms检查一次
                int checkCount = 0;
                const int maxChecks = 100; // 最多检查5秒
                
                checkTimer.Tick += (sender, e) =>
                {
                    try
                    {
                        checkCount++;
                        
                        // 查找所有 ToolStrip 并隐藏按钮
                        findAndHideButtons(editorForm);
                        
                        // 也查找所有打开的窗口（工具栏可能是独立的窗口）
                        foreach (Form form in Application.OpenForms)
                        {
                            if (form != editorForm && form.Visible)
                            {
                                findAndHideButtons(form);
                            }
                        }
                        
                        // 检查5秒后停止
                        if (checkCount >= maxChecks)
                        {
                            checkTimer.Stop();
                            checkTimer.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("隐藏工具栏按钮失败", ex);
                        if (checkCount >= maxChecks)
                        {
                            checkTimer.Stop();
                            checkTimer.Dispose();
                        }
                    }
                };
                
                // 在编辑器显示后启动定时器
                editorForm.Shown += (sender, e) =>
                {
                    // 立即执行一次
                    findAndHideButtons(editorForm);
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form != editorForm && form.Visible)
                        {
                            findAndHideButtons(form);
                        }
                    }
                    checkTimer.Start();
                };
                
                // 如果编辑器已显示，立即执行
                if (editorForm.Visible)
                {
                    findAndHideButtons(editorForm);
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form != editorForm && form.Visible)
                        {
                            findAndHideButtons(form);
                        }
                    }
                    checkTimer.Start();
                }
            }
            catch (Exception ex)
            {
                LogError("设置工具栏按钮隐藏失败", ex);
            }
        }
        
        // 打开图像分割器功能
        private void OpenImageSplitter()
        {
            try
            {
                // 使用 ShareX 的图像分割器功能
                TaskHelpers.OpenImageSplitter();
            }
            catch (Exception ex)
            {
                ShowNotification($"打开图像分割器失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 打开图像合并功能
        private void OpenImageCombiner()
        {
            try
            {
                // 直接创建 ImageCombinerForm，避免 TaskSettings 依赖问题
                ImageCombinerOptions options = new ImageCombinerOptions();
                ImageCombinerForm imageCombinerForm = new ImageCombinerForm(options);
                
                // 自定义表单：修改按钮文本和添加快捷键
                CustomizeImageCombinerForm(imageCombinerForm);
                
                imageCombinerForm.Show();
            }
            catch (Exception ex)
            {
                LogError("打开图像合并失败", ex);
                ShowNotification($"打开图像合并失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 打开图像缩略图功能
        private void OpenImageThumbnailer()
        {
            try
            {
                // 使用 ShareX 的图像缩略图功能
                TaskHelpers.OpenImageThumbnailer();
            }
            catch (Exception ex)
            {
                ShowNotification($"打开图像缩略图失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 获取保存的自定义 FFmpeg 路径
        private string GetCustomFFmpegPath()
        {
            try
            {
                string configPath = Path.Combine(Application.UserAppDataPath, "ffmpeg_path.txt");
                if (File.Exists(configPath))
                {
                    string path = File.ReadAllText(configPath).Trim();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("读取 FFmpeg 配置文件失败", ex);
            }
            return null;
        }
        
        // 保存自定义 FFmpeg 路径
        private void SaveCustomFFmpegPath(string path)
        {
            try
            {
                string configDir = Application.UserAppDataPath;
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                string configPath = Path.Combine(configDir, "ffmpeg_path.txt");
                File.WriteAllText(configPath, path);
            }
            catch (Exception ex)
            {
                LogError("保存 FFmpeg 路径失败", ex);
            }
        }
        
        // 让用户选择 FFmpeg 路径
        private string SelectFFmpegPath()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "选择 FFmpeg 可执行文件";
                ofd.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                ofd.FileName = "ffmpeg.exe";
                
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    string selectedPath = ofd.FileName;
                    if (File.Exists(selectedPath))
                    {
                        // 验证是否是 FFmpeg
                        try
                        {
                            using (System.Diagnostics.Process process = new System.Diagnostics.Process())
                            {
                                process.StartInfo.FileName = selectedPath;
                                process.StartInfo.Arguments = "-version";
                                process.StartInfo.UseShellExecute = false;
                                process.StartInfo.RedirectStandardOutput = true;
                                process.StartInfo.RedirectStandardError = true;
                                process.StartInfo.CreateNoWindow = true;
                                process.Start();
                                string output = process.StandardOutput.ReadToEnd();
                                process.WaitForExit(3000); // 3秒超时
                                
                                if (output.Contains("ffmpeg") || output.Contains("FFmpeg"))
                                {
                                    // 保存路径
                                    SaveCustomFFmpegPath(selectedPath);
                                    return selectedPath;
                                }
                                else
                                {
                                    MessageBox.Show(
                                        "所选文件不是有效的 FFmpeg 可执行文件。\n\n请选择正确的 FFmpeg.exe 文件。",
                                        "无效的 FFmpeg 文件",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning);
                                }
                            }
                        }
                        catch
                        {
                            // 如果验证失败，仍然保存路径（可能是权限问题）
                            SaveCustomFFmpegPath(selectedPath);
                            return selectedPath;
                        }
                    }
                }
            }
            return null;
        }
        
        // 打开视频转换器功能
        private void OpenVideoConverter()
        {
            try
            {
                // 直接创建 VideoConverterOptions，避免依赖 TaskSettings
                VideoConverterOptions options = new VideoConverterOptions();
                
                // 尝试查找 FFmpeg 路径
                string ffmpegPath = "";
                
                // 首先检查是否有保存的自定义路径
                string customPath = GetCustomFFmpegPath();
                if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                {
                    ffmpegPath = customPath;
                    LogDebug($"使用保存的自定义 FFmpeg 路径: {ffmpegPath}");
                }
                
                // 如果没有自定义路径，尝试自动查找
                if (string.IsNullOrEmpty(ffmpegPath))
                {
                    // 首先尝试使用 FileHelpers.GetAbsolutePath（ShareX 的标准方式）
                    try
                    {
                        string absolutePath = FileHelpers.GetAbsolutePath("ffmpeg.exe");
                        if (File.Exists(absolutePath))
                        {
                            ffmpegPath = absolutePath;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("解析 FFmpeg 绝对路径失败", ex);
                    }
                }
                
                // 如果还没找到，尝试在常见位置查找
                if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
                {
                    string[] commonPaths = new string[]
                    {
                        Path.Combine(Application.StartupPath, "ffmpeg.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ffmpeg", "bin", "ffmpeg.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ffmpeg", "bin", "ffmpeg.exe"),
                    };
                    
                    foreach (string path in commonPaths)
                    {
                        if (File.Exists(path))
                        {
                            ffmpegPath = path;
                            break;
                        }
                    }
                }
                
                // 如果还没找到，尝试在系统 PATH 中查找
                if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
                {
                    try
                    {
                        using (System.Diagnostics.Process process = new System.Diagnostics.Process())
                        {
                            process.StartInfo.FileName = "where";
                            process.StartInfo.Arguments = "ffmpeg.exe";
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.CreateNoWindow = true;
                            process.Start();
                            string output = process.StandardOutput.ReadToEnd();
                            process.WaitForExit();
                            
                            if (!string.IsNullOrEmpty(output))
                            {
                                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0 && File.Exists(lines[0]))
                                {
                                    ffmpegPath = lines[0].Trim();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("读取 FFmpeg 路径配置失败", ex);
                    }
                }
                
                // 如果仍然找不到，提示用户选择路径
                if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
                {
                    DialogResult result = MessageBox.Show(
                        "未找到 FFmpeg 可执行文件。\n\n是否要手动选择 FFmpeg 路径？\n\n点击\"是\"选择路径，点击\"否\"继续（可能无法编码）。",
                        "FFmpeg 未找到",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (result == DialogResult.Yes)
                    {
                        string selectedPath = SelectFFmpegPath();
                        if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
                        {
                            ffmpegPath = selectedPath;
                        }
                        else
                        {
                            ffmpegPath = "";
                        }
                    }
                    else
                    {
                        ffmpegPath = "";
                    }
                }
                else
                {
                    // 确保路径是绝对路径
                    try
                    {
                        ffmpegPath = Path.GetFullPath(ffmpegPath);
                    }
                    catch (Exception ex)
                    {
                        LogError("转换路径为绝对路径失败", ex);
                    }
                }
                
                // 调试输出
                LogDebug($"FFmpeg 路径: {ffmpegPath}");
                if (!string.IsNullOrEmpty(ffmpegPath))
                {
                    LogDebug($"FFmpeg 文件存在: {File.Exists(ffmpegPath)}");
                }
                
                // 直接创建 VideoConverterForm，不依赖 TaskSettings
                VideoConverterForm videoConverterForm = new VideoConverterForm(ffmpegPath, options);
                
                // 自定义表单：隐藏"使用自定义参数"相关控件
                CustomizeVideoConverterForm(videoConverterForm);
                
                // 在表单显示后验证并确保 FFmpeg 路径正确，并确保输出文件名有扩展名
                videoConverterForm.Shown += (sender, e) =>
                {
                    CustomizeVideoConverterForm(videoConverterForm);
                    
                    // 确保输出文件名包含扩展名
                    try
                    {
                        var txtOutputFileName = GetControl<TextBox>(videoConverterForm, "txtOutputFileName");
                        if (txtOutputFileName != null && !string.IsNullOrEmpty(txtOutputFileName.Text))
                        {
                            string fileName = txtOutputFileName.Text;
                            if (!Path.HasExtension(fileName))
                            {
                                // 根据视频编码器添加默认扩展名（通常是 mp4）
                                string extension = videoConverterForm.Options.GetFileExtension();
                                if (!string.IsNullOrEmpty(extension))
                                {
                                    fileName = Path.ChangeExtension(fileName, extension);
                                    txtOutputFileName.Text = fileName;
                                    // 更新 Options 以确保同步
                                    videoConverterForm.Options.OutputFileName = fileName;
                                    LogDebug($"已为输出文件名添加扩展名: {fileName}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("确保输出文件名扩展名失败", ex);
                    }
                    
                    // 监听编码按钮点击，确保在编码前输出文件名有扩展名
                    try
                    {
                        var btnEncode = GetControl<Button>(videoConverterForm, "btnEncode");
                        if (btnEncode != null)
                        {
                            // 获取原有的事件处理器列表
                            var clickEvent = typeof(Button).GetEvent("Click");
                            if (clickEvent != null)
                            {
                                // 添加我们的事件处理器（会在原有处理器之前执行）
                                EventHandler ensureExtensionHandler = (s, args) =>
                                {
                                    // 在编码前再次确保输出文件名有扩展名
                                    try
                                    {
                                        var txtOutputFileName2 = GetControl<TextBox>(videoConverterForm, "txtOutputFileName");
                                        if (txtOutputFileName2 != null && !string.IsNullOrEmpty(txtOutputFileName2.Text))
                                        {
                                            string fileName2 = txtOutputFileName2.Text;
                                            if (!Path.HasExtension(fileName2))
                                            {
                                                string extension2 = videoConverterForm.Options.GetFileExtension();
                                                if (!string.IsNullOrEmpty(extension2))
                                                {
                                                    fileName2 = Path.ChangeExtension(fileName2, extension2);
                                                    txtOutputFileName2.Text = fileName2;
                                                    videoConverterForm.Options.OutputFileName = fileName2;
                                                    LogDebug($"编码前已为输出文件名添加扩展名: {fileName2}");
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex2)
                                    {
                                        LogError("编码前确保输出文件名扩展名失败", ex2);
                                    }
                                };
                                
                                // 使用反射添加事件处理器
                                clickEvent.AddEventHandler(btnEncode, ensureExtensionHandler);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("添加编码按钮事件处理器失败", ex);
                    }
                    
                    // 验证 FFmpeg 路径
                    string currentPath = videoConverterForm.FFmpegFilePath;
                    LogDebug($"表单显示后 FFmpeg 路径: {currentPath}");
                    LogDebug($"路径文件存在: {!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath)}");
                    
                    // 如果路径无效但我们已经找到了有效的路径，尝试通过反射设置
                    if ((string.IsNullOrEmpty(currentPath) || !File.Exists(currentPath)) && 
                        !string.IsNullOrEmpty(ffmpegPath) && File.Exists(ffmpegPath))
                    {
                        try
                        {
                            // 尝试通过反射设置 FFmpegFilePath
                            var property = typeof(VideoConverterForm).GetProperty("FFmpegFilePath");
                            if (property != null && property.CanWrite)
                            {
                                property.SetValue(videoConverterForm, ffmpegPath);
                                LogDebug($"已通过反射设置 FFmpeg 路径: {ffmpegPath}");
                            }
                            else
                            {
                                // 如果属性不可写，尝试查找私有字段
                                var field = typeof(VideoConverterForm).GetField("<FFmpegFilePath>k__BackingField", 
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (field == null)
                                {
                                    // 尝试查找其他可能的字段名
                                    var fields = typeof(VideoConverterForm).GetFields(
                                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    foreach (var f in fields)
                                    {
                                        if (f.FieldType == typeof(string) && f.Name.Contains("FFmpeg"))
                                        {
                                            field = f;
                                            break;
                                        }
                                    }
                                }
                                
                                if (field != null)
                                {
                                    field.SetValue(videoConverterForm, ffmpegPath);
                                    LogDebug($"已通过反射字段设置 FFmpeg 路径: {ffmpegPath}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError("无法通过反射设置 FFmpeg 路径", ex);
                        }
                    }
                    
                    // 最终验证：如果 FFmpeg 路径仍然无效，提示用户
                    string finalPath = videoConverterForm.FFmpegFilePath;
                    if (string.IsNullOrEmpty(finalPath) || !File.Exists(finalPath))
                    {
                        LogDebug($"错误: FFmpeg 路径无效，无法开始编码。路径: {finalPath}");
                        DialogResult result = MessageBox.Show(
                            $"无法找到 FFmpeg 可执行文件。\n\n" +
                            $"请确保 FFmpeg 已安装并在以下位置之一：\n" +
                            $"- {Path.Combine(Application.StartupPath, "ffmpeg.exe")}\n" +
                            $"- 系统 PATH 环境变量中\n\n" +
                            $"当前路径: {finalPath ?? "(空)"}\n\n" +
                            $"是否要手动选择 FFmpeg 路径？",
                            "FFmpeg 未找到",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning);
                        
                        if (result == DialogResult.Yes)
                        {
                            string selectedPath = SelectFFmpegPath();
                            if (!string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath))
                            {
                                // 重新创建表单并设置正确的路径
                                try
                                {
                                    videoConverterForm.Close();
                                    OpenVideoConverter(); // 递归调用，这次应该能找到路径
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    LogError("重新创建视频转换器表单失败", ex);
                                }
                            }
                        }
                    }
                };
                
                videoConverterForm.Show();
            }
            catch (Exception ex)
            {
                LogError("打开视频转换器失败", ex);
                ShowNotification($"打开视频转换器失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 打开文件重命名工具
        private void OpenSystemCleaner()
        {
            try
            {
                SystemCleanerForm cleanerForm = new SystemCleanerForm();
                cleanerForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开系统清理工具失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private DesktopForm desktopForm = null;

        private void OpenDesktop()
        {
            try
            {
                if (desktopForm != null && !desktopForm.IsDisposed)
                {
                    // 如果窗口已显示，则隐藏；如果隐藏，则显示
                    if (desktopForm.Visible)
                    {
                        desktopForm.Hide();
                    }
                    else
                    {
                        // 更新位置（侧边栏可能移动了）
                        int iconY = TOP_MARGIN + SHADOW_SIZE;
                        desktopForm.SetPosition(this.Location, iconY, dockSide == DockSide.Left);
                        desktopForm.Show();
                        desktopForm.BringToFront();
                        desktopForm.Activate();
                    }
                    return;
                }

                desktopForm = new DesktopForm();
                
                // 计算窗口位置：紧贴侧边栏顶部图标
                int topIconY = TOP_MARGIN + SHADOW_SIZE;
                desktopForm.SetPosition(this.Location, topIconY, dockSide == DockSide.Left);
                
                desktopForm.Show();
                desktopForm.Activate();
            }
            catch (Exception ex)
            {
                ShowNotification($"打开桌面失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }

        private void OpenFileRenamer()
        {
            try
            {
                FileRenamerForm renamerForm = new FileRenamerForm();
                renamerForm.Show();
            }
            catch (Exception ex)
            {
                ShowNotification($"打开文件重命名工具失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 自定义视频转换器表单
        private void CustomizeVideoConverterForm(VideoConverterForm form)
        {
            try
            {
                // 通过反射获取并隐藏"使用自定义参数"复选框
                FieldInfo cbUseCustomArgumentsField = typeof(VideoConverterForm).GetField("cbUseCustomArguments", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (cbUseCustomArgumentsField != null)
                {
                    CheckBox cbUseCustomArguments = cbUseCustomArgumentsField.GetValue(form) as CheckBox;
                    if (cbUseCustomArguments != null)
                    {
                        cbUseCustomArguments.Visible = false;
                        cbUseCustomArguments.Enabled = false;
                        // 确保不使用自定义参数
                        cbUseCustomArguments.Checked = false;
                    }
                }
                
                // 通过反射获取并隐藏参数文本框
                FieldInfo txtArgumentsField = typeof(VideoConverterForm).GetField("txtArguments", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (txtArgumentsField != null)
                {
                    TextBox txtArguments = txtArgumentsField.GetValue(form) as TextBox;
                    if (txtArguments != null)
                    {
                        txtArguments.Visible = false;
                        txtArguments.Enabled = false;
                    }
                }
                
                // 确保 Options.UseCustomArguments 为 false
                if (form.Options != null)
                {
                    form.Options.UseCustomArguments = false;
                }
            }
            catch (Exception ex)
            {
                LogError("自定义视频转换器表单失败", ex);
            }
        }
        
        // 自定义图像合并表单
        private void CustomizeImageCombinerForm(ImageCombinerForm form)
        {
            try
            {
                // 通过反射获取 btnCombine 按钮
                FieldInfo btnCombineField = typeof(ImageCombinerForm).GetField("btnCombine", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (btnCombineField != null)
                {
                    Button btnCombine = btnCombineField.GetValue(form) as Button;
                    if (btnCombine != null)
                    {
                        // 修改按钮文本为"保存"
                        btnCombine.Text = "保存";
                    }
                }
                
                // 移除原有的 ProcessRequested 事件处理器（如果有的话）
                // 添加自定义的保存处理
                form.ProcessRequested += (bmp) =>
                {
                    try
                    {
                        // 显示保存对话框
                        using (SaveFileDialog saveDialog = new SaveFileDialog())
                        {
                            saveDialog.Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp|All Files|*.*";
                            saveDialog.FilterIndex = 1;
                            saveDialog.FileName = "CombinedImage";
                            
                            if (saveDialog.ShowDialog(form) == DialogResult.OK)
                            {
                                ImageHelpers.SaveImage(bmp, saveDialog.FileName);
                                MessageBox.Show(form, "图像已保存", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(form, $"保存图像失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };
                
                // 添加 Ctrl+S 快捷键
                form.KeyDown += (sender, e) =>
                {
                    if (e.Control && e.KeyCode == Keys.S)
                    {
                        e.Handled = true;
                        // 触发合并按钮的点击事件
                        if (btnCombineField != null)
                        {
                            Button btnCombine = btnCombineField.GetValue(form) as Button;
                            if (btnCombine != null && btnCombine.Enabled)
                            {
                                btnCombine.PerformClick();
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                LogError("自定义图像合并表单失败", ex);
            }
        }
        
        // 打开图片特效功能
        private void OpenImageEffects()
        {
            try
            {
                const string EFFECTS_FOLDER = @"C:\Users\zbfzb\Documents\projects\Sidebar\特效\";
                
                // 使用 ShareX 的图片特效功能
                // 首先让用户选择一张图片
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*";
                    openFileDialog.Title = "选择要添加特效的图片";
                    
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string filePath = openFileDialog.FileName;
                        
                        // 检查文件是否存在
                        if (!File.Exists(filePath))
                        {
                            ShowNotification("文件不存在或已被删除", "错误", 3000, MessageBoxIcon.Error);
                            return;
                        }
                        
                        // 验证文件是否为有效的图片格式
                        Bitmap image = null;
                        try
                        {
                            image = new Bitmap(filePath);
                        }
                        catch (Exception imgEx)
                        {
                            ShowNotification($"无法打开图片文件：{imgEx.Message}", "错误", 3000, MessageBoxIcon.Error);
                            return;
                        }
                        
                        try
                        {
                            // 初始化 ShareXSpecialFolders（如果未初始化）
                            InitializeShareXSpecialFolders(EFFECTS_FOLDER);
                            
                            // 从指定目录加载预设
                            List<ImageEffectPreset> presets = LoadPresetsFromFolder(EFFECTS_FOLDER);
                            
                            if (presets.Count == 0)
                            {
                                presets.Add(new ImageEffectPreset());
                            }
                            
                            // 创建 ImageEffectsForm
                            ImageEffectsForm imageEffectsForm = new ImageEffectsForm(image, presets, 0);
                            
                            // 启用工具模式，允许加载和保存图片
                            imageEffectsForm.EnableToolMode((processedImage) => {
                                // 处理后的图片回调（如果需要）
                            }, filePath);
                            
                            // 保存原始文件路径，用于防止覆盖
                            FieldInfo filePathField = imageEffectsForm.GetType().GetField("originalFilePath", 
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            if (filePathField == null)
                            {
                                // 如果字段不存在，创建一个私有字段来存储原始路径
                                // 使用 Tag 属性来存储原始文件路径
                                imageEffectsForm.Tag = filePath;
                            }
                            else
                            {
                                filePathField.SetValue(imageEffectsForm, filePath);
                            }
                            
                            // 设置窗口标题为中文
                            imageEffectsForm.Text = "图片特效";
                            
                            // 自定义预设列表以显示预览图
                            CustomizePresetListView(imageEffectsForm, EFFECTS_FOLDER);
                            
                            // 自定义界面：隐藏不需要的按钮，修改保存按钮文本
                            CustomizeImageEffectsForm(imageEffectsForm, EFFECTS_FOLDER);
                            
                            // 翻译界面为中文
                            TranslateImageEffectsForm(imageEffectsForm);
                            
                            // 显示窗口
                            imageEffectsForm.Show();
                        }
                        catch (Exception formEx)
                        {
                            ShowNotification($"创建图片特效窗口失败：{formEx.Message}", "错误", 3000, MessageBoxIcon.Error);
                            if (image != null)
                            {
                                image.Dispose();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"打开图片特效失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 初始化 ShareXSpecialFolders
        private void InitializeShareXSpecialFolders(string effectsFolder)
        {
            try
            {
                // 直接设置 HelpersOptions.ShareXSpecialFolders
                if (HelpersOptions.ShareXSpecialFolders == null)
                {
                    HelpersOptions.ShareXSpecialFolders = new Dictionary<string, string>();
                }
                
                // 确保 ShareXImageEffects 键存在
                if (!HelpersOptions.ShareXSpecialFolders.ContainsKey("ShareXImageEffects"))
                {
                    HelpersOptions.ShareXSpecialFolders["ShareXImageEffects"] = effectsFolder;
                }
            }
            catch (Exception ex)
            {
                LogError("初始化 ShareXSpecialFolders 失败", ex);
            }
        }
        
        // 从文件夹加载预设
        private List<ImageEffectPreset> LoadPresetsFromFolder(string folderPath)
        {
            List<ImageEffectPreset> presets = new List<ImageEffectPreset>();
            
            if (!Directory.Exists(folderPath))
            {
                return presets;
            }
            
            try
            {
                ISerializationBinder serializationBinder = new ImageEffectsSerializationBinder();
                string[] sxieFiles = Directory.GetFiles(folderPath, "*.sxie");
                
                // 创建资源文件夹，用于存放解压的图片素材
                string assetsFolder = Path.Combine(folderPath, "assets");
                if (!Directory.Exists(assetsFolder))
                {
                    Directory.CreateDirectory(assetsFolder);
                }
                
                foreach (string sxieFile in sxieFiles)
                {
                    try
                    {
                        // 解压 .sxie 文件到临时目录
                        string tempExtractPath = Path.Combine(Path.GetTempPath(), "SidebarEffects", Guid.NewGuid().ToString());
                        Directory.CreateDirectory(tempExtractPath);
                        
                        try
                        {
                            string configJson = ImageEffectPackager.ExtractPackage(sxieFile, tempExtractPath);
                            
                            if (!string.IsNullOrEmpty(configJson))
                            {
                                // 获取预设名称（从文件名或配置中）
                                string presetName = Path.GetFileNameWithoutExtension(sxieFile);
                                
                                // 创建预设专用的资源文件夹
                                string presetAssetsFolder = Path.Combine(assetsFolder, presetName);
                                if (!Directory.Exists(presetAssetsFolder))
                                {
                                    Directory.CreateDirectory(presetAssetsFolder);
                                }
                                
                                // 将解压的图片文件复制到预设资源文件夹
                                Dictionary<string, string> imagePathMapping = new Dictionary<string, string>();
                                if (Directory.Exists(tempExtractPath))
                                {
                                    string[] imageFiles = Directory.GetFiles(tempExtractPath, "*.*", SearchOption.AllDirectories)
                                        .Where(f => FileHelpers.IsImageFile(f)).ToArray();
                                    
                                    foreach (string imageFile in imageFiles)
                                    {
                                        string relativePath = Path.GetRelativePath(tempExtractPath, imageFile);
                                        string targetPath = Path.Combine(presetAssetsFolder, relativePath);
                                        string targetDir = Path.GetDirectoryName(targetPath);
                                        
                                        if (!Directory.Exists(targetDir))
                                        {
                                            Directory.CreateDirectory(targetDir);
                                        }
                                        
                                        // 复制文件
                                        File.Copy(imageFile, targetPath, true);
                                        
                                        // 记录路径映射（原始路径 -> 新路径）
                                        imagePathMapping[imageFile] = targetPath;
                                        
                                        // 也记录相对路径映射
                                        string originalRelative = relativePath.Replace('\\', '/');
                                        imagePathMapping[originalRelative] = targetPath;
                                    }
                                }
                                
                                // 反序列化预设
                                ImageEffectPreset preset = JsonHelpers.DeserializeFromString<ImageEffectPreset>(configJson, serializationBinder);
                                
                                if (preset != null)
                                {
                                    // 修复预设中的图片路径
                                    FixImagePathsInPreset(preset, presetAssetsFolder, folderPath);
                                    
                                    presets.Add(preset);
                                }
                            }
                        }
                        finally
                        {
                            // 清理临时文件
                            try
                            {
                                if (Directory.Exists(tempExtractPath))
                                {
                                    Directory.Delete(tempExtractPath, true);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError("清理临时文件失败", ex);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"加载预设失败: {sxieFile}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("加载预设文件夹失败", ex);
            }
            
            return presets;
        }
        
        // 修复预设中的图片路径
        private void FixImagePathsInPreset(ImageEffectPreset preset, string assetsFolder, string effectsFolder)
        {
            if (preset?.Effects == null) return;
            
            foreach (ImageEffect effect in preset.Effects)
            {
                if (effect == null) continue;
                
                Type effectType = effect.GetType();
                
                // 处理 DrawImage 的 ImageLocation 属性
                if (effectType.Name == "DrawImage")
                {
                    PropertyInfo imageLocationProp = effectType.GetProperty("ImageLocation");
                    if (imageLocationProp != null && imageLocationProp.CanRead && imageLocationProp.CanWrite)
                    {
                        string imageLocation = imageLocationProp.GetValue(effect) as string;
                        if (!string.IsNullOrEmpty(imageLocation))
                        {
                            string fixedPath = FixImagePath(imageLocation, assetsFolder, effectsFolder);
                            if (fixedPath != imageLocation)
                            {
                                imageLocationProp.SetValue(effect, fixedPath);
                            }
                        }
                    }
                }
                // 处理 DrawBackgroundImage 的 ImageFilePath 属性
                else if (effectType.Name == "DrawBackgroundImage")
                {
                    PropertyInfo imageFilePathProp = effectType.GetProperty("ImageFilePath");
                    if (imageFilePathProp != null && imageFilePathProp.CanRead && imageFilePathProp.CanWrite)
                    {
                        string imageFilePath = imageFilePathProp.GetValue(effect) as string;
                        if (!string.IsNullOrEmpty(imageFilePath))
                        {
                            string fixedPath = FixImagePath(imageFilePath, assetsFolder, effectsFolder);
                            if (fixedPath != imageFilePath)
                            {
                                imageFilePathProp.SetValue(effect, fixedPath);
                            }
                        }
                    }
                }
            }
        }
        
        // 修复单个图片路径
        private string FixImagePath(string originalPath, string assetsFolder, string effectsFolder)
        {
            if (string.IsNullOrEmpty(originalPath))
            {
                return originalPath;
            }
            
            // 如果路径已经是特殊文件夹变量格式，直接返回
            if (originalPath.Contains("%ShareXImageEffects%"))
            {
                return originalPath;
            }
            
            // 尝试展开路径
            string expandedPath = FileHelpers.ExpandFolderVariables(originalPath, true);
            
            // 如果展开后的路径存在，直接返回原始路径（让系统自己解析）
            if (File.Exists(expandedPath))
            {
                return originalPath;
            }
            
            // 如果路径是相对路径，尝试在资源文件夹中查找
            string relativePath = originalPath.Replace('\\', '/');
            if (!Path.IsPathRooted(relativePath))
            {
                // 尝试在预设资源文件夹中查找
                string potentialPath = Path.Combine(assetsFolder, relativePath);
                if (File.Exists(potentialPath))
                {
                    // 转换为使用特殊文件夹变量的路径
                    string relativeToEffectsFolder = Path.GetRelativePath(effectsFolder, potentialPath);
                    return $"%ShareXImageEffects%\\{relativeToEffectsFolder.Replace('/', '\\')}";
                }
                
                // 尝试直接使用文件名在资源文件夹中查找
                string fileName = Path.GetFileName(relativePath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    string[] foundFiles = Directory.GetFiles(assetsFolder, fileName, SearchOption.AllDirectories);
                    if (foundFiles.Length > 0)
                    {
                        string foundPath = foundFiles[0];
                        string relativeToEffectsFolder = Path.GetRelativePath(effectsFolder, foundPath);
                        return $"%ShareXImageEffects%\\{relativeToEffectsFolder.Replace('/', '\\')}";
                    }
                }
            }
            
            // 如果原始路径是绝对路径，尝试在资源文件夹中查找同名文件
            if (Path.IsPathRooted(originalPath))
            {
                string fileName = Path.GetFileName(originalPath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    string[] foundFiles = Directory.GetFiles(assetsFolder, fileName, SearchOption.AllDirectories);
                    if (foundFiles.Length > 0)
                    {
                        string foundPath = foundFiles[0];
                        string relativeToEffectsFolder = Path.GetRelativePath(effectsFolder, foundPath);
                        return $"%ShareXImageEffects%\\{relativeToEffectsFolder.Replace('/', '\\')}";
                    }
                }
            }
            
            // 如果都找不到，返回原始路径（让系统处理错误）
            return originalPath;
        }
        
        // 自定义预设列表以显示预览图
        private void CustomizePresetListView(ImageEffectsForm form, string effectsFolder)
        {
            try
            {
                // 使用反射获取 lvPresets
                FieldInfo lvPresetsField = form.GetType().GetField("lvPresets", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (lvPresetsField == null)
                {
                    return;
                }
                
                ListView lvPresets = lvPresetsField.GetValue(form) as ListView;
                if (lvPresets == null)
                {
                    return;
                }
                
                // 创建 ImageList 用于显示预览图
                ImageList imageList = new ImageList
                {
                    ImageSize = new Size(120, 120),
                    ColorDepth = ColorDepth.Depth32Bit
                };
                
                // 设置 ListView 为大图标视图
                lvPresets.View = View.LargeIcon;
                lvPresets.LargeImageList = imageList;
                
                // 获取 Presets 列表
                PropertyInfo presetsProperty = form.GetType().GetProperty("Presets", 
                    BindingFlags.Public | BindingFlags.Instance);
                if (presetsProperty == null)
                {
                    return;
                }
                
                List<ImageEffectPreset> presets = presetsProperty.GetValue(form) as List<ImageEffectPreset>;
                if (presets == null)
                {
                    return;
                }
                
                // 获取所有 .sxie 文件列表（用于匹配文件名）
                string[] sxieFiles = Directory.Exists(effectsFolder) 
                    ? Directory.GetFiles(effectsFolder, "*.sxie") 
                    : new string[0];
                
                // 为每个预设加载预览图
                for (int i = 0; i < presets.Count; i++)
                {
                    ImageEffectPreset preset = presets[i];
                    string presetName = preset.Name;
                    
                    // 如果预设名称为空，尝试从文件名获取
                    if (string.IsNullOrEmpty(presetName) && i < sxieFiles.Length)
                    {
                        presetName = Path.GetFileNameWithoutExtension(sxieFiles[i]);
                    }
                    
                    // 查找预览图
                    Image previewImage = null;
                    string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };
                    
                    foreach (string ext in imageExtensions)
                    {
                        string previewPath = Path.Combine(effectsFolder, presetName + ext);
                        if (File.Exists(previewPath))
                        {
                            try
                            {
                                previewImage = Image.FromFile(previewPath);
                                break;
                            }
                            catch
                            {
                                // 如果加载失败，继续尝试下一个
                            }
                        }
                    }
                    
                    // 如果没有找到预览图，创建默认预览图
                    if (previewImage == null)
                    {
                        previewImage = CreateDefaultPresetPreview(presetName);
                    }
                    
                    // 调整预览图大小
                    Image thumbnail = new Bitmap(previewImage, new Size(120, 120));
                    imageList.Images.Add(thumbnail);
                    
                    // 更新 ListViewItem 以显示预览图
                    if (i < lvPresets.Items.Count)
                    {
                        ListViewItem item = lvPresets.Items[i];
                        item.ImageIndex = imageList.Images.Count - 1;
                    }
                    
                    // 释放临时图片（thumbnail 会被 ImageList 管理）
                    if (previewImage != thumbnail)
                    {
                        previewImage.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("自定义预设列表失败", ex);
            }
        }
        
        // 自定义 ImageEffectsForm 界面
        private void CustomizeImageEffectsForm(ImageEffectsForm form, string effectsFolder)
        {
            try
            {
                // 隐藏"图像特效"按钮
                HideButton(form, "btnImageEffects");
                
                // 隐藏"加载图像"按钮（MenuButton）
                try
                {
                    FieldInfo mbLoadImageField = form.GetType().GetField("mbLoadImage", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mbLoadImageField != null)
                    {
                        Control mbLoadImage = mbLoadImageField.GetValue(form) as Control;
                        if (mbLoadImage != null)
                        {
                            mbLoadImage.Visible = false;
                            mbLoadImage.Enabled = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError("隐藏 ImageEffectsForm 按钮失败", ex);
                }
                
                // 隐藏"上传图像"按钮
                HideButton(form, "btnUploadImage");
                
                // 隐藏"关闭"按钮
                HideButton(form, "btnClose");
                
                // 修改"保存图像"按钮文本为"保存"，并自定义保存逻辑防止覆盖原文件
                Button btnSaveImage = GetControl<Button>(form, "btnSaveImage");
                if (btnSaveImage != null)
                {
                    btnSaveImage.Text = "保存";
                    
                    // 设置保存按钮位置为最左侧（在窗口显示后调整位置）
                    btnSaveImage.Dock = DockStyle.None;
                    btnSaveImage.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
                    form.Shown += (s, e) => {
                        btnSaveImage.Location = new Point(10, form.ClientSize.Height - btnSaveImage.Height - 10);
                    };
                    
                    // 移除原有的事件处理程序
                    try
                    {
                        MethodInfo originalMethod = form.GetType().GetMethod("btnSaveImage_Click", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (originalMethod != null)
                        {
                            EventHandler originalHandler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, originalMethod);
                            btnSaveImage.Click -= originalHandler;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("移除事件处理器失败", ex);
                    }
                    
                    // 添加新的事件处理程序，防止覆盖原文件
                    btnSaveImage.Click += (sender, e) => {
                        try
                        {
                            // 获取 PreviewImage
                            PropertyInfo previewImageProperty = form.GetType().GetProperty("PreviewImage", 
                                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            Bitmap previewImage = previewImageProperty?.GetValue(form) as Bitmap;
                            
                            if (previewImage == null)
                            {
                                return;
                            }
                            
                            // 获取 ApplyEffects 方法
                            MethodInfo applyEffectsMethod = form.GetType().GetMethod("ApplyEffects", 
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            if (applyEffectsMethod == null)
                            {
                                return;
                            }
                            
                            // 应用特效
                            Image processedImage = applyEffectsMethod.Invoke(form, null) as Image;
                            if (processedImage == null)
                            {
                                return;
                            }
                            
                            using (processedImage)
                            {
                                // 获取原始文件路径
                                string originalFilePath = form.Tag as string;
                                if (string.IsNullOrEmpty(originalFilePath))
                                {
                                    PropertyInfo filePathProperty = form.GetType().GetProperty("FilePath", 
                                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                    originalFilePath = filePathProperty?.GetValue(form) as string;
                                }
                                
                                // 使用自定义保存对话框，防止覆盖原文件
                                string savePath = ShowSaveImageDialogWithProtection(processedImage, originalFilePath);
                                
                                if (!string.IsNullOrEmpty(savePath))
                                {
                                    // 更新 FilePath 属性
                                    PropertyInfo filePathProperty = form.GetType().GetProperty("FilePath", 
                                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                    if (filePathProperty != null && filePathProperty.CanWrite)
                                    {
                                        filePathProperty.SetValue(form, savePath);
                                    }
                                    
                                    ShowNotification("图片保存成功", "成功", 2000, MessageBoxIcon.Information);
                                    
                                    // 保存成功后，自动关闭窗口
                                    form.Close();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ShowNotification($"保存图片失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                        }
                    };
                }
                
                // 隐藏打包器按钮
                HideButton(form, "btnPackager");
            }
            catch (Exception ex)
            {
                LogError("自定义 ImageEffectsForm 界面失败", ex);
            }
        }
        
        // 处理打包器按钮点击，绕过路径限制
        private void HandlePackagerClick(ImageEffectsForm form, string effectsFolder)
        {
            try
            {
                // 获取当前选中的预设
                MethodInfo getSelectedPresetMethod = form.GetType().GetMethod("GetSelectedPreset", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (getSelectedPresetMethod == null)
                {
                    return;
                }
                
                object presetObj = getSelectedPresetMethod.Invoke(form, null);
                if (presetObj == null)
                {
                    return;
                }
                
                // 获取预设名称
                PropertyInfo nameProperty = presetObj.GetType().GetProperty("Name");
                string presetName = nameProperty?.GetValue(presetObj) as string;
                if (string.IsNullOrEmpty(presetName))
                {
                    presetName = "Unnamed";
                }
                
                // 序列化预设为 JSON
                ISerializationBinder serializationBinder = new ImageEffectsSerializationBinder();
                string json = JsonHelpers.SerializeToString(presetObj, serializationBinder: serializationBinder);
                
                // 创建打包器窗口，但绕过路径验证
                Type packagerFormType = typeof(ImageEffectPackagerForm);
                ConstructorInfo constructor = packagerFormType.GetConstructor(
                    new Type[] { typeof(string), typeof(string), typeof(string) });
                
                if (constructor != null)
                {
                    // 使用特效文件夹路径创建打包器窗口
                    object packagerForm = constructor.Invoke(new object[] { json, presetName, effectsFolder });
                    
                    // 使用反射移除路径验证限制
                    RemovePathValidationRestriction(packagerForm);
                    
                    // 显示窗口
                    MethodInfo showMethod = packagerFormType.GetMethod("Show", new Type[] { });
                    if (showMethod != null)
                    {
                        showMethod.Invoke(packagerForm, null);
                    }
                    else
                    {
                        // 如果 Show 方法不存在，使用 ShowDialog
                        MethodInfo showDialogMethod = packagerFormType.GetMethod("ShowDialog", new Type[] { });
                        showDialogMethod?.Invoke(packagerForm, null);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("处理打包器点击失败", ex);
                ShowNotification($"打包失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 移除路径验证限制
        private void RemovePathValidationRestriction(object packagerForm)
        {
            try
            {
                Type formType = packagerForm.GetType();
                
                // 获取 btnPackage 按钮
                FieldInfo btnPackageField = formType.GetField("btnPackage", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (btnPackageField == null)
                {
                    return;
                }
                
                Button btnPackage = btnPackageField.GetValue(packagerForm) as Button;
                if (btnPackage == null)
                {
                    return;
                }
                
                // 移除原有的点击事件处理程序
                // 通过反射获取事件字段并清除所有处理程序
                FieldInfo eventsField = typeof(Control).GetField("Events", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (eventsField != null)
                {
                    object events = eventsField.GetValue(btnPackage);
                    if (events != null)
                    {
                        PropertyInfo clickProperty = events.GetType().GetProperty("Item");
                        if (clickProperty != null)
                        {
                            object clickHandler = clickProperty.GetValue(events, new object[] { typeof(EventHandler) });
                            if (clickHandler != null)
                            {
                                // 清除所有处理程序
                                MethodInfo removeAllMethod = clickHandler.GetType().GetMethod("RemoveAll");
                                removeAllMethod?.Invoke(clickHandler, null);
                            }
                        }
                    }
                }
                
                // 添加新的点击事件处理程序，绕过路径验证
                btnPackage.Click += (sender, e) => {
                    HandlePackageWithoutRestriction(packagerForm);
                };
            }
            catch (Exception ex)
            {
                LogError("移除路径验证限制失败", ex);
            }
        }
        
        // 处理打包，不进行路径验证
        private void HandlePackageWithoutRestriction(object packagerForm)
        {
            try
            {
                Type formType = packagerForm.GetType();
                
                // 获取属性值
                PropertyInfo packageFilePathProp = formType.GetProperty("PackageFilePath");
                PropertyInfo assetsFolderPathProp = formType.GetProperty("AssetsFolderPath");
                PropertyInfo imageEffectJsonProp = formType.GetProperty("ImageEffectJson");
                
                string packageFilePath = packageFilePathProp?.GetValue(packagerForm) as string;
                string assetsFolderPath = assetsFolderPathProp?.GetValue(packagerForm) as string;
                string imageEffectJson = imageEffectJsonProp?.GetValue(packagerForm) as string;
                
                if (string.IsNullOrEmpty(packageFilePath) || string.IsNullOrEmpty(imageEffectJson))
                {
                    ShowNotification("打包路径或配置不能为空", "错误", 3000, MessageBoxIcon.Error);
                    return;
                }
                
                // 检查文件是否已存在
                if (File.Exists(packageFilePath))
                {
                    DialogResult result = MessageBox.Show(
                        "文件已存在，是否覆盖？",
                        "确认",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    
                    if (result != DialogResult.Yes)
                    {
                        return;
                    }
                }
                
                // 直接调用打包方法，不进行路径验证
                string outputFilePath = ImageEffectPackager.Package(packageFilePath, imageEffectJson, assetsFolderPath);
                
                if (!string.IsNullOrEmpty(outputFilePath) && File.Exists(outputFilePath))
                {
                    FileHelpers.OpenFolderWithFile(outputFilePath);
                    ShowNotification("打包成功", "成功", 2000, MessageBoxIcon.Information);
                    
                    // 关闭打包器窗口
                    MethodInfo closeMethod = formType.GetMethod("Close");
                    closeMethod?.Invoke(packagerForm, null);
                }
                else
                {
                    ShowNotification("打包失败", "错误", 3000, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                LogError("打包失败", ex);
                ShowNotification($"打包失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 翻译 ImageEffectsForm 界面为中文
        private void TranslateImageEffectsForm(ImageEffectsForm form)
        {
            try
            {
                // 翻译标签
                TranslateControl(form, "lblPresets", "预设");
                TranslateControl(form, "lblPresetName", "预设名称：");
                TranslateControl(form, "lblEffects", "特效");
                TranslateControl(form, "lblEffectName", "特效名称：");
                
                // 翻译按钮的 ToolTip（按钮本身是图标按钮）
                TranslateButtonToolTip(form, "btnPresetNew", "新建预设");
                TranslateButtonToolTip(form, "btnPresetRemove", "删除预设");
                TranslateButtonToolTip(form, "btnPresetDuplicate", "复制预设");
                TranslateButtonToolTip(form, "btnEffectAdd", "添加特效");
                TranslateButtonToolTip(form, "btnEffectRemove", "删除特效");
                TranslateButtonToolTip(form, "btnEffectDuplicate", "复制特效");
                TranslateButtonToolTip(form, "btnEffectClear", "清空特效");
                TranslateButtonToolTip(form, "btnEffectRefresh", "刷新预览");
                TranslateButtonToolTip(form, "btnPackager", "打包器");
                
                // 设置 PropertyGrid 的属性名称翻译
                SetupPropertyGridTranslation(form);
                
                // 设置特效列表的翻译
                SetupEffectsListTranslation(form);
                
                // 设置特效添加菜单的翻译
                SetupEffectContextMenuTranslation(form);
                
                // 延迟翻译，确保所有控件都已加载完成
                form.Load += (sender, e) => {
                    System.Windows.Forms.Timer translateTimer = new System.Windows.Forms.Timer();
                    translateTimer.Interval = 100;
                    translateTimer.Tick += (s, args) => {
                        translateTimer.Stop();
                        translateTimer.Dispose();
                        // 再次翻译以确保所有控件都已加载
                        TranslateControl(form, "lblPresets", "预设");
                        TranslateControl(form, "lblPresetName", "预设名称：");
                        TranslateControl(form, "lblEffects", "特效");
                        TranslateControl(form, "lblEffectName", "特效名称：");
                    };
                    translateTimer.Start();
                };
            }
            catch (Exception ex)
            {
                LogError("翻译 ImageEffectsForm 界面失败", ex);
            }
        }
        
        // 设置 PropertyGrid 的属性名称翻译
        private void SetupPropertyGridTranslation(ImageEffectsForm form)
        {
            try
            {
                // 获取 PropertyGrid 控件
                PropertyGrid pgSettings = GetControl<PropertyGrid>(form, "pgSettings");
                if (pgSettings != null)
                {
                    // 使用反射监听 PropertyGrid 的内部事件
                    // PropertyGrid 没有直接的 SelectedObjectChanged 事件，我们需要通过其他方式监听
                    // 方法：定期检查 SelectedObject 是否改变
                    System.Windows.Forms.Timer checkTimer = new System.Windows.Forms.Timer();
                    object lastSelectedObject = null;
                    
                    checkTimer.Interval = 100; // 每100ms检查一次
                    checkTimer.Tick += (sender, e) => {
                        if (pgSettings.SelectedObject != lastSelectedObject)
                        {
                            lastSelectedObject = pgSettings.SelectedObject;
                            if (lastSelectedObject != null)
                            {
                                ApplyPropertyTranslation(pgSettings);
                            }
                        }
                    };
                    checkTimer.Start();
                    
                    // 监听 PropertyValueChanged 事件（当属性值改变时，刷新翻译）
                    pgSettings.PropertyValueChanged += (sender, e) => {
                        ApplyPropertyTranslation(pgSettings);
                    };
                    
                    // 如果已经有选中的对象，立即应用翻译
                    if (pgSettings.SelectedObject != null)
                    {
                        lastSelectedObject = pgSettings.SelectedObject;
                        ApplyPropertyTranslation(pgSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("设置 PropertyGrid 翻译失败", ex);
            }
        }
        
        // 设置特效添加菜单的翻译
        private void SetupEffectContextMenuTranslation(ImageEffectsForm form)
        {
            try
            {
                // 获取 ContextMenuStrip 控件
                ContextMenuStrip cmsEffects = GetControl<ContextMenuStrip>(form, "cmsEffects");
                if (cmsEffects != null)
                {
                    // 翻译菜单项
                    TranslateEffectContextMenu(cmsEffects);
                    
                    // 监听菜单打开事件，确保每次打开时都翻译
                    cmsEffects.Opening += (sender, e) => {
                        TranslateEffectContextMenu(cmsEffects);
                    };
                }
            }
            catch (Exception ex)
            {
                LogError("设置特效菜单翻译失败", ex);
            }
        }
        
        // 翻译特效添加菜单
        private void TranslateEffectContextMenu(ContextMenuStrip cmsEffects)
        {
            try
            {
                if (cmsEffects?.Items == null) return;
                
                Dictionary<string, string> effectNameTranslator = GetEffectNameTranslator();
                Dictionary<string, string> groupNameTranslator = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Drawings", "绘制" },
                    { "Manipulations", "操作" },
                    { "Adjustments", "调整" },
                    { "Filters", "滤镜" }
                };
                
                foreach (ToolStripItem parentItem in cmsEffects.Items)
                {
                    if (parentItem is ToolStripMenuItem tsmiParent)
                    {
                        // 翻译父菜单项（分组名称）
                        string parentText = tsmiParent.Text;
                        foreach (var kvp in groupNameTranslator)
                        {
                            if (parentText.Contains(kvp.Key))
                            {
                                parentText = parentText.Replace(kvp.Key, kvp.Value);
                            }
                        }
                        tsmiParent.Text = parentText;
                        
                        // 翻译子菜单项（特效名称）
                        if (tsmiParent.DropDownItems != null)
                        {
                            foreach (ToolStripItem childItem in tsmiParent.DropDownItems)
                            {
                                if (childItem is ToolStripMenuItem tsmiChild && tsmiChild.Tag is Type effectType)
                                {
                                    string typeName = effectType.Name;
                                    if (effectNameTranslator.TryGetValue(typeName, out string chineseName))
                                    {
                                        tsmiChild.Text = chineseName;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("翻译特效菜单失败", ex);
            }
        }
        
        // 设置特效列表的翻译
        private void SetupEffectsListTranslation(ImageEffectsForm form)
        {
            try
            {
                // 获取 ListView 控件
                ListView lvEffects = GetControl<ListView>(form, "lvEffects");
                if (lvEffects != null)
                {
                    // 使用定时器定期检查并翻译所有特效项
                    System.Windows.Forms.Timer translateTimer = new System.Windows.Forms.Timer();
                    translateTimer.Interval = 200; // 每200ms检查一次
                    translateTimer.Tick += (sender, e) => {
                        TranslateAllEffectListItems(lvEffects);
                    };
                    translateTimer.Start();
                    
                    // 监听 SelectedIndexChanged 事件（当选中特效改变时，更新显示）
                    lvEffects.SelectedIndexChanged += (sender, e) => {
                        TranslateAllEffectListItems(lvEffects);
                    };
                    
                    // 立即翻译已有的特效项
                    TranslateAllEffectListItems(lvEffects);
                }
            }
            catch (Exception ex)
            {
                LogError("设置特效列表翻译失败", ex);
            }
        }
        
        // 翻译所有特效列表项
        private void TranslateAllEffectListItems(ListView lvEffects)
        {
            try
            {
                if (lvEffects?.Items == null) return;
                
                foreach (ListViewItem item in lvEffects.Items)
                {
                    TranslateEffectListItem(item);
                }
            }
            catch
            {
                // 忽略错误
            }
        }
        
        // 翻译单个特效列表项
        private void TranslateEffectListItem(ListViewItem item)
        {
            try
            {
                if (item?.Tag is ShareX.ImageEffectsLib.ImageEffect imageEffect)
                {
                    string originalText = imageEffect.ToString();
                    string translatedText = TranslateEffectName(originalText, imageEffect.GetType());
                    
                    if (!string.IsNullOrEmpty(translatedText) && translatedText != originalText)
                    {
                        item.Text = translatedText;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
        }
        
        // 翻译特效名称
        private string TranslateEffectName(string originalName, Type effectType)
        {
            try
            {
                // 如果特效有自定义名称，保留自定义名称，但尝试翻译类型名称
                if (effectType != null)
                {
                    string typeName = effectType.Name;
                    Dictionary<string, string> effectNameTranslator = GetEffectNameTranslator();
                    
                    if (effectNameTranslator.TryGetValue(typeName, out string chineseName))
                    {
                        // 如果原始名称包含冒号（有摘要），保留摘要部分
                        int colonIndex = originalName.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            string summary = originalName.Substring(colonIndex);
                            return chineseName + summary;
                        }
                        return chineseName;
                    }
                }
                
                return originalName;
            }
            catch
            {
                return originalName;
            }
        }
        
        // 获取特效名称翻译字典
        private Dictionary<string, string> GetEffectNameTranslator()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Drawings
                { "DrawBackground", "绘制背景" },
                { "DrawBackgroundImage", "绘制背景图片" },
                { "DrawBorder", "绘制边框" },
                { "DrawCheckerboard", "绘制棋盘" },
                { "DrawImage", "绘制图片" },
                { "DrawParticles", "绘制粒子" },
                { "DrawText", "绘制文本" },
                { "DrawTextEx", "绘制文本扩展" },
                
                // Manipulations
                { "AutoCrop", "自动裁剪" },
                { "Canvas", "画布" },
                { "Crop", "裁剪" },
                { "Flip", "翻转" },
                { "ForceProportions", "强制比例" },
                { "Resize", "调整大小" },
                { "Rotate", "旋转" },
                { "RoundedCorners", "圆角" },
                { "Scale", "缩放" },
                { "Skew", "倾斜" },
                
                // Adjustments
                { "Alpha", "透明度" },
                { "BlackWhite", "黑白" },
                { "Brightness", "亮度" },
                { "Colorize", "着色" },
                { "Contrast", "对比度" },
                { "Gamma", "伽马" },
                { "Grayscale", "灰度" },
                { "Hue", "色调" },
                { "Inverse", "反转" },
                { "MatrixColor", "颜色矩阵" },
                { "ReplaceColor", "替换颜色" },
                { "Saturation", "饱和度" },
                { "SelectiveColor", "选择性颜色" },
                { "Sepia", "怀旧" },
                { "Polaroid", "宝丽来" },
                
                // Filters
                { "Blur", "模糊" },
                { "ColorDepth", "颜色深度" },
                { "EdgeDetect", "边缘检测" },
                { "Emboss", "浮雕" },
                { "GaussianBlur", "高斯模糊" },
                { "Glow", "发光" },
                { "MeanRemoval", "均值移除" },
                { "MatrixConvolution", "卷积矩阵" },
                { "Outline", "轮廓" },
                { "Pixelate", "像素化" },
                { "Sharpen", "锐化" },
                { "Smooth", "平滑" },
                { "Reflection", "反射" },
                { "RGBSplit", "RGB分离" },
                { "Shadow", "阴影" },
                { "Slice", "切片" },
                { "TornEdge", "撕裂边缘" },
                { "WaveEdge", "波浪边缘" }
            };
        }
        
        // 应用属性翻译到 PropertyGrid
        private void ApplyPropertyTranslation(PropertyGrid propertyGrid)
        {
            try
            {
                if (propertyGrid.SelectedObject == null)
                {
                    return;
                }
                
                object selectedObject = propertyGrid.SelectedObject;
                Type objectType = selectedObject.GetType();
                
                // 移除旧的提供程序（如果存在）
                TypeDescriptionProvider existingProvider = TypeDescriptor.GetProvider(objectType);
                if (existingProvider is TranslatedTypeDescriptionProvider oldProvider)
                {
                    TypeDescriptor.RemoveProvider(oldProvider, selectedObject);
                    existingProvider = TypeDescriptor.GetProvider(objectType);
                }
                
                // 创建并注册新的翻译提供程序
                Dictionary<string, string> translator = GetPropertyNameTranslator();
                TranslatedTypeDescriptionProvider provider = new TranslatedTypeDescriptionProvider(
                    existingProvider, translator);
                TypeDescriptor.AddProvider(provider, selectedObject);
                
                // 强制刷新 PropertyGrid 显示
                propertyGrid.SelectedObject = null;
                propertyGrid.SelectedObject = selectedObject;
            }
            catch (Exception ex)
            {
                LogError("应用属性翻译失败", ex);
            }
        }
        
        // 获取属性名称翻译字典
        private Dictionary<string, string> GetPropertyNameTranslator()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Enabled", "启用" },
                { "Name", "名称" },
                { "Value", "值" },
                { "Radius", "半径" },
                { "Intensity", "强度" },
                { "Threshold", "阈值" },
                { "Color", "颜色" },
                { "Size", "大小" },
                { "Width", "宽度" },
                { "Height", "高度" },
                { "X", "X坐标" },
                { "Y", "Y坐标" },
                { "Offset", "偏移" },
                { "Margin", "边距" },
                { "Padding", "内边距" },
                { "Angle", "角度" },
                { "Distance", "距离" },
                { "Opacity", "透明度" },
                { "Blur", "模糊" },
                { "Brightness", "亮度" },
                { "Contrast", "对比度" },
                { "Saturation", "饱和度" },
                { "Hue", "色调" },
                { "Gamma", "伽马" },
                { "Rotation", "旋转" },
                { "Scale", "缩放" },
                { "Flip", "翻转" },
                { "Crop", "裁剪" },
                { "Resize", "调整大小" },
                { "Text", "文本" },
                { "Font", "字体" },
                { "Background", "背景" },
                { "Border", "边框" },
                { "Shadow", "阴影" },
                { "Glow", "发光" },
                { "Outline", "轮廓" },
                { "Edge", "边缘" },
                { "Pixelate", "像素化" },
                { "Emboss", "浮雕" },
                { "Sharpen", "锐化" },
                { "Smooth", "平滑" },
                { "Inverse", "反转" },
                { "Grayscale", "灰度" },
                { "Sepia", "怀旧" },
                { "BlackWhite", "黑白" },
                { "Colorize", "着色" },
                { "ReplaceColor", "替换颜色" },
                { "SelectiveColor", "选择性颜色" },
                { "MatrixColor", "矩阵颜色" },
                { "Polaroid", "宝丽来" },
                { "Alpha", "透明度" },
                { "BlockSize", "块大小" },
                { "GlowRadius", "发光半径" },
                { "GlowR", "发光红色" },
                { "GlowG", "发光绿色" },
                { "GlowB", "发光蓝色" },
                { "UseGradient", "使用渐变" },
                { "GradientType", "渐变类型" },
                { "GradientColors", "渐变颜色" },
                { "GradientAngle", "渐变角度" },
                { "CheckerboardSize", "棋盘大小" },
                { "CheckerboardColor1", "棋盘颜色1" },
                { "CheckerboardColor2", "棋盘颜色2" },
                { "ImagePath", "图片路径" },
                { "ImageSize", "图片大小" },
                { "ImagePosition", "图片位置" },
                { "ImageAlignment", "图片对齐" },
                { "TextAlignment", "文本对齐" },
                { "TextPosition", "文本位置" },
                { "TextColor", "文本颜色" },
                { "TextShadow", "文本阴影" },
                { "TextShadowColor", "文本阴影颜色" },
                { "TextShadowOffset", "文本阴影偏移" },
                { "TextShadowBlur", "文本阴影模糊" },
                { "AutoSize", "自动大小" },
                { "KeepAspectRatio", "保持宽高比" },
                { "ForceProportions", "强制比例" },
                { "MinSize", "最小大小" },
                { "MaxSize", "最大大小" },
                { "TopMost", "置顶" },
                { "KeepCenterLocation", "保持中心位置" },
                { "Placement", "位置" },
                { "PlacementOffset", "位置偏移" },
                { "BorderSize", "边框大小" },
                { "BorderColor", "边框颜色" },
                { "MinimizeSize", "最小化大小" },
                { "SmartPadding", "智能内边距" },
                { "RoundedCorner", "圆角" },
                { "ShadowRadius", "阴影半径" },
                { "ShadowAngle", "阴影角度" },
                { "ShadowDistance", "阴影距离" },
                { "ShadowOpacity", "阴影透明度" },
                { "BackgroundImageFilePath", "背景图片路径" },
                { "BackgroundType", "背景类型" },
                { "BackgroundColor", "背景颜色" },
                { "BackgroundGradient", "背景渐变" },
                { "BackgroundGradientColors", "背景渐变颜色" },
                { "BackgroundGradientAngle", "背景渐变角度" },
                { "BackgroundImage", "背景图片" },
                { "BackgroundImageAlignment", "背景图片对齐" },
                { "BackgroundImageSize", "背景图片大小" },
                { "BackgroundImageOpacity", "背景图片透明度" },
                { "ParticlesCount", "粒子数量" },
                { "ParticlesColor", "粒子颜色" },
                { "ParticlesSize", "粒子大小" },
                { "ParticlesSpeed", "粒子速度" },
                { "ParticlesLife", "粒子寿命" },
                { "ParticlesGravity", "粒子重力" },
                { "ParticlesWind", "粒子风力" },
                { "ParticlesFade", "粒子淡出" },
                { "ParticlesBlend", "粒子混合" },
                { "ParticlesRandom", "粒子随机" },
                { "ParticlesDirection", "粒子方向" },
                { "ParticlesSpread", "粒子扩散" },
                { "ParticlesBounce", "粒子弹跳" },
                { "ParticlesCollision", "粒子碰撞" },
                { "ParticlesTrail", "粒子轨迹" },
                { "ParticlesTrailLength", "粒子轨迹长度" },
                { "ParticlesTrailFade", "粒子轨迹淡出" },
                { "ParticlesTrailColor", "粒子轨迹颜色" },
                { "ParticlesTrailSize", "粒子轨迹大小" },
                { "ParticlesTrailOpacity", "粒子轨迹透明度" },
                { "ParticlesTrailBlend", "粒子轨迹混合" },
                { "ParticlesTrailRandom", "粒子轨迹随机" },
                { "ParticlesTrailDirection", "粒子轨迹方向" },
                { "ParticlesTrailSpread", "粒子轨迹扩散" },
                { "ParticlesTrailBounce", "粒子轨迹弹跳" },
                { "ParticlesTrailCollision", "粒子轨迹碰撞" },
                { "ParticlesTrailGravity", "粒子轨迹重力" },
                { "ParticlesTrailWind", "粒子轨迹风力" },
                { "ParticlesTrailLife", "粒子轨迹寿命" },
                { "ParticlesTrailSpeed", "粒子轨迹速度" },
                
                // DrawText 相关属性
                { "TextFont", "文本字体" },
                { "TextRenderingMode", "文本渲染模式" },
                { "DrawTextShadow", "绘制文本阴影" },
                { "TextShadowColor", "文本阴影颜色" },
                { "TextShadowOffset", "文本阴影偏移" },
                { "CornerRadius", "圆角半径" },
                { "DrawBorder", "绘制边框" },
                { "DrawBackground", "绘制背景" },
                { "UseGradient", "使用渐变" },
                { "Gradient", "渐变" },
                { "AutoHide", "自动隐藏" },
                { "Placement", "位置" },
                
                // 其他常见属性
                { "Amount", "数量" },
                { "Factor", "因子" },
                { "Level", "级别" },
                { "Strength", "强度" },
                { "AmountX", "X数量" },
                { "AmountY", "Y数量" },
                { "FactorX", "X因子" },
                { "FactorY", "Y因子" },
                { "LevelX", "X级别" },
                { "LevelY", "Y级别" },
                { "StrengthX", "X强度" },
                { "StrengthY", "Y强度" },
                { "Red", "红色" },
                { "Green", "绿色" },
                { "Blue", "蓝色" },
                { "R", "红色" },
                { "G", "绿色" },
                { "B", "蓝色" },
                { "A", "透明度" },
                { "HueShift", "色调偏移" },
                { "SaturationShift", "饱和度偏移" },
                { "BrightnessShift", "亮度偏移" },
                { "ContrastShift", "对比度偏移" },
                { "GammaShift", "伽马偏移" },
                { "RedShift", "红色偏移" },
                { "GreenShift", "绿色偏移" },
                { "BlueShift", "蓝色偏移" },
                { "AlphaShift", "透明度偏移" },
                { "FromColor", "源颜色" },
                { "ToColor", "目标颜色" },
                { "Tolerance", "容差" },
                { "Fade", "淡出" },
                { "Blend", "混合" },
                { "Mode", "模式" },
                { "Type", "类型" },
                { "Style", "样式" },
                { "Quality", "质量" },
                { "Method", "方法" },
                { "Algorithm", "算法" },
                { "Kernel", "内核" },
                { "Matrix", "矩阵" },
                { "Convolution", "卷积" },
                { "Filter", "滤镜" },
                { "Preset", "预设" },
                { "Custom", "自定义" },
                { "Default", "默认" },
                { "None", "无" },
                { "All", "全部" },
                { "Selected", "已选择" },
                { "Unselected", "未选择" },
                { "Enabled", "启用" },
                { "Disabled", "禁用" },
                { "True", "是" },
                { "False", "否" },
                { "Yes", "是" },
                { "No", "否" },
                { "On", "开" },
                { "Off", "关" },
                { "Horizontal", "水平" },
                { "Vertical", "垂直" },
                { "Both", "两者" },
                { "Left", "左" },
                { "Right", "右" },
                { "Top", "上" },
                { "Bottom", "下" },
                { "Center", "中心" },
                { "Middle", "中间" },
                { "Start", "开始" },
                { "End", "结束" },
                { "Begin", "开始" },
                { "Finish", "完成" },
                { "First", "第一个" },
                { "Last", "最后一个" },
                { "Previous", "上一个" },
                { "Next", "下一个" },
                { "Before", "之前" },
                { "After", "之后" },
                { "Inside", "内部" },
                { "Outside", "外部" },
                { "Inner", "内部" },
                { "Outer", "外部" },
                { "InnerRadius", "内半径" },
                { "OuterRadius", "外半径" },
                { "InnerSize", "内大小" },
                { "OuterSize", "外大小" },
                { "InnerWidth", "内宽度" },
                { "OuterWidth", "外宽度" },
                { "InnerHeight", "内高度" },
                { "OuterHeight", "外高度" },
                { "InnerX", "内X坐标" },
                { "OuterX", "外X坐标" },
                { "InnerY", "内Y坐标" },
                { "OuterY", "外Y坐标" },
                { "InnerOffset", "内偏移" },
                { "OuterOffset", "外偏移" },
                { "InnerMargin", "内边距" },
                { "OuterMargin", "外边距" },
                { "InnerPadding", "内填充" },
                { "OuterPadding", "外填充" },
                { "InnerAngle", "内角度" },
                { "OuterAngle", "外角度" },
                { "InnerDistance", "内距离" },
                { "OuterDistance", "外距离" },
                { "InnerOpacity", "内透明度" },
                { "OuterOpacity", "外透明度" },
                { "InnerBlur", "内模糊" },
                { "OuterBlur", "外模糊" },
                { "InnerBrightness", "内亮度" },
                { "OuterBrightness", "外亮度" },
                { "InnerContrast", "内对比度" },
                { "OuterContrast", "外对比度" },
                { "InnerSaturation", "内饱和度" },
                { "OuterSaturation", "外饱和度" },
                { "InnerHue", "内色调" },
                { "OuterHue", "外色调" },
                { "InnerGamma", "内伽马" },
                { "OuterGamma", "外伽马" },
                { "InnerRotation", "内旋转" },
                { "OuterRotation", "外旋转" },
                { "InnerScale", "内缩放" },
                { "OuterScale", "外缩放" },
                { "InnerFlip", "内翻转" },
                { "OuterFlip", "外翻转" },
                { "InnerCrop", "内裁剪" },
                { "OuterCrop", "外裁剪" },
                { "InnerResize", "内调整大小" },
                { "OuterResize", "外调整大小" },
                { "InnerText", "内文本" },
                { "OuterText", "外文本" },
                { "InnerFont", "内字体" },
                { "OuterFont", "外字体" },
                { "InnerBackground", "内背景" },
                { "OuterBackground", "外背景" },
                { "InnerBorder", "内边框" },
                { "OuterBorder", "外边框" },
                { "InnerShadow", "内阴影" },
                { "OuterShadow", "外阴影" },
                { "InnerGlow", "内发光" },
                { "OuterGlow", "外发光" },
                { "InnerOutline", "内轮廓" },
                { "OuterOutline", "外轮廓" },
                { "InnerEdge", "内边缘" },
                { "OuterEdge", "外边缘" },
                { "InnerPixelate", "内像素化" },
                { "OuterPixelate", "外像素化" },
                { "InnerEmboss", "内浮雕" },
                { "OuterEmboss", "外浮雕" },
                { "InnerSharpen", "内锐化" },
                { "OuterSharpen", "外锐化" },
                { "InnerSmooth", "内平滑" },
                { "OuterSmooth", "外平滑" },
                { "InnerInverse", "内反转" },
                { "OuterInverse", "外反转" },
                { "InnerGrayscale", "内灰度" },
                { "OuterGrayscale", "外灰度" },
                { "InnerSepia", "内怀旧" },
                { "OuterSepia", "外怀旧" },
                { "InnerBlackWhite", "内黑白" },
                { "OuterBlackWhite", "外黑白" },
                { "InnerColorize", "内着色" },
                { "OuterColorize", "外着色" },
                { "InnerReplaceColor", "内替换颜色" },
                { "OuterReplaceColor", "外替换颜色" },
                { "InnerSelectiveColor", "内选择性颜色" },
                { "OuterSelectiveColor", "外选择性颜色" },
                { "InnerMatrixColor", "内矩阵颜色" },
                { "OuterMatrixColor", "外矩阵颜色" },
                { "InnerPolaroid", "内宝丽来" },
                { "OuterPolaroid", "外宝丽来" },
                { "InnerAlpha", "内透明度" },
                { "OuterAlpha", "外透明度" },
                { "InnerBlockSize", "内块大小" },
                { "OuterBlockSize", "外块大小" },
                { "InnerGlowRadius", "内发光半径" },
                { "OuterGlowRadius", "外发光半径" },
                { "InnerGlowR", "内发光红色" },
                { "OuterGlowR", "外发光红色" },
                { "InnerGlowG", "内发光绿色" },
                { "OuterGlowG", "外发光绿色" },
                { "InnerGlowB", "内发光蓝色" },
                { "OuterGlowB", "外发光蓝色" },
                { "InnerUseGradient", "内使用渐变" },
                { "OuterUseGradient", "外使用渐变" },
                { "InnerGradientType", "内渐变类型" },
                { "OuterGradientType", "外渐变类型" },
                { "InnerGradientColors", "内渐变颜色" },
                { "OuterGradientColors", "外渐变颜色" },
                { "InnerGradientAngle", "内渐变角度" },
                { "OuterGradientAngle", "外渐变角度" },
                { "InnerCheckerboardSize", "内棋盘大小" },
                { "OuterCheckerboardSize", "外棋盘大小" },
                { "InnerCheckerboardColor1", "内棋盘颜色1" },
                { "OuterCheckerboardColor1", "外棋盘颜色1" },
                { "InnerCheckerboardColor2", "内棋盘颜色2" },
                { "OuterCheckerboardColor2", "外棋盘颜色2" },
                { "InnerImagePath", "内图片路径" },
                { "OuterImagePath", "外图片路径" },
                { "InnerImageSize", "内图片大小" },
                { "OuterImageSize", "外图片大小" },
                { "InnerImagePosition", "内图片位置" },
                { "OuterImagePosition", "外图片位置" },
                { "InnerImageAlignment", "内图片对齐" },
                { "OuterImageAlignment", "外图片对齐" },
                { "InnerTextAlignment", "内文本对齐" },
                { "OuterTextAlignment", "外文本对齐" },
                { "InnerTextPosition", "内文本位置" },
                { "OuterTextPosition", "外文本位置" },
                { "InnerTextColor", "内文本颜色" },
                { "OuterTextColor", "外文本颜色" },
                { "InnerTextShadow", "内文本阴影" },
                { "OuterTextShadow", "外文本阴影" },
                { "InnerTextShadowColor", "内文本阴影颜色" },
                { "OuterTextShadowColor", "外文本阴影颜色" },
                { "InnerTextShadowOffset", "内文本阴影偏移" },
                { "OuterTextShadowOffset", "外文本阴影偏移" },
                { "InnerTextShadowBlur", "内文本阴影模糊" },
                { "OuterTextShadowBlur", "外文本阴影模糊" },
                { "InnerAutoSize", "内自动大小" },
                { "OuterAutoSize", "外自动大小" },
                { "InnerKeepAspectRatio", "内保持宽高比" },
                { "OuterKeepAspectRatio", "外保持宽高比" },
                { "InnerForceProportions", "内强制比例" },
                { "OuterForceProportions", "外强制比例" },
                { "InnerMinSize", "内最小大小" },
                { "OuterMinSize", "外最小大小" },
                { "InnerMaxSize", "内最大大小" },
                { "OuterMaxSize", "外最大大小" },
                { "InnerTopMost", "内置顶" },
                { "OuterTopMost", "外置顶" },
                { "InnerKeepCenterLocation", "内保持中心位置" },
                { "OuterKeepCenterLocation", "外保持中心位置" },
                { "InnerPlacement", "内位置" },
                { "OuterPlacement", "外位置" },
                { "InnerPlacementOffset", "内位置偏移" },
                { "OuterPlacementOffset", "外位置偏移" },
                { "InnerBorderSize", "内边框大小" },
                { "OuterBorderSize", "外边框大小" },
                { "InnerBorderColor", "内边框颜色" },
                { "OuterBorderColor", "外边框颜色" },
                { "InnerMinimizeSize", "内最小化大小" },
                { "OuterMinimizeSize", "外最小化大小" },
                { "InnerSmartPadding", "内智能内边距" },
                { "OuterSmartPadding", "外智能内边距" },
                { "InnerRoundedCorner", "内圆角" },
                { "OuterRoundedCorner", "外圆角" },
                { "InnerShadowRadius", "内阴影半径" },
                { "OuterShadowRadius", "外阴影半径" },
                { "InnerShadowAngle", "内阴影角度" },
                { "OuterShadowAngle", "外阴影角度" },
                { "InnerShadowDistance", "内阴影距离" },
                { "OuterShadowDistance", "外阴影距离" },
                { "InnerShadowOpacity", "内阴影透明度" },
                { "OuterShadowOpacity", "外阴影透明度" },
                { "InnerBackgroundImageFilePath", "内背景图片路径" },
                { "OuterBackgroundImageFilePath", "外背景图片路径" },
                { "InnerBackgroundType", "内背景类型" },
                { "OuterBackgroundType", "外背景类型" },
                { "InnerBackgroundColor", "内背景颜色" },
                { "OuterBackgroundColor", "外背景颜色" },
                { "InnerBackgroundGradient", "内背景渐变" },
                { "OuterBackgroundGradient", "外背景渐变" },
                { "InnerBackgroundGradientColors", "内背景渐变颜色" },
                { "OuterBackgroundGradientColors", "外背景渐变颜色" },
                { "InnerBackgroundGradientAngle", "内背景渐变角度" },
                { "OuterBackgroundGradientAngle", "外背景渐变角度" },
                { "InnerBackgroundImage", "内背景图片" },
                { "OuterBackgroundImage", "外背景图片" },
                { "InnerBackgroundImageAlignment", "内背景图片对齐" },
                { "OuterBackgroundImageAlignment", "外背景图片对齐" },
                { "InnerBackgroundImageSize", "内背景图片大小" },
                { "OuterBackgroundImageSize", "外背景图片大小" },
                { "InnerBackgroundImageOpacity", "内背景图片透明度" },
                { "OuterBackgroundImageOpacity", "外背景图片透明度" },
                { "InnerParticlesCount", "内粒子数量" },
                { "OuterParticlesCount", "外粒子数量" },
                { "InnerParticlesColor", "内粒子颜色" },
                { "OuterParticlesColor", "外粒子颜色" },
                { "InnerParticlesSize", "内粒子大小" },
                { "OuterParticlesSize", "外粒子大小" },
                { "InnerParticlesSpeed", "内粒子速度" },
                { "OuterParticlesSpeed", "外粒子速度" },
                { "InnerParticlesLife", "内粒子寿命" },
                { "OuterParticlesLife", "外粒子寿命" },
                { "InnerParticlesGravity", "内粒子重力" },
                { "OuterParticlesGravity", "外粒子重力" },
                { "InnerParticlesWind", "内粒子风力" },
                { "OuterParticlesWind", "外粒子风力" },
                { "InnerParticlesFade", "内粒子淡出" },
                { "OuterParticlesFade", "外粒子淡出" },
                { "InnerParticlesBlend", "内粒子混合" },
                { "OuterParticlesBlend", "外粒子混合" },
                { "InnerParticlesRandom", "内粒子随机" },
                { "OuterParticlesRandom", "外粒子随机" },
                { "InnerParticlesDirection", "内粒子方向" },
                { "OuterParticlesDirection", "外粒子方向" },
                { "InnerParticlesSpread", "内粒子扩散" },
                { "OuterParticlesSpread", "外粒子扩散" },
                { "InnerParticlesBounce", "内粒子弹跳" },
                { "OuterParticlesBounce", "外粒子弹跳" },
                { "InnerParticlesCollision", "内粒子碰撞" },
                { "OuterParticlesCollision", "外粒子碰撞" },
                { "InnerParticlesTrail", "内粒子轨迹" },
                { "OuterParticlesTrail", "外粒子轨迹" },
                { "InnerParticlesTrailLength", "内粒子轨迹长度" },
                { "OuterParticlesTrailLength", "外粒子轨迹长度" },
                { "InnerParticlesTrailFade", "内粒子轨迹淡出" },
                { "OuterParticlesTrailFade", "外粒子轨迹淡出" },
                { "InnerParticlesTrailColor", "内粒子轨迹颜色" },
                { "OuterParticlesTrailColor", "外粒子轨迹颜色" },
                { "InnerParticlesTrailSize", "内粒子轨迹大小" },
                { "OuterParticlesTrailSize", "外粒子轨迹大小" },
                { "InnerParticlesTrailOpacity", "内粒子轨迹透明度" },
                { "OuterParticlesTrailOpacity", "外粒子轨迹透明度" },
                { "InnerParticlesTrailBlend", "内粒子轨迹混合" },
                { "OuterParticlesTrailBlend", "外粒子轨迹混合" },
                { "InnerParticlesTrailRandom", "内粒子轨迹随机" },
                { "OuterParticlesTrailRandom", "外粒子轨迹随机" },
                { "InnerParticlesTrailDirection", "内粒子轨迹方向" },
                { "OuterParticlesTrailDirection", "外粒子轨迹方向" },
                { "InnerParticlesTrailSpread", "内粒子轨迹扩散" },
                { "OuterParticlesTrailSpread", "外粒子轨迹扩散" },
                { "InnerParticlesTrailBounce", "内粒子轨迹弹跳" },
                { "OuterParticlesTrailBounce", "外粒子轨迹弹跳" },
                { "InnerParticlesTrailCollision", "内粒子轨迹碰撞" },
                { "OuterParticlesTrailCollision", "外粒子轨迹碰撞" },
                { "InnerParticlesTrailGravity", "内粒子轨迹重力" },
                { "OuterParticlesTrailGravity", "外粒子轨迹重力" },
                { "InnerParticlesTrailWind", "内粒子轨迹风力" },
                { "OuterParticlesTrailWind", "外粒子轨迹风力" },
                { "InnerParticlesTrailLife", "内粒子轨迹寿命" },
                { "OuterParticlesTrailLife", "外粒子轨迹寿命" },
                { "InnerParticlesTrailSpeed", "内粒子轨迹速度" },
                { "OuterParticlesTrailSpeed", "外粒子轨迹速度" }
            };
        }
        
        // 翻译按钮的 ToolTip
        private void TranslateButtonToolTip(Control parent, string buttonName, string chineseToolTip)
        {
            try
            {
                Control button = GetControl<Control>(parent, buttonName);
                if (button != null)
                {
                    // 获取 ToolTip 控件
                    FieldInfo ttMainField = parent.GetType().GetField("ttMain", 
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (ttMainField != null)
                    {
                        ToolTip ttMain = ttMainField.GetValue(parent) as ToolTip;
                        if (ttMain != null)
                        {
                            ttMain.SetToolTip(button, chineseToolTip);
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
        }
        
        // 显示保存图片对话框，防止覆盖原文件
        private string ShowSaveImageDialogWithProtection(Image image, string originalFilePath)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "PNG 图片|*.png|JPEG 图片|*.jpg|BMP 图片|*.bmp|所有文件|*.*";
                saveFileDialog.Title = "保存处理后的图片";
                saveFileDialog.DefaultExt = "png";
                
                // 设置初始文件名（如果原文件存在，添加后缀避免覆盖）
                if (!string.IsNullOrEmpty(originalFilePath) && File.Exists(originalFilePath))
                {
                    string directory = Path.GetDirectoryName(originalFilePath);
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalFilePath);
                    string extension = Path.GetExtension(originalFilePath);
                    
                    // 生成新文件名（添加 "_特效" 后缀）
                    string newFileName = fileNameWithoutExt + "_特效" + extension;
                    string initialPath = Path.Combine(directory, newFileName);
                    
                    // 如果文件已存在，添加数字后缀
                    int counter = 1;
                    while (File.Exists(initialPath))
                    {
                        newFileName = fileNameWithoutExt + "_特效_" + counter + extension;
                        initialPath = Path.Combine(directory, newFileName);
                        counter++;
                    }
                    
                    saveFileDialog.FileName = newFileName;
                    saveFileDialog.InitialDirectory = directory;
                }
                else
                {
                    saveFileDialog.FileName = "特效图片.png";
                }
                
                // 显示保存对话框
                while (true)
                {
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string selectedPath = saveFileDialog.FileName;
                        
                        // 检查是否与原始文件路径相同
                        if (!string.IsNullOrEmpty(originalFilePath) && 
                            Path.GetFullPath(selectedPath).Equals(Path.GetFullPath(originalFilePath), StringComparison.OrdinalIgnoreCase))
                        {
                            // 提示用户不能覆盖原文件
                            ShowNotification("不能覆盖原始文件，请重新命名保存", "提示", 3000, MessageBoxIcon.Warning);
                            continue; // 重新显示对话框
                        }
                        
                        // 保存图片
                        try
                        {
                            string extension = Path.GetExtension(selectedPath).ToLower();
                            
                            if (extension == ".png")
                            {
                                SavePngWithNoCompression((Bitmap)image, selectedPath);
                            }
                            else if (extension == ".jpg" || extension == ".jpeg")
                            {
                                SaveJpegWithQuality((Bitmap)image, selectedPath, 100L);
                            }
                            else
                            {
                                image.Save(selectedPath);
                            }
                            
                            return selectedPath;
                        }
                        catch (Exception ex)
                        {
                            ShowNotification($"保存图片失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                            return null;
                        }
                    }
                    else
                    {
                        return null; // 用户取消
                    }
                }
            }
        }
        
        // 创建默认预设预览图
        private Image CreateDefaultPresetPreview(string presetName)
        {
            Bitmap bmp = new Bitmap(120, 120);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.LightGray);
                using (Font font = new Font("Microsoft YaHei UI", 9F))
                {
                    // 绘制预设名称（如果太长则截断）
                    string displayName = presetName;
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = "预设";
                    }
                    else if (displayName.Length > 10)
                    {
                        displayName = displayName.Substring(0, 10) + "...";
                    }
                    
                    SizeF textSize = g.MeasureString(displayName, font);
                    PointF textPos = new PointF(
                        (bmp.Width - textSize.Width) / 2,
                        (bmp.Height - textSize.Height) / 2
                    );
                    
                    g.DrawString(displayName, font, Brushes.Black, textPos);
                }
            }
            return bmp;
        }
        
        // 打开图像美化功能
        private void OpenImageBeautifier()
        {
            try
            {
                // 使用 ShareX 的图像美化功能
                // 首先让用户选择一张图片
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.gif|所有文件|*.*";
                    openFileDialog.Title = "选择要美化的图片";
                    
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string filePath = openFileDialog.FileName;
                        
                        // 检查文件是否存在
                        if (!File.Exists(filePath))
                        {
                            ShowNotification("文件不存在或已被删除", "错误", 3000, MessageBoxIcon.Error);
                            return;
                        }
                        
                        // 验证文件是否为有效的图片格式
                        try
                        {
                            using (Image testImage = Image.FromFile(filePath))
                            {
                                // 文件有效，继续处理
                            }
                        }
                        catch (Exception imgEx)
                        {
                            ShowNotification($"无法打开图片文件：{imgEx.Message}", "错误", 3000, MessageBoxIcon.Error);
                            return;
                        }
                        
                        // 直接创建 ImageBeautifierOptions，避免依赖 TaskSettings（可能未初始化）
                        try
                        {
                            // 创建默认的 ImageBeautifierOptions
                            ImageBeautifierOptions options = new ImageBeautifierOptions();
                            
                            // 创建 ImageBeautifierForm
                            ImageBeautifierForm imageBeautifierForm = new ImageBeautifierForm(filePath, options);
                            
                            // 在窗口显示后修改界面
                            imageBeautifierForm.Shown += (sender, e) => {
                                CustomizeImageBeautifierForm(imageBeautifierForm);
                            };
                            
                            // 显示窗口
                            imageBeautifierForm.Show();
                        }
                        catch (Exception formEx)
                        {
                            ShowNotification($"创建图像美化窗口失败：{formEx.Message}", "错误", 3000, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"打开图像美化失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
        }
        
        // 自定义 Image Beautifier 界面
        private void CustomizeImageBeautifierForm(ImageBeautifierForm form)
        {
            try
            {
                // 使用反射获取按钮控件
                Type formType = typeof(ImageBeautifierForm);
                
                // 隐藏不需要的按钮：复制、另存为、上传、打印
                HideButton(form, "btnCopy");
                HideButton(form, "btnSaveAs");
                HideButton(form, "btnUpload");
                HideButton(form, "btnPrint");
                
                // 设置保存按钮的快捷键 Ctrl+S，并修改保存后自动关闭
                CustomizeSaveButton(form);
                
                // 修改重置选项按钮，移除确认对话框，直接重置
                CustomizeResetOptionsButton(form);
                
                // 设置中文字体和界面
                ApplyChineseFontToForm(form);
                
                // 设置窗口标题为中文
                form.Text = "图像美化";
                
                // 延迟翻译，确保所有控件都已加载完成
                form.Load += (sender, e) => {
                    // 在 Load 事件中翻译，此时控件已初始化但可能还未完全加载
                    System.Windows.Forms.Timer translateTimer = new System.Windows.Forms.Timer();
                    translateTimer.Interval = 100; // 延迟 100ms 确保控件完全加载
                    translateTimer.Tick += (s, args) => {
                        translateTimer.Stop();
                        translateTimer.Dispose();
                        TranslateImageBeautifierForm(form);
                    };
                    translateTimer.Start();
                };
                
                // 如果窗口已经加载，立即翻译
                if (form.IsHandleCreated)
                {
                    System.Windows.Forms.Timer translateTimer = new System.Windows.Forms.Timer();
                    translateTimer.Interval = 100;
                    translateTimer.Tick += (s, args) => {
                        translateTimer.Stop();
                        translateTimer.Dispose();
                        TranslateImageBeautifierForm(form);
                    };
                    translateTimer.Start();
                }
            }
            catch (Exception ex)
            {
                // 如果修改失败，不影响功能使用
                LogError("自定义 Image Beautifier 界面失败", ex);
            }
        }
        
        // 翻译 Image Beautifier 界面文本为中文
        private void TranslateImageBeautifierForm(Form form)
        {
            try
            {
                // 翻译标签文本
                TranslateControl(form, "lblMargin", "边距：");
                TranslateControl(form, "lblPadding", "内边距：");
                TranslateControl(form, "cbSmartPadding", "智能内边距");
                TranslateControl(form, "lblRoundedCorner", "圆角：");
                TranslateControl(form, "lblShadowRadius", "半径：");
                TranslateControl(form, "lblBackground", "背景：");
                TranslateControl(form, "lblShadowAngle", "角度：");
                TranslateControl(form, "lblShadowDistance", "距离：");
                TranslateControl(form, "lblShadowOpacity", "透明度：");
                TranslateControl(form, "lblBackgroundImageFilePath", "背景图片路径");
                
                // 翻译按钮文本（保存按钮保持原样，只添加悬停提示）
                // 为保存按钮添加 ToolTip
                Button btnSave = GetControl<Button>(form, "btnSave");
                if (btnSave != null)
                {
                    // 通过反射获取 ToolTip
                    try
                    {
                        FieldInfo ttField = form.GetType().GetField("ttMain", 
                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                        if (ttField != null)
                        {
                            ToolTip toolTip = ttField.GetValue(form) as ToolTip;
                            if (toolTip != null)
                            {
                                toolTip.SetToolTip(btnSave, "保存");
                            }
                            else
                            {
                                // 如果 ToolTip 不存在，创建一个新的
                                toolTip = new ToolTip();
                                toolTip.SetToolTip(btnSave, "保存");
                            }
                        }
                    }
                    catch
                    {
                        // 如果获取失败，忽略错误
                    }
                }
                
                TranslateControl(form, "btnCopy", "复制");
                TranslateControl(form, "btnSaveAs", "另存为");
                TranslateControl(form, "btnUpload", "上传");
                TranslateControl(form, "btnPrint", "打印");
                TranslateControl(form, "btnResetOptions", "重置选项");
                TranslateControl(form, "btnBackgroundImageFilePathBrowse", "浏览");
                TranslateControl(form, "btnShadowColor", "阴影颜色");
                
                // 翻译 GroupBox 文本
                TranslateControl(form, "gbShadow", "阴影");
                
                // 翻译 ComboBox 选项（背景类型）
                ComboBox cbBackgroundType = GetControl<ComboBox>(form, "cbBackgroundType");
                if (cbBackgroundType != null)
                {
                    try
                    {
                        // 背景类型选项翻译映射（包括 desktop、gradient 等）
                        // 根据枚举定义：Gradient, Color, Image, Desktop, Transparent
                        Dictionary<string, string> backgroundTypeMap = new Dictionary<string, string>
                        {
                            { "Gradient", "渐变" },
                            { "Color", "颜色" },
                            { "Image", "图片" },
                            { "Desktop", "桌面" },
                            { "Transparent", "透明" },
                            { "None", "无" },
                            // 小写版本
                            { "gradient", "渐变" },
                            { "color", "颜色" },
                            { "image", "图片" },
                            { "desktop", "桌面" },
                            { "transparent", "透明" },
                            { "none", "无" }
                        };
                        
                        // 如果 Items 已填充，尝试翻译
                        if (cbBackgroundType.Items.Count > 0)
                        {
                            int selectedIndex = cbBackgroundType.SelectedIndex;
                            List<string> chineseItems = new List<string>();
                            
                            // 遍历现有 Items，尝试翻译
                            foreach (object item in cbBackgroundType.Items)
                            {
                                string itemText = item.ToString();
                                string translatedText = itemText;
                                
                                // 尝试在字典中查找翻译（精确匹配）
                                if (backgroundTypeMap.ContainsKey(itemText))
                                {
                                    translatedText = backgroundTypeMap[itemText];
                                }
                                else
                                {
                                    // 尝试不区分大小写的匹配
                                    foreach (var kvp in backgroundTypeMap)
                                    {
                                        if (itemText.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase))
                                        {
                                            translatedText = kvp.Value;
                                            break;
                                        }
                                    }
                                    
                                    // 如果仍然找不到，尝试部分匹配
                                    if (translatedText == itemText)
                                    {
                                        foreach (var kvp in backgroundTypeMap)
                                        {
                                            if (itemText.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                                            {
                                                translatedText = kvp.Value;
                                                break;
                                            }
                                        }
                                    }
                                }
                                
                                chineseItems.Add(translatedText);
                            }
                            
                            // 如果翻译成功，替换 Items
                            if (chineseItems.Count == cbBackgroundType.Items.Count)
                            {
                                cbBackgroundType.Items.Clear();
                                foreach (string item in chineseItems)
                                {
                                    cbBackgroundType.Items.Add(item);
                                }
                                if (selectedIndex >= 0 && selectedIndex < chineseItems.Count)
                                {
                                    cbBackgroundType.SelectedIndex = selectedIndex;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 如果翻译失败，忽略错误
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("翻译 Image Beautifier 界面失败", ex);
            }
        }
        
        // 翻译控件的文本
        private void TranslateControl(Control parent, string controlName, string chineseText)
        {
            try
            {
                Control control = GetControl<Control>(parent, controlName);
                if (control != null)
                {
                    if (control is Label || control is Button || control is CheckBox || control is GroupBox)
                    {
                        control.Text = chineseText;
                    }
                }
            }
            catch
            {
                // 忽略单个控件的错误
            }
        }
        
        // 隐藏按钮的辅助方法
        private void HideButton(Control parent, string buttonName)
        {
            try
            {
                Button button = GetControl<Button>(parent, buttonName);
                if (button != null)
                {
                    button.Visible = false;
                    button.Enabled = false;
                }
            }
            catch
            {
                // 忽略错误
            }
        }
        
        // 获取控件的辅助方法（使用反射）
        private T GetControl<T>(Control parent, string controlName) where T : Control
        {
            try
            {
                FieldInfo field = parent.GetType().GetField(controlName, 
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                
                if (field != null)
                {
                    object control = field.GetValue(parent);
                    if (control is T)
                    {
                        return (T)control;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
            
            return null;
        }
        
        // 自定义保存按钮，保存后自动关闭窗口
        private void CustomizeSaveButton(Form form)
        {
            try
            {
                Button btnSave = GetControl<Button>(form, "btnSave");
                if (btnSave != null)
                {
                    // 移除原有的事件处理程序
                    try
                    {
                        MethodInfo originalMethod = form.GetType().GetMethod("btnSave_Click", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (originalMethod != null)
                        {
                            EventHandler originalHandler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, originalMethod);
                            btnSave.Click -= originalHandler;
                        }
                    }
                    catch
                    {
                        // 如果移除失败，继续执行
                    }
                    
                    // 添加新的事件处理程序，保存后自动关闭
                    btnSave.Click += (sender, e) => {
                        try
                        {
                            // 获取 PreviewImage 和 FilePath
                            PropertyInfo previewImageProperty = form.GetType().GetProperty("PreviewImage", 
                                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            PropertyInfo filePathProperty = form.GetType().GetProperty("FilePath", 
                                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            
                            object previewImage = null;
                            string filePath = null;
                            
                            if (previewImageProperty != null)
                            {
                                previewImage = previewImageProperty.GetValue(form);
                            }
                            
                            if (filePathProperty != null)
                            {
                                filePath = filePathProperty.GetValue(form) as string;
                            }
                            
                            // 执行保存操作
                            if (previewImage != null && previewImage is Bitmap && !string.IsNullOrEmpty(filePath))
                            {
                                ImageHelpers.SaveImage((Bitmap)previewImage, filePath);
                                
                                // 保存成功后，关闭窗口
                                form.Close();
                            }
                        }
                        catch
                        {
                            // 如果保存失败，忽略错误（不关闭窗口）
                        }
                    };
                    
                    // 设置快捷键 Ctrl+S
                    form.KeyPreview = true;
                    form.KeyDown += (sender, e) => {
                        if (e.Control && e.KeyCode == Keys.S)
                        {
                            btnSave.PerformClick();
                            e.Handled = true;
                        }
                    };
                }
            }
            catch
            {
                // 如果修改失败，忽略错误
            }
        }
        
        // 自定义重置选项按钮，移除确认对话框
        private void CustomizeResetOptionsButton(Form form)
        {
            try
            {
                Button btnResetOptions = GetControl<Button>(form, "btnResetOptions");
                if (btnResetOptions != null)
                {
                    // 移除原有的事件处理程序
                    try
                    {
                        // 通过反射获取原有的事件处理程序并移除
                        MethodInfo originalMethod = form.GetType().GetMethod("btnResetOptions_Click", 
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        if (originalMethod != null)
                        {
                            EventHandler originalHandler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), form, originalMethod);
                            btnResetOptions.Click -= originalHandler;
                        }
                    }
                    catch
                    {
                        // 如果移除失败，继续执行（可能事件处理程序还未绑定）
                    }
                    
                    // 添加新的事件处理程序，直接重置不显示确认对话框
                    btnResetOptions.Click += async (sender, e) => {
                        try
                        {
                            // 直接重置选项，不显示确认对话框
                            PropertyInfo optionsProperty = form.GetType().GetProperty("Options", 
                                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                            object options = null;
                            
                            if (optionsProperty != null)
                            {
                                options = optionsProperty.GetValue(form);
                            }
                            else
                            {
                                FieldInfo optionsField = form.GetType().GetField("Options", 
                                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                                if (optionsField != null)
                                {
                                    options = optionsField.GetValue(form);
                                }
                            }
                            
                            if (options != null)
                            {
                                // 调用 ResetOptions 方法
                                MethodInfo resetMethod = options.GetType().GetMethod("ResetOptions");
                                if (resetMethod != null)
                                {
                                    resetMethod.Invoke(options, null);
                                    
                                    // 调用 LoadOptions 方法
                                    MethodInfo loadOptionsMethod = form.GetType().GetMethod("LoadOptions", 
                                        BindingFlags.NonPublic | BindingFlags.Instance);
                                    if (loadOptionsMethod != null)
                                    {
                                        loadOptionsMethod.Invoke(form, null);
                                        
                                        // 调用 UpdatePreview 方法
                                        MethodInfo updatePreviewMethod = form.GetType().GetMethod("UpdatePreview", 
                                            BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (updatePreviewMethod != null)
                                        {
                                            object task = updatePreviewMethod.Invoke(form, new object[] { false });
                                            if (task is Task)
                                            {
                                                await (Task)task;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // 如果反射失败，忽略错误
                        }
                    };
                }
            }
            catch
            {
                // 如果修改失败，忽略错误
            }
        }
        
        // 应用中文字体到表单及其控件
        private void ApplyChineseFontToForm(Form form)
        {
            try
            {
                // 使用系统常用中文字体，避免笔画重影
                Font chineseFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
                
                // 应用到表单
                form.Font = chineseFont;
                
                // 递归应用到所有控件
                ApplyFontToControls(form.Controls, chineseFont);
            }
            catch
            {
                // 如果字体设置失败，使用默认字体
            }
        }
        
        // 递归应用字体到控件集合
        private void ApplyFontToControls(Control.ControlCollection controls, Font font)
        {
            foreach (Control control in controls)
            {
                try
                {
                    // 设置控件字体
                    control.Font = font;
                    
                    // 递归处理子控件
                    if (control.HasChildren)
                    {
                        ApplyFontToControls(control.Controls, font);
                    }
                }
                catch
                {
                    // 忽略单个控件的错误，继续处理其他控件
                }
            }
        }
        
        // 恢复录制设置窗口显示状态（统一方法，避免重复代码）
        private void RestoreRecordSettingsForm()
        {
            if (recordSettingsForm != null && !recordSettingsForm.IsDisposed)
            {
                recordSettingsForm.Opacity = 1.0;
                recordSettingsForm.Visible = true;
                recordSettingsForm.Show();
                recordSettingsForm.BringToFront();
            }
        }
        
        // 隐藏录制设置窗口（统一方法，避免重复代码）
        private void HideRecordSettingsForm()
        {
            if (recordSettingsForm != null && !recordSettingsForm.IsDisposed)
            {
                recordSettingsForm.Hide();
                recordSettingsForm.Visible = false;
                recordSettingsForm.Opacity = 0;
                recordSettingsForm.Update();
            }
        }
        
        // 复制 FFmpegOptions 到 ScreenRecordingOptions（统一方法，避免重复代码）
        private void CopyFFmpegOptions(FFmpegOptions source, FFmpegOptions target, bool includeVideo = true, bool includeAudio = true)
        {
            target.OverrideCLIPath = source.OverrideCLIPath;
            target.CLIPath = source.CLIPath;
            
            if (includeVideo)
            {
                target.VideoSource = string.IsNullOrEmpty(source.VideoSource) ? FFmpegCaptureDevice.GDIGrab.Value : source.VideoSource;
                target.VideoCodec = source.VideoCodec;
                target.x264_Preset = source.x264_Preset;
                target.x264_CRF = source.x264_CRF;
                target.x264_Use_Bitrate = source.x264_Use_Bitrate;
                target.x264_Bitrate = source.x264_Bitrate;
                target.VPx_Bitrate = source.VPx_Bitrate;
            }
            else
            {
                target.VideoSource = "";
            }
            
            if (includeAudio)
            {
                target.AudioSource = string.IsNullOrEmpty(source.AudioSource) ? "" : source.AudioSource;
                target.AudioCodec = source.AudioCodec;
                target.AAC_Bitrate = source.AAC_Bitrate;
                target.Opus_Bitrate = source.Opus_Bitrate;
                target.Vorbis_QScale = source.Vorbis_QScale;
                target.MP3_QScale = source.MP3_QScale;
            }
            else
            {
                target.AudioSource = "";
            }
        }
        
        // 验证 FFmpegOptions 配置（统一方法，避免重复代码）
        private void ValidateFFmpegOptions(FFmpegOptions options, bool requireVideo, bool requireAudio)
        {
            if (requireVideo && requireAudio)
            {
                // 视频和音频至少需要一个
                if (!options.IsVideoSourceSelected && !options.IsAudioSourceSelected)
                {
                    throw new Exception("必须至少选择一个视频源或音频源");
                }
            }
            else if (requireAudio)
            {
                // 仅音频：必须选择音频源
                if (!options.IsAudioSourceSelected || 
                    string.IsNullOrEmpty(options.AudioSource) ||
                    options.AudioSource == FFmpegCaptureDevice.None.Value)
                {
                    throw new Exception("必须选择一个音频源才能进行音频录制。\n\n请点击\"选项\"按钮，在音频源下拉菜单中选择一个音频输入设备。");
                }
            }
            
            // 检查 FFmpeg 是否存在
            if (!System.IO.File.Exists(options.FFmpegPath))
            {
                throw new Exception($"FFmpeg 未找到。路径: {options.FFmpegPath}\n请确保 FFmpeg 已正确安装。");
            }
        }
        
        // 调整通知位置（通过反射访问 NotificationForm 实例）
        private void AdjustNotificationPosition(int offsetX, int offsetY)
        {
            try
            {
                // 使用反射获取 NotificationForm 的静态 instance 字段
                Type notificationFormType = typeof(NotificationForm);
                FieldInfo instanceField = notificationFormType.GetField("instance", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                
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
        
        // 显示通知（ShareX 风格，右下角渐变动画）
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
                    Offset = 10, // 距离边缘的偏移量
                    Size = new Size(300, 80), // 通知窗口大小
                    Title = title,
                    Text = text,
                    BackgroundColor = backgroundColor,
                    BorderColor = borderColor,
                    TextColor = Color.FromArgb(210, 210, 210), // 文本颜色
                    TitleColor = Color.FromArgb(240, 240, 240) // 标题颜色
                };
                
                // 显示通知（在主线程中）
                if (this.InvokeRequired)
                {
                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                    {
                        NotificationForm.Show(config);
                        // 向左移动5像素（通过反射获取实例并调整位置）
                        AdjustNotificationPosition(-5, 0);
                    });
                }
                else
                {
                    NotificationForm.Show(config);
                    // 向左移动5像素
                    AdjustNotificationPosition(-5, 0);
                }
            }
            catch
            {
                // 如果通知显示失败，回退到 MessageBox（仅作为最后手段）
                MessageBox.Show(text, title, MessageBoxButtons.OK, icon);
            }
        }
        
        // 滚动截图并保存（使用 ShareX 的滚动截图功能，不打开 ShareX 窗口）
        private async void CaptureScrollingAndSave()
        {
            bool wasVisible = HideSidebarForCapture();
            
            try
            {
                // 创建滚动截图选项
                ScrollingCaptureOptions options = new ScrollingCaptureOptions();
                options.AutoUpload = false; // 不自动上传
                options.ShowRegion = false; // 不显示区域选择窗口
                
                // 使用反射访问 internal 的 ScrollingCaptureManager
                Assembly screenCaptureLib = Assembly.GetAssembly(typeof(ScrollingCaptureOptions));
                Type managerType = screenCaptureLib.GetType("ShareX.ScreenCaptureLib.ScrollingCaptureManager");
                
                if (managerType != null)
                {
                    // 创建 ScrollingCaptureManager 实例
                    object manager = Activator.CreateInstance(managerType, options);
                    
                    if (manager != null)
                    {
                        // 获取 SelectWindow 方法
                        MethodInfo selectWindowMethod = managerType.GetMethod("SelectWindow", BindingFlags.Public | BindingFlags.Instance);
                        
                        if (selectWindowMethod != null)
                        {
                            // 选择窗口（使用区域选择界面选择要截图的窗口）
                            bool windowSelected = (bool)selectWindowMethod.Invoke(manager, null);
                            
                            if (windowSelected)
                            {
                                // 获取 StartCapture 方法
                                MethodInfo startCaptureMethod = managerType.GetMethod("StartCapture", BindingFlags.Public | BindingFlags.Instance);
                                
                                if (startCaptureMethod != null)
                                {
                                    // 启动滚动截图（异步）
                                    // StartCapture 返回 Task<ScrollingCaptureStatus>
                                    dynamic captureTask = startCaptureMethod.Invoke(manager, null);
                                    await captureTask;
                                    
                                    // 获取返回值（ScrollingCaptureStatus）
                                    object status = captureTask.Result;
                                    
                                    // 获取 Result 属性
                                    PropertyInfo resultProperty = managerType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                                    
                                    if (resultProperty != null)
                                    {
                                        Bitmap result = resultProperty.GetValue(manager) as Bitmap;
                                        
                                        // 检查状态和结果
                                        if (result != null)
                                        {
                                            // 使用 Windows 保存对话框保存图片
                                            Bitmap resultClone = (Bitmap)result.Clone();
                                            ShowSaveDialogAndSave(resultClone, $"滚动截图_{DateTime.Now:yyyyMMdd_HHmmss}");
                                            resultClone.Dispose();
                                        }
                                    }
                                }
                            }
                        }
                        
                        // 释放资源
                        IDisposable disposable = manager as IDisposable;
                        disposable?.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"滚动截图失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
            finally
            {
                RestoreSidebarAfterCapture(wasVisible);
            }
        }
        
        // 显示录制设置窗口
        private void ShowRecordSettings()
        {
            if (recordSettingsForm != null && !recordSettingsForm.IsDisposed)
            {
                // 确保窗口状态正确
                recordSettingsForm.Opacity = 1.0;
                recordSettingsForm.Visible = true;
                recordSettingsForm.Show();
                recordSettingsForm.BringToFront();
                return;
            }
            
            // 初始化 FFmpegOptions（如果还没有初始化）
            if (ffmpegOptions == null)
            {
                RecordSettings settings = RecordSettings.Load();
                ffmpegOptions = new FFmpegOptions();
                
                // 加载保存的设置
                ffmpegOptions.OverrideCLIPath = settings.FFmpegOverrideCLIPath;
                ffmpegOptions.CLIPath = settings.FFmpegCLIPath;
                ffmpegOptions.VideoSource = string.IsNullOrEmpty(settings.VideoSource) ? FFmpegCaptureDevice.GDIGrab.Value : settings.VideoSource;
                ffmpegOptions.AudioSource = settings.AudioSource ?? FFmpegCaptureDevice.None.Value;
                ffmpegOptions.VideoCodec = (FFmpegVideoCodec)settings.VideoCodec;
                ffmpegOptions.AudioCodec = (FFmpegAudioCodec)settings.AudioCodec;
                ffmpegOptions.x264_Preset = (FFmpegPreset)settings.x264_Preset;
                ffmpegOptions.x264_CRF = settings.x264_CRF;
                ffmpegOptions.x264_Use_Bitrate = settings.x264_Use_Bitrate;
                ffmpegOptions.x264_Bitrate = settings.x264_Bitrate;
                ffmpegOptions.VPx_Bitrate = settings.VPx_Bitrate;
                ffmpegOptions.AAC_Bitrate = settings.AAC_Bitrate;
                ffmpegOptions.Opus_Bitrate = settings.Opus_Bitrate;
                ffmpegOptions.Vorbis_QScale = settings.Vorbis_QScale;
                ffmpegOptions.MP3_QScale = settings.MP3_QScale;
                
                // 如果未设置路径，尝试自动检测 FFmpeg 路径（程序目录内）
                if (string.IsNullOrEmpty(ffmpegOptions.CLIPath) || !System.IO.File.Exists(ffmpegOptions.CLIPath))
                {
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string defaultFFmpegPath = Path.Combine(appDir, "ffmpeg-8.0.1-essentials_build", "bin", "ffmpeg.exe");
                    if (System.IO.File.Exists(defaultFFmpegPath))
                    {
                        ffmpegOptions.OverrideCLIPath = true;
                        ffmpegOptions.CLIPath = defaultFFmpegPath;
                        
                        // 保存自动检测的路径
                        settings.FFmpegOverrideCLIPath = true;
                        settings.FFmpegCLIPath = defaultFFmpegPath;
                        settings.Save();
                    }
                }
            }
            
            recordSettingsForm = new RecordSettingsForm();
            recordSettingsForm.RecordButtonClicked += RecordSettingsForm_RecordButtonClicked;
            recordSettingsForm.Show();
        }
        
        // 录制按钮点击事件
        private async void RecordSettingsForm_RecordButtonClicked(RecordType recordType)
        {
            currentRecordType = recordType;
            
            // 从设置窗口获取参数
            bool captureCursor = true; // 默认值
            if (recordSettingsForm != null)
            {
                gifFPS = recordSettingsForm.GIF_FPS;
                ffmpegOptions = recordSettingsForm.FFmpegOptions;
                captureCursor = recordSettingsForm.CaptureCursor; // 从设置窗口获取鼠标指针设置
            }
            
            // 如果设置窗口没有提供，从保存的设置中获取
            if (recordSettingsForm == null)
            {
                RecordSettings settings = RecordSettings.Load();
                captureCursor = settings.CaptureCursor;
            }
            
            // 隐藏侧边栏和设置窗口（设置窗口已经在按钮点击时隐藏了）
            bool wasVisible = HideSidebarForCapture();
            
            // 确保设置窗口完全隐藏（双重保险）
            if (recordSettingsForm != null && !recordSettingsForm.IsDisposed)
            {
                recordSettingsForm.Hide();
                recordSettingsForm.Visible = false;
                recordSettingsForm.Opacity = 0; // 设置为完全透明
                recordSettingsForm.Update(); // 立即更新窗口
            }
            
            // 确保窗口立即更新
            Application.DoEvents();
            
            try
            {
                // 音频录制不需要选区，直接开始录制
                if (recordType == RecordType.Audio)
                {
                    // 检查音频源是否已选择
                    if (ffmpegOptions == null || string.IsNullOrEmpty(ffmpegOptions.AudioSource) || 
                        ffmpegOptions.AudioSource == FFmpegCaptureDevice.None.Value)
                    {
                        // 恢复侧边栏和设置窗口
                        RestoreSidebarAfterCapture(wasVisible);
                        RestoreRecordSettingsForm();
                        
                        ShowNotification(
                            "音频录制需要选择一个音频源。\n\n请点击\"选项\"按钮，在音频源下拉菜单中选择一个音频输入设备。",
                            "需要选择音频源",
                            4000);
                        return;
                    }
                    
                    // 音频录制不需要视频区域，使用空矩形
                    Rectangle captureRect = Rectangle.Empty;
                    await StartRecording(recordType, captureRect);
                }
                else
                {
                    // GIF 和视频录制需要选择录制区域
                    Rectangle captureRect;
                    if (RegionCaptureTasks.GetRectangleRegion(out captureRect))
                    {
                        // 开始录制
                        await StartRecording(recordType, captureRect);
                    }
                    else
                    {
                        // 用户取消了选区，恢复窗口状态
                        RestoreSidebarAfterCapture(wasVisible);
                        RestoreRecordSettingsForm();
                    }
                }
            }
            catch (Exception ex)
            {
                // 恢复侧边栏和设置窗口
                RestoreSidebarAfterCapture(wasVisible);
                RestoreRecordSettingsForm();
                
                ShowNotification($"录制失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
            finally
            {
                    // 如果录制没有开始，确保恢复窗口状态
                    if (!isRecording)
                    {
                        RestoreSidebarAfterCapture(wasVisible);
                        RestoreRecordSettingsForm();
                    }
            }
        }
        
        // 开始录制
        private async Task StartRecording(RecordType recordType, Rectangle captureRect)
        {
            isRecording = true;
            escKeyTimer.Start(); // 开始监听 Ctrl+ESC 键
            
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "SidebarRecord");
                Directory.CreateDirectory(tempDir);
                
                // 从设置中获取鼠标指针设置（仅用于视频录制）
                RecordSettings recordSettings = RecordSettings.Load();
                bool captureCursor = recordSettings.CaptureCursor;
                
                Screenshot screenshot = new Screenshot();
                ScreenRecordingOptions options = new ScreenRecordingOptions();
                options.CaptureArea = captureRect;
                options.Duration = 0; // 无限制
                
                if (recordType == RecordType.GIF)
                {
                    // GIF 录制（不录制鼠标指针）
                    screenshot.CaptureCursor = false; // GIF 录制默认不录制鼠标指针
                    options.DrawCursor = false;
                    options.FPS = gifFPS;
                    options.OutputPath = Path.Combine(tempDir, "temp_gif");
                    currentRecorder = new ScreenRecorder(ScreenRecordOutput.GIF, options, screenshot, captureRect);
                    tempRecordPath = Path.Combine(tempDir, "temp.gif");
                }
                else if (recordType == RecordType.Video)
                {
                    // 视频录制（使用用户设置的鼠标指针选项）
                    screenshot.CaptureCursor = captureCursor;
                    options.DrawCursor = captureCursor;
                    options.FPS = 30;
                    
                    // 创建 FFmpegOptions 的副本，避免修改原始对象
                    options.FFmpeg = new FFmpegOptions();
                    CopyFFmpegOptions(ffmpegOptions, options.FFmpeg, includeVideo: true, includeAudio: true);
                    
                    // 验证和检查 FFmpeg
                    ValidateFFmpegOptions(options.FFmpeg, requireVideo: true, requireAudio: false);
                    
                    // H.264 编码器要求宽度和高度必须是偶数，调整捕获区域
                    if (options.FFmpeg.IsEvenSizeRequired)
                    {
                        captureRect = CaptureHelpers.EvenRectangleSize(captureRect);
                        options.CaptureArea = captureRect;
                    }
                    
                    // 设置输出路径（FFmpeg 会自动添加扩展名）
                    string outputBasePath = Path.Combine(tempDir, "temp_video");
                    options.OutputPath = outputBasePath;
                    options.IsRecording = true; // 标记为录制模式
                    
                    // FFmpeg 实际输出文件路径 = OutputPath + Extension
                    tempRecordPath = Path.ChangeExtension(outputBasePath, options.FFmpeg.Extension);
                    
                    currentRecorder = new ScreenRecorder(ScreenRecordOutput.FFmpeg, options, screenshot, captureRect);
                }
                else if (recordType == RecordType.Audio)
                {
                    // 音频录制
                    options.FPS = 30;
                    
                    // 创建 FFmpegOptions 的副本（仅音频）
                    options.FFmpeg = new FFmpegOptions();
                    CopyFFmpegOptions(ffmpegOptions, options.FFmpeg, includeVideo: false, includeAudio: true);
                    
                    // 验证和检查 FFmpeg（仅音频）
                    ValidateFFmpegOptions(options.FFmpeg, requireVideo: false, requireAudio: true);
                    
                    // 设置输出路径（FFmpeg 会自动添加扩展名）
                    string outputBasePath = Path.Combine(tempDir, "temp_audio");
                    options.OutputPath = outputBasePath;
                    options.IsRecording = true; // 标记为录制模式
                    
                    // FFmpeg 实际输出文件路径 = OutputPath + Extension
                    tempRecordPath = Path.ChangeExtension(outputBasePath, options.FFmpeg.Extension);
                    
                    currentRecorder = new ScreenRecorder(ScreenRecordOutput.FFmpeg, options, screenshot, captureRect);
                }
                
                // 在后台线程中启动录制
                await Task.Run(() =>
                {
                    currentRecorder.StartRecording();
                });
            }
            catch (Exception ex)
            {
                ShowNotification($"录制启动失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                isRecording = false;
                escKeyTimer.Stop();
            }
        }
        
        // Ctrl+ESC 键监听（用于结束录制）
        private void EscKeyTimer_Tick(object sender, EventArgs e)
        {
            if (isRecording)
            {
                // 检查 Ctrl+ESC 组合键
                // VK_CONTROL = 0x11, VK_ESCAPE = 0x1B
                bool ctrlPressed = (GetAsyncKeyState(0x11) & 0x8000) != 0; // Ctrl 键
                bool escPressed = (GetAsyncKeyState(0x1B) & 0x8000) != 0;   // ESC 键
                
                if (ctrlPressed && escPressed)
                {
                    StopRecording();
                }
            }
        }
        
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        
        // 停止录制
        private async void StopRecording()
        {
            if (!isRecording || currentRecorder == null) return;
            
            escKeyTimer.Stop();
            isRecording = false;
            
            // 显示加载动画
            string loadingMessage = "正在处理录制文件...";
            if (currentRecordType == RecordType.GIF)
            {
                loadingMessage = "正在编码 GIF 文件...";
            }
            else if (currentRecordType == RecordType.Video)
            {
                loadingMessage = "正在处理视频文件...";
            }
            else if (currentRecordType == RecordType.Audio)
            {
                loadingMessage = "正在处理音频文件...";
            }
            
            // 在主线程中显示加载窗口
            if (this.InvokeRequired)
            {
                this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                {
                    ShowLoadingForm(loadingMessage);
                });
            }
            else
            {
                ShowLoadingForm(loadingMessage);
            }
            
            try
            {
                // 停止录制
                currentRecorder.StopRecording();
                
                // 等待录制完成
                // 对于 FFmpeg 录制，需要等待 FFmpeg 进程完全结束
                await Task.Run(() =>
                {
                    int maxWaitTime = 10000; // 最多等待 10 秒
                    int waited = 0;
                    while (currentRecorder.IsRecording && waited < maxWaitTime)
                    {
                        System.Threading.Thread.Sleep(100);
                        waited += 100;
                    }
                    
                    // 额外等待一下，确保文件写入完成
                    System.Threading.Thread.Sleep(500);
                });
                
                // 如果是 GIF，需要保存
                if (currentRecordType == RecordType.GIF)
                {
                    // GIF 需要从缓存保存
                    string tempGifPath = Path.Combine(Path.GetTempPath(), "SidebarRecord", "temp.gif");
                    Directory.CreateDirectory(Path.GetDirectoryName(tempGifPath));
                    currentRecorder.SaveAsGIF(tempGifPath, GIFQuality.Default);
                    
                    // 等待 GIF 文件写入完成
                    int gifRetryCount = 0;
                    while (!File.Exists(tempGifPath) && gifRetryCount < 50)
                    {
                        await Task.Delay(200);
                        gifRetryCount++;
                    }
                    
                    tempRecordPath = tempGifPath;
                }
                
                // 等待文件写入完成（对于 FFmpeg 录制）
                if (currentRecordType == RecordType.Video || currentRecordType == RecordType.Audio)
                {
                    // 检查文件是否存在，如果不存在则等待
                    int retryCount = 0;
                    while (!File.Exists(tempRecordPath) && retryCount < 50)
                    {
                        await Task.Delay(200);
                        retryCount++;
                    }
                }
                
                // 隐藏加载动画并显示保存对话框（在主线程中）
                if (File.Exists(tempRecordPath))
                {
                    // 在主线程中显示对话框
                    if (this.InvokeRequired)
                    {
                        this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                        {
                            HideLoadingForm();
                            ShowSaveDialogForRecord(tempRecordPath);
                        });
                    }
                    else
                    {
                        HideLoadingForm();
                        ShowSaveDialogForRecord(tempRecordPath);
                    }
                }
                else
                {
                    // 文件不存在，隐藏加载动画并显示错误消息
                    string errorMsg = $"录制文件未找到：{tempRecordPath}\n可能录制过程中出现错误。";
                    if (this.InvokeRequired)
                    {
                        this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                        {
                            HideLoadingForm();
                            ShowNotification(errorMsg, "错误", 3000, MessageBoxIcon.Warning);
                        });
                    }
                    else
                    {
                        HideLoadingForm();
                        ShowNotification(errorMsg, "错误", 3000, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"停止录制失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
            }
            finally
            {
                currentRecorder?.Dispose();
                currentRecorder = null;
            }
        }
        
        // 显示加载动画窗口
        private void ShowLoadingForm(string message)
        {
            if (loadingForm != null && !loadingForm.IsDisposed)
            {
                loadingForm.Close();
            }
            
            loadingForm = new LoadingForm(message);
            loadingForm.Show();
            Application.DoEvents(); // 确保窗口立即显示
        }
        
        // 隐藏加载动画窗口
        private void HideLoadingForm()
        {
            if (loadingForm != null && !loadingForm.IsDisposed)
            {
                loadingForm.Close();
                loadingForm = null;
            }
        }
        
        // 显示录制文件的保存对话框
        private void ShowSaveDialogForRecord(string tempFilePath)
        {
            // 首先检查文件是否存在
            if (!File.Exists(tempFilePath))
            {
                ShowNotification($"录制文件未找到：{tempFilePath}\n可能录制过程中出现错误。", "错误", 3000, MessageBoxIcon.Warning);
                // 恢复录制设置窗口
                if (recordSettingsForm != null && !recordSettingsForm.IsDisposed)
                {
                    recordSettingsForm.Opacity = 1.0;
                    recordSettingsForm.Show();
                    recordSettingsForm.BringToFront();
                }
                return;
            }
            
            string extension = Path.GetExtension(tempFilePath).TrimStart('.');
            string filter = extension.ToUpper() + " 文件|*." + extension + "|所有文件|*.*";
            
            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = filter;
                saveDialog.DefaultExt = extension;
                saveDialog.FileName = $"录制_{DateTime.Now:yyyyMMdd_HHmmss}";
                
                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // 再次检查源文件是否存在（可能在对话框显示期间被删除）
                        if (!File.Exists(tempFilePath))
                        {
                            ShowNotification($"录制文件未找到：{tempFilePath}\n可能录制过程中出现错误。", "错误", 3000, MessageBoxIcon.Warning);
                            return;
                        }
                        
                        File.Copy(tempFilePath, saveDialog.FileName, true);
                        ShowNotification($"录制已保存到：\n{saveDialog.FileName}", "保存成功");
                        
                        // 仅在成功保存后清理临时文件
                        try
                        {
                            if (File.Exists(tempFilePath))
                            {
                                File.Delete(tempFilePath);
                            }
                            string tempDir = Path.GetDirectoryName(tempFilePath);
                            if (Directory.Exists(tempDir))
                            {
                                // 检查目录是否为空，如果为空则删除
                                if (Directory.GetFiles(tempDir).Length == 0 && Directory.GetDirectories(tempDir).Length == 0)
                                {
                                    Directory.Delete(tempDir, true);
                                }
                            }
                        }
                        catch (Exception cleanupEx)
                        {
                            // 清理失败不影响保存成功
                            LogError("清理临时文件失败", cleanupEx);
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowNotification($"保存失败：{ex.Message}", "错误", 3000, MessageBoxIcon.Error);
                    }
                }
            }
            
            // 保存完成后，恢复录制设置窗口
            RestoreRecordSettingsForm();
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 清理工具提示
                SafeDisposeTooltip();
                
                // 清理定时器（先停止再释放）
                tooltipTimer?.Stop();
                tooltipTimer?.Dispose();
                animationTimer?.Stop();
                animationTimer?.Dispose();
                iconScaleTimer?.Stop();
                iconScaleTimer?.Dispose();
                autoHideTimer?.Stop();
                autoHideTimer?.Dispose();
                collapseAnimationTimer?.Stop();
                collapseAnimationTimer?.Dispose();
                escKeyTimer?.Stop();
                escKeyTimer?.Dispose();
                
                // 清理录制相关资源
                currentRecorder?.Dispose();
                
                // 清理表单
                recordSettingsForm?.Dispose();
                hotkeySettingsForm?.Dispose();
                loadingForm?.Dispose();
                
                // 注销所有快捷键
                UnregisterAllHotkeys();
                globalHotkeyForm?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
    
    public enum DockSide
    {
        Left,
        Right
    }
    
    public class SidebarButton
    {
        public string Name { get; set; }
        public string Icon { get; set; }  // Emoji 字符
        public string IconPath { get; set; }  // PNG 图片路径（可选）
        public Action OnClick { get; set; }
    }
    
    // 工具提示窗口（ShareX 风格）
    public class TooltipForm : Form
    {
        private Label lblText;
        private string tooltipText;
        
        public TooltipForm(string text)
        {
            this.tooltipText = text;
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            
            // 应用 ShareX 主题
            ShareXResources.ApplyTheme(this, false);
            
            // 获取主题颜色
            Color backgroundColor = ShareXResources.Theme?.BackgroundColor ?? Color.FromArgb(40, 40, 40);
            Color textColor = ShareXResources.Theme?.TextColor ?? Color.White;
            
            // 创建字体
            Font font = new Font("Microsoft YaHei UI", 9F);
            int padding = 16; // 左右各8像素
            int maxWidth = 400; // 最大宽度（像素）
            
            // 创建临时 Graphics 对象来计算文本大小
            SizeF textSize;
            using (Bitmap tempBitmap = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(tempBitmap))
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                StringFormat format = new StringFormat(StringFormatFlags.NoClip);
                format.Trimming = StringTrimming.None;
                format.FormatFlags |= StringFormatFlags.LineLimit;
                
                // 计算文本大小（使用最大宽度，自动换行）
                textSize = g.MeasureString(tooltipText, font, maxWidth - padding, format);
            }
            
            // 设置窗口大小（确保能显示完整文本）
            int windowWidth = Math.Min((int)Math.Ceiling(textSize.Width) + padding, maxWidth);
            int windowHeight = (int)Math.Ceiling(textSize.Height) + 12;
            
            this.Size = new Size(windowWidth, windowHeight);
            
            // 创建标签
            lblText = new Label
            {
                Text = tooltipText,
                AutoSize = false,
                ForeColor = textColor,
                BackColor = backgroundColor,
                Padding = new Padding(8, 6, 8, 6),
                Font = font,
                TextAlign = ContentAlignment.TopLeft, // 改为顶部对齐，确保多行文本正确显示
                Dock = DockStyle.Fill,
                UseCompatibleTextRendering = true // 使用兼容的文本渲染
            };
            
            this.Controls.Add(lblText);
        }
        
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            // 绘制边框
            Color borderColor = ShareXResources.Theme?.BorderColor ?? Color.FromArgb(100, 100, 100);
            using (Pen borderPen = new Pen(borderColor, 1))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
    
    // 自定义 TypeDescriptionProvider 用于翻译属性名称
    public class TranslatedTypeDescriptionProvider : TypeDescriptionProvider
    {
        private TypeDescriptionProvider baseProvider;
        private Dictionary<string, string> propertyNameTranslator;
        
        public TranslatedTypeDescriptionProvider(TypeDescriptionProvider baseProvider, Dictionary<string, string> translator)
            : base(baseProvider)
        {
            this.baseProvider = baseProvider;
            this.propertyNameTranslator = translator;
        }
        
        public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
        {
            ICustomTypeDescriptor baseDescriptor = baseProvider.GetTypeDescriptor(objectType, instance);
            return new TranslatedTypeDescriptor(baseDescriptor, propertyNameTranslator);
        }
    }
    
    // 自定义 TypeDescriptor 用于翻译属性名称
    public class TranslatedTypeDescriptor : CustomTypeDescriptor
    {
        private Dictionary<string, string> propertyNameTranslator;
        
        public TranslatedTypeDescriptor(ICustomTypeDescriptor parent, Dictionary<string, string> translator)
            : base(parent)
        {
            this.propertyNameTranslator = translator;
        }
        
        public override PropertyDescriptorCollection GetProperties()
        {
            return GetProperties(new Attribute[] { });
        }
        
        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            PropertyDescriptorCollection baseProperties = base.GetProperties(attributes);
            List<PropertyDescriptor> translatedProperties = new List<PropertyDescriptor>();
            
            foreach (PropertyDescriptor prop in baseProperties)
            {
                string translatedName = prop.Name;
                if (propertyNameTranslator != null && propertyNameTranslator.TryGetValue(prop.Name, out string chineseName))
                {
                    translatedName = chineseName;
                }
                
                // 创建翻译后的 PropertyDescriptor
                TranslatedPropertyDescriptor translatedProp = new TranslatedPropertyDescriptor(prop, translatedName);
                translatedProperties.Add(translatedProp);
            }
            
            return new PropertyDescriptorCollection(translatedProperties.ToArray());
        }
    }
    
    // 自定义 PropertyDescriptor 用于显示翻译后的属性名称
    public class TranslatedPropertyDescriptor : PropertyDescriptor
    {
        private PropertyDescriptor baseDescriptor;
        private string displayName;
        
        public TranslatedPropertyDescriptor(PropertyDescriptor baseDescriptor, string displayName)
            : base(baseDescriptor)
        {
            this.baseDescriptor = baseDescriptor;
            this.displayName = displayName;
        }
        
        public override string DisplayName
        {
            get { return displayName; }
        }
        
        public override string Name
        {
            get { return baseDescriptor.Name; }
        }
        
        public override Type ComponentType
        {
            get { return baseDescriptor.ComponentType; }
        }
        
        public override bool IsReadOnly
        {
            get { return baseDescriptor.IsReadOnly; }
        }
        
        public override Type PropertyType
        {
            get { return baseDescriptor.PropertyType; }
        }
        
        public override bool CanResetValue(object component)
        {
            return baseDescriptor.CanResetValue(component);
        }
        
        public override object GetValue(object component)
        {
            return baseDescriptor.GetValue(component);
        }
        
        public override void ResetValue(object component)
        {
            baseDescriptor.ResetValue(component);
        }
        
        public override void SetValue(object component, object value)
        {
            baseDescriptor.SetValue(component, value);
        }
        
        public override bool ShouldSerializeValue(object component)
        {
            return baseDescriptor.ShouldSerializeValue(component);
        }
        
        public override AttributeCollection Attributes
        {
            get { return baseDescriptor.Attributes; }
        }
        
        public override string Description
        {
            get { return baseDescriptor.Description; }
        }
        
        public override string Category
        {
            get { return baseDescriptor.Category; }
        }
    }
}

