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
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ShareX.ScreenCaptureLib;
using ShareX.HelpersLib;

namespace Sidebar
{
    public enum RecordType
    {
        GIF,
        Video,
        Audio
    }

    public partial class RecordSettingsForm : Form
    {
        private ComboBox cmbRecordType;
        private Button btnRecord;
        private Button btnOptions;
        private RecordType currentRecordType;
        
        // 录制按钮图标相关
        private Image normalRecordIcon = null; // 正常大小图标
        private Image hoverRecordIcon = null; // 悬停时放大图标
        
        // GIF 设置
        private int gifFPS = 10;
        
        // 视频/音频设置（使用 ShareX 的 FFmpegOptions）
        private FFmpegOptions ffmpegOptions = new FFmpegOptions();
        private bool captureCursor = true; // 默认勾选
        
        private RecordSettings settings;
        
        public RecordType SelectedRecordType => currentRecordType;
        public int GIF_FPS => gifFPS;
        public FFmpegOptions FFmpegOptions => ffmpegOptions;
        public bool CaptureCursor => captureCursor;
        
        public event Action<RecordType> RecordButtonClicked;
        
        public RecordSettingsForm()
        {
            // 加载保存的设置
            settings = RecordSettings.Load();
            currentRecordType = settings.LastUsedRecordType;
            gifFPS = settings.GIF_FPS;
            captureCursor = settings.CaptureCursor; // 加载鼠标指针设置
            
            // 加载 FFmpeg 路径设置
            ffmpegOptions.OverrideCLIPath = settings.FFmpegOverrideCLIPath;
            ffmpegOptions.CLIPath = settings.FFmpegCLIPath;
            
            // 如果未设置路径或路径无效，尝试自动检测
            if (string.IsNullOrEmpty(ffmpegOptions.CLIPath) || !File.Exists(ffmpegOptions.CLIPath))
            {
                string detectedPath = DetectFFmpegPath();
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    ffmpegOptions.OverrideCLIPath = true;
                    ffmpegOptions.CLIPath = detectedPath;
                    
                    // 保存检测到的路径
                    settings.FFmpegOverrideCLIPath = true;
                    settings.FFmpegCLIPath = detectedPath;
                    settings.Save();
                }
            }
            
            // 加载 FFmpeg 视频/音频源和编码器设置
            ffmpegOptions.VideoSource = settings.VideoSource;
            ffmpegOptions.AudioSource = settings.AudioSource;
            ffmpegOptions.VideoCodec = (FFmpegVideoCodec)settings.VideoCodec;
            ffmpegOptions.AudioCodec = (FFmpegAudioCodec)settings.AudioCodec;
            
            // 加载编码器参数
            ffmpegOptions.x264_Preset = (FFmpegPreset)settings.x264_Preset;
            ffmpegOptions.x264_CRF = settings.x264_CRF;
            ffmpegOptions.x264_Use_Bitrate = settings.x264_Use_Bitrate;
            ffmpegOptions.x264_Bitrate = settings.x264_Bitrate;
            ffmpegOptions.VPx_Bitrate = settings.VPx_Bitrate;
            ffmpegOptions.AAC_Bitrate = settings.AAC_Bitrate;
            ffmpegOptions.Opus_Bitrate = settings.Opus_Bitrate;
            ffmpegOptions.Vorbis_QScale = settings.Vorbis_QScale;
            ffmpegOptions.MP3_QScale = settings.MP3_QScale;
            
            InitializeComponent();
            
            // 应用样式（样式与逻辑分离）
            StyleManager.ApplyThemeToForm(this, true);
        }
        
        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // 窗体属性
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "录制设置";
            this.Size = new Size(320, 100); // 增加30像素高度，确保所有按钮完整显示
            this.ShowInTaskbar = false;
            this.TopMost = true;
            
            int yPos = 15; // 统一的垂直位置
            int leftMargin = 10; // 左边距
            int rightMargin = 10; // 右边距
            
            // 下拉菜单：录制类型
            int cmbWidth = 120;
            cmbRecordType = new ComboBox();
            cmbRecordType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbRecordType.Items.AddRange(new object[] { "GIF录制", "视频录制", "音频录制" });
            cmbRecordType.SelectedIndex = (int)currentRecordType; // 使用保存的录制类型
            cmbRecordType.Location = new Point(leftMargin, yPos);
            cmbRecordType.Size = new Size(cmbWidth, 23);
            cmbRecordType.SelectedIndexChanged += CmbRecordType_SelectedIndexChanged;
            this.Controls.Add(cmbRecordType);
            
            // 选项按钮
            int btnOptionsWidth = 70;
            int recordButtonSize = 40; // 圆形按钮大小
            
            // 计算等间距：可用宽度 = 窗口宽度 - 左边距 - 右边距 - 三个控件宽度
            int availableWidth = this.Width - leftMargin - rightMargin - cmbWidth - btnOptionsWidth - recordButtonSize;
            int spacing = availableWidth / 3; // 三个间距，每个间距相等
            
            int optionsX = cmbRecordType.Right + spacing;
            btnOptions = new Button();
            btnOptions.Text = "选项";
            btnOptions.Location = new Point(optionsX, yPos);
            btnOptions.Size = new Size(btnOptionsWidth, 25);
            btnOptions.Click += BtnOptions_Click;
            this.Controls.Add(btnOptions);
            
            // 录制按钮（使用 PNG 图片）- 等间距
            int recordX = btnOptions.Right + spacing;
            btnRecord = new Button();
            btnRecord.Size = new Size(recordButtonSize, recordButtonSize);
            
            // 加载 PNG 图片（尝试多个可能的路径）
            bool imageLoaded = false;
            string[] possiblePaths = new string[]
            {
                Path.Combine(Application.StartupPath, "icons", "rec.png"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", "rec.png"),
                Path.Combine(Application.StartupPath, "..", "..", "..", "icons", "rec.png"), // 开发环境
                "icons/rec.png" // 相对路径
            };
            
            foreach (string iconPath in possiblePaths)
            {
                if (File.Exists(iconPath))
                {
                    try
                    {
                        Image originalImage = Image.FromFile(iconPath);
                        
                        // 先设置按钮基本属性
                        btnRecord.FlatStyle = FlatStyle.Flat;
                        btnRecord.FlatAppearance.BorderSize = 0;
                        btnRecord.UseVisualStyleBackColor = false;
                        btnRecord.Text = ""; // 不使用文本，使用图片
                        
                        // 创建正常大小图标（按钮大小的 70%）
                        int normalImageSize = (int)(recordButtonSize * 0.7f);
                        normalRecordIcon = new Bitmap(originalImage, normalImageSize, normalImageSize);
                        
                        // 创建悬停时放大图标（按钮大小的 85%，稍微放大）
                        int hoverImageSize = (int)(recordButtonSize * 0.85f);
                        hoverRecordIcon = new Bitmap(originalImage, hoverImageSize, hoverImageSize);
                        
                        originalImage.Dispose(); // 释放原始图片
                        
                        // 设置初始图片（正常大小）
                        btnRecord.Image = normalRecordIcon;
                        btnRecord.ImageAlign = ContentAlignment.MiddleCenter;
                        
                        // 设置透明背景（使用窗口的背景色）
                        // 注意：Button 控件不支持真正的透明，只能使用与父控件相同的背景色
                        Color windowBackColor = ShareXResources.Theme.BackgroundColor;
                        btnRecord.BackColor = windowBackColor;
                        btnRecord.FlatAppearance.MouseOverBackColor = windowBackColor; // 悬停时也保持窗口背景色
                        btnRecord.FlatAppearance.MouseDownBackColor = windowBackColor; // 按下时也保持窗口背景色
                        
                        imageLoaded = true;
                        System.Diagnostics.Debug.WriteLine($"成功加载录制按钮图片: {iconPath}, 背景色: {windowBackColor}");
                        break; // 找到图片后退出循环
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"加载录制按钮图片失败 ({iconPath}): {ex.Message}");
                    }
                }
            }
            
            if (!imageLoaded)
            {
                System.Diagnostics.Debug.WriteLine($"未找到录制按钮图片，尝试的路径: {string.Join(", ", possiblePaths)}");
                // 如果图片未加载，使用 Emoji 样式
                StyleManager.ConfigureSpecialButton(
                    btnRecord, 
                    StyleManager.ThemeColors.RecordButtonRed, 
                    StyleManager.ThemeFonts.RecordButtonEmoji, 
                    transparentBackground: true
                );
                btnRecord.Text = "🔘";
            }
            else
            {
                // 图片加载成功，确保背景色正确（在控件添加到父容器后再次设置）
                // 这将在控件添加到父容器后通过事件处理
            }
            
            // 计算垂直位置，使与选项按钮水平中心对齐
            int optionsCenterY = btnOptions.Top + btnOptions.Height / 2;
            int recordTop = optionsCenterY - recordButtonSize / 2;
            btnRecord.Location = new Point(recordX, recordTop);
            
            btnRecord.Cursor = Cursors.Hand;
            btnRecord.Click += BtnRecord_Click;
            
            // 如果图片已加载，添加鼠标悬停事件处理
            if (imageLoaded)
            {
                btnRecord.MouseEnter += BtnRecord_MouseEnter;
                btnRecord.MouseLeave += BtnRecord_MouseLeave;
            }
            
            this.Controls.Add(btnRecord);
            
            // 在控件添加到父容器后，确保所有状态都使用窗口背景色
            if (imageLoaded)
            {
                // 使用窗口的实际背景色（此时窗口已完全初始化）
                Color windowBackColor = this.BackColor;
                btnRecord.BackColor = windowBackColor;
                btnRecord.FlatAppearance.MouseOverBackColor = windowBackColor; // 悬停时也保持窗口背景色
                btnRecord.FlatAppearance.MouseDownBackColor = windowBackColor; // 按下时也保持窗口背景色
                System.Diagnostics.Debug.WriteLine($"按钮添加到父容器后，设置背景色: {windowBackColor}, R:{windowBackColor.R}, G:{windowBackColor.G}, B:{windowBackColor.B}");
            }
            
            this.ResumeLayout(false);
        }
        
        private void CmbRecordType_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentRecordType = (RecordType)cmbRecordType.SelectedIndex;
            
            // 保存最后使用的录制类型
            if (settings != null)
            {
                settings.LastUsedRecordType = currentRecordType;
                settings.Save();
            }
        }
        
        
        // 录制按钮鼠标进入事件（图标放大）
        private void BtnRecord_MouseEnter(object sender, EventArgs e)
        {
            if (hoverRecordIcon != null)
            {
                btnRecord.Image = hoverRecordIcon;
            }
            
            // 确保悬停时背景色保持为窗口背景色（透明效果）
            Color windowBackColor = this.BackColor;
            btnRecord.BackColor = windowBackColor;
            btnRecord.FlatAppearance.MouseOverBackColor = windowBackColor;
        }
        
        // 录制按钮鼠标离开事件（图标恢复）
        private void BtnRecord_MouseLeave(object sender, EventArgs e)
        {
            if (normalRecordIcon != null)
            {
                btnRecord.Image = normalRecordIcon;
            }
            
            // 确保离开时背景色保持为窗口背景色
            Color windowBackColor = this.BackColor;
            btnRecord.BackColor = windowBackColor;
        }
        
        private void BtnRecord_Click(object sender, EventArgs e)
        {
            // 保存最后使用的录制类型
            if (settings != null)
            {
                settings.LastUsedRecordType = currentRecordType;
                settings.Save();
            }
            
            // 立即隐藏窗口，避免半透明残留影响选区
            this.Hide();
            this.Visible = false;
            this.Opacity = 0; // 设置为完全透明
            this.Update(); // 立即更新窗口
            Application.DoEvents(); // 确保窗口立即更新
            
            // 触发录制事件
            RecordButtonClicked?.Invoke(currentRecordType);
        }
        
        private void BtnOptions_Click(object sender, EventArgs e)
        {
            ShowOptionsDialog();
        }
        
        private void ShowOptionsDialog()
        {
            using (RecordOptionsForm optionsForm = new RecordOptionsForm(currentRecordType, gifFPS, ffmpegOptions))
            {
                if (optionsForm.ShowDialog() == DialogResult.OK)
                {
                    if (currentRecordType == RecordType.GIF)
                    {
                        gifFPS = optionsForm.GIF_FPS;
                        captureCursor = optionsForm.CaptureCursor; // 更新鼠标指针设置
                        if (settings != null)
                        {
                            settings.GIF_FPS = gifFPS;
                            settings.CaptureCursor = captureCursor;
                            settings.Save();
                        }
                    }
                    else
                    {
                        ffmpegOptions = optionsForm.FFmpegOptions;
                        captureCursor = optionsForm.CaptureCursor; // 更新鼠标指针设置
                        
                        // 保存 FFmpeg 路径设置
                        if (settings != null)
                        {
                            settings.FFmpegOverrideCLIPath = ffmpegOptions.OverrideCLIPath;
                            settings.FFmpegCLIPath = ffmpegOptions.CLIPath;
                            settings.CaptureCursor = captureCursor;
                            settings.Save();
                        }
                    }
                }
            }
        }
        
        // 检测 FFmpeg 路径
        private string DetectFFmpegPath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string startupDir = Application.StartupPath;
            
            // 尝试多个可能的路径（按优先级，优先检查 StartupPath）
            string[] possiblePaths = new string[]
            {
                Path.Combine(startupDir, "ffmpeg-8.0.1", "bin", "ffmpeg.exe"),
                Path.Combine(appDir, "ffmpeg-8.0.1", "bin", "ffmpeg.exe"),
                Path.Combine(startupDir, "ffmpeg-8.0.1-essentials_build", "bin", "ffmpeg.exe"),
                Path.Combine(appDir, "ffmpeg-8.0.1-essentials_build", "bin", "ffmpeg.exe"),
                Path.Combine(startupDir, "ffmpeg.exe"),
                Path.Combine(appDir, "ffmpeg.exe"),
                // 也检查 Program Files 目录（安装后的位置）
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SideBar", "ffmpeg-8.0.1", "bin", "ffmpeg.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SideBar", "ffmpeg-8.0.1", "bin", "ffmpeg.exe"),
            };
            
            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
            
            // 尝试在系统 PATH 中查找
            try
            {
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo.FileName = "where";
                    process.StartInfo.Arguments = "ffmpeg.exe";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0 && File.Exists(lines[0]))
                        {
                            return lines[0].Trim();
                        }
                    }
                }
            }
            catch
            {
                // 忽略错误
            }
            
            return null;
        }
    }
}

