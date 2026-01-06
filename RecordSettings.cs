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
using System.IO;
using System.Text;
using ShareX.ScreenCaptureLib;

namespace Sidebar
{
    public class RecordSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "record_settings.json");
        
        public RecordType LastUsedRecordType { get; set; } = RecordType.Video; // 默认视频录制
        public int GIF_FPS { get; set; } = 10;
        
        // FFmpeg 路径设置
        public bool FFmpegOverrideCLIPath { get; set; } = false;
        public string FFmpegCLIPath { get; set; } = "";
        
        // FFmpeg 视频/音频源和编码器设置
        public string VideoSource { get; set; } = "";
        public string AudioSource { get; set; } = "";
        public int VideoCodec { get; set; } = 0; // FFmpegVideoCodec 枚举值
        public int AudioCodec { get; set; } = 0; // FFmpegAudioCodec 枚举值
        
        // 视频编码器参数
        public int x264_Preset { get; set; } = 0; // FFmpegPreset 枚举值
        public int x264_CRF { get; set; } = 28;
        public bool x264_Use_Bitrate { get; set; } = false;
        public int x264_Bitrate { get; set; } = 3000;
        public int VPx_Bitrate { get; set; } = 3000;
        
        // 音频编码器参数
        public int AAC_Bitrate { get; set; } = 128;
        public int Opus_Bitrate { get; set; } = 128;
        public int Vorbis_QScale { get; set; } = 3;
        public int MP3_QScale { get; set; } = 4;
        
        // 鼠标指针录制设置
        public bool CaptureCursor { get; set; } = true; // 默认勾选
        
        public static RecordSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath, Encoding.UTF8);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<RecordSettings>(json) ?? new RecordSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载录制设置失败: {ex.Message}");
            }
            
            return new RecordSettings();
        }
        
        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存录制设置失败: {ex.Message}");
            }
        }
    }
}

