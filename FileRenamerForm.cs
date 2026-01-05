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
using System.Windows.Forms;
using ShareX.HelpersLib;

namespace Sidebar
{
    public partial class FileRenamerForm : Form
    {
        // 常量
        private const int MAX_RENAME_ATTEMPTS = 10000;
        private const string DEFAULT_CUSTOM_NAME = "文件";
        private const string DEFAULT_FOLDER_NAME = "文件夹";
        
        private string selectedFolder = "";
        private List<FileInfo> files = new List<FileInfo>();
        private List<DirectoryInfo> directories = new List<DirectoryInfo>();
        
        // UI 控件
        private TextBox txtFolder;
        private Button btnBrowseFolder;
        private ListBox lstFiles;
        private GroupBox gbRenameRule;
        private RadioButton rbSuffixNumber;
        private RadioButton rbPrefixNumber;
        private RadioButton rbPrefixDateNumber;
        private RadioButton rbSuffixDateNumber;
        private CheckBox chkKeepOriginalName;
        private TextBox txtCustomName;
        private Label lblCustomName;
        private NumericUpDown nudStartNumber;
        private Label lblStartNumber;
        private NumericUpDown nudDigits;
        private Label lblDigits;
        private ComboBox cmbFileType;
        private Label lblFileType;
        private Button btnRename;
        private Button btnRefresh;
        private ProgressBar progressBar;
        private Label lblStatus;
        
        // 颜色变量（需要在类级别定义，以便在事件处理程序中使用）
        private Color lightGray;
        private Color textColor;
        private Color borderColor;
        
        public FileRenamerForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "文件重命名工具";
            this.Size = new Size(800, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            // 应用 ShareX 主题
            ShareXResources.ApplyTheme(this, true);
            
            // 设置控件背景颜色为浅灰色（符合主题）
            lightGray = ShareXResources.Theme?.LightBackgroundColor ?? Color.FromArgb(240, 240, 240);
            // 设置文字颜色为白色
            textColor = Color.White;
            // 设置边框颜色为浅灰色（1像素）
            borderColor = Color.FromArgb(200, 200, 200);
            
            // 文件夹选择区域
            Label lblFolder = new Label
            {
                Text = "选择文件夹:",
                Location = new Point(10, 10),
                Size = new Size(80, 23),
                AutoSize = false,
                ForeColor = textColor
            };
            this.Controls.Add(lblFolder);
            
            txtFolder = new TextBox
            {
                Location = new Point(95, 10),
                Size = new Size(500, 23),
                ReadOnly = true,
                AllowDrop = true,
                BackColor = lightGray,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtFolder.DragEnter += TxtFolder_DragEnter;
            txtFolder.DragDrop += TxtFolder_DragDrop;
            this.Controls.Add(txtFolder);
            
            btnBrowseFolder = new Button
            {
                Text = "浏览...",
                Location = new Point(600, 10),
                Size = new Size(75, 25),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            ConfigureButton(btnBrowseFolder);
            btnBrowseFolder.Click += BtnBrowseFolder_Click;
            this.Controls.Add(btnBrowseFolder);
            
            btnRefresh = new Button
            {
                Text = "刷新",
                Location = new Point(680, 10),
                Size = new Size(75, 25),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            ConfigureButton(btnRefresh);
            btnRefresh.Click += BtnRefresh_Click;
            this.Controls.Add(btnRefresh);
            
            // 文件列表
            Label lblFileList = new Label
            {
                Text = "文件列表:",
                Location = new Point(10, 40),
                Size = new Size(100, 23),
                ForeColor = textColor
            };
            this.Controls.Add(lblFileList);
            
            lstFiles = new ListBox
            {
                Location = new Point(10, 65),
                Size = new Size(750, 200),
                AllowDrop = true,
                BackColor = lightGray,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            lstFiles.DragEnter += LstFiles_DragEnter;
            lstFiles.DragDrop += LstFiles_DragDrop;
            this.Controls.Add(lstFiles);
            
            // 重命名规则区域
            gbRenameRule = new GroupBox
            {
                Text = "重命名规则",
                Location = new Point(10, 275),
                Size = new Size(750, 230),
                ForeColor = textColor
            };
            gbRenameRule.Paint += GbRenameRule_Paint;
            this.Controls.Add(gbRenameRule);
            
            rbSuffixNumber = new RadioButton
            {
                Text = "后缀序号 (文件名_001, 文件名_002, ...)",
                Location = new Point(10, 20),
                Size = new Size(300, 23),
                Checked = true,
                ForeColor = textColor
            };
            rbSuffixNumber.CheckedChanged += RenameRule_Changed;
            gbRenameRule.Controls.Add(rbSuffixNumber);
            
            rbPrefixNumber = new RadioButton
            {
                Text = "前缀序号 (001_文件名, 002_文件名, ...)",
                Location = new Point(10, 50),
                Size = new Size(300, 23),
                ForeColor = textColor
            };
            rbPrefixNumber.CheckedChanged += RenameRule_Changed;
            gbRenameRule.Controls.Add(rbPrefixNumber);
            
            rbPrefixDateNumber = new RadioButton
            {
                Text = "前缀日期序号 (001_20250101_文件名, 002_20250101_文件名, ...)",
                Location = new Point(10, 80),
                Size = new Size(500, 23),
                ForeColor = textColor
            };
            rbPrefixDateNumber.CheckedChanged += RenameRule_Changed;
            gbRenameRule.Controls.Add(rbPrefixDateNumber);
            
            rbSuffixDateNumber = new RadioButton
            {
                Text = "后缀日期序号 (文件名_20250101_001, 文件名_20250101_002, ...)",
                Location = new Point(10, 110),
                Size = new Size(500, 23),
                ForeColor = textColor
            };
            rbSuffixDateNumber.CheckedChanged += RenameRule_Changed;
            gbRenameRule.Controls.Add(rbSuffixDateNumber);
            
            chkKeepOriginalName = new CheckBox
            {
                Text = "保留原名称（在原有名称基础上添加前缀或后缀）",
                Location = new Point(10, 140),
                Size = new Size(500, 23),
                Checked = false,
                ForeColor = textColor
            };
            chkKeepOriginalName.CheckedChanged += ChkKeepOriginalName_Changed;
            gbRenameRule.Controls.Add(chkKeepOriginalName);
            
            // 注意：需要在所有控件创建后再调用 UpdateCustomNameVisibility
            // 所以这里先创建控件，稍后在初始化结束时调用
            
            lblCustomName = new Label
            {
                Text = "自定义名称:",
                Location = new Point(10, 170),
                Size = new Size(100, 23),
                ForeColor = textColor
            };
            gbRenameRule.Controls.Add(lblCustomName);
            
            txtCustomName = new TextBox
            {
                Location = new Point(115, 170),
                Size = new Size(200, 23),
                Text = "文件",
                BackColor = lightGray,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            gbRenameRule.Controls.Add(txtCustomName);
            
            lblStartNumber = new Label
            {
                Text = "起始序号:",
                Location = new Point(330, 170),
                Size = new Size(80, 23),
                ForeColor = textColor
            };
            gbRenameRule.Controls.Add(lblStartNumber);
            
            nudStartNumber = new NumericUpDown
            {
                Location = new Point(415, 170),
                Size = new Size(80, 23),
                Minimum = 1,
                Maximum = 999999,
                Value = 1,
                BackColor = lightGray,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            gbRenameRule.Controls.Add(nudStartNumber);
            
            lblDigits = new Label
            {
                Text = "位数:",
                Location = new Point(510, 170),
                Size = new Size(50, 23),
                ForeColor = textColor
            };
            gbRenameRule.Controls.Add(lblDigits);
            
            nudDigits = new NumericUpDown
            {
                Location = new Point(565, 170),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 10,
                Value = 3,
                BackColor = lightGray,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            gbRenameRule.Controls.Add(nudDigits);
            
            lblFileType = new Label
            {
                Text = "文件类型:",
                Location = new Point(10, 200),
                Size = new Size(100, 23),
                ForeColor = textColor
            };
            gbRenameRule.Controls.Add(lblFileType);
            
            cmbFileType = new ComboBox
            {
                Location = new Point(115, 200),
                Size = new Size(200, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = lightGray,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            cmbFileType.Paint += CmbFileType_Paint;
            cmbFileType.Items.AddRange(new string[] { "所有文件", "文件夹", "图片文件 (*.jpg, *.png, *.gif, ...)", "视频文件 (*.mp4, *.avi, *.mov, ...)", "音频文件 (*.mp3, *.wav, *.flac, ...)", "文档文件 (*.doc, *.pdf, *.txt, ...)", "自定义" });
            cmbFileType.SelectedIndex = 0;
            cmbFileType.SelectedIndexChanged += CmbFileType_Changed;
            gbRenameRule.Controls.Add(cmbFileType);
            
            // 进度条
            progressBar = new ProgressBar
            {
                Location = new Point(10, 455),
                Size = new Size(750, 23),
                Style = ProgressBarStyle.Continuous
            };
            this.Controls.Add(progressBar);
            
            // 状态标签
            lblStatus = new Label
            {
                Text = "请选择文件夹",
                Location = new Point(10, 485),
                Size = new Size(750, 23),
                ForeColor = textColor
            };
            this.Controls.Add(lblStatus);
            
            // 重命名按钮
            btnRename = new Button
            {
                Text = "重命名",
                Location = new Point(650, 515),
                Size = new Size(110, 30),
                Enabled = false,
                ForeColor = Color.Gray, // 禁用状态下使用灰色
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            ConfigureButton(btnRename);
            btnRename.EnabledChanged += BtnRename_EnabledChanged;
            btnRename.Click += BtnRename_Click;
            this.Controls.Add(btnRename);
            
            // 初始化时调用一次，确保UI状态正确
            UpdateCustomNameVisibility();
        }
        
        // 配置按钮样式（减少重复代码）
        private void ConfigureButton(Button button)
        {
            button.FlatAppearance.BorderColor = borderColor;
            button.FlatAppearance.BorderSize = 1;
            button.MouseEnter += Button_MouseEnter;
            button.MouseLeave += Button_MouseLeave;
        }
        
        // 重命名按钮启用状态改变事件
        private void BtnRename_EnabledChanged(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                // 根据启用状态设置文字颜色
                if (button.Enabled)
                {
                    button.ForeColor = textColor; // 启用时使用白色
                }
                else
                {
                    button.ForeColor = Color.Gray; // 禁用时使用灰色
                }
            }
        }
        
        // 按钮鼠标悬停效果
        private void Button_MouseEnter(object sender, EventArgs e)
        {
            if (sender is Button button && button.Enabled)
            {
                button.BackColor = Color.FromArgb(220, 220, 220); // 稍亮的灰色
            }
        }
        
        private void Button_MouseLeave(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                button.BackColor = lightGray; // 恢复原色
            }
        }
        
        // GroupBox自定义边框绘制
        private void GbRenameRule_Paint(object sender, PaintEventArgs e)
        {
            GroupBox gb = sender as GroupBox;
            if (gb == null) return;
            
            Rectangle rect = gb.ClientRectangle;
            using (Pen pen = new Pen(borderColor, 1))
            {
                // 绘制顶部边框（留出文字空间）
                int textWidth = TextRenderer.MeasureText(gb.Text, gb.Font).Width;
                int textX = 8;
                e.Graphics.DrawLine(pen, rect.Left, rect.Top + 8, textX, rect.Top + 8);
                e.Graphics.DrawLine(pen, textX + textWidth + 4, rect.Top + 8, rect.Right - 1, rect.Top + 8);
                
                // 绘制其他边框
                e.Graphics.DrawLine(pen, rect.Left, rect.Top + 8, rect.Left, rect.Bottom - 1);
                e.Graphics.DrawLine(pen, rect.Right - 1, rect.Top + 8, rect.Right - 1, rect.Bottom - 1);
                e.Graphics.DrawLine(pen, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
            }
        }
        
        // ComboBox自定义边框绘制
        private void CmbFileType_Paint(object sender, PaintEventArgs e)
        {
            ComboBox cmb = sender as ComboBox;
            if (cmb == null) return;
            
            Rectangle rect = cmb.ClientRectangle;
            using (Pen pen = new Pen(borderColor, 1))
            {
                // 绘制边框
                e.Graphics.DrawRectangle(pen, new Rectangle(rect.Left, rect.Top, rect.Width - 1, rect.Height - 1));
            }
        }
        
        private void BtnBrowseFolder_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "选择要重命名文件的文件夹";
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    selectedFolder = fbd.SelectedPath;
                    txtFolder.Text = selectedFolder;
                    LoadFiles();
                }
            }
        }
        
        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                LoadFiles();
            }
        }
        
        // 拖放处理辅助方法
        private void HandleDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (droppedFiles.Length > 0)
                {
                    string path = droppedFiles[0];
                    if (Directory.Exists(path) || File.Exists(path))
                    {
                        e.Effect = DragDropEffects.Copy;
                        return;
                    }
                }
            }
            e.Effect = DragDropEffects.None;
        }
        
        private void HandleDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFiles = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (droppedFiles.Length > 0)
                {
                    string path = droppedFiles[0];
                    string folderPath = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                    
                    if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                    {
                        selectedFolder = folderPath;
                        txtFolder.Text = selectedFolder;
                        LoadFiles();
                    }
                }
            }
        }
        
        private void TxtFolder_DragEnter(object sender, DragEventArgs e) => HandleDragEnter(sender, e);
        private void TxtFolder_DragDrop(object sender, DragEventArgs e) => HandleDragDrop(sender, e);
        private void LstFiles_DragEnter(object sender, DragEventArgs e) => HandleDragEnter(sender, e);
        private void LstFiles_DragDrop(object sender, DragEventArgs e) => HandleDragDrop(sender, e);
        
        private void LoadFiles()
        {
            files.Clear();
            directories.Clear();
            lstFiles.Items.Clear();
            
            if (string.IsNullOrEmpty(selectedFolder) || !Directory.Exists(selectedFolder))
            {
                lblStatus.Text = "文件夹不存在";
                btnRename.Enabled = false;
                return;
            }
            
            try
            {
                int selectedIndex = cmbFileType.SelectedIndex;
                
                if (selectedIndex == 1) // 文件夹
                {
                    // 只加载文件夹
                    string[] allDirs = Directory.GetDirectories(selectedFolder);
                    foreach (string dir in allDirs)
                    {
                        directories.Add(new DirectoryInfo(dir));
                        lstFiles.Items.Add($"[文件夹] {Path.GetFileName(dir)}");
                    }
                    lblStatus.Text = $"找到 {directories.Count} 个文件夹";
                    btnRename.Enabled = directories.Count > 0;
                }
                else
                {
                    // 加载文件
                    string[] fileExtensions = GetFileExtensions();
                    
                    if (fileExtensions == null || fileExtensions.Length == 0)
                    {
                        // 所有文件
                        string[] allFiles = Directory.GetFiles(selectedFolder);
                        foreach (string file in allFiles)
                        {
                            files.Add(new FileInfo(file));
                            lstFiles.Items.Add(Path.GetFileName(file));
                        }
                    }
                    else
                    {
                        // 特定类型文件
                        foreach (string extension in fileExtensions)
                        {
                            string[] foundFiles = Directory.GetFiles(selectedFolder, "*" + extension, SearchOption.TopDirectoryOnly);
                            foreach (string file in foundFiles)
                            {
                                files.Add(new FileInfo(file));
                                lstFiles.Items.Add(Path.GetFileName(file));
                            }
                        }
                    }
                    
                    lblStatus.Text = $"找到 {files.Count} 个文件";
                    btnRename.Enabled = files.Count > 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载文件时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "加载文件失败";
            }
        }
        
        private string[] GetFileExtensions()
        {
            switch (cmbFileType.SelectedIndex)
            {
                case 0: // 所有文件
                    return null;
                case 1: // 文件夹（已在 LoadFiles 中处理）
                    return null;
                case 2: // 图片文件
                    return new string[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".tif", ".tiff", ".webp", ".svg" };
                case 3: // 视频文件
                    return new string[] { ".mp4", ".avi", ".mov", ".wmv", ".flv", ".mkv", ".webm", ".m4v", ".3gp", ".mpg", ".mpeg" };
                case 4: // 音频文件
                    return new string[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a", ".opus" };
                case 5: // 文档文件
                    return new string[] { ".doc", ".docx", ".pdf", ".txt", ".rtf", ".xls", ".xlsx", ".ppt", ".pptx" };
                case 6: // 自定义
                    using (CustomExtensionForm form = new CustomExtensionForm())
                    {
                        if (form.ShowDialog() == DialogResult.OK)
                        {
                            return form.GetExtensions();
                        }
                    }
                    return null;
                default:
                    return null;
            }
        }
        
        private void CmbFileType_Changed(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                LoadFiles();
            }
        }
        
        private void RenameRule_Changed(object sender, EventArgs e)
        {
            // 更新 UI 显示
            UpdateCustomNameVisibility();
        }
        
        private void ChkKeepOriginalName_Changed(object sender, EventArgs e)
        {
            UpdateCustomNameVisibility();
        }
        
        private void UpdateCustomNameVisibility()
        {
            // 自定义名称输入框始终显示，不再隐藏
            // 保留原名称功能只是改变重命名规则，不影响UI显示
            if (chkKeepOriginalName != null && lblCustomName != null && txtCustomName != null)
            {
                lblCustomName.Visible = true;
                txtCustomName.Visible = true;
            }
        }
        
        private void BtnRename_Click(object sender, EventArgs e)
        {
            int totalCount = files.Count + directories.Count;
            if (totalCount == 0)
            {
                MessageBox.Show("没有可重命名的项目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            string itemType = cmbFileType.SelectedIndex == 1 ? "文件夹" : "文件";
            DialogResult result = MessageBox.Show(
                $"确定要重命名 {totalCount} 个{itemType}吗？\n\n此操作不可撤销！",
                "确认重命名",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes)
            {
                return;
            }
            
            try
            {
                btnRename.Enabled = false;
                progressBar.Maximum = totalCount;
                progressBar.Value = 0;
                
                int successCount = 0;
                int failCount = 0;
                int currentNumber = (int)nudStartNumber.Value;
                int digits = (int)nudDigits.Value;
                
                // 获取自定义名称（始终需要，无论是否勾选"保留原名称"）
                string customName = string.IsNullOrWhiteSpace(txtCustomName.Text.Trim()) 
                    ? DEFAULT_CUSTOM_NAME 
                    : txtCustomName.Text.Trim();
                
                DateTime baseDate = DateTime.Now.Date;
                int itemIndex = 0;
                
                // 重命名文件夹
                RenameDirectories(ref successCount, ref failCount, ref currentNumber, ref itemIndex, 
                    customName, digits, baseDate);
                
                // 重命名文件
                RenameFiles(ref successCount, ref failCount, ref currentNumber, ref itemIndex, 
                    customName, digits, baseDate);
                
                lblStatus.Text = $"重命名完成: 成功 {successCount} 个, 失败 {failCount} 个";
                MessageBox.Show(
                    $"重命名完成！\n\n成功: {successCount} 个\n失败: {failCount} 个",
                    "完成",
                    MessageBoxButtons.OK,
                    failCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                
                // 刷新文件列表（使用try-catch防止刷新失败）
                try
                {
                    LoadFiles();
                }
                catch (Exception refreshEx)
                {
                    System.Diagnostics.Debug.WriteLine($"刷新文件列表失败: {refreshEx.Message}");
                }
                btnRename.Enabled = (files.Count > 0 || directories.Count > 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重命名过程中出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "重命名失败";
            }
            finally
            {
                progressBar.Value = 0;
            }
        }
        
        // 生成新名称的辅助方法
        private string GenerateNewName(string cleanCustomName, string cleanOldName, int currentNumber, 
            int digits, DateTime baseDate, bool keepOriginalName, string extension = "")
        {
            bool isFile = !string.IsNullOrEmpty(extension);
            
            // 日期字符串（固定当天日期，不递增）
            string dateStr = baseDate.ToString("yyyyMMdd");
            // 序号字符串（格式化）
            string numberStr = currentNumber.ToString($"D{digits}");
            
            string newName;
            
            // 生成文件名部分（不包含扩展名）
            if (keepOriginalName)
            {
                // 保留原名称
                if (rbSuffixNumber.Checked)
                {
                    // 后缀序号（保留原名称）：ON_CN_N
                    newName = $"{cleanOldName}_{cleanCustomName}_{numberStr}";
                }
                else if (rbPrefixNumber.Checked)
                {
                    // 前缀序号（保留原名称）：N_CN_ON
                    newName = $"{numberStr}_{cleanCustomName}_{cleanOldName}";
                }
                else if (rbPrefixDateNumber.Checked)
                {
                    // 前缀日期序号（保留原名称）：N_D_CN_ON
                    newName = $"{numberStr}_{dateStr}_{cleanCustomName}_{cleanOldName}";
                }
                else // rbSuffixDateNumber.Checked
                {
                    // 后缀日期序号（保留原名称）：ON_CN_D_N
                    newName = $"{cleanOldName}_{cleanCustomName}_{dateStr}_{numberStr}";
                }
            }
            else
            {
                // 不保留原名称
                if (rbSuffixNumber.Checked)
                {
                    // 后缀序号（不保留原名称）：CN_N
                    newName = $"{cleanCustomName}_{numberStr}";
                }
                else if (rbPrefixNumber.Checked)
                {
                    // 前缀序号（不保留原名称）：N_CN
                    newName = $"{numberStr}_{cleanCustomName}";
                }
                else if (rbPrefixDateNumber.Checked)
                {
                    // 前缀日期序号（不保留原名称）：N_D_CN
                    newName = $"{numberStr}_{dateStr}_{cleanCustomName}";
                }
                else // rbSuffixDateNumber.Checked
                {
                    // 后缀日期序号（不保留原名称）：CN_D_N
                    newName = $"{cleanCustomName}_{dateStr}_{numberStr}";
                }
            }
            
            // 清理新名称中的非法字符（只处理文件名部分，不处理扩展名）
            string namePart = FileHelpers.SanitizeFileName(newName, "_");
            
            if (string.IsNullOrWhiteSpace(namePart))
            {
                // 使用新的规则格式生成默认名称
                string defaultOldName = isFile ? DEFAULT_CUSTOM_NAME : DEFAULT_FOLDER_NAME;
                namePart = GenerateDefaultName(defaultOldName, numberStr, dateStr, keepOriginalName, isFile);
            }
            
            // 如果是文件，添加原始扩展名（保持不变）
            return isFile ? namePart + extension : namePart;
        }
        
        // 生成默认名称（当清理后名称为空时使用）
        private string GenerateDefaultName(string defaultOldName, string numberStr, string dateStr, 
            bool keepOriginalName, bool isFile)
        {
            if (keepOriginalName)
            {
                if (rbSuffixNumber.Checked)
                    return $"{defaultOldName}_{DEFAULT_CUSTOM_NAME}_{numberStr}";
                else if (rbPrefixNumber.Checked)
                    return $"{numberStr}_{DEFAULT_CUSTOM_NAME}_{defaultOldName}";
                else if (rbPrefixDateNumber.Checked)
                    return $"{numberStr}_{dateStr}_{DEFAULT_CUSTOM_NAME}_{defaultOldName}";
                else // rbSuffixDateNumber
                    return $"{defaultOldName}_{DEFAULT_CUSTOM_NAME}_{dateStr}_{numberStr}";
            }
            else
            {
                if (rbSuffixNumber.Checked)
                    return $"{DEFAULT_CUSTOM_NAME}_{numberStr}";
                else if (rbPrefixNumber.Checked)
                    return $"{numberStr}_{DEFAULT_CUSTOM_NAME}";
                else if (rbPrefixDateNumber.Checked)
                    return $"{numberStr}_{dateStr}_{DEFAULT_CUSTOM_NAME}";
                else // rbSuffixDateNumber
                    return $"{DEFAULT_CUSTOM_NAME}_{dateStr}_{numberStr}";
            }
        }
        
        // 重命名文件夹
        private void RenameDirectories(ref int successCount, ref int failCount, ref int currentNumber, 
            ref int itemIndex, string customName, int digits, DateTime baseDate)
        {
            if (directories.Count == 0) return;
            
            string cleanCustomName = FileHelpers.SanitizeFileName(customName, "_");
            if (string.IsNullOrWhiteSpace(cleanCustomName))
            {
                cleanCustomName = DEFAULT_CUSTOM_NAME;
            }
            
            for (int i = 0; i < directories.Count; i++)
            {
                try
                {
                    DirectoryInfo dir = directories[i];
                    string oldName = dir.Name;
                    string cleanOldName = FileHelpers.SanitizeFileName(oldName, "_");
                    
                    if (string.IsNullOrWhiteSpace(cleanOldName))
                    {
                        cleanOldName = DEFAULT_FOLDER_NAME;
                    }
                    
                    string newName = GenerateNewName(cleanCustomName, cleanOldName, currentNumber, 
                        digits, baseDate, chkKeepOriginalName.Checked);
                    
                    string newPath = Path.Combine(dir.Parent.FullName, newName);
                    
                    // 如果新名称和原名称相同，跳过重命名
                    if (newPath.Equals(dir.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        successCount++;
                        currentNumber++;
                        itemIndex++;
                        UpdateProgress(itemIndex);
                        continue;
                    }
                    
                    // 处理名称冲突
                    newPath = ResolveNameConflict(newPath, newName, dir.Parent.FullName, isDirectory: true);
                    
                    Directory.Move(dir.FullName, newPath);
                    successCount++;
                    currentNumber++;
                    itemIndex++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    System.Diagnostics.Debug.WriteLine($"重命名文件夹失败: {directories[i].Name}, 错误: {ex.Message}");
                    itemIndex++;
                }
                
                UpdateProgress(itemIndex);
            }
        }
        
        // 重命名文件
        private void RenameFiles(ref int successCount, ref int failCount, ref int currentNumber, 
            ref int itemIndex, string customName, int digits, DateTime baseDate)
        {
            if (files.Count == 0) return;
            
            string cleanCustomName = FileHelpers.SanitizeFileName(customName, "_");
            if (string.IsNullOrWhiteSpace(cleanCustomName))
            {
                cleanCustomName = DEFAULT_CUSTOM_NAME;
            }
            
            for (int i = 0; i < files.Count; i++)
            {
                try
                {
                    FileInfo file = files[i];
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
                    string extension = file.Extension;
                    string cleanOldName = FileHelpers.SanitizeFileName(nameWithoutExt, "_");
                    
                    if (string.IsNullOrWhiteSpace(cleanOldName))
                    {
                        cleanOldName = DEFAULT_CUSTOM_NAME;
                    }
                    
                    string newName = GenerateNewName(cleanCustomName, cleanOldName, currentNumber, 
                        digits, baseDate, chkKeepOriginalName.Checked, extension);
                    
                    string newPath = Path.Combine(file.DirectoryName, newName);
                    
                    // 如果新名称和原名称相同，跳过重命名
                    if (newPath.Equals(file.FullName, StringComparison.OrdinalIgnoreCase))
                    {
                        successCount++;
                        currentNumber++;
                        itemIndex++;
                        UpdateProgress(itemIndex);
                        continue;
                    }
                    
                    // 处理名称冲突
                    newPath = ResolveNameConflict(newPath, newName, file.DirectoryName, isDirectory: false, extension);
                    
                    File.Move(file.FullName, newPath);
                    successCount++;
                    currentNumber++;
                    itemIndex++;
                }
                catch (Exception ex)
                {
                    failCount++;
                    System.Diagnostics.Debug.WriteLine($"重命名文件失败: {files[i].Name}, 错误: {ex.Message}");
                    itemIndex++;
                }
                
                UpdateProgress(itemIndex);
            }
        }
        
        // 解决名称冲突
        private string ResolveNameConflict(string originalPath, string baseName, string directory, 
            bool isDirectory, string extension = "")
        {
            string newPath = originalPath;
            int counter = 1;
            
            while ((isDirectory && Directory.Exists(newPath)) || 
                   (!isDirectory && (File.Exists(newPath) || Directory.Exists(newPath))))
            {
                if (isDirectory)
                {
                    newPath = Path.Combine(directory, $"{baseName}_{counter}");
                }
                else
                {
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
                    newPath = Path.Combine(directory, $"{nameWithoutExt}_{counter}{extension}");
                }
                
                counter++;
                
                if (counter > MAX_RENAME_ATTEMPTS)
                {
                    throw new Exception($"无法生成唯一的{(isDirectory ? "文件夹" : "文件")}名称");
                }
            }
            
            return newPath;
        }
        
        // 更新进度条
        private void UpdateProgress(int value)
        {
            progressBar.Value = value;
            Application.DoEvents();
        }
    }
    
    // 自定义扩展名输入表单
    public class CustomExtensionForm : Form
    {
        private TextBox txtExtensions;
        private Button btnOK;
        private Button btnCancel;
        private string[] extensions;
        
        public CustomExtensionForm()
        {
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "自定义文件类型";
            this.Size = new Size(400, 155);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            
            // 应用 ShareX 主题
            ShareXResources.ApplyTheme(this, true);
            
            // 设置控件背景颜色为浅灰色（符合主题）
            Color lightGray = ShareXResources.Theme?.LightBackgroundColor ?? Color.FromArgb(240, 240, 240);
            // 设置文字颜色为白色
            Color textColor = Color.White;
            // 设置边框颜色为浅灰色（1像素）
            Color borderColor = Color.FromArgb(200, 200, 200);
            
            Label lblHint = new Label
            {
                Text = "请输入文件扩展名，用逗号分隔（例如: .jpg,.png,.gif）",
                Location = new Point(10, 10),
                Size = new Size(370, 40),
                AutoSize = false,
                ForeColor = textColor
            };
            this.Controls.Add(lblHint);
            
            txtExtensions = new TextBox
            {
                Location = new Point(10, 50),
                Size = new Size(370, 23),
                BackColor = lightGray,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            // 添加提示文本（如果支持）
            try
            {
                var placeholderProperty = typeof(TextBox).GetProperty("PlaceholderText");
                if (placeholderProperty != null)
                {
                    placeholderProperty.SetValue(txtExtensions, ".jpg,.png,.gif");
                }
            }
            catch { }
            this.Controls.Add(txtExtensions);
            
            btnOK = new Button
            {
                Text = "确定",
                Location = new Point(220, 85),
                Size = new Size(75, 30),
                DialogResult = DialogResult.OK,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            btnOK.FlatAppearance.BorderColor = borderColor;
            btnOK.FlatAppearance.BorderSize = 1;
            btnOK.Click += BtnOK_Click;
            this.Controls.Add(btnOK);
            
            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(305, 85),
                Size = new Size(75, 30),
                DialogResult = DialogResult.Cancel,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            btnCancel.FlatAppearance.BorderColor = borderColor;
            btnCancel.FlatAppearance.BorderSize = 1;
            this.Controls.Add(btnCancel);
        }
        
        private void BtnOK_Click(object sender, EventArgs e)
        {
            string text = txtExtensions.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                MessageBox.Show("请输入文件扩展名", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.None;
                return;
            }
            
            string[] parts = text.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            extensions = parts.Select(p => p.StartsWith(".") ? p : "." + p).ToArray();
        }
        
        public string[] GetExtensions()
        {
            return extensions;
        }
    }
}

