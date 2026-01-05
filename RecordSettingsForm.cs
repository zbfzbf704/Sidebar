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
using System.Windows.Forms;
using ShareX.ScreenCaptureLib;

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
            
            // 录制按钮（使用🔘图标）- 等间距
            int recordX = btnOptions.Right + spacing;
            btnRecord = new Button();
            btnRecord.Text = "🔘";
            btnRecord.Size = new Size(recordButtonSize, recordButtonSize);
            
            // 应用特殊按钮样式（样式与逻辑分离）
            StyleManager.ConfigureSpecialButton(
                btnRecord, 
                StyleManager.ThemeColors.RecordButtonRed, 
                StyleManager.ThemeFonts.RecordButtonEmoji, 
                transparentBackground: true
            );
            
            // 计算垂直位置，使与选项按钮水平中心对齐
            int optionsCenterY = btnOptions.Top + btnOptions.Height / 2;
            int recordTop = optionsCenterY - recordButtonSize / 2;
            btnRecord.Location = new Point(recordX, recordTop);
            
            btnRecord.Cursor = Cursors.Hand;
            btnRecord.Click += BtnRecord_Click;
            this.Controls.Add(btnRecord);
            
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
    }
}

