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
    Email: 1780555120@qq.com
    
    This code is part of the Sidebar application.
    All rights reserved.
*/

#endregion License Information (GPL v3)

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Sidebar
{
    public partial class LoadingForm : Form
    {
        #region Windows API Declarations
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int ULW_ALPHA = 0x2;
        
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
            
            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }
        
        #endregion
        
        private Timer rotationTimer;
        private float rotationAngle = 0f;
        private const float ROTATION_SPEED = 5f; // 每次旋转角度
        private const int SPINNER_SIZE = 40;
        private const int SPINNER_THICKNESS = 4;
        private Bitmap layeredBitmap;
        
        public LoadingForm(string message = "正在处理录制文件...")
        {
            InitializeComponent(message);
        }
        
        private void InitializeComponent(string message)
        {
            this.SuspendLayout();
            
            // 窗体属性
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(220, 130); // 增大窗口尺寸以容纳更大的文字
            this.ShowInTaskbar = false;
            this.TopMost = true;
            
            // 设置窗口样式为支持分层窗口
            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
            
            // 创建旋转动画定时器
            rotationTimer = new Timer();
            rotationTimer.Interval = 16; // 约60fps
            rotationTimer.Tick += RotationTimer_Tick;
            rotationTimer.Start();
            
            // 保存消息文本
            this.message = message;
            
            // 创建分层位图
            CreateLayeredBitmap();
            
            this.ResumeLayout(false);
        }
        
        private string message = "正在处理录制文件...";
        
        private void CreateLayeredBitmap()
        {
            if (layeredBitmap != null)
            {
                layeredBitmap.Dispose();
            }
            
            layeredBitmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb);
            
            using (Graphics g = Graphics.FromImage(layeredBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.Half; // 使用 Half 模式避免重影
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit; // 使用 ClearType 获得更好的文字渲染
                
                // 绘制透明背景（完全不绘制，保持透明）
                
                // 计算旋转中心
                int centerX = this.Width / 2;
                int centerY = 35;
                
                // 绘制旋转的加载指示器
                using (Pen spinnerPen = new Pen(Color.FromArgb(255, 255, 255), SPINNER_THICKNESS))
                {
                    spinnerPen.StartCap = LineCap.Round;
                    spinnerPen.EndCap = LineCap.Round;
                    
                    // 创建旋转矩阵
                    Matrix rotationMatrix = new Matrix();
                    rotationMatrix.RotateAt(rotationAngle, new PointF(centerX, centerY));
                    g.Transform = rotationMatrix;
                    
                    // 绘制圆弧（270度，留出缺口）
                    RectangleF arcRect = new RectangleF(
                        centerX - SPINNER_SIZE / 2,
                        centerY - SPINNER_SIZE / 2,
                        SPINNER_SIZE,
                        SPINNER_SIZE);
                    
                    g.DrawArc(spinnerPen, arcRect, 0, 270);
                    
                    g.ResetTransform();
                }
                
                // 绘制文字（使用高质量渲染，避免重影）
                using (SolidBrush textBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    
                    // 使用更大的中文字体（Microsoft YaHei UI 是中文友好字体）
                    using (Font textFont = new Font("Microsoft YaHei UI", 13F, FontStyle.Regular, GraphicsUnit.Pixel))
                    {
                        RectangleF textRect = new RectangleF(10, 72, 200, 50);
                        g.DrawString(message, textFont, textBrush, textRect, sf);
                    }
                }
            }
            
            UpdateLayeredWindowBitmap();
        }
        
        private void UpdateLayeredWindowBitmap()
        {
            if (layeredBitmap == null || this.Handle == IntPtr.Zero) return;
            
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOldBitmap = IntPtr.Zero;
            
            try
            {
                hBitmap = layeredBitmap.GetHbitmap(Color.FromArgb(0));
                hOldBitmap = SelectObject(memDc, hBitmap);
                
                POINT pointSource = new POINT(0, 0);
                POINT topPos = new POINT(this.Left, this.Top);
                SIZE size = new SIZE { cx = this.Width, cy = this.Height };
                
                BLENDFUNCTION blend = new BLENDFUNCTION
                {
                    BlendOp = 0,
                    BlendFlags = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = 1
                };
                
                UpdateLayeredWindow(this.Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
                if (hOldBitmap != IntPtr.Zero) SelectObject(memDc, hOldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (memDc != IntPtr.Zero) DeleteDC(memDc);
            }
        }
        
        private void RotationTimer_Tick(object sender, EventArgs e)
        {
            rotationAngle += ROTATION_SPEED;
            if (rotationAngle >= 360f)
            {
                rotationAngle -= 360f;
            }
            CreateLayeredBitmap(); // 重新创建位图以更新动画
        }
        
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // 设置窗口样式为支持分层窗口
            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
            CreateLayeredBitmap();
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            // 不使用标准绘制，使用 UpdateLayeredWindow
            // 但保留此方法以避免默认绘制
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (rotationTimer != null)
            {
                rotationTimer.Stop();
                rotationTimer.Dispose();
            }
            base.OnFormClosing(e);
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (rotationTimer != null)
                {
                    rotationTimer.Stop();
                    rotationTimer.Dispose();
                }
                if (layeredBitmap != null)
                {
                    layeredBitmap.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}

