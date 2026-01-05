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
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ShareX.HelpersLib;
using ShareX.ScreenCaptureLib;

namespace Sidebar
{
    public partial class RecordOptionsForm : Form
    {
        private RecordType recordType;
        private int gifFPS;
        private FFmpegOptions ffmpegOptions;
        
        // GIF 控件
        private Label lblGIF_FPS;
        private NumericUpDown nudGIF_FPS;
        
        // FFmpeg 路径设置控件
        private CheckBox cbUseCustomFFmpegPath;
        private Label lblFFmpegPath;
        private TextBox txtFFmpegPath;
        private Button btnFFmpegBrowse;
        
        // 视频/音频控件
        private Label lblVideoSource;
        private ComboBox cmbVideoSource;
        private Label lblAudioSource;
        private ComboBox cmbAudioSource;
        private Label lblVideoEncoder;
        private ComboBox cmbVideoEncoder;
        private Label lblAudioEncoder;
        private ComboBox cmbAudioEncoder;
        
        // 参数设置面板
        private Panel pnlVideoParams;
        private Panel pnlAudioParams;
        
        private CheckBox cbCaptureCursor; // 鼠标指针录制复选框
        
        private Button btnOK;
        private Button btnCancel;
        
        public int GIF_FPS => gifFPS;
        public FFmpegOptions FFmpegOptions => ffmpegOptions;
        public bool CaptureCursor { get; private set; } = true; // 默认勾选
        
        public RecordOptionsForm(RecordType type, int gifFps, FFmpegOptions options)
        {
            recordType = type;
            gifFPS = gifFps;
            
            // 如果 options 为空，从保存的设置中加载
            if (options == null)
            {
                RecordSettings settings = RecordSettings.Load();
                ffmpegOptions = new FFmpegOptions();
                ffmpegOptions.OverrideCLIPath = settings.FFmpegOverrideCLIPath;
                ffmpegOptions.CLIPath = settings.FFmpegCLIPath;
                ffmpegOptions.VideoSource = settings.VideoSource;
                ffmpegOptions.AudioSource = settings.AudioSource;
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
                CaptureCursor = settings.CaptureCursor; // 加载鼠标指针设置
            }
            else
            {
                ffmpegOptions = options;
                // 从保存的设置中加载鼠标指针设置
                RecordSettings settings = RecordSettings.Load();
                CaptureCursor = settings.CaptureCursor;
            }
            
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
            this.ShowInTaskbar = false;
            this.TopMost = true;
            
            int yPos = 10;
            const int spacing = 35;
            
            if (recordType == RecordType.GIF)
            {
                this.Text = "GIF 录制设置";
                
                // GIF FPS 设置
                lblGIF_FPS = new Label();
                lblGIF_FPS.Text = "GIF FPS:";
                lblGIF_FPS.Location = new Point(10, yPos);
                lblGIF_FPS.Size = new Size(80, 23);
                this.Controls.Add(lblGIF_FPS);
                
                nudGIF_FPS = new NumericUpDown();
                nudGIF_FPS.Minimum = 1;
                nudGIF_FPS.Maximum = 60;
                nudGIF_FPS.Value = gifFPS;
                nudGIF_FPS.Location = new Point(100, yPos);
                nudGIF_FPS.Size = new Size(100, 23);
                this.Controls.Add(nudGIF_FPS);
                yPos += spacing + 10;
                
                // 确定和取消按钮
                yPos += 10; // 增加间距
                btnOK = new Button();
                btnOK.Text = "确定";
                btnOK.DialogResult = DialogResult.OK;
                btnOK.Size = new Size(75, 30);
                btnOK.Location = new Point(120, yPos);
                btnOK.Click += BtnOK_Click;
                this.Controls.Add(btnOK);
                
                btnCancel = new Button();
                btnCancel.Text = "取消";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Size = new Size(75, 30);
                btnCancel.Location = new Point(205, yPos);
                this.Controls.Add(btnCancel);
                
                // 窗口高度 = 按钮位置 + 按钮高度 + 底部边距
                // 窗口高度 = 按钮位置 + 按钮高度 + 底部边距（增加20像素）
                this.Size = new Size(300, yPos + 70);
            }
            else if (recordType == RecordType.Video)
            {
                this.Text = "视频录制设置";
                
                // FFmpeg 路径设置
                cbUseCustomFFmpegPath = new CheckBox();
                cbUseCustomFFmpegPath.Text = "使用自定义 FFmpeg 路径";
                cbUseCustomFFmpegPath.Location = new Point(10, yPos);
                cbUseCustomFFmpegPath.Size = new Size(200, 23);
                cbUseCustomFFmpegPath.Checked = ffmpegOptions.OverrideCLIPath;
                cbUseCustomFFmpegPath.CheckedChanged += CbUseCustomFFmpegPath_CheckedChanged;
                this.Controls.Add(cbUseCustomFFmpegPath);
                yPos += spacing;
                
                lblFFmpegPath = new Label();
                lblFFmpegPath.Text = "FFmpeg 路径:";
                lblFFmpegPath.Location = new Point(10, yPos);
                lblFFmpegPath.Size = new Size(100, 23);
                this.Controls.Add(lblFFmpegPath);
                
                txtFFmpegPath = new TextBox();
                txtFFmpegPath.Location = new Point(120, yPos);
                txtFFmpegPath.Size = new Size(200, 23);
                txtFFmpegPath.Text = ffmpegOptions.CLIPath;
                txtFFmpegPath.TextChanged += TxtFFmpegPath_TextChanged;
                txtFFmpegPath.Enabled = ffmpegOptions.OverrideCLIPath;
                this.Controls.Add(txtFFmpegPath);
                
                btnFFmpegBrowse = new Button();
                btnFFmpegBrowse.Text = "浏览...";
                btnFFmpegBrowse.Location = new Point(330, yPos);
                btnFFmpegBrowse.Size = new Size(60, 23);
                btnFFmpegBrowse.Click += BtnFFmpegBrowse_Click;
                btnFFmpegBrowse.Enabled = ffmpegOptions.OverrideCLIPath;
                this.Controls.Add(btnFFmpegBrowse);
                yPos += spacing;
                
                // 视频源
                lblVideoSource = new Label();
                lblVideoSource.Text = "视频源:";
                lblVideoSource.Location = new Point(10, yPos);
                lblVideoSource.Size = new Size(100, 23);
                this.Controls.Add(lblVideoSource);
                
                cmbVideoSource = new ComboBox();
                cmbVideoSource.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbVideoSource.Location = new Point(120, yPos);
                cmbVideoSource.Size = new Size(250, 23);
                this.Controls.Add(cmbVideoSource);
                yPos += spacing;
                
                // 音频源
                lblAudioSource = new Label();
                lblAudioSource.Text = "音频源:";
                lblAudioSource.Location = new Point(10, yPos);
                lblAudioSource.Size = new Size(100, 23);
                this.Controls.Add(lblAudioSource);
                
                cmbAudioSource = new ComboBox();
                cmbAudioSource.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbAudioSource.Location = new Point(120, yPos);
                cmbAudioSource.Size = new Size(250, 23);
                this.Controls.Add(cmbAudioSource);
                yPos += spacing;
                
                // Video Encoder
                lblVideoEncoder = new Label();
                lblVideoEncoder.Text = "Video Encoder:";
                lblVideoEncoder.Location = new Point(10, yPos);
                lblVideoEncoder.Size = new Size(100, 23);
                this.Controls.Add(lblVideoEncoder);
                
                cmbVideoEncoder = new ComboBox();
                cmbVideoEncoder.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbVideoEncoder.Items.AddRange(Helpers.GetEnumDescriptions<FFmpegVideoCodec>());
                cmbVideoEncoder.Location = new Point(120, yPos);
                cmbVideoEncoder.Size = new Size(250, 23);
                cmbVideoEncoder.SelectedIndexChanged += CmbVideoEncoder_SelectedIndexChanged;
                this.Controls.Add(cmbVideoEncoder);
                yPos += spacing;
                
                // Audio Encoder
                lblAudioEncoder = new Label();
                lblAudioEncoder.Text = "Audio Encoder:";
                lblAudioEncoder.Location = new Point(10, yPos);
                lblAudioEncoder.Size = new Size(100, 23);
                this.Controls.Add(lblAudioEncoder);
                
                cmbAudioEncoder = new ComboBox();
                cmbAudioEncoder.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbAudioEncoder.Items.AddRange(Helpers.GetEnumDescriptions<FFmpegAudioCodec>());
                cmbAudioEncoder.Location = new Point(120, yPos);
                cmbAudioEncoder.Size = new Size(250, 23);
                cmbAudioEncoder.SelectedIndexChanged += CmbAudioEncoder_SelectedIndexChanged;
                this.Controls.Add(cmbAudioEncoder);
                yPos += spacing;
                
                // 鼠标指针录制选项（视频录制）
                cbCaptureCursor = new CheckBox();
                cbCaptureCursor.Text = "录制鼠标指针";
                cbCaptureCursor.Location = new Point(10, yPos);
                cbCaptureCursor.Size = new Size(200, 23);
                cbCaptureCursor.Checked = CaptureCursor; // 从保存的设置中加载
                this.Controls.Add(cbCaptureCursor);
                yPos += spacing + 10;
                
                // 参数设置面板
                pnlVideoParams = new Panel();
                pnlVideoParams.Location = new Point(10, yPos);
                pnlVideoParams.Size = new Size(360, 200);
                pnlVideoParams.AutoScroll = true;
                pnlVideoParams.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                this.Controls.Add(pnlVideoParams);
                yPos += 210;
                
                pnlAudioParams = new Panel();
                pnlAudioParams.Location = new Point(10, yPos);
                pnlAudioParams.Size = new Size(360, 100);
                pnlAudioParams.AutoScroll = true;
                pnlAudioParams.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                this.Controls.Add(pnlAudioParams);
                yPos += 120;
                
                // 确定和取消按钮
                yPos += 10; // 增加间距
                btnOK = new Button();
                btnOK.Text = "确定";
                btnOK.DialogResult = DialogResult.OK;
                btnOK.Size = new Size(75, 30);
                btnOK.Location = new Point(200, yPos);
                btnOK.Click += BtnOK_Click;
                this.Controls.Add(btnOK);
                
                btnCancel = new Button();
                btnCancel.Text = "取消";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Size = new Size(75, 30);
                btnCancel.Location = new Point(285, yPos);
                this.Controls.Add(btnCancel);
                
                // 窗口高度 = 按钮位置 + 按钮高度 + 底部边距（增加20像素）
                this.Size = new Size(420, yPos + 70);
                
                // 异步加载设备列表
                LoadVideoAudioSources();
            }
            else if (recordType == RecordType.Audio)
            {
                this.Text = "音频录制设置";
                
                // FFmpeg 路径设置
                cbUseCustomFFmpegPath = new CheckBox();
                cbUseCustomFFmpegPath.Text = "使用自定义 FFmpeg 路径";
                cbUseCustomFFmpegPath.Location = new Point(10, yPos);
                cbUseCustomFFmpegPath.Size = new Size(200, 23);
                cbUseCustomFFmpegPath.Checked = ffmpegOptions.OverrideCLIPath;
                cbUseCustomFFmpegPath.CheckedChanged += CbUseCustomFFmpegPath_CheckedChanged;
                this.Controls.Add(cbUseCustomFFmpegPath);
                yPos += spacing;
                
                lblFFmpegPath = new Label();
                lblFFmpegPath.Text = "FFmpeg 路径:";
                lblFFmpegPath.Location = new Point(10, yPos);
                lblFFmpegPath.Size = new Size(100, 23);
                this.Controls.Add(lblFFmpegPath);
                
                txtFFmpegPath = new TextBox();
                txtFFmpegPath.Location = new Point(120, yPos);
                txtFFmpegPath.Size = new Size(200, 23);
                txtFFmpegPath.Text = ffmpegOptions.CLIPath;
                txtFFmpegPath.TextChanged += TxtFFmpegPath_TextChanged;
                txtFFmpegPath.Enabled = ffmpegOptions.OverrideCLIPath;
                this.Controls.Add(txtFFmpegPath);
                
                btnFFmpegBrowse = new Button();
                btnFFmpegBrowse.Text = "浏览...";
                btnFFmpegBrowse.Location = new Point(330, yPos);
                btnFFmpegBrowse.Size = new Size(60, 23);
                btnFFmpegBrowse.Click += BtnFFmpegBrowse_Click;
                btnFFmpegBrowse.Enabled = ffmpegOptions.OverrideCLIPath;
                this.Controls.Add(btnFFmpegBrowse);
                yPos += spacing;
                
                // 音频源
                lblAudioSource = new Label();
                lblAudioSource.Text = "音频源:";
                lblAudioSource.Location = new Point(10, yPos);
                lblAudioSource.Size = new Size(100, 23);
                this.Controls.Add(lblAudioSource);
                
                cmbAudioSource = new ComboBox();
                cmbAudioSource.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbAudioSource.Location = new Point(120, yPos);
                cmbAudioSource.Size = new Size(250, 23);
                this.Controls.Add(cmbAudioSource);
                yPos += spacing;
                
                // Audio Encoder
                lblAudioEncoder = new Label();
                lblAudioEncoder.Text = "Audio Encoder:";
                lblAudioEncoder.Location = new Point(10, yPos);
                lblAudioEncoder.Size = new Size(100, 23);
                this.Controls.Add(lblAudioEncoder);
                
                cmbAudioEncoder = new ComboBox();
                cmbAudioEncoder.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbAudioEncoder.Items.AddRange(Helpers.GetEnumDescriptions<FFmpegAudioCodec>());
                cmbAudioEncoder.Location = new Point(120, yPos);
                cmbAudioEncoder.Size = new Size(250, 23);
                cmbAudioEncoder.SelectedIndexChanged += CmbAudioEncoder_SelectedIndexChanged;
                this.Controls.Add(cmbAudioEncoder);
                yPos += spacing + 10;
                
                // 参数设置面板
                pnlAudioParams = new Panel();
                pnlAudioParams.Location = new Point(10, yPos);
                pnlAudioParams.Size = new Size(360, 100);
                pnlAudioParams.AutoScroll = true;
                pnlAudioParams.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
                this.Controls.Add(pnlAudioParams);
                yPos += 120;
                
                // 确定和取消按钮
                yPos += 10; // 增加间距
                btnOK = new Button();
                btnOK.Text = "确定";
                btnOK.DialogResult = DialogResult.OK;
                btnOK.Size = new Size(75, 30);
                btnOK.Location = new Point(200, yPos);
                btnOK.Click += BtnOK_Click;
                this.Controls.Add(btnOK);
                
                btnCancel = new Button();
                btnCancel.Text = "取消";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Size = new Size(75, 30);
                btnCancel.Location = new Point(285, yPos);
                this.Controls.Add(btnCancel);
                
                // 窗口高度 = 按钮位置 + 按钮高度 + 底部边距（增加20像素）
                this.Size = new Size(420, yPos + 70);
                
                // 异步加载设备列表
                LoadAudioSources();
            }
            
            this.ResumeLayout(false);
        }
        
        private async void LoadVideoAudioSources()
        {
            // 使用 ShareX 的方法获取设备列表
            ShareX.MediaLib.DirectShowDevices devices = null;
            
            // 显示加载提示
            cmbVideoSource.Items.Clear();
            cmbVideoSource.Items.Add("正在加载设备列表...");
            cmbVideoSource.Enabled = false;
            
            cmbAudioSource.Items.Clear();
            cmbAudioSource.Items.Add("正在加载设备列表...");
            cmbAudioSource.Enabled = false;
            
            string ffmpegPath = ffmpegOptions.FFmpegPath;
            string errorMessage = null;
            
            await Task.Run(() =>
            {
                try
                {
                    // 检查 FFmpeg 路径
                    if (string.IsNullOrEmpty(ffmpegPath) || !System.IO.File.Exists(ffmpegPath))
                    {
                        errorMessage = $"FFmpeg 未找到。路径: {ffmpegPath ?? "(空)"}\n请确保 FFmpeg 已正确安装。";
                        System.Diagnostics.Debug.WriteLine(errorMessage);
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"正在使用 FFmpeg 路径: {ffmpegPath}");
                    
                    using (ShareX.MediaLib.FFmpegCLIManager ffmpeg = new ShareX.MediaLib.FFmpegCLIManager(ffmpegPath))
                    {
                        ffmpeg.ShowError = false; // 不显示错误，我们自己处理
                        devices = ffmpeg.GetDirectShowDevices();
                        
                        if (devices != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"找到 {devices.VideoDevices?.Count ?? 0} 个视频设备，{devices.AudioDevices?.Count ?? 0} 个音频设备");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("未找到任何设备");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"加载设备列表失败: {ex.Message}\n堆栈跟踪: {ex.StackTrace}";
                    System.Diagnostics.Debug.WriteLine(errorMessage);
                }
            });
            
            // 如果有错误，显示提示
            if (!string.IsNullOrEmpty(errorMessage) && this.Visible)
            {
                MessageBox.Show(errorMessage + "\n\n提示：请确保 FFmpeg 已正确安装，并且路径正确。", 
                    "FFmpeg 错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            
            // 更新视频源列表
            cmbVideoSource.Items.Clear();
            cmbVideoSource.Items.Add(FFmpegCaptureDevice.None);
            cmbVideoSource.Items.Add(FFmpegCaptureDevice.GDIGrab);
            
            if (Helpers.IsWindows10OrGreater())
            {
                cmbVideoSource.Items.Add(FFmpegCaptureDevice.DDAGrab);
            }
            
            if (devices != null && devices.VideoDevices != null && devices.VideoDevices.Count > 0)
            {
                cmbVideoSource.Items.AddRange(devices.VideoDevices.Select(x => new FFmpegCaptureDevice(x, $"dshow ({x})")).ToArray());
            }
            
            cmbVideoSource.Enabled = true;
            
            // 更新音频源列表
            cmbAudioSource.Items.Clear();
            cmbAudioSource.Items.Add(FFmpegCaptureDevice.None);
            
            if (devices != null && devices.AudioDevices != null && devices.AudioDevices.Count > 0)
            {
                cmbAudioSource.Items.AddRange(devices.AudioDevices.Select(x => new FFmpegCaptureDevice(x, $"dshow ({x})")).ToArray());
            }
            
            cmbAudioSource.Enabled = true;
            
            // 设置当前选中的设备
            SetSelectedDevice(cmbVideoSource, ffmpegOptions.VideoSource);
            if (cmbVideoSource.SelectedItem == null && cmbVideoSource.Items.Count > 0)
            {
                cmbVideoSource.SelectedIndex = 0; // 选择 None
            }
            
            // 设置音频源：如果当前值为空或 None，选择 None；否则尝试匹配设备
            if (string.IsNullOrEmpty(ffmpegOptions.AudioSource) || ffmpegOptions.AudioSource.Equals(FFmpegCaptureDevice.None.Value, StringComparison.OrdinalIgnoreCase))
            {
                cmbAudioSource.SelectedIndex = 0; // 选择 None
            }
            else
            {
                SetSelectedDevice(cmbAudioSource, ffmpegOptions.AudioSource);
                if (cmbAudioSource.SelectedItem == null && cmbAudioSource.Items.Count > 0)
                {
                    cmbAudioSource.SelectedIndex = 0; // 如果找不到匹配的设备，选择 None
                }
            }
            
            // 设置编码器
            if (cmbVideoEncoder.Items.Count > 0)
            {
                int videoCodecIndex = (int)ffmpegOptions.VideoCodec;
                if (videoCodecIndex >= 0 && videoCodecIndex < cmbVideoEncoder.Items.Count)
                {
                    cmbVideoEncoder.SelectedIndex = videoCodecIndex;
                }
                else
                {
                    cmbVideoEncoder.SelectedIndex = 0;
                }
            }
            
            if (cmbAudioEncoder.Items.Count > 0)
            {
                int audioCodecIndex = (int)ffmpegOptions.AudioCodec;
                if (audioCodecIndex >= 0 && audioCodecIndex < cmbAudioEncoder.Items.Count)
                {
                    cmbAudioEncoder.SelectedIndex = audioCodecIndex;
                }
                else
                {
                    cmbAudioEncoder.SelectedIndex = 0;
                }
            }
            
            // 设置当前选中的设备（在设备列表加载完成后）
            SetDeviceSelections();
        }
        
        private void SetDeviceSelections()
        {
            // 设置视频源（仅视频录制）
            if (recordType == RecordType.Video && cmbVideoSource != null && cmbVideoSource.Items.Count > 0)
            {
                SetSelectedDevice(cmbVideoSource, ffmpegOptions.VideoSource);
                if (cmbVideoSource.SelectedItem == null)
                {
                    cmbVideoSource.SelectedIndex = 0; // 选择 None
                }
            }
            
            // 设置音频源
            if (cmbAudioSource != null && cmbAudioSource.Items.Count > 0)
            {
                if (string.IsNullOrEmpty(ffmpegOptions.AudioSource) || ffmpegOptions.AudioSource.Equals(FFmpegCaptureDevice.None.Value, StringComparison.OrdinalIgnoreCase))
                {
                    cmbAudioSource.SelectedIndex = 0; // 选择 None
                }
                else
                {
                    SetSelectedDevice(cmbAudioSource, ffmpegOptions.AudioSource);
                    if (cmbAudioSource.SelectedItem == null)
                    {
                        cmbAudioSource.SelectedIndex = 0;
                    }
                }
            }
            
            // 设置编码器
            if (recordType == RecordType.Video && cmbVideoEncoder != null && cmbVideoEncoder.Items.Count > 0)
            {
                int videoCodecIndex = (int)ffmpegOptions.VideoCodec;
                if (videoCodecIndex >= 0 && videoCodecIndex < cmbVideoEncoder.Items.Count)
                {
                    cmbVideoEncoder.SelectedIndex = videoCodecIndex;
                }
                else
                {
                    cmbVideoEncoder.SelectedIndex = 0;
                }
            }
            
            if (cmbAudioEncoder != null && cmbAudioEncoder.Items.Count > 0)
            {
                int audioCodecIndex = (int)ffmpegOptions.AudioCodec;
                if (audioCodecIndex >= 0 && audioCodecIndex < cmbAudioEncoder.Items.Count)
                {
                    cmbAudioEncoder.SelectedIndex = audioCodecIndex;
                }
                else
                {
                    cmbAudioEncoder.SelectedIndex = 0;
                }
            }
            
            if (recordType == RecordType.Video)
            {
                UpdateVideoParams();
            }
            UpdateAudioParams();
        }
        
        private async void LoadAudioSources()
        {
            ShareX.MediaLib.DirectShowDevices devices = null;
            
            // 显示加载提示
            cmbAudioSource.Items.Clear();
            cmbAudioSource.Items.Add("正在加载设备列表...");
            cmbAudioSource.Enabled = false;
            
            string ffmpegPath = ffmpegOptions.FFmpegPath;
            string errorMessage = null;
            
            await Task.Run(() =>
            {
                try
                {
                    // 检查 FFmpeg 路径
                    if (string.IsNullOrEmpty(ffmpegPath) || !System.IO.File.Exists(ffmpegPath))
                    {
                        errorMessage = $"FFmpeg 未找到。路径: {ffmpegPath ?? "(空)"}\n请确保 FFmpeg 已正确安装。";
                        System.Diagnostics.Debug.WriteLine(errorMessage);
                        return;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"正在使用 FFmpeg 路径: {ffmpegPath}");
                    
                    using (ShareX.MediaLib.FFmpegCLIManager ffmpeg = new ShareX.MediaLib.FFmpegCLIManager(ffmpegPath))
                    {
                        ffmpeg.ShowError = false; // 不显示错误，我们自己处理
                        devices = ffmpeg.GetDirectShowDevices();
                        
                        if (devices != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"找到 {devices.AudioDevices?.Count ?? 0} 个音频设备");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("未找到任何音频设备");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = $"加载音频设备失败: {ex.Message}\n堆栈跟踪: {ex.StackTrace}";
                    System.Diagnostics.Debug.WriteLine(errorMessage);
                }
            });
            
            // 如果有错误，显示提示
            if (!string.IsNullOrEmpty(errorMessage) && this.Visible)
            {
                MessageBox.Show(errorMessage + "\n\n提示：请确保 FFmpeg 已正确安装，并且路径正确。", 
                    "FFmpeg 错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            
            // 更新音频源列表
            cmbAudioSource.Items.Clear();
            cmbAudioSource.Items.Add(FFmpegCaptureDevice.None);
            
            if (devices != null && devices.AudioDevices != null && devices.AudioDevices.Count > 0)
            {
                cmbAudioSource.Items.AddRange(devices.AudioDevices.Select(x => new FFmpegCaptureDevice(x, $"dshow ({x})")).ToArray());
            }
            else
            {
                // 如果没有找到设备，至少显示 None
                // 可能 FFmpeg 路径不正确或没有音频设备
            }
            
            cmbAudioSource.Enabled = true;
            
            // 设置当前选中的设备（在设备列表加载完成后）
            SetDeviceSelections();
        }
        
        private void SetSelectedDevice(ComboBox cmb, string value)
        {
            // 如果值为空或 None，选择第一个（应该是 None）
            if (string.IsNullOrEmpty(value) || value.Equals(FFmpegCaptureDevice.None.Value, StringComparison.OrdinalIgnoreCase))
            {
                if (cmb.Items.Count > 0)
                {
                    cmb.SelectedIndex = 0;
                }
                return;
            }
            
            // 尝试匹配设备
            foreach (FFmpegCaptureDevice device in cmb.Items)
            {
                if (device.Value.Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = device;
                    return;
                }
            }
        }
        
        private void CmbVideoEncoder_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbVideoEncoder.SelectedIndex >= 0)
            {
                ffmpegOptions.VideoCodec = (FFmpegVideoCodec)cmbVideoEncoder.SelectedIndex;
                UpdateVideoParams();
            }
        }
        
        private void CmbAudioEncoder_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbAudioEncoder.SelectedIndex >= 0)
            {
                ffmpegOptions.AudioCodec = (FFmpegAudioCodec)cmbAudioEncoder.SelectedIndex;
                UpdateAudioParams();
            }
        }
        
        private void CbUseCustomFFmpegPath_CheckedChanged(object sender, EventArgs e)
        {
            ffmpegOptions.OverrideCLIPath = cbUseCustomFFmpegPath.Checked;
            txtFFmpegPath.Enabled = btnFFmpegBrowse.Enabled = ffmpegOptions.OverrideCLIPath;
            
            // 如果启用自定义路径，重新加载设备列表
            if (ffmpegOptions.OverrideCLIPath && !string.IsNullOrEmpty(txtFFmpegPath.Text))
            {
                if (recordType == RecordType.Video)
                {
                    LoadVideoAudioSources();
                }
                else if (recordType == RecordType.Audio)
                {
                    LoadAudioSources();
                }
            }
        }
        
        private void TxtFFmpegPath_TextChanged(object sender, EventArgs e)
        {
            ffmpegOptions.CLIPath = txtFFmpegPath.Text;
        }
        
        private void BtnFFmpegBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openDialog = new OpenFileDialog())
            {
                openDialog.Filter = "可执行文件|*.exe|所有文件|*.*";
                openDialog.Title = "选择 FFmpeg 可执行文件";
                openDialog.FileName = "ffmpeg.exe";
                
                // 如果当前路径存在，设置为初始目录
                if (!string.IsNullOrEmpty(txtFFmpegPath.Text))
                {
                    string dir = System.IO.Path.GetDirectoryName(txtFFmpegPath.Text);
                    if (System.IO.Directory.Exists(dir))
                    {
                        openDialog.InitialDirectory = dir;
                    }
                }
                else
                {
                    // 默认使用程序目录内的 FFmpeg 路径
                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string defaultPath = System.IO.Path.Combine(appDir, "ffmpeg-8.0.1-essentials_build", "bin");
                    if (System.IO.Directory.Exists(defaultPath))
                    {
                        openDialog.InitialDirectory = defaultPath;
                    }
                }
                
                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    txtFFmpegPath.Text = openDialog.FileName;
                    
                    // 重新加载设备列表
                    if (recordType == RecordType.Video)
                    {
                        LoadVideoAudioSources();
                    }
                    else if (recordType == RecordType.Audio)
                    {
                        LoadAudioSources();
                    }
                }
            }
        }
        
        private void UpdateVideoParams()
        {
            pnlVideoParams.Controls.Clear();
            int yPos = 10;
            
            // 根据视频编码器显示不同的参数
            switch (ffmpegOptions.VideoCodec)
            {
                case FFmpegVideoCodec.libx264:
                case FFmpegVideoCodec.libx265:
                    AddNumericControl(pnlVideoParams, "CRF:", ffmpegOptions.x264_CRF, 0, 51, ref yPos, (val) => ffmpegOptions.x264_CRF = val);
                    AddNumericControl(pnlVideoParams, "Bitrate (kbps):", ffmpegOptions.x264_Bitrate, 100, 50000, ref yPos, (val) => ffmpegOptions.x264_Bitrate = val);
                    AddComboBoxControl(pnlVideoParams, "Preset:", Helpers.GetEnumDescriptions<FFmpegPreset>(), (int)ffmpegOptions.x264_Preset, ref yPos, (idx) => ffmpegOptions.x264_Preset = (FFmpegPreset)idx);
                    break;
                case FFmpegVideoCodec.libvpx:
                case FFmpegVideoCodec.libvpx_vp9:
                    AddNumericControl(pnlVideoParams, "Bitrate (kbps):", ffmpegOptions.VPx_Bitrate, 100, 50000, ref yPos, (val) => ffmpegOptions.VPx_Bitrate = val);
                    break;
                // 可以添加更多编码器的参数
            }
        }
        
        private void UpdateAudioParams()
        {
            pnlAudioParams.Controls.Clear();
            int yPos = 10;
            
            // 根据音频编码器显示不同的参数
            switch (ffmpegOptions.AudioCodec)
            {
                case FFmpegAudioCodec.libvoaacenc:
                    AddNumericControl(pnlAudioParams, "Bitrate (kbps):", ffmpegOptions.AAC_Bitrate, 64, 320, ref yPos, (val) => ffmpegOptions.AAC_Bitrate = val);
                    break;
                case FFmpegAudioCodec.libopus:
                    AddNumericControl(pnlAudioParams, "Bitrate (kbps):", ffmpegOptions.Opus_Bitrate, 32, 512, ref yPos, (val) => ffmpegOptions.Opus_Bitrate = val);
                    break;
                case FFmpegAudioCodec.libvorbis:
                    AddNumericControl(pnlAudioParams, "Quality:", ffmpegOptions.Vorbis_QScale, 0, 10, ref yPos, (val) => ffmpegOptions.Vorbis_QScale = val);
                    break;
                case FFmpegAudioCodec.libmp3lame:
                    AddNumericControl(pnlAudioParams, "Quality:", ffmpegOptions.MP3_QScale, 0, 9, ref yPos, (val) => ffmpegOptions.MP3_QScale = val);
                    break;
            }
        }
        
        private void AddNumericControl(Panel panel, string label, int value, int min, int max, ref int yPos, Action<int> onValueChanged)
        {
            Label lbl = new Label();
            lbl.Text = label;
            lbl.Location = new Point(10, yPos);
            lbl.Size = new Size(120, 23);
            panel.Controls.Add(lbl);
            
            NumericUpDown nud = new NumericUpDown();
            nud.Minimum = min;
            nud.Maximum = max;
            nud.Value = value;
            nud.Location = new Point(140, yPos);
            nud.Size = new Size(100, 23);
            nud.ValueChanged += (s, e) => onValueChanged((int)nud.Value);
            panel.Controls.Add(nud);
            
            yPos += 30;
        }
        
        private void AddComboBoxControl(Panel panel, string label, string[] items, int selectedIndex, ref int yPos, Action<int> onSelectedIndexChanged)
        {
            Label lbl = new Label();
            lbl.Text = label;
            lbl.Location = new Point(10, yPos);
            lbl.Size = new Size(120, 23);
            panel.Controls.Add(lbl);
            
            ComboBox cmb = new ComboBox();
            cmb.DropDownStyle = ComboBoxStyle.DropDownList;
            cmb.Items.AddRange(items);
            cmb.SelectedIndex = selectedIndex;
            cmb.Location = new Point(140, yPos);
            cmb.Size = new Size(200, 23);
            cmb.SelectedIndexChanged += (s, e) => onSelectedIndexChanged(cmb.SelectedIndex);
            panel.Controls.Add(cmb);
            
            yPos += 30;
        }
        
        private void BtnOK_Click(object sender, EventArgs e)
        {
            // 加载设置
            RecordSettings settings = RecordSettings.Load();
            
            if (recordType == RecordType.GIF)
            {
                gifFPS = (int)nudGIF_FPS.Value;
                settings.GIF_FPS = gifFPS;
                // GIF 录制不录制鼠标指针，不需要保存
            }
            else
            {
                // 保存 FFmpeg 路径设置
                if (cbUseCustomFFmpegPath != null)
                {
                    ffmpegOptions.OverrideCLIPath = cbUseCustomFFmpegPath.Checked;
                }
                if (txtFFmpegPath != null)
                {
                    ffmpegOptions.CLIPath = txtFFmpegPath.Text;
                }
                
                // 保存视频/音频源和编码器设置
                if (cmbVideoSource != null && cmbVideoSource.SelectedItem != null)
                {
                    FFmpegCaptureDevice device = cmbVideoSource.SelectedItem as FFmpegCaptureDevice;
                    if (device != null)
                    {
                        ffmpegOptions.VideoSource = string.IsNullOrEmpty(device.Value) ? "" : device.Value;
                    }
                }
                else
                {
                    ffmpegOptions.VideoSource = "";
                }
                
                if (cmbAudioSource != null && cmbAudioSource.SelectedItem != null)
                {
                    FFmpegCaptureDevice device = cmbAudioSource.SelectedItem as FFmpegCaptureDevice;
                    if (device != null)
                    {
                        ffmpegOptions.AudioSource = string.IsNullOrEmpty(device.Value) ? "" : device.Value;
                    }
                }
                else
                {
                    ffmpegOptions.AudioSource = "";
                }
                
                // 保存所有设置到配置文件
                settings.FFmpegOverrideCLIPath = ffmpegOptions.OverrideCLIPath;
                settings.FFmpegCLIPath = ffmpegOptions.CLIPath;
                settings.VideoSource = ffmpegOptions.VideoSource;
                settings.AudioSource = ffmpegOptions.AudioSource;
                settings.VideoCodec = (int)ffmpegOptions.VideoCodec;
                settings.AudioCodec = (int)ffmpegOptions.AudioCodec;
                
                // 保存编码器参数
                settings.x264_Preset = (int)ffmpegOptions.x264_Preset;
                settings.x264_CRF = ffmpegOptions.x264_CRF;
                settings.x264_Use_Bitrate = ffmpegOptions.x264_Use_Bitrate;
                settings.x264_Bitrate = ffmpegOptions.x264_Bitrate;
                settings.VPx_Bitrate = ffmpegOptions.VPx_Bitrate;
                settings.AAC_Bitrate = ffmpegOptions.AAC_Bitrate;
                settings.Opus_Bitrate = ffmpegOptions.Opus_Bitrate;
                settings.Vorbis_QScale = ffmpegOptions.Vorbis_QScale;
                settings.MP3_QScale = ffmpegOptions.MP3_QScale;
                
                // 保存鼠标指针设置（视频录制）
                if (cbCaptureCursor != null)
                {
                    CaptureCursor = cbCaptureCursor.Checked;
                    settings.CaptureCursor = CaptureCursor;
                }
            }
            
            // 保存所有设置
            settings.Save();
        }
    }
}

