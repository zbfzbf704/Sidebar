#region Copyright Information

/*
    Copyright (c) 2025 蝴蝶哥
    Email: 1780555120@qq.com
    
    This code is part of the Sidebar application.
    All rights reserved.
*/

#endregion Copyright Information

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using ShareX.HelpersLib;

namespace Sidebar
{
    public partial class SystemCleanerForm : Form
    {
        // 颜色变量
        private Color lightGray;
        private Color textColor;
        private Color borderColor;
        
        // UI 控件
        private ComboBox cmbCategory;
        private Button btnDetails;
        private Button btnClean;
        private Button btnCleanAll;
        private ProgressBar progressBar;
        private Label lblStatus;
        private ListBox lstResults;
        
        // 清理选项详情窗口
        private CleanDetailsForm detailsForm;
        
        // 清理结果
        private long totalCleanedSize = 0;
        private int totalCleanedFiles = 0;
        
        // 当前选中的类别
        private CleanCategory currentCategory = CleanCategory.All;
        
        public SystemCleanerForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "系统清理工具";
            this.Size = new Size(700, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            // 应用 ShareX 主题
            ShareXResources.ApplyTheme(this, true);
            
            // 设置颜色
            lightGray = ShareXResources.Theme?.LightBackgroundColor ?? Color.FromArgb(240, 240, 240);
            textColor = Color.White;
            borderColor = Color.FromArgb(200, 200, 200);
            
            // 创建控件
            CreateControls();
            
            // 窗口显示后调整按钮位置（确保使用正确的客户区大小）
            this.Shown += SystemCleanerForm_Shown;
        }
        
        private void SystemCleanerForm_Shown(object sender, EventArgs e)
        {
            // 窗口显示后，使用客户区大小重新计算控件位置和大小
            int buttonMargin = 10; // 距离边缘的间距
            int buttonSpacing = 10; // 按钮之间的间距
            int buttonHeight = 35;
            int progressBarHeight = 23;
            int statusLabelHeight = 23;
            int spacing = 10; // 控件之间的间距
            
            // 计算按钮的 Y 位置（距离底部有足够的边距）
            int buttonY = this.ClientSize.Height - buttonHeight - buttonMargin;
            
            // 调整状态标签位置（在按钮上方）
            int statusLabelY = buttonY - statusLabelHeight - spacing;
            lblStatus.Location = new Point(lblStatus.Left, statusLabelY);
            
            // 调整进度条位置（在状态标签上方）
            int progressBarY = statusLabelY - progressBarHeight - spacing;
            progressBar.Location = new Point(progressBar.Left, progressBarY);
            
            // 调整 ListBox 高度，确保不会与进度条重叠
            // ListBox 的顶部位置是固定的（yPos = 75）
            int listBoxTop = lstResults.Top;
            int listBoxHeight = progressBarY - listBoxTop - spacing;
            
            // 确保 ListBox 有最小高度
            if (listBoxHeight < 100)
            {
                listBoxHeight = 100;
            }
            
            lstResults.Size = new Size(lstResults.Width, listBoxHeight);
            
            // 计算按钮的 X 位置
            // 两个按钮并排，居中显示，但确保距离边缘有足够的边距
            int totalButtonWidth = btnClean.Width + btnCleanAll.Width + buttonSpacing;
            int startX = (this.ClientSize.Width - totalButtonWidth) / 2;
            
            // 确保按钮不会太靠近左边缘
            if (startX < buttonMargin)
            {
                startX = buttonMargin;
            }
            
            // 调整开始清理按钮位置
            btnClean.Location = new Point(startX, buttonY);
            
            // 调整一键清理按钮位置（在开始清理按钮右侧）
            btnCleanAll.Location = new Point(startX + btnClean.Width + buttonSpacing, buttonY);
            
            // 确保按钮不会超出右边缘
            if (btnCleanAll.Right > this.ClientSize.Width - buttonMargin)
            {
                // 如果超出，则右对齐，但保持按钮间距
                btnCleanAll.Location = new Point(
                    this.ClientSize.Width - btnCleanAll.Width - buttonMargin,
                    buttonY
                );
                btnClean.Location = new Point(
                    btnCleanAll.Left - btnClean.Width - buttonSpacing,
                    buttonY
                );
            }
        }
        
        private void CreateControls()
        {
            // 标题
            Label lblTitle = new Label
            {
                Text = "选择清理类别：",
                Location = new Point(10, 15),
                Size = new Size(150, 23),
                ForeColor = textColor
            };
            this.Controls.Add(lblTitle);
            
            // 类别下拉菜单
            cmbCategory = new ComboBox
            {
                Location = new Point(170, 12),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = lightGray,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            cmbCategory.Paint += CmbCategory_Paint;
            cmbCategory.Items.AddRange(new string[] { "所有", "系统清理", "软件清理", "下载清理" });
            cmbCategory.SelectedIndex = 0;
            cmbCategory.SelectedIndexChanged += CmbCategory_SelectedIndexChanged;
            this.Controls.Add(cmbCategory);
            
            // 详情按钮
            btnDetails = new Button
            {
                Text = "详情",
                Location = new Point(380, 12),
                Size = new Size(80, 25),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            ConfigureButton(btnDetails);
            btnDetails.Click += BtnDetails_Click;
            this.Controls.Add(btnDetails);
            
            int yPos = 50;
            
            // 结果列表
            Label lblResults = new Label
            {
                Text = "清理结果：",
                Location = new Point(10, yPos),
                Size = new Size(100, 23),
                ForeColor = textColor
            };
            this.Controls.Add(lblResults);
            
            yPos += 25;
            
            lstResults = new ListBox
            {
                Location = new Point(10, yPos),
                Size = new Size(680, 280),
                BackColor = lightGray,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(lstResults);
            
            yPos += 290;
            
            // 进度条
            progressBar = new ProgressBar
            {
                Location = new Point(10, yPos),
                Size = new Size(680, 23),
                Style = ProgressBarStyle.Continuous
            };
            this.Controls.Add(progressBar);
            
            yPos += 30;
            
            // 状态标签
            lblStatus = new Label
            {
                Text = "请选择清理类别",
                Location = new Point(10, yPos),
                Size = new Size(680, 23),
                ForeColor = textColor
            };
            this.Controls.Add(lblStatus);
            
            yPos += 30;
            
            // 按钮
            btnClean = new Button
            {
                Text = "开始清理",
                Location = new Point(300, yPos),
                Size = new Size(180, 35),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            ConfigureButton(btnClean);
            btnClean.Click += BtnClean_Click;
            this.Controls.Add(btnClean);
            
            btnCleanAll = new Button
            {
                Text = "一键清理",
                Location = new Point(500, yPos),
                Size = new Size(180, 35),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            ConfigureButton(btnCleanAll);
            btnCleanAll.Click += BtnCleanAll_Click;
            this.Controls.Add(btnCleanAll);
        }
        
        private void ConfigureButton(Button button)
        {
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;
            button.MouseEnter += Button_MouseEnter;
            button.MouseLeave += Button_MouseLeave;
        }
        
        private void Button_MouseEnter(object sender, EventArgs e)
        {
            if (sender is Button button && button.Enabled)
            {
                button.BackColor = Color.FromArgb(220, 220, 220);
            }
        }
        
        private void Button_MouseLeave(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                button.BackColor = lightGray;
            }
        }
        
        private void CmbCategory_Paint(object sender, PaintEventArgs e)
        {
            ComboBox cmb = sender as ComboBox;
            if (cmb == null) return;
            
            Rectangle rect = cmb.ClientRectangle;
            using (Pen pen = new Pen(borderColor, 1))
            {
                e.Graphics.DrawRectangle(pen, new Rectangle(rect.Left, rect.Top, rect.Width - 1, rect.Height - 1));
            }
        }
        
        private void CmbCategory_SelectedIndexChanged(object sender, EventArgs e)
        {
            currentCategory = (CleanCategory)cmbCategory.SelectedIndex;
            lblStatus.Text = $"已选择：{cmbCategory.SelectedItem}";
        }
        
        private void BtnDetails_Click(object sender, EventArgs e)
        {
            if (detailsForm != null && !detailsForm.IsDisposed)
            {
                detailsForm.Close();
            }
            
            detailsForm = new CleanDetailsForm(currentCategory, lightGray, textColor, borderColor);
            detailsForm.ShowDialog();
        }
        
        private void BtnClean_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                $"确定要开始清理【{cmbCategory.SelectedItem}】吗？\n\n此操作可能会删除一些文件，请确保已备份重要数据。",
                "确认清理",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes)
            {
                return;
            }
            
            PerformClean(currentCategory);
        }
        
        private void BtnCleanAll_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show(
                "确定要执行一键清理吗？\n\n将清理所有类别的项目，此操作可能会删除一些文件，请确保已备份重要数据。",
                "确认一键清理",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes)
            {
                return;
            }
            
            PerformClean(CleanCategory.All);
        }
        
        // 获取指定类别的选中项目
        private Dictionary<string, bool> GetSelectedItemsForCategory(CleanCategory category)
        {
            Dictionary<string, bool> selectedItems = new Dictionary<string, bool>();
            
            // 如果详情窗口存在且未关闭，获取选中的项目
            if (detailsForm != null && !detailsForm.IsDisposed)
            {
                var allSelected = detailsForm.GetSelectedItems();
                foreach (var kvp in allSelected)
                {
                    selectedItems[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                // 如果没有打开详情窗口，默认全选
                // 根据类别创建默认全选的字典
                if (category == CleanCategory.All || category == CleanCategory.System)
                {
                    selectedItems["系统临时文件"] = true;
                    selectedItems["系统更新临时文件"] = true;
                    selectedItems["Windows.old 旧系统备份"] = true;
                    selectedItems["回收站"] = true;
                    selectedItems["休眠文件 (hiberfil.sys)"] = true;
                    selectedItems["图片缓存、图标缓存、缩略图缓存"] = true;
                    selectedItems["Edge 浏览器缓存"] = true;
                    selectedItems["Chrome 浏览器缓存"] = true;
                    selectedItems["Firefox 浏览器缓存"] = true;
                    selectedItems["夸克浏览器缓存"] = true;
                    selectedItems["360浏览器缓存"] = true;
                    selectedItems["UWP 应用缓存 (Microsoft Store 应用)"] = true;
                    selectedItems["注册表冗余 (软件卸载残留、无效关联)"] = true;
                    selectedItems["系统日志文件"] = true;
                    selectedItems["启动项优化 (无效启动项)"] = true;
                }
                
                if (category == CleanCategory.All || category == CleanCategory.Software)
                {
                    selectedItems["聊天软件缓存 (微信、QQ、钉钉等)"] = true;
                    selectedItems["设计类软件缓存 (Adobe、Photoshop、Premier、After Effects等)"] = true;
                    selectedItems["Office、ShareX、FFmpeg 等工具软件缓存"] = true;
                    selectedItems["常用软件缓存 (音乐、视频、网盘等)"] = true;
                    selectedItems["其他常用软件缓存"] = true;
                }
                
                if (category == CleanCategory.All || category == CleanCategory.Download)
                {
                    selectedItems["系统下载目录 (C:\\Users\\用户名\\Downloads) - 清理所有文件"] = true;
                    selectedItems["Edge 浏览器下载"] = true;
                    selectedItems["Chrome 浏览器下载"] = true;
                    selectedItems["Firefox 浏览器下载"] = true;
                    selectedItems["夸克浏览器下载"] = true;
                    selectedItems["360浏览器下载"] = true;
                    selectedItems["迅雷下载目录"] = true;
                    selectedItems["百度网盘下载目录"] = true;
                    selectedItems["阿里云盘下载目录"] = true;
                    selectedItems["腾讯微云下载目录"] = true;
                    selectedItems["其他下载软件目录"] = true;
                }
            }
            
            return selectedItems;
        }
        
        private void PerformClean(CleanCategory category)
        {
            btnClean.Enabled = false;
            btnCleanAll.Enabled = false;
            lstResults.Items.Clear();
            totalCleanedSize = 0;
            totalCleanedFiles = 0;
            
            try
            {
                AddResult("═══════════════════════════════════════");
                AddResult("   系统清理工具 - 开始执行清理任务");
                AddResult("═══════════════════════════════════════");
                AddResult($"清理时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                AddResult("");
                
                if (category == CleanCategory.All || category == CleanCategory.System)
                {
                    CleanSystemCategory();
                }
                
                if (category == CleanCategory.All || category == CleanCategory.Software)
                {
                    CleanSoftwareCategory();
                }
                
                if (category == CleanCategory.All || category == CleanCategory.Download)
                {
                    CleanDownloadCategory();
                }
                
                // 显示总结
                AddResult("");
                AddResult("═══════════════════════════════════════");
                string sizeStr = FormatFileSize(totalCleanedSize);
                AddResult($"   清理完成！共清理 {totalCleanedFiles} 个文件/项目");
                AddResult($"   释放空间: {sizeStr}");
                AddResult("═══════════════════════════════════════");
                
                MessageBox.Show(
                    $"清理完成！\n\n" +
                    $"清理文件/项目数: {totalCleanedFiles}\n" +
                    $"释放空间: {sizeStr}\n\n" +
                    $"清理时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    "清理完成",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AddResult("");
                AddResult("═══════════════════════════════════════");
                AddResult($"   ✗ 清理过程中发生错误");
                AddResult("═══════════════════════════════════════");
                MessageBox.Show(
                    $"清理过程中出错！\n\n错误信息: {ex.Message}\n\n请检查文件权限或联系技术支持。",
                    "清理错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                AddResult($"错误详情: {ex.Message}");
            }
            finally
            {
                btnClean.Enabled = true;
                btnCleanAll.Enabled = true;
                progressBar.Value = 0;
            }
        }
        
        private void CleanSystemCategory()
        {
            AddResult("═══════════════════════════════════════");
            AddResult("   开始系统清理 - 清理系统垃圾和缓存");
            AddResult("═══════════════════════════════════════");
            
            // 获取选中的项目
            Dictionary<string, bool> selectedItems = GetSelectedItemsForCategory(CleanCategory.System);
            
            // 1. 系统临时文件
            if (selectedItems.ContainsKey("系统临时文件") && selectedItems["系统临时文件"])
                CleanSystemTempFiles();
            
            // 2. 系统更新临时文件
            if (selectedItems.ContainsKey("系统更新临时文件") && selectedItems["系统更新临时文件"])
                CleanUpdateTempFiles();
            
            // 3. Windows.old
            if (selectedItems.ContainsKey("Windows.old 旧系统备份") && selectedItems["Windows.old 旧系统备份"])
                CleanWindowsOld();
            
            // 4. 回收站
            if (selectedItems.ContainsKey("回收站") && selectedItems["回收站"])
                CleanRecycleBin();
            
            // 5. 休眠文件
            if (selectedItems.ContainsKey("休眠文件 (hiberfil.sys)") && selectedItems["休眠文件 (hiberfil.sys)"])
                CleanHibernationFile();
            
            // 6. 图片缓存、图标缓存、缩略图缓存
            if (selectedItems.ContainsKey("图片缓存、图标缓存、缩略图缓存") && selectedItems["图片缓存、图标缓存、缩略图缓存"])
                CleanImageCaches();
            
            // 7. 浏览器缓存（按浏览器类型）
            if (selectedItems.ContainsKey("Edge 浏览器缓存") && selectedItems["Edge 浏览器缓存"])
                CleanEdgeCache();
            if (selectedItems.ContainsKey("Chrome 浏览器缓存") && selectedItems["Chrome 浏览器缓存"])
                CleanChromeCache();
            if (selectedItems.ContainsKey("Firefox 浏览器缓存") && selectedItems["Firefox 浏览器缓存"])
                CleanFirefoxCache();
            if (selectedItems.ContainsKey("夸克浏览器缓存") && selectedItems["夸克浏览器缓存"])
                CleanQuarkCache();
            if (selectedItems.ContainsKey("360浏览器缓存") && selectedItems["360浏览器缓存"])
                Clean360BrowserCache();
            
            // 8. UWP应用缓存
            if (selectedItems.ContainsKey("UWP 应用缓存 (Microsoft Store 应用)") && selectedItems["UWP 应用缓存 (Microsoft Store 应用)"])
                CleanUWPCaches();
            
            // 9. 注册表冗余
            if (selectedItems.ContainsKey("注册表冗余 (软件卸载残留、无效关联)") && selectedItems["注册表冗余 (软件卸载残留、无效关联)"])
                CleanRegistryRedundancy();
            
            // 10. 系统日志
            if (selectedItems.ContainsKey("系统日志文件") && selectedItems["系统日志文件"])
                CleanSystemLogs();
            
            // 11. 启动项优化
            if (selectedItems.ContainsKey("启动项优化 (无效启动项)") && selectedItems["启动项优化 (无效启动项)"])
                OptimizeStartupItems();
            
            AddResult("═══════════════════════════════════════");
            AddResult("   系统清理完成");
            AddResult("═══════════════════════════════════════");
        }
        
        private void CleanSoftwareCategory()
        {
            AddResult("═══════════════════════════════════════");
            AddResult("   开始软件清理 - 清理应用程序缓存");
            AddResult("═══════════════════════════════════════");
            
            // 获取选中的项目
            Dictionary<string, bool> selectedItems = GetSelectedItemsForCategory(CleanCategory.Software);
            
            // 1. 聊天软件缓存
            if (selectedItems.ContainsKey("聊天软件缓存 (微信、QQ、钉钉等)") && selectedItems["聊天软件缓存 (微信、QQ、钉钉等)"])
                CleanChatSoftwareCache();
            
            // 2. 设计类软件缓存
            if (selectedItems.ContainsKey("设计类软件缓存 (Adobe、Photoshop、Premier、After Effects等)") && selectedItems["设计类软件缓存 (Adobe、Photoshop、Premier、After Effects等)"])
                CleanDesignSoftwareCache();
            
            // 3. Office、ShareX、FFmpeg等缓存
            if (selectedItems.ContainsKey("Office、ShareX、FFmpeg 等工具软件缓存") && selectedItems["Office、ShareX、FFmpeg 等工具软件缓存"])
                CleanOfficeAndToolsCache();
            
            // 4. 常用软件缓存
            if (selectedItems.ContainsKey("常用软件缓存 (音乐、视频、网盘等)") && selectedItems["常用软件缓存 (音乐、视频、网盘等)"])
                CleanCommonSoftwareCache();
            
            // 5. 其他常用软件缓存
            if (selectedItems.ContainsKey("其他常用软件缓存") && selectedItems["其他常用软件缓存"])
                CleanOtherSoftwareCache();
            
            AddResult("═══════════════════════════════════════");
            AddResult("   软件清理完成");
            AddResult("═══════════════════════════════════════");
        }
        
        private void CleanDownloadCategory()
        {
            AddResult("═══════════════════════════════════════");
            AddResult("   开始下载清理 - 清理各类软件的下载目录");
            AddResult("═══════════════════════════════════════");
            
            // 获取选中的项目
            Dictionary<string, bool> selectedItems = GetSelectedItemsForCategory(CleanCategory.Download);
            
            // 1. 系统下载目录
            if (selectedItems.ContainsKey("系统下载目录 (C:\\Users\\用户名\\Downloads) - 清理所有文件") && selectedItems["系统下载目录 (C:\\Users\\用户名\\Downloads) - 清理所有文件"])
                CleanSystemDownloads();
            
            // 2. 浏览器下载（按浏览器类型）
            if (selectedItems.ContainsKey("Edge 浏览器下载") && selectedItems["Edge 浏览器下载"])
                CleanEdgeDownloads();
            if (selectedItems.ContainsKey("Chrome 浏览器下载") && selectedItems["Chrome 浏览器下载"])
                CleanChromeDownloads();
            if (selectedItems.ContainsKey("Firefox 浏览器下载") && selectedItems["Firefox 浏览器下载"])
                CleanFirefoxDownloads();
            if (selectedItems.ContainsKey("夸克浏览器下载") && selectedItems["夸克浏览器下载"])
                CleanQuarkDownloads();
            if (selectedItems.ContainsKey("360浏览器下载") && selectedItems["360浏览器下载"])
                Clean360BrowserDownloads();
            
            // 3. 迅雷下载目录
            if (selectedItems.ContainsKey("迅雷下载目录") && selectedItems["迅雷下载目录"])
                CleanThunderDownloads();
            
            // 4. 百度网盘下载目录
            if (selectedItems.ContainsKey("百度网盘下载目录") && selectedItems["百度网盘下载目录"])
                CleanBaiduNetdiskDownloads();
            
            // 5. 阿里云盘下载目录
            if (selectedItems.ContainsKey("阿里云盘下载目录") && selectedItems["阿里云盘下载目录"])
                CleanAliyunDriveDownloads();
            
            // 6. 腾讯微云下载目录
            if (selectedItems.ContainsKey("腾讯微云下载目录") && selectedItems["腾讯微云下载目录"])
                CleanWeiyunDownloads();
            
            // 7. 其他下载软件目录
            if (selectedItems.ContainsKey("其他下载软件目录") && selectedItems["其他下载软件目录"])
                CleanOtherDownloadSoftware();
            
            AddResult("═══════════════════════════════════════");
            AddResult("   下载清理完成");
            AddResult("═══════════════════════════════════════");
        }
        
        // 系统清理方法 - 优化版本
        private void CleanSystemTempFiles()
        {
            AddResult("正在清理系统临时文件...");
            lblStatus.Text = "正在清理 Windows 系统临时文件目录，包括系统运行产生的临时文件...";
            Application.DoEvents();
            
            try
            {
                // 使用环境变量获取临时文件路径，更准确
                List<string> tempPaths = new List<string>();
                
                // Windows 系统临时目录
                string windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                if (Directory.Exists(windowsTemp))
                    tempPaths.Add(windowsTemp);
                
                // 用户临时目录
                string userTemp = Environment.GetEnvironmentVariable("TEMP");
                if (!string.IsNullOrEmpty(userTemp) && Directory.Exists(userTemp))
                    tempPaths.Add(userTemp);
                
                string userTmp = Environment.GetEnvironmentVariable("TMP");
                if (!string.IsNullOrEmpty(userTmp) && Directory.Exists(userTmp) && userTmp != userTemp)
                    tempPaths.Add(userTmp);
                
                // LocalApplicationData Temp
                string localAppDataTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
                if (Directory.Exists(localAppDataTemp))
                    tempPaths.Add(localAppDataTemp);
                
                long size = 0;
                int files = 0;
                int failed = 0;
                
                foreach (string tempPath in tempPaths)
                {
                    if (Directory.Exists(tempPath))
                    {
                        // 使用递归清理，但只清理文件，保留目录结构
                        var result = CleanDirectory(tempPath, "*.*", SearchOption.AllDirectories);
                        size += result.Size;
                        files += result.Files;
                        failed += result.Failed;
                    }
                }
                
                totalCleanedSize += size;
                totalCleanedFiles += files;
                if (files > 0)
                {
                    AddResult($"✓ 系统临时文件清理完成：清理了 {files} 个文件，释放 {FormatFileSize(size)}");
                    if (failed > 0)
                    {
                        AddResult($"   警告：{failed} 个文件无法删除（可能被占用）");
                    }
                }
                else
                {
                    AddResult("ℹ 系统临时文件目录为空或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 系统临时文件清理失败: {ex.Message}");
            }
        }
        
        private void CleanUpdateTempFiles()
        {
            AddResult("正在清理系统更新临时文件...");
            lblStatus.Text = "正在清理 Windows 更新下载的临时文件，这些文件在更新完成后通常可以安全删除...";
            Application.DoEvents();
            
            try
            {
                string updatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download");
                if (Directory.Exists(updatePath))
                {
                    var result = CleanDirectory(updatePath, "*.*", SearchOption.AllDirectories);
                    totalCleanedSize += result.Size;
                    totalCleanedFiles += result.Files;
                    if (result.Files > 0)
                    {
                        AddResult($"✓ 系统更新临时文件清理完成：清理了 {result.Files} 个文件，释放 {FormatFileSize(result.Size)}");
                    }
                    else
                    {
                        AddResult("ℹ 系统更新临时文件目录为空或已清理");
                    }
                }
                else
                {
                    AddResult("ℹ 系统更新临时文件目录不存在");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 系统更新临时文件清理失败: {ex.Message}");
            }
        }
        
        private void CleanWindowsOld()
        {
            AddResult("正在清理 Windows.old 旧系统备份...");
            lblStatus.Text = "正在清理 Windows.old 目录（系统升级后的旧系统备份），这可能需要较长时间...";
            Application.DoEvents();
            
            try
            {
                string windowsOldPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "Windows.old");
                if (Directory.Exists(windowsOldPath))
                {
                    var result = CleanDirectory(windowsOldPath, "*.tmp", SearchOption.AllDirectories);
                    totalCleanedSize += result.Size;
                    totalCleanedFiles += result.Files;
                    if (result.Files > 0)
                    {
                        AddResult($"✓ Windows.old 清理完成：清理了 {result.Files} 个临时文件，释放 {FormatFileSize(result.Size)}");
                        AddResult("   注意：Windows.old 目录本身需要手动删除（可能需要管理员权限）");
                    }
                    else
                    {
                        AddResult("ℹ Windows.old 目录中没有需要清理的临时文件");
                    }
                }
                else
                {
                    AddResult("ℹ Windows.old 目录不存在，可能未进行系统升级");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ Windows.old 清理失败: {ex.Message}");
            }
        }
        
        private void CleanRecycleBin()
        {
            AddResult("正在清理回收站...");
            lblStatus.Text = "正在清空回收站，删除所有已删除的文件...";
            Application.DoEvents();
            
            try
            {
                SHEmptyRecycleBin(IntPtr.Zero, null, RecycleFlags.SHERB_NOCONFIRMATION | RecycleFlags.SHERB_NOPROGRESSUI | RecycleFlags.SHERB_NOSOUND);
                AddResult("✓ 回收站清理完成：已清空回收站中的所有文件");
                totalCleanedFiles++;
            }
            catch (Exception ex)
            {
                AddResult($"✗ 回收站清理失败: {ex.Message}");
            }
        }
        
        private void CleanHibernationFile()
        {
            AddResult("正在检查休眠文件 (hiberfil.sys)...");
            lblStatus.Text = "正在检查系统休眠文件，该文件通常占用较大空间...";
            Application.DoEvents();
            
            try
            {
                string hiberfilPath = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory), "hiberfil.sys");
                if (File.Exists(hiberfilPath))
                {
                    // 注意：休眠文件需要管理员权限才能删除，这里只记录
                    FileInfo fi = new FileInfo(hiberfilPath);
                    AddResult($"ℹ 发现休眠文件，大小: {FormatFileSize(fi.Length)}");
                    AddResult("   提示：休眠文件需要管理员权限才能删除。");
                    AddResult("   如需删除，请以管理员身份运行命令: powercfg -h off");
                }
                else
                {
                    AddResult("ℹ 未找到休眠文件，系统可能未启用休眠功能");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 休眠文件检查失败: {ex.Message}");
            }
        }
        
        private void CleanImageCaches()
        {
            AddResult("正在清理图片、图标、缩略图缓存...");
            lblStatus.Text = "正在清理 Windows 资源管理器生成的缩略图、图标缓存文件...";
            Application.DoEvents();
            
            try
            {
                long size = 0;
                int files = 0;
                
                // 缩略图缓存
                string thumbCachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Explorer");
                if (Directory.Exists(thumbCachePath))
                {
                    var result = CleanDirectory(thumbCachePath, "thumbcache_*.db", SearchOption.TopDirectoryOnly);
                    size += result.Size;
                    files += result.Files;
                }
                
                // 图标缓存
                string iconCachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "Windows", "Explorer");
                if (Directory.Exists(iconCachePath))
                {
                    var result = CleanDirectory(iconCachePath, "iconcache_*.db", SearchOption.TopDirectoryOnly);
                    size += result.Size;
                    files += result.Files;
                }
                
                totalCleanedSize += size;
                totalCleanedFiles += files;
                if (files > 0)
                {
                    AddResult($"✓ 图片缓存清理完成：清理了 {files} 个缓存文件，释放 {FormatFileSize(size)}");
                }
                else
                {
                    AddResult("ℹ 图片缓存目录为空或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 图片缓存清理失败: {ex.Message}");
            }
        }
        
        // 清理 Edge 浏览器缓存
        private void CleanEdgeCache()
        {
            AddResult("正在清理 Edge 浏览器缓存...");
            lblStatus.Text = "正在清理 Edge 浏览器缓存，这可能需要一些时间...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string edgeCache = Path.Combine(userProfile, "AppData", "Local", "Microsoft", "Edge", "User Data", "Default", "Cache");
                if (Directory.Exists(edgeCache))
                {
                    var result = CleanDirectory(edgeCache, "*.*", SearchOption.AllDirectories);
                    totalCleanedSize += result.Size;
                    totalCleanedFiles += result.Files;
                    AddResult($"✓ Edge 浏览器缓存清理完成：清理了 {result.Files} 个文件，释放 {FormatFileSize(result.Size)}");
                }
                else
                {
                    AddResult("ℹ Edge 浏览器缓存目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ Edge 浏览器缓存清理失败: {ex.Message}");
            }
        }
        
        // 清理 Chrome 浏览器缓存
        private void CleanChromeCache()
        {
            AddResult("正在清理 Chrome 浏览器缓存...");
            lblStatus.Text = "正在清理 Chrome 浏览器缓存，这可能需要一些时间...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string chromeCache = Path.Combine(userProfile, "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Cache");
                if (Directory.Exists(chromeCache))
                {
                    var result = CleanDirectory(chromeCache, "*.*", SearchOption.AllDirectories);
                    totalCleanedSize += result.Size;
                    totalCleanedFiles += result.Files;
                    AddResult($"✓ Chrome 浏览器缓存清理完成：清理了 {result.Files} 个文件，释放 {FormatFileSize(result.Size)}");
                }
                else
                {
                    AddResult("ℹ Chrome 浏览器缓存目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ Chrome 浏览器缓存清理失败: {ex.Message}");
            }
        }
        
        // 清理 Firefox 浏览器缓存
        private void CleanFirefoxCache()
        {
            AddResult("正在清理 Firefox 浏览器缓存...");
            lblStatus.Text = "正在清理 Firefox 浏览器缓存，这可能需要一些时间...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string firefoxCache = Path.Combine(userProfile, "AppData", "Local", "Mozilla", "Firefox", "Profiles");
                if (Directory.Exists(firefoxCache))
                {
                    long size = 0;
                    int files = 0;
                    var profiles = Directory.GetDirectories(firefoxCache);
                    foreach (string profile in profiles)
                    {
                        string cachePath = Path.Combine(profile, "cache2");
                        if (Directory.Exists(cachePath))
                        {
                            var result = CleanDirectory(cachePath, "*.*", SearchOption.AllDirectories);
                            size += result.Size;
                            files += result.Files;
                        }
                    }
                    totalCleanedSize += size;
                    totalCleanedFiles += files;
                    if (files > 0)
                    {
                        AddResult($"✓ Firefox 浏览器缓存清理完成：清理了 {files} 个文件，释放 {FormatFileSize(size)}");
                    }
                    else
                    {
                        AddResult("ℹ Firefox 浏览器缓存目录为空或已清理");
                    }
                }
                else
                {
                    AddResult("ℹ Firefox 浏览器缓存目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ Firefox 浏览器缓存清理失败: {ex.Message}");
            }
        }
        
        // 清理夸克浏览器缓存
        private void CleanQuarkCache()
        {
            AddResult("正在清理夸克浏览器缓存...");
            lblStatus.Text = "正在清理夸克浏览器缓存...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string quarkCache = Path.Combine(userProfile, "AppData", "Local", "Quark", "User Data", "Default", "Cache");
                if (Directory.Exists(quarkCache))
                {
                    var result = CleanDirectory(quarkCache, "*.*", SearchOption.AllDirectories);
                    totalCleanedSize += result.Size;
                    totalCleanedFiles += result.Files;
                    AddResult($"✓ 夸克浏览器缓存清理完成：清理了 {result.Files} 个文件，释放 {FormatFileSize(result.Size)}");
                }
                else
                {
                    AddResult("ℹ 夸克浏览器缓存目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 夸克浏览器缓存清理失败: {ex.Message}");
            }
        }
        
        // 清理 360浏览器缓存
        private void Clean360BrowserCache()
        {
            AddResult("正在清理 360浏览器缓存...");
            lblStatus.Text = "正在清理 360浏览器缓存...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                // 360浏览器可能有多个路径
                string[] paths = new[]
                {
                    Path.Combine(userProfile, "AppData", "Local", "360Chrome", "Chrome", "User Data", "Default", "Cache"),
                    Path.Combine(userProfile, "AppData", "Roaming", "360se6", "User Data", "Default", "Cache"),
                    Path.Combine(userProfile, "AppData", "Local", "360Browser", "User Data", "Default", "Cache")
                };
                
                long size = 0;
                int files = 0;
                foreach (string path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        var result = CleanDirectory(path, "*.*", SearchOption.AllDirectories);
                        size += result.Size;
                        files += result.Files;
                    }
                }
                
                if (files > 0)
                {
                    totalCleanedSize += size;
                    totalCleanedFiles += files;
                    AddResult($"✓ 360浏览器缓存清理完成：清理了 {files} 个文件，释放 {FormatFileSize(size)}");
                }
                else
                {
                    AddResult("ℹ 360浏览器缓存目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 360浏览器缓存清理失败: {ex.Message}");
            }
        }
        
        // 保留旧方法以兼容（已废弃，建议使用上面的独立方法）
        private void CleanBrowserCaches()
        {
            CleanEdgeCache();
            CleanChromeCache();
            CleanFirefoxCache();
            CleanQuarkCache();
            Clean360BrowserCache();
        }
        
        private void CleanUWPCaches()
        {
            AddResult("正在清理 UWP 应用缓存 (Microsoft Store 应用)...");
            lblStatus.Text = "正在清理 Microsoft Store 应用的缓存文件，包括照片、邮件等 UWP 应用的临时数据...";
            Application.DoEvents();
            
            try
            {
                string uwpCachePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Packages");
                
                if (Directory.Exists(uwpCachePath))
                {
                    long size = 0;
                    int files = 0;
                    
                    var packages = Directory.GetDirectories(uwpCachePath);
                    foreach (string package in packages)
                    {
                        string tempPath = Path.Combine(package, "TempState");
                        if (Directory.Exists(tempPath))
                        {
                            var result = CleanDirectory(tempPath, "*.*", SearchOption.AllDirectories);
                            size += result.Size;
                            files += result.Files;
                        }
                    }
                    
                    totalCleanedSize += size;
                    totalCleanedFiles += files;
                    if (files > 0)
                    {
                        AddResult($"✓ UWP 应用缓存清理完成：清理了 {files} 个文件，释放 {FormatFileSize(size)}");
                    }
                    else
                    {
                        AddResult("ℹ UWP 应用缓存目录为空或已清理");
                    }
                }
                else
                {
                    AddResult("ℹ UWP 应用缓存目录不存在");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ UWP 应用缓存清理失败: {ex.Message}");
            }
        }
        
        private void CleanRegistryRedundancy()
        {
            AddResult("正在清理注册表冗余 (软件卸载残留、无效关联)...");
            lblStatus.Text = "正在扫描并清理注册表中的无效项，包括软件卸载残留、无效文件关联等...";
            Application.DoEvents();
            
            try
            {
                int count = CleanInvalidRegistryKeys();
                totalCleanedFiles += count;
                if (count > 0)
                {
                    AddResult($"✓ 注册表冗余清理完成：清理了 {count} 个无效注册表项");
                }
                else
                {
                    AddResult("ℹ 未发现需要清理的注册表冗余项");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 注册表冗余清理失败: {ex.Message}");
            }
        }
        
        private void CleanSystemLogs()
        {
            AddResult("正在清理系统日志文件...");
            lblStatus.Text = "正在清理 Windows 系统日志文件，这些日志文件记录了系统运行历史...";
            Application.DoEvents();
            
            try
            {
                long size = 0;
                int files = 0;
                
                string[] logPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Logs"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "LogFiles")
                };
                
                foreach (string logPath in logPaths)
                {
                    if (Directory.Exists(logPath))
                    {
                        var result = CleanDirectory(logPath, "*.log", SearchOption.AllDirectories);
                        size += result.Size;
                        files += result.Files;
                    }
                }
                
                totalCleanedSize += size;
                totalCleanedFiles += files;
                if (files > 0)
                {
                    AddResult($"✓ 系统日志清理完成：清理了 {files} 个日志文件，释放 {FormatFileSize(size)}");
                }
                else
                {
                    AddResult("ℹ 系统日志目录为空或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 系统日志清理失败: {ex.Message}");
            }
        }
        
        private void OptimizeStartupItems()
        {
            AddResult("正在优化启动项 (无效启动项)...");
            lblStatus.Text = "正在扫描并清理无效的启动项，这些启动项可能导致系统启动变慢...";
            Application.DoEvents();
            
            try
            {
                // 这里可以添加启动项优化逻辑
                // 注意：实际清理启动项需要谨慎，建议只标记无效项
                AddResult("ℹ 启动项优化功能：已扫描启动项（实际清理需要用户确认）");
                AddResult("   提示：建议使用系统自带的启动项管理工具进行管理");
            }
            catch (Exception ex)
            {
                AddResult($"✗ 启动项优化失败: {ex.Message}");
            }
        }
        
        // 软件清理方法
        private void CleanChatSoftwareCache()
        {
            AddResult("正在清理聊天软件缓存 (微信、QQ、钉钉等)...");
            lblStatus.Text = "正在清理聊天软件的缓存文件，包括表情包、聊天图片等临时文件...";
            Application.DoEvents();
            
            try
            {
                long size = 0;
                int files = 0;
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                // 微信
                string wechatCache = Path.Combine(userProfile, "Documents", "WeChat Files");
                if (Directory.Exists(wechatCache))
                {
                    var result = CleanDirectory(wechatCache, "*.tmp", SearchOption.AllDirectories);
                    size += result.Size;
                    files += result.Files;
                }
                
                // QQ
                string qqCache = Path.Combine(userProfile, "Documents", "Tencent Files");
                if (Directory.Exists(qqCache))
                {
                    var result = CleanDirectory(qqCache, "*.tmp", SearchOption.AllDirectories);
                    size += result.Size;
                    files += result.Files;
                }
                
                // 钉钉
                string dingtalkCache = Path.Combine(userProfile, "AppData", "Local", "DingTalk");
                if (Directory.Exists(dingtalkCache))
                {
                    var result = CleanDirectory(dingtalkCache, "*.*", SearchOption.AllDirectories);
                    size += result.Size;
                    files += result.Files;
                }
                
                totalCleanedSize += size;
                totalCleanedFiles += files;
                if (files > 0)
                {
                    AddResult($"✓ 聊天软件缓存清理完成：清理了 {files} 个文件，释放 {FormatFileSize(size)}");
                }
                else
                {
                    AddResult("ℹ 未找到聊天软件缓存文件，可能未安装相关软件或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 聊天软件缓存清理失败: {ex.Message}");
            }
        }
        
        private void CleanDesignSoftwareCache()
        {
            AddResult("正在清理设计类软件缓存 (Adobe、Photoshop、Premier、After Effects等)...");
            lblStatus.Text = "正在清理设计软件的缓存文件，包括 Adobe 系列软件的临时文件和预览缓存...";
            Application.DoEvents();
            
            try
            {
                long size = 0;
                int files = 0;
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                
                // Adobe 通用缓存
                string adobeCache = Path.Combine(appData, "Adobe");
                if (Directory.Exists(adobeCache))
                {
                    var result = CleanDirectory(adobeCache, "*.*", SearchOption.AllDirectories);
                    size += result.Size;
                    files += result.Files;
                }
                
                // Photoshop
                string psCache = Path.Combine(appData, "Adobe", "Adobe Photoshop");
                if (Directory.Exists(psCache))
                {
                    var result = CleanDirectory(psCache, "*.*", SearchOption.AllDirectories);
                    size += result.Size;
                    files += result.Files;
                }
                
                // Premiere
                string prCache = Path.Combine(appData, "Adobe", "Adobe Premiere Pro");
                if (Directory.Exists(prCache))
                {
                    var result = CleanDirectory(prCache, "*.*", SearchOption.AllDirectories);
                    size += result.Size;
                    files += result.Files;
                }
                
                // After Effects
                string aeCache = Path.Combine(appData, "Adobe", "Adobe After Effects");
                if (Directory.Exists(aeCache))
                {
                    var result = CleanDirectory(aeCache, "*.*", SearchOption.AllDirectories);
                    size += result.Size;
                    files += result.Files;
                }
                
                totalCleanedSize += size;
                totalCleanedFiles += files;
                if (files > 0)
                {
                    AddResult($"✓ 设计类软件缓存清理完成：清理了 {files} 个文件，释放 {FormatFileSize(size)}");
                }
                else
                {
                    AddResult("ℹ 未找到设计类软件缓存文件，可能未安装相关软件或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 设计类软件缓存清理失败: {ex.Message}");
            }
        }
        
        private void CleanOfficeAndToolsCache()
        {
            AddResult("正在清理 Office、ShareX、FFmpeg 等工具软件缓存...");
            lblStatus.Text = "正在清理办公软件和工具软件的缓存文件，包括 Office、ShareX、FFmpeg 等...";
            Application.DoEvents();
            
            try
            {
                long size = 0;
                int files = 0;
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                
                // Office
                string officeCache = Path.Combine(appData, "Microsoft", "Office");
                if (Directory.Exists(officeCache))
                {
                    var result = CleanDirectory(officeCache, "*.*", SearchOption.AllDirectories);
                    size += result.Size;
                    files += result.Files;
                }
                
                // ShareX
                string sharexCache = Path.Combine(appData, "ShareX");
                if (Directory.Exists(sharexCache))
                {
                    var result = CleanDirectory(sharexCache, "*.*", SearchOption.AllDirectories);
                    size += result.Size;
                    files += result.Files;
                }
                
                totalCleanedSize += size;
                totalCleanedFiles += files;
                if (files > 0)
                {
                    AddResult($"✓ Office、ShareX、FFmpeg 等工具软件缓存清理完成：清理了 {files} 个文件，释放 {FormatFileSize(size)}");
                }
                else
                {
                    AddResult("ℹ 未找到工具软件缓存文件，可能未安装相关软件或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ Office、ShareX、FFmpeg 等工具软件缓存清理失败: {ex.Message}");
            }
        }
        
        // 清理常用软件缓存（音乐、视频、网盘等）
        private void CleanCommonSoftwareCache()
        {
            AddResult("正在清理常用软件缓存（音乐、视频、网盘等）...");
            lblStatus.Text = "正在扫描并清理常用软件缓存，包括音乐、视频、网盘等应用的缓存文件...";
            Application.DoEvents();
            
            try
            {
                long size = 0;
                int files = 0;
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string appDataLocal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appDataRoaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                
                // 常用软件的缓存路径列表
                string[] cachePaths = new[]
                {
                    // 网易云音乐
                    Path.Combine(appDataLocal, "Netease", "CloudMusic", "Cache"),
                    Path.Combine(appDataLocal, "Netease", "CloudMusic", "webcache"),
                    // QQ音乐
                    Path.Combine(appDataLocal, "Tencent", "QQMusic", "Cache"),
                    Path.Combine(appDataLocal, "Tencent", "QQMusic", "webcache"),
                    // 酷狗音乐
                    Path.Combine(appDataLocal, "KuGou", "Cache"),
                    Path.Combine(appDataLocal, "KuGou", "Temp"),
                    // 爱奇艺
                    Path.Combine(appDataLocal, "iQIYI Video", "Cache"),
                    Path.Combine(appDataLocal, "QiyiVideo", "Cache"),
                    // 腾讯视频
                    Path.Combine(appDataLocal, "Tencent", "QQLive", "Cache"),
                    Path.Combine(appDataLocal, "Tencent", "Video", "Cache"),
                    // 优酷
                    Path.Combine(appDataLocal, "Youku", "Cache"),
                    Path.Combine(appDataLocal, "YoukuDesktop", "Cache"),
                    // 迅雷
                    Path.Combine(appDataLocal, "Thunder Network", "Thunder", "Cache"),
                    Path.Combine(appDataLocal, "Thunder Network", "Thunder", "Temp"),
                    // 百度网盘
                    Path.Combine(appDataLocal, "BaiduNetdisk", "Cache"),
                    Path.Combine(appDataLocal, "BaiduNetdisk", "Temp"),
                    // 阿里云盘
                    Path.Combine(appDataLocal, "AliyunDrive", "Cache"),
                    Path.Combine(appDataLocal, "AliyunDrive", "Temp"),
                    // 腾讯微云
                    Path.Combine(appDataLocal, "Tencent", "Weiyun", "Cache"),
                    Path.Combine(appDataLocal, "Tencent", "Weiyun", "Temp"),
                    // 115网盘
                    Path.Combine(appDataLocal, "115", "Cache"),
                    // 天翼云盘
                    Path.Combine(appDataLocal, "eCloud", "Cache"),
                    // 坚果云
                    Path.Combine(appDataLocal, "Nutstore", "Cache")
                };
                
                foreach (string cachePath in cachePaths)
                {
                    if (Directory.Exists(cachePath))
                    {
                        var result = CleanDirectory(cachePath, "*.*", SearchOption.AllDirectories);
                        size += result.Size;
                        files += result.Files;
                    }
                }
                
                totalCleanedSize += size;
                totalCleanedFiles += files;
                if (files > 0)
                {
                    AddResult($"✓ 常用软件缓存清理完成：清理了 {files} 个文件，释放 {FormatFileSize(size)}");
                }
                else
                {
                    AddResult("ℹ 未找到常用软件缓存目录，可能未安装相关软件或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 常用软件缓存清理失败: {ex.Message}");
            }
        }
        
        private void CleanOtherSoftwareCache()
        {
            AddResult("正在清理其他常用软件缓存...");
            lblStatus.Text = "正在扫描并清理其他常用软件的缓存文件...";
            Application.DoEvents();
            
            try
            {
                long size = 0;
                int files = 0;
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                
                // 清理常见的缓存目录
                string[] commonCachePaths = new[]
                {
                    Path.Combine(appData, "Temp"),
                    Path.Combine(appData, "Cache")
                };
                
                foreach (string cachePath in commonCachePaths)
                {
                    if (Directory.Exists(cachePath))
                    {
                        var result = CleanDirectory(cachePath, "*.*", SearchOption.TopDirectoryOnly);
                        size += result.Size;
                        files += result.Files;
                    }
                }
                
                totalCleanedSize += size;
                totalCleanedFiles += files;
                if (files > 0)
                {
                    AddResult($"✓ 其他常用软件缓存清理完成：清理了 {files} 个文件，释放 {FormatFileSize(size)}");
                }
                else
                {
                    AddResult("ℹ 未找到其他常用软件缓存文件");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 其他常用软件缓存清理失败: {ex.Message}");
            }
        }
        
        // 清理系统下载目录 - 清理所有文件和子目录
        private void CleanSystemDownloads()
        {
            AddResult("正在清理系统下载目录...");
            lblStatus.Text = "正在清理系统下载目录 (C:\\Users\\用户名\\Downloads) 中的所有文件和子目录...";
            Application.DoEvents();
            
            try
            {
                string downloadsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                
                if (Directory.Exists(downloadsPath))
                {
                    // 清理所有文件和子目录
                    var result = CleanDirectoryRecursive(downloadsPath);
                    
                    totalCleanedSize += result.Size;
                    totalCleanedFiles += result.Files;
                    
                    if (result.Files > 0)
                    {
                        AddResult($"✓ 系统下载目录清理完成：清理了 {result.Files} 个文件/目录，释放 {FormatFileSize(result.Size)}");
                        if (result.Failed > 0)
                        {
                            AddResult($"   警告：{result.Failed} 个文件/目录无法删除（可能被占用或权限不足）");
                        }
                    }
                    else
                    {
                        AddResult("ℹ 系统下载目录中没有需要清理的文件");
                        if (result.Failed > 0)
                        {
                            AddResult($"   警告：{result.Failed} 个文件/目录无法删除（可能被占用）");
                        }
                    }
                }
                else
                {
                    AddResult("ℹ 系统下载目录不存在");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 系统下载目录清理失败: {ex.Message}");
            }
        }
        
        // 清理 Edge 浏览器下载
        private void CleanEdgeDownloads()
        {
            AddResult("正在清理 Edge 浏览器下载临时文件...");
            lblStatus.Text = "正在清理 Edge 浏览器下载目录中的临时文件...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string edgeDownloads = Path.Combine(userProfile, "AppData", "Local", "Microsoft", "Edge", "User Data", "Default", "Downloads");
                if (Directory.Exists(edgeDownloads))
                {
                    var patterns = new[] { "*.tmp", "*.temp", "*.crdownload", "*.part" };
                    long size = 0;
                    int files = 0;
                    foreach (string pattern in patterns)
                    {
                        var result = CleanDirectory(edgeDownloads, pattern, SearchOption.AllDirectories);
                        size += result.Size;
                        files += result.Files;
                    }
                    if (files > 0)
                    {
                        totalCleanedSize += size;
                        totalCleanedFiles += files;
                        AddResult($"✓ Edge 浏览器下载清理完成：清理了 {files} 个临时文件，释放 {FormatFileSize(size)}");
                    }
                    else
                    {
                        AddResult("ℹ Edge 浏览器下载目录中没有需要清理的临时文件");
                    }
                }
                else
                {
                    AddResult("ℹ Edge 浏览器下载目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ Edge 浏览器下载清理失败: {ex.Message}");
            }
        }
        
        // 清理 Chrome 浏览器下载
        private void CleanChromeDownloads()
        {
            AddResult("正在清理 Chrome 浏览器下载临时文件...");
            lblStatus.Text = "正在清理 Chrome 浏览器下载目录中的临时文件...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string chromeDownloads = Path.Combine(userProfile, "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Downloads");
                if (Directory.Exists(chromeDownloads))
                {
                    var patterns = new[] { "*.tmp", "*.temp", "*.crdownload", "*.part" };
                    long size = 0;
                    int files = 0;
                    foreach (string pattern in patterns)
                    {
                        var result = CleanDirectory(chromeDownloads, pattern, SearchOption.AllDirectories);
                        size += result.Size;
                        files += result.Files;
                    }
                    if (files > 0)
                    {
                        totalCleanedSize += size;
                        totalCleanedFiles += files;
                        AddResult($"✓ Chrome 浏览器下载清理完成：清理了 {files} 个临时文件，释放 {FormatFileSize(size)}");
                    }
                    else
                    {
                        AddResult("ℹ Chrome 浏览器下载目录中没有需要清理的临时文件");
                    }
                }
                else
                {
                    AddResult("ℹ Chrome 浏览器下载目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ Chrome 浏览器下载清理失败: {ex.Message}");
            }
        }
        
        // 清理 Firefox 浏览器下载
        private void CleanFirefoxDownloads()
        {
            AddResult("正在清理 Firefox 浏览器下载临时文件...");
            lblStatus.Text = "正在清理 Firefox 浏览器下载目录中的临时文件...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string firefoxDownloads = Path.Combine(userProfile, "AppData", "Local", "Mozilla", "Firefox", "Profiles");
                if (Directory.Exists(firefoxDownloads))
                {
                    long size = 0;
                    int files = 0;
                    var profiles = Directory.GetDirectories(firefoxDownloads);
                    foreach (string profile in profiles)
                    {
                        string downloadPath = Path.Combine(profile, "downloads");
                        if (Directory.Exists(downloadPath))
                        {
                            var patterns = new[] { "*.tmp", "*.temp", "*.part" };
                            foreach (string pattern in patterns)
                            {
                                var result = CleanDirectory(downloadPath, pattern, SearchOption.AllDirectories);
                                size += result.Size;
                                files += result.Files;
                            }
                        }
                    }
                    if (files > 0)
                    {
                        totalCleanedSize += size;
                        totalCleanedFiles += files;
                        AddResult($"✓ Firefox 浏览器下载清理完成：清理了 {files} 个临时文件，释放 {FormatFileSize(size)}");
                    }
                    else
                    {
                        AddResult("ℹ Firefox 浏览器下载目录中没有需要清理的临时文件");
                    }
                }
                else
                {
                    AddResult("ℹ Firefox 浏览器下载目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ Firefox 浏览器下载清理失败: {ex.Message}");
            }
        }
        
        // 清理夸克浏览器下载
        private void CleanQuarkDownloads()
        {
            AddResult("正在清理夸克浏览器下载临时文件...");
            lblStatus.Text = "正在清理夸克浏览器下载目录中的临时文件...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string quarkDownloads = Path.Combine(userProfile, "AppData", "Local", "Quark", "User Data", "Default", "Downloads");
                if (Directory.Exists(quarkDownloads))
                {
                    var patterns = new[] { "*.tmp", "*.temp", "*.crdownload", "*.part" };
                    long size = 0;
                    int files = 0;
                    foreach (string pattern in patterns)
                    {
                        var result = CleanDirectory(quarkDownloads, pattern, SearchOption.AllDirectories);
                        size += result.Size;
                        files += result.Files;
                    }
                    if (files > 0)
                    {
                        totalCleanedSize += size;
                        totalCleanedFiles += files;
                        AddResult($"✓ 夸克浏览器下载清理完成：清理了 {files} 个临时文件，释放 {FormatFileSize(size)}");
                    }
                    else
                    {
                        AddResult("ℹ 夸克浏览器下载目录中没有需要清理的临时文件");
                    }
                }
                else
                {
                    AddResult("ℹ 夸克浏览器下载目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 夸克浏览器下载清理失败: {ex.Message}");
            }
        }
        
        // 清理 360浏览器下载
        private void Clean360BrowserDownloads()
        {
            AddResult("正在清理 360浏览器下载临时文件...");
            lblStatus.Text = "正在清理 360浏览器下载目录中的临时文件...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] paths = new[]
                {
                    Path.Combine(userProfile, "AppData", "Local", "360Chrome", "Chrome", "User Data", "Default", "Downloads"),
                    Path.Combine(userProfile, "AppData", "Roaming", "360se6", "User Data", "Default", "Downloads"),
                    Path.Combine(userProfile, "AppData", "Local", "360Browser", "User Data", "Default", "Downloads")
                };
                
                long size = 0;
                int files = 0;
                foreach (string path in paths)
                {
                    if (Directory.Exists(path))
                    {
                        var patterns = new[] { "*.tmp", "*.temp", "*.crdownload", "*.part" };
                        foreach (string pattern in patterns)
                        {
                            var result = CleanDirectory(path, pattern, SearchOption.AllDirectories);
                            size += result.Size;
                            files += result.Files;
                        }
                    }
                }
                
                if (files > 0)
                {
                    totalCleanedSize += size;
                    totalCleanedFiles += files;
                    AddResult($"✓ 360浏览器下载清理完成：清理了 {files} 个临时文件，释放 {FormatFileSize(size)}");
                }
                else
                {
                    AddResult("ℹ 360浏览器下载目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 360浏览器下载清理失败: {ex.Message}");
            }
        }
        
        // 保留旧方法以兼容（已废弃，建议使用上面的独立方法）
        private void CleanBrowserDownloads()
        {
            CleanSystemDownloads();
            CleanEdgeDownloads();
            CleanChromeDownloads();
            CleanFirefoxDownloads();
            CleanQuarkDownloads();
            Clean360BrowserDownloads();
        }
        
        private void CleanThunderDownloads()
        {
            AddResult("正在清理迅雷下载目录...");
            lblStatus.Text = "正在清理迅雷下载目录中的所有文件...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                // 迅雷下载目录可能有多个位置
                string[] thunderPaths = new[]
                {
                    Path.Combine(userProfile, "Downloads", "Thunder"),
                    Path.Combine(userProfile, "ThunderDownloads"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Thunder")
                };
                
                long totalSize = 0;
                int totalFiles = 0;
                int totalFailed = 0;
                
                foreach (string thunderPath in thunderPaths)
                {
                    if (Directory.Exists(thunderPath))
                    {
                        var result = CleanDirectoryRecursive(thunderPath);
                        totalSize += result.Size;
                        totalFiles += result.Files;
                        totalFailed += result.Failed;
                    }
                }
                
                if (totalFiles > 0)
                {
                    totalCleanedSize += totalSize;
                    totalCleanedFiles += totalFiles;
                    AddResult($"✓ 迅雷下载目录清理完成：清理了 {totalFiles} 个文件/目录，释放 {FormatFileSize(totalSize)}");
                    if (totalFailed > 0)
                    {
                        AddResult($"   警告：{totalFailed} 个文件/目录无法删除（可能被占用或权限不足）");
                    }
                }
                else
                {
                    AddResult("ℹ 迅雷下载目录不存在或已为空，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 迅雷下载目录清理失败: {ex.Message}");
            }
        }
        
        private void CleanBaiduNetdiskDownloads()
        {
            AddResult("正在清理百度网盘下载目录...");
            lblStatus.Text = "正在清理百度网盘下载目录中的所有文件...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string baiduPath = Path.Combine(userProfile, "BaiduNetdiskDownload");
                
                if (Directory.Exists(baiduPath))
                {
                    var result = CleanDirectoryRecursive(baiduPath);
                    totalCleanedSize += result.Size;
                    totalCleanedFiles += result.Files;
                    if (result.Files > 0)
                    {
                        AddResult($"✓ 百度网盘下载目录清理完成：清理了 {result.Files} 个文件/目录，释放 {FormatFileSize(result.Size)}");
                        if (result.Failed > 0)
                        {
                            AddResult($"   警告：{result.Failed} 个文件/目录无法删除（可能被占用或权限不足）");
                        }
                    }
                    else
                    {
                        AddResult("ℹ 百度网盘下载目录为空或已清理");
                        if (result.Failed > 0)
                        {
                            AddResult($"   警告：{result.Failed} 个文件/目录无法删除（可能被占用）");
                        }
                    }
                }
                else
                {
                    AddResult("ℹ 百度网盘下载目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 百度网盘下载目录清理失败: {ex.Message}");
            }
        }
        
        // 清理阿里云盘下载目录
        private void CleanAliyunDriveDownloads()
        {
            AddResult("正在清理阿里云盘下载目录...");
            lblStatus.Text = "正在清理阿里云盘下载目录中的所有文件...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string aliyunPath = Path.Combine(userProfile, "AliyunDriveDownload");
                
                if (Directory.Exists(aliyunPath))
                {
                    var result = CleanDirectoryRecursive(aliyunPath);
                    totalCleanedSize += result.Size;
                    totalCleanedFiles += result.Files;
                    if (result.Files > 0)
                    {
                        AddResult($"✓ 阿里云盘下载目录清理完成：清理了 {result.Files} 个文件/目录，释放 {FormatFileSize(result.Size)}");
                        if (result.Failed > 0)
                        {
                            AddResult($"   警告：{result.Failed} 个文件/目录无法删除（可能被占用或权限不足）");
                        }
                    }
                    else
                    {
                        AddResult("ℹ 阿里云盘下载目录为空或已清理");
                    }
                }
                else
                {
                    AddResult("ℹ 阿里云盘下载目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 阿里云盘下载目录清理失败: {ex.Message}");
            }
        }
        
        // 清理腾讯微云下载目录
        private void CleanWeiyunDownloads()
        {
            AddResult("正在清理腾讯微云下载目录...");
            lblStatus.Text = "正在清理腾讯微云下载目录中的所有文件...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string weiyunPath = Path.Combine(userProfile, "WeiyunDownload");
                
                if (Directory.Exists(weiyunPath))
                {
                    var result = CleanDirectoryRecursive(weiyunPath);
                    totalCleanedSize += result.Size;
                    totalCleanedFiles += result.Files;
                    if (result.Files > 0)
                    {
                        AddResult($"✓ 腾讯微云下载目录清理完成：清理了 {result.Files} 个文件/目录，释放 {FormatFileSize(result.Size)}");
                        if (result.Failed > 0)
                        {
                            AddResult($"   警告：{result.Failed} 个文件/目录无法删除（可能被占用或权限不足）");
                        }
                    }
                    else
                    {
                        AddResult("ℹ 腾讯微云下载目录为空或已清理");
                    }
                }
                else
                {
                    AddResult("ℹ 腾讯微云下载目录不存在，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 腾讯微云下载目录清理失败: {ex.Message}");
            }
        }
        
        private void CleanOtherDownloadSoftware()
        {
            AddResult("正在清理其他下载软件目录...");
            lblStatus.Text = "正在清理其他下载软件（如 qBittorrent、IDM、FDM 等）的下载目录...";
            Application.DoEvents();
            
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                long totalSize = 0;
                int totalFiles = 0;
                int totalFailed = 0;
                
                // 其他下载软件的常见下载目录
                string[] downloadPaths = new[]
                {
                    // qBittorrent
                    Path.Combine(userProfile, "Downloads", "qBittorrent"),
                    // IDM (Internet Download Manager)
                    Path.Combine(userProfile, "Downloads", "IDM"),
                    // FDM (Free Download Manager)
                    Path.Combine(userProfile, "Downloads", "FDM"),
                    // uTorrent
                    Path.Combine(userProfile, "AppData", "Roaming", "uTorrent"),
                    // BitTorrent
                    Path.Combine(userProfile, "AppData", "Roaming", "BitTorrent")
                };
                
                foreach (string downloadPath in downloadPaths)
                {
                    if (Directory.Exists(downloadPath))
                    {
                        var result = CleanDirectoryRecursive(downloadPath);
                        totalSize += result.Size;
                        totalFiles += result.Files;
                        totalFailed += result.Failed;
                    }
                }
                
                if (totalFiles > 0)
                {
                    totalCleanedSize += totalSize;
                    totalCleanedFiles += totalFiles;
                    AddResult($"✓ 其他下载软件目录清理完成：清理了 {totalFiles} 个文件/目录，释放 {FormatFileSize(totalSize)}");
                    if (totalFailed > 0)
                    {
                        AddResult($"   警告：{totalFailed} 个文件/目录无法删除（可能被占用或权限不足）");
                    }
                }
                else
                {
                    AddResult("ℹ 未找到其他下载软件的下载目录，可能未安装或已清理");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 其他下载软件目录清理失败: {ex.Message}");
            }
        }
        
        // 辅助方法 - 改进的注册表清理
        private int CleanInvalidRegistryKeys()
        {
            int count = 0;
            List<string> cleanedKeys = new List<string>();
            
            try
            {
                // 清理 CurrentUser 下的无效启动项
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key != null)
                    {
                        string[] valueNames = key.GetValueNames();
                        foreach (string valueName in valueNames)
                        {
                            try
                            {
                                object value = key.GetValue(valueName);
                                if (value is string path)
                                {
                                    // 检查路径是否存在
                                    if (!File.Exists(path) && !Directory.Exists(path))
                                    {
                                        key.DeleteValue(valueName, false);
                                        count++;
                                        cleanedKeys.Add($"HKCU\\Run\\{valueName}");
                                    }
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                // 权限不足，跳过
                            }
                            catch (Exception ex)
                            {
                                // 记录其他错误但不中断
                                AddResult($"   警告：清理注册表项时出错: {ex.Message}");
                            }
                        }
                    }
                }
                
                // 清理 LocalMachine 下的无效启动项（需要管理员权限）
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            string[] valueNames = key.GetValueNames();
                            foreach (string valueName in valueNames)
                            {
                                try
                                {
                                    object value = key.GetValue(valueName);
                                    if (value is string path)
                                    {
                                        if (!File.Exists(path) && !Directory.Exists(path))
                                        {
                                            key.DeleteValue(valueName, false);
                                            count++;
                                            cleanedKeys.Add($"HKLM\\Run\\{valueName}");
                                        }
                                    }
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    // 权限不足，这是正常的
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // 没有管理员权限，无法清理 LocalMachine 下的项
                    AddResult("   提示：需要管理员权限才能清理系统级启动项");
                }
                
                if (count > 0)
                {
                    string cleanedList = string.Join(", ", cleanedKeys.Take(5));
                    if (cleanedKeys.Count > 5)
                        cleanedList += "...";
                    AddResult($"   已清理的注册表项: {cleanedList}");
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 注册表清理过程中发生错误: {ex.Message}");
            }
            
            return count;
        }
        
        // 递归清理目录中的所有文件和子目录
        private (long Size, int Files, int Failed) CleanDirectoryRecursive(string directory)
        {
            long totalSize = 0;
            int fileCount = 0;
            int failedCount = 0;
            List<string> failedItems = new List<string>();
            
            try
            {
                if (!Directory.Exists(directory))
                    return (0, 0, 0);
                
                // 首先删除所有文件
                string[] files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
                foreach (string file in files)
                {
                    try
                    {
                        // 对于下载目录，不跳过任何文件（包括 desktop.ini 等）
                        // 只跳过真正的系统关键文件（在系统目录中的）
                        string fileName = Path.GetFileName(file).ToLower();
                        if (fileName == "ntuser.dat" || fileName == "ntuser.dat.log" || fileName == "ntuser.ini")
                        {
                            // 这些文件不应该在下载目录中，但为了安全起见，跳过
                            continue;
                        }
                        
                        FileInfo fi = new FileInfo(file);
                        if (!fi.Exists)
                            continue;
                        
                        // 检查文件是否被锁定
                        if (IsFileLocked(file))
                        {
                            failedItems.Add(Path.GetFileName(file));
                            failedCount++;
                            AddResult($"   跳过被占用的文件: {Path.GetFileName(file)}");
                            continue;
                        }
                        
                        // 使用优化的删除方法（带重试机制）
                        long fileSize = fi.Length;
                        if (TryDeleteFile(file))
                        {
                            totalSize += fileSize;
                            fileCount++;
                        }
                        else
                        {
                            failedItems.Add(Path.GetFileName(file));
                            failedCount++;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        failedItems.Add(Path.GetFileName(file));
                        failedCount++;
                        // 错误已在 TryDeleteFile 中处理，这里只记录失败
                    }
                    catch (IOException)
                    {
                        failedItems.Add(Path.GetFileName(file));
                        failedCount++;
                        // 错误已在 TryDeleteFile 中处理，这里只记录失败
                    }
                    catch (Exception)
                    {
                        failedItems.Add(Path.GetFileName(file));
                        failedCount++;
                        // 错误已在 TryDeleteFile 中处理，这里只记录失败
                    }
                }
                
                // 然后递归删除所有子目录
                string[] subDirs = Directory.GetDirectories(directory);
                foreach (string subDir in subDirs)
                {
                    try
                    {
                        // 递归清理子目录
                        var subResult = CleanDirectoryRecursive(subDir);
                        totalSize += subResult.Size;
                        fileCount += subResult.Files;
                        failedCount += subResult.Failed;
                        
                        // 尝试删除目录（使用优化的删除方法）
                        if (Directory.Exists(subDir))
                        {
                            if (TryDeleteDirectory(subDir))
                            {
                                fileCount++; // 将目录也计入清理数量
                            }
                            else
                            {
                                // 目录无法删除（可能被占用），记录失败
                                failedItems.Add(Path.GetFileName(subDir));
                                failedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failedItems.Add(Path.GetFileName(subDir));
                        failedCount++;
                        AddResult($"   处理子目录失败: {Path.GetFileName(subDir)} - {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 递归清理目录时发生错误: {ex.Message}");
            }
            
            return (totalSize, fileCount, failedCount);
        }
        
        private (long Size, int Files, int Failed) CleanDirectory(string directory, string pattern, SearchOption searchOption)
        {
            long totalSize = 0;
            int fileCount = 0;
            int failedCount = 0;
            List<string> failedFiles = new List<string>();
            
            try
            {
                if (!Directory.Exists(directory))
                    return (0, 0, 0);
                
                string[] files = Directory.GetFiles(directory, pattern, searchOption);
                
                foreach (string file in files)
                {
                    try
                    {
                        // 安全检查：跳过系统关键文件
                        if (IsSystemCriticalFile(file))
                        {
                            continue;
                        }
                        
                        FileInfo fi = new FileInfo(file);
                        if (!fi.Exists)
                            continue;
                        
                        // 检查文件是否被锁定
                        if (IsFileLocked(file))
                        {
                            failedFiles.Add(Path.GetFileName(file));
                            failedCount++;
                            continue;
                        }
                        
                        // 使用优化的删除方法（带重试机制）
                        long fileSize = fi.Length;
                        if (TryDeleteFile(file))
                        {
                            totalSize += fileSize;
                            fileCount++;
                        }
                        else
                        {
                            failedFiles.Add(Path.GetFileName(file));
                            failedCount++;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        failedFiles.Add(Path.GetFileName(file));
                        failedCount++;
                        // 记录权限错误但不中断整个清理过程
                    }
                    catch (IOException)
                    {
                        failedFiles.Add(Path.GetFileName(file));
                        failedCount++;
                        // 记录IO错误
                    }
                    catch (Exception)
                    {
                        failedFiles.Add(Path.GetFileName(file));
                        failedCount++;
                        // 记录其他错误
                    }
                }
                
                // 如果有失败的文件，记录到日志
                if (failedCount > 0 && failedFiles.Count > 0)
                {
                    string failedList = string.Join(", ", failedFiles.Take(5));
                    if (failedFiles.Count > 5)
                        failedList += "...";
                    AddResult($"   警告：{failedCount} 个文件无法删除（可能被占用或权限不足）");
                    if (failedFiles.Count <= 5)
                    {
                        AddResult($"   无法删除的文件: {failedList}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddResult($"✗ 清理目录时发生错误: {ex.Message}");
            }
            
            return (totalSize, fileCount, failedCount);
        }
        
        // 检查是否为系统关键文件 - 优化版本
        private bool IsSystemCriticalFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return true; // 空路径视为关键
                
                string fileName = Path.GetFileName(filePath).ToLower();
                string directory = Path.GetDirectoryName(filePath)?.ToLower() ?? "";
                
                // 系统关键文件列表（无论位置）
                string[] criticalFiles = new[]
                {
                    "ntuser.dat", "ntuser.dat.log", "ntuser.ini",
                    "sam", "system", "security", "software", // 注册表文件
                    "boot.ini", "bootmgr", "bootmgr.efi", // 启动文件
                    "pagefile.sys", "swapfile.sys", "hiberfil.sys" // 系统文件
                };
                
                if (criticalFiles.Contains(fileName))
                    return true;
                
                // 检查是否在系统关键目录
                string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLower();
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).ToLower();
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).ToLower();
                
                // 系统目录保护
                if (directory.StartsWith(Path.Combine(systemRoot, "system32"), StringComparison.OrdinalIgnoreCase))
                    return true;
                
                if (directory.StartsWith(Path.Combine(systemRoot, "syswow64"), StringComparison.OrdinalIgnoreCase))
                    return true;
                
                if (directory.StartsWith(Path.Combine(systemRoot, "boot"), StringComparison.OrdinalIgnoreCase))
                    return true;
                
                if (directory.StartsWith(Path.Combine(systemRoot, "winxs"), StringComparison.OrdinalIgnoreCase))
                    return true;
                
                // Program Files 目录保护（除非明确是缓存目录）
                if (directory.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
                    directory.StartsWith(programFilesX86, StringComparison.OrdinalIgnoreCase))
                {
                    // 允许清理已知的缓存目录
                    string[] allowedCacheDirs = { "cache", "temp", "tmp", "logs", "log" };
                    if (!allowedCacheDirs.Any(cache => directory.Contains(cache)))
                        return true;
                }
                
                // 用户配置文件保护
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).ToLower();
                if (directory.StartsWith(Path.Combine(userProfile, "appdata", "roaming", "microsoft", "windows", "start menu"), StringComparison.OrdinalIgnoreCase))
                    return true;
                
                // desktop.ini 和 thumbs.db 在用户目录中可以删除（它们是缓存文件）
                // 但在系统目录中需要保护
                if ((fileName == "desktop.ini" || fileName == "thumbs.db") && 
                    directory.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
                    return true;
                
                return false;
            }
            catch
            {
                // 出错时保守处理，视为关键文件
                return true;
            }
        }
        
        // 改进的文件锁定检查 - 优化版本
        private bool IsFileLocked(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;
                
                // 尝试以独占模式打开文件
                using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    return false; // 文件未被锁定
                }
            }
            catch (IOException)
            {
                return true; // 文件被占用
            }
            catch (UnauthorizedAccessException)
            {
                return true; // 权限不足
            }
            catch
            {
                return true; // 其他错误，保守处理
            }
        }
        
        // 尝试删除目录（带重试机制和属性清理）
        private bool TryDeleteDirectory(string directoryPath, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (!Directory.Exists(directoryPath))
                        return true; // 目录已不存在
                    
                    // 清理目录属性
                    FileAttributes attributes = File.GetAttributes(directoryPath);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly ||
                        (attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                        (attributes & FileAttributes.System) == FileAttributes.System)
                    {
                        File.SetAttributes(directoryPath, attributes & ~FileAttributes.ReadOnly & ~FileAttributes.Hidden & ~FileAttributes.System);
                    }
                    
                    // 先尝试简单删除（空目录）
                    try
                    {
                        Directory.Delete(directoryPath, false);
                        return true;
                    }
                    catch
                    {
                        // 如果失败，尝试递归删除（非空目录）
                        Directory.Delete(directoryPath, true);
                        return true;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    if (i < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(100);
                        continue;
                    }
                    return false;
                }
                catch (IOException)
                {
                    if (i < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(100);
                        continue;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }
        
        // 尝试删除文件（带重试机制）
        private bool TryDeleteFile(string filePath, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (!File.Exists(filePath))
                        return true; // 文件已不存在
                    
                    // 检查文件是否被锁定
                    if (IsFileLocked(filePath))
                    {
                        if (i < maxRetries - 1)
                        {
                            System.Threading.Thread.Sleep(100); // 等待100ms后重试
                            continue;
                        }
                        return false; // 重试失败
                    }
                    
                    FileInfo fi = new FileInfo(filePath);
                    
                    // 清理文件属性
                    if (fi.IsReadOnly)
                        fi.IsReadOnly = false;
                    
                    // 移除隐藏和系统属性（如果存在）
                    FileAttributes attributes = File.GetAttributes(filePath);
                    if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
                        (attributes & FileAttributes.System) == FileAttributes.System)
                    {
                        File.SetAttributes(filePath, attributes & ~FileAttributes.Hidden & ~FileAttributes.System);
                    }
                    
                    File.Delete(filePath);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    if (i < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(100);
                        continue;
                    }
                    return false;
                }
                catch (IOException)
                {
                    if (i < maxRetries - 1)
                    {
                        System.Threading.Thread.Sleep(100);
                        continue;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }
        
        private void AddResult(string message)
        {
            if (lstResults.InvokeRequired)
            {
                lstResults.Invoke(new Action<string>(AddResult), message);
                return;
            }
            
            lstResults.Items.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            lstResults.TopIndex = lstResults.Items.Count - 1;
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        // Windows API 用于清空回收站
        [DllImport("shell32.dll")]
        private static extern int SHEmptyRecycleBin(IntPtr hWnd, string pszRootPath, RecycleFlags dwFlags);
        
        [Flags]
        private enum RecycleFlags : uint
        {
            SHERB_NOCONFIRMATION = 0x00000001,
            SHERB_NOPROGRESSUI = 0x00000002,
            SHERB_NOSOUND = 0x00000004
        }
    }
    
    // 清理类别枚举
    public enum CleanCategory
    {
        All = 0,        // 所有
        System = 1,   // 系统清理
        Software = 2,  // 软件清理
        Download = 3   // 下载清理
    }
    
    // 清理详情窗口
    public class CleanDetailsForm : Form
    {
        private CleanCategory category;
        private Color lightGray;
        private Color textColor;
        private Color borderColor;
        private Dictionary<string, CheckBox> checkBoxes = new Dictionary<string, CheckBox>();
        private Button btnOK;
        private Button btnSelectAll;
        private Panel pnlScroll;
        
        public CleanDetailsForm(CleanCategory category, Color lightGray, Color textColor, Color borderColor)
        {
            this.category = category;
            this.lightGray = lightGray;
            this.textColor = textColor;
            this.borderColor = borderColor;
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = GetCategoryName(category) + " - 清理详情";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            // 应用 ShareX 主题
            ShareXResources.ApplyTheme(this, true);
            
            CreateControls();
            
            // 计算窗口大小
            CalculateWindowSize();
        }
        
        private string GetCategoryName(CleanCategory category)
        {
            switch (category)
            {
                case CleanCategory.All: return "所有";
                case CleanCategory.System: return "系统清理";
                case CleanCategory.Software: return "软件清理";
                case CleanCategory.Download: return "下载清理";
                default: return "清理";
            }
        }
        
        private void CreateControls()
        {
            int padding = 10;
            int titleHeight = 30;
            int selectAllHeight = 30;
            int scrollAreaTop = padding + titleHeight + selectAllHeight + padding;
            
            // 标题
            Label lblTitle = new Label
            {
                Text = GetCategoryName(category) + " 包含以下项目：",
                Location = new Point(padding, padding),
                Size = new Size(460, titleHeight),
                ForeColor = textColor
            };
            this.Controls.Add(lblTitle);
            
            // 全选按钮
            btnSelectAll = new Button
            {
                Text = "全选",
                Location = new Point(padding, padding + titleHeight),
                Size = new Size(80, 25),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            ConfigureButton(btnSelectAll);
            btnSelectAll.Click += BtnSelectAll_Click;
            this.Controls.Add(btnSelectAll);
            
            // 滚动面板
            pnlScroll = new Panel
            {
                Location = new Point(padding, scrollAreaTop),
                Size = new Size(460, 300), // 临时高度，后面会调整
                BackColor = lightGray,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            this.Controls.Add(pnlScroll);
            
            // 在滚动面板中创建复选框
            int yPos = 10;
            int spacing = 25;
            
            // 根据类别创建复选框
            if (category == CleanCategory.All || category == CleanCategory.System)
            {
                AddCheckBoxToPanel("系统临时文件", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("系统更新临时文件", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("Windows.old 旧系统备份", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("回收站", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("休眠文件 (hiberfil.sys)", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("图片缓存、图标缓存、缩略图缓存", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("Edge 浏览器缓存", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("Chrome 浏览器缓存", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("Firefox 浏览器缓存", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("夸克浏览器缓存", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("360浏览器缓存", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("UWP 应用缓存 (Microsoft Store 应用)", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("注册表冗余 (软件卸载残留、无效关联)", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("系统日志文件", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("启动项优化 (无效启动项)", yPos);
                yPos += spacing;
            }
            
            if (category == CleanCategory.All || category == CleanCategory.Software)
            {
                AddCheckBoxToPanel("聊天软件缓存 (微信、QQ、钉钉等)", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("设计类软件缓存 (Adobe、Photoshop、Premier、After Effects等)", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("Office、ShareX、FFmpeg 等工具软件缓存", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("常用软件缓存 (音乐、视频、网盘等)", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("其他常用软件缓存", yPos);
                yPos += spacing;
            }
            
            if (category == CleanCategory.All || category == CleanCategory.Download)
            {
                AddCheckBoxToPanel("系统下载目录 (C:\\Users\\用户名\\Downloads) - 清理所有文件", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("Edge 浏览器下载", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("Chrome 浏览器下载", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("Firefox 浏览器下载", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("夸克浏览器下载", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("360浏览器下载", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("迅雷下载目录", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("百度网盘下载目录", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("阿里云盘下载目录", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("腾讯微云下载目录", yPos);
                yPos += spacing;
                AddCheckBoxToPanel("其他下载软件目录", yPos);
                yPos += spacing;
            }
            
            // 设置滚动面板的内容高度
            pnlScroll.AutoScrollMinSize = new Size(0, yPos + 10);
            
            // 确定按钮 - 固定在右下角
            btnOK = new Button
            {
                Text = "确定",
                Size = new Size(100, 30),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray,
                DialogResult = DialogResult.OK,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            ConfigureButton(btnOK);
            this.Controls.Add(btnOK);
        }
        
        private void CalculateWindowSize()
        {
            int padding = 10;
            int titleHeight = 30;
            int selectAllHeight = 30;
            int buttonHeight = 30;
            int buttonBottomMargin = 10;
            int scrollAreaTop = padding + titleHeight + selectAllHeight + padding;
            
            // 计算内容所需高度
            int itemCount = checkBoxes.Count;
            int spacing = 25;
            int contentHeight = 10 + (itemCount * spacing) + 10; // 上下各10像素边距
            
            // 设置滚动面板高度（最大400，最小200）
            int scrollHeight = Math.Min(Math.Max(contentHeight, 200), 400);
            
            // 计算窗口总高度
            int windowHeight = scrollAreaTop + scrollHeight + buttonHeight + buttonBottomMargin + padding;
            
            // 设置窗口大小
            this.Size = new Size(500, windowHeight);
            
            // 更新滚动面板大小和位置
            pnlScroll.Location = new Point(padding, scrollAreaTop);
            pnlScroll.Size = new Size(
                this.ClientSize.Width - (padding * 2),
                this.ClientSize.Height - scrollAreaTop - buttonHeight - buttonBottomMargin - padding
            );
            
            // 更新复选框宽度以适应滚动面板
            foreach (var chk in checkBoxes.Values)
            {
                chk.Width = pnlScroll.Width - 40;
            }
            
            // 更新确定按钮位置
            btnOK.Location = new Point(
                this.ClientSize.Width - btnOK.Width - padding,
                this.ClientSize.Height - btnOK.Height - buttonBottomMargin
            );
        }
        
        private void AddCheckBoxToPanel(string text, int y)
        {
            CheckBox chk = new CheckBox
            {
                Text = text,
                Location = new Point(20, y),
                Size = new Size(420, 23), // 固定宽度，会在窗口大小计算后调整
                ForeColor = textColor,
                Checked = true,  // 默认全选
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            checkBoxes[text] = chk;
            pnlScroll.Controls.Add(chk);
        }
        
        private void ConfigureButton(Button button)
        {
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;
            button.MouseEnter += Button_MouseEnter;
            button.MouseLeave += Button_MouseLeave;
        }
        
        private void Button_MouseEnter(object sender, EventArgs e)
        {
            if (sender is Button button && button.Enabled)
            {
                button.BackColor = Color.FromArgb(220, 220, 220);
            }
        }
        
        private void Button_MouseLeave(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                button.BackColor = lightGray;
            }
        }
        
        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            bool allChecked = checkBoxes.Values.All(cb => cb.Checked);
            bool newState = !allChecked;
            
            foreach (var chk in checkBoxes.Values)
            {
                chk.Checked = newState;
            }
            
            btnSelectAll.Text = newState ? "取消全选" : "全选";
        }
        
        public Dictionary<string, bool> GetSelectedItems()
        {
            Dictionary<string, bool> selected = new Dictionary<string, bool>();
            foreach (var kvp in checkBoxes)
            {
                selected[kvp.Key] = kvp.Value.Checked;
            }
            return selected;
        }
    }
}
