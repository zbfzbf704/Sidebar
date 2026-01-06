#region Copyright Information

/*
    Copyright (c) 2025 蝴蝶哥
    Email: your-email@example.com
    
    This code is part of the Sidebar application.
    All rights reserved.
*/

#endregion Copyright Information

using System;
using System.Drawing;
using System.Windows.Forms;
using ShareX.HelpersLib;

namespace Sidebar
{
    public partial class HotkeyEditForm : Form
    {
        private Color lightGray;
        private Color textColor;
        private Color borderColor;
        
        private Label lblToolName;
        private Label lblHotkey;
        private TextBox txtHotkey;
        private Button btnOK;
        private Button btnClear;
        
        private string toolName;
        private HotkeyConfig config;
        
        public HotkeyConfig HotkeyConfig { get; private set; }
        
        public HotkeyEditForm(string toolName, HotkeyConfig config)
        {
            this.toolName = toolName;
            this.config = config ?? new HotkeyConfig();
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            this.Text = $"编辑快捷键 - {toolName}";
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterParent;
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
            LoadHotkeys();
        }
        
        private void CreateControls()
        {
            int padding = 10;
            int yPos = padding;
            
            // 工具名称
            lblToolName = new Label
            {
                Text = $"工具：{toolName}",
                Location = new Point(padding, yPos),
                Size = new Size(380, 25),
                ForeColor = textColor,
                Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold)
            };
            this.Controls.Add(lblToolName);
            yPos += 30;
            
            // 快捷键
            lblHotkey = new Label
            {
                Text = "快捷键：",
                Location = new Point(padding, yPos),
                Size = new Size(100, 23),
                ForeColor = textColor
            };
            this.Controls.Add(lblHotkey);
            
            txtHotkey = new TextBox
            {
                Location = new Point(110, yPos),
                Size = new Size(200, 23),
                BackColor = lightGray,
                ForeColor = textColor,
                ReadOnly = true
            };
            txtHotkey.KeyDown += TxtHotkey_KeyDown;
            txtHotkey.GotFocus += TxtHotkey_GotFocus;
            this.Controls.Add(txtHotkey);
            
            btnClear = new Button
            {
                Text = "清除",
                Location = new Point(320, yPos),
                Size = new Size(60, 23),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray
            };
            ConfigureButton(btnClear);
            btnClear.Click += BtnClear_Click;
            this.Controls.Add(btnClear);
            yPos += 35;
            
            // 提示
            Label lblTip = new Label
            {
                Text = "提示：在输入框中按下要设置的快捷键组合",
                Location = new Point(padding, yPos),
                Size = new Size(380, 20),
                ForeColor = Color.FromArgb(180, 180, 180),
                Font = new Font("Microsoft YaHei", 8F)
            };
            this.Controls.Add(lblTip);
            yPos += 30;
            
            // 确定按钮（右下角）
            btnOK = new Button
            {
                Text = "确定",
                Location = new Point(310, yPos),
                Size = new Size(80, 30),
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat,
                BackColor = lightGray,
                DialogResult = DialogResult.OK
            };
            ConfigureButton(btnOK);
            btnOK.Click += BtnOK_Click;
            this.Controls.Add(btnOK);
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
        
        private void LoadHotkeys()
        {
            if (config.Hotkey != null && config.Hotkey.Hotkey != Keys.None)
            {
                txtHotkey.Text = config.Hotkey.ToString();
            }
            else
            {
                txtHotkey.Text = "未设置";
            }
        }
        
        private void TxtHotkey_GotFocus(object sender, EventArgs e)
        {
            txtHotkey.Text = "按下快捷键...";
        }
        
        private void TxtHotkey_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            
            if (e.KeyCode == Keys.Escape)
            {
                txtHotkey.Text = "未设置";
                config.Hotkey = null;
                return;
            }
            
            // 组合键
            Keys modifiers = e.Modifiers;
            Keys key = e.KeyCode;
            
            // 排除单独的修饰键
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin)
            {
                return;
            }
            
            Keys hotkey = modifiers | key;
            HotkeyInfo hotkeyInfo = new HotkeyInfo(hotkey);
            
            if (hotkeyInfo.IsValidHotkey)
            {
                config.Hotkey = hotkeyInfo;
                txtHotkey.Text = hotkeyInfo.ToString();
            }
        }
        
        private void BtnClear_Click(object sender, EventArgs e)
        {
            config.Hotkey = null;
            txtHotkey.Text = "未设置";
        }
        
        private void BtnOK_Click(object sender, EventArgs e)
        {
            HotkeyConfig = config;
        }
    }
}

