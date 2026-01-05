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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ShareX.HelpersLib;
using Newtonsoft.Json;

namespace Sidebar
{
    public partial class HotkeySettingsForm : Form
    {
        // 颜色变量
        private Color lightGray;
        private Color textColor;
        private Color borderColor;
        
        // UI 控件
        private ListView lstHotkeys;
        private Button btnSave;
        private Button btnLogin;
        private Label lblInfo;
        
        // 快捷键配置
        private Dictionary<string, HotkeyConfig> hotkeyConfigs;
        
        // 工具按钮列表（从SidebarForm获取）
        private List<ToolButtonInfo> toolButtons;
        
        // 事件：快捷键保存后触发
        public event EventHandler HotkeysSaved;
        
        public HotkeySettingsForm(List<ToolButtonInfo> buttons)
        {
            this.toolButtons = buttons;
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = "设置";
            this.Size = new Size(500, 500);
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
            
            CreateControls();
            LoadHotkeyConfigs();
            
            // 窗口显示后调整按钮位置（确保使用正确的客户区宽度）
            this.Shown += HotkeySettingsForm_Shown;
        }
        
        private void HotkeySettingsForm_Shown(object sender, EventArgs e)
        {
            // 窗口显示后，使用客户区宽度重新计算按钮位置
            int rightMargin = 5; // 距离右侧边缘的间距
            int topMargin = 5; // 距离顶部边缘的间距
            int bottomMargin = 5; // 距离底部边缘的间距
            
            // 计算登录按钮的正确位置（右上角）
            int loginButtonX = this.ClientSize.Width - btnLogin.Width - rightMargin;
            int loginButtonY = topMargin;
            
            // 调整登录按钮位置
            btnLogin.Location = new Point(loginButtonX, loginButtonY);
            
            // 调整标题标签宽度，确保不与登录按钮重叠
            // 标题标签应该从左侧开始，到登录按钮左侧结束（留出间距）
            int titleLabelWidth = loginButtonX - 10 - 10; // 左侧padding(10) + 右侧间距(10)
            foreach (Control control in this.Controls)
            {
                if (control is Label label && label.Text == "快捷键设置")
                {
                    label.Size = new Size(titleLabelWidth, label.Height);
                    break;
                }
            }
            
            // 调整说明标签宽度，确保不与登录按钮重叠
            if (lblInfo != null)
            {
                int infoLabelWidth = loginButtonX - 10 - 10; // 左侧padding(10) + 右侧间距(10)
                lblInfo.Size = new Size(infoLabelWidth, lblInfo.Height);
            }
            
            // 调整保存按钮位置（右下角）
            btnSave.Location = new Point(
                this.ClientSize.Width - btnSave.Width - rightMargin,
                this.ClientSize.Height - btnSave.Height - bottomMargin
            );
            
            // 确保按钮在 Z-order 的最上层，避免被其他控件遮挡
            // 在窗口完全初始化后再调整 Z-order
            if (this.Controls.Contains(btnLogin))
            {
                this.Controls.SetChildIndex(btnLogin, 0);
            }
            if (this.Controls.Contains(btnSave))
            {
                this.Controls.SetChildIndex(btnSave, 0);
            }
        }
        
        private void CreateControls()
        {
            int padding = 10;
            int yPos = padding;
            
            // 标题
            // 宽度会在 Shown 事件中根据登录按钮位置动态调整
            Label lblTitle = new Label
            {
                Text = "快捷键设置",
                Location = new Point(padding, yPos),
                Size = new Size(300, 30), // 临时宽度，会在 Shown 事件中调整
                ForeColor = textColor,
                Font = new Font("Microsoft YaHei", 12F, FontStyle.Bold)
            };
            this.Controls.Add(lblTitle);
            yPos += 35;
            
            // 说明标签
            // 宽度会在 Shown 事件中根据登录按钮位置动态调整
            lblInfo = new Label
            {
                Text = "提示：侧边栏图标的快捷键优先级为最高，无论打开什么程序都能触发。",
                Location = new Point(padding, yPos),
                Size = new Size(300, 40), // 临时宽度，会在 Shown 事件中调整
                ForeColor = textColor,
                Font = new Font("Microsoft YaHei", 9F)
            };
            this.Controls.Add(lblInfo);
            yPos += 45;
            
            // 快捷键列表
            Label lblList = new Label
            {
                Text = "工具快捷键：",
                Location = new Point(padding, yPos),
                Size = new Size(480, 23),
                ForeColor = textColor
            };
            this.Controls.Add(lblList);
            yPos += 25;
            
            lstHotkeys = new ListView
            {
                Location = new Point(padding, yPos),
                Size = new Size(480, 300),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = lightGray,
                ForeColor = textColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            lstHotkeys.Columns.Add("工具名称", 250);
            lstHotkeys.Columns.Add("快捷键", 230); // 调整列宽以适应新窗口宽度
            lstHotkeys.MouseDoubleClick += LstHotkeys_MouseDoubleClick;
            this.Controls.Add(lstHotkeys);
            yPos += 310;
            
            // 登录按钮（右上角，距离右侧窗口边缘5像素）
            // 初始位置会在窗口显示后通过 Shown 事件调整
            btnLogin = new Button
            {
                Text = "登录",
                Location = new Point(400, padding), // 临时位置，会在 Shown 事件中调整
                Size = new Size(80, 25),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
                // 不使用 Anchor，手动计算位置以保持5像素间距
            };
            ConfigureButton(btnLogin);
            btnLogin.Click += BtnLogin_Click;
            this.Controls.Add(btnLogin);
            
            // 保存按钮（右下角，距离右侧窗口边缘5像素）
            // 初始位置会在窗口显示后通过 Shown 事件调整
            btnSave = new Button
            {
                Text = "保存",
                Location = new Point(400, yPos), // 临时位置，会在 Shown 事件中调整
                Size = new Size(90, 30),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
                // 不使用 Anchor，手动计算位置以保持5像素间距
            };
            ConfigureButton(btnSave);
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);
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
        
        private void LstHotkeys_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (lstHotkeys.SelectedItems.Count > 0)
            {
                var item = lstHotkeys.SelectedItems[0];
                string toolName = item.Text;
                EditHotkey(toolName);
            }
        }
        
        private void EditHotkey(string toolName)
        {
            var config = hotkeyConfigs.ContainsKey(toolName) ? hotkeyConfigs[toolName] : new HotkeyConfig();
            
            using (var editForm = new HotkeyEditForm(toolName, config))
            {
                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    hotkeyConfigs[toolName] = editForm.HotkeyConfig;
                    RefreshListView();
                }
            }
        }
        
        private void RefreshListView()
        {
            lstHotkeys.Items.Clear();
            
            foreach (var button in toolButtons)
            {
                var config = hotkeyConfigs.ContainsKey(button.Name) ? hotkeyConfigs[button.Name] : new HotkeyConfig();
                
                ListViewItem item = new ListViewItem(button.Name);
                item.SubItems.Add(config.Hotkey?.ToString() ?? "未设置");
                
                item.ForeColor = textColor;
                lstHotkeys.Items.Add(item);
            }
        }
        
        private void LoadHotkeyConfigs()
        {
            hotkeyConfigs = new Dictionary<string, HotkeyConfig>();
            
            string configPath = GetConfigPath();
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
                    MessageBox.Show($"加载快捷键配置失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            
            RefreshListView();
        }
        
        private void BtnLogin_Click(object sender, EventArgs e)
        {
            // TODO: 后续接入服务器的登录注册功能
            MessageBox.Show("登录功能\n\n此功能将在后续版本中接入服务器登录注册功能。", 
                "登录", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void BtnSave_Click(object sender, EventArgs e)
        {
            // 检查快捷键冲突
            if (CheckHotkeyConflicts())
            {
                return;
            }
            
            // 保存配置
            SaveHotkeyConfigs();
            
            // 触发保存事件，通知SidebarForm重新注册快捷键
            HotkeysSaved?.Invoke(this, EventArgs.Empty);
            
            // 保存后关闭窗口
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        
        private bool CheckHotkeyConflicts()
        {
            var allHotkeys = new Dictionary<Keys, string>();
            
            foreach (var kvp in hotkeyConfigs)
            {
                if (kvp.Value.Hotkey != null && kvp.Value.Hotkey.Hotkey != Keys.None)
                {
                    Keys key = kvp.Value.Hotkey.Hotkey;
                    if (allHotkeys.ContainsKey(key))
                    {
                        MessageBox.Show($"快捷键冲突：{kvp.Key} 和 {allHotkeys[key]} 使用了相同的快捷键 {kvp.Value.Hotkey}，请重新设置。", 
                            "快捷键冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return true;
                    }
                    allHotkeys[key] = kvp.Key;
                }
            }
            
            return false;
        }
        
        private void SaveHotkeyConfigs()
        {
            try
            {
                string configPath = GetConfigPath();
                string json = JsonConvert.SerializeObject(hotkeyConfigs, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存快捷键配置失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private string GetConfigPath()
        {
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sidebar");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            return Path.Combine(appDataPath, "hotkeys.json");
        }
        
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // 如果用户点击关闭按钮（X），不保存直接关闭
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // 不保存，直接关闭
            }
            base.OnFormClosing(e);
        }
        
    }
    
    // 快捷键配置类
    public class HotkeyConfig
    {
        public HotkeyInfo Hotkey { get; set; }
    }
    
    // 工具按钮信息类
    public class ToolButtonInfo
    {
        public string Name { get; set; }
        public string Icon { get; set; }
        public Action OnClick { get; set; }
    }
}

