#region Copyright Information

/*
    Copyright (c) 2025 蝴蝶哥
    Email: 1780555120@qq.com
    
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
    public partial class ProgressForm : Form
    {
        private ProgressBar progressBar;
        private Label lblStatus;
        private Label lblProgress;
        
        public ProgressForm(string title = "处理中...")
        {
            InitializeComponent(title);
        }
        
        private void InitializeComponent(string title)
        {
            this.Text = title;
            this.Size = new Size(400, 120);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            
            // 应用主题
            ShareXResources.ApplyTheme(this, true);
            
            // 进度条
            progressBar = new ProgressBar
            {
                Location = new Point(20, 20),
                Size = new Size(360, 23),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Value = 0
            };
            this.Controls.Add(progressBar);
            
            // 状态标签
            lblStatus = new Label
            {
                Text = "准备中...",
                Location = new Point(20, 50),
                Size = new Size(360, 20),
                AutoSize = false
            };
            this.Controls.Add(lblStatus);
            
            // 进度标签
            lblProgress = new Label
            {
                Text = "0 / 0",
                Location = new Point(20, 70),
                Size = new Size(360, 20),
                AutoSize = false,
                TextAlign = ContentAlignment.TopRight
            };
            this.Controls.Add(lblProgress);
        }
        
        public void SetProgress(int current, int total, string status = "")
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<int, int, string>(SetProgress), current, total, status);
                return;
            }
            
            if (total > 0)
            {
                int percentage = (int)((double)current / total * 100);
                progressBar.Value = Math.Min(100, Math.Max(0, percentage));
                lblProgress.Text = $"{current} / {total} ({percentage}%)";
            }
            
            if (!string.IsNullOrEmpty(status))
            {
                lblStatus.Text = status;
            }
            
            Application.DoEvents();
        }
    }
}

