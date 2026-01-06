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
using System.Windows.Forms;
using ShareX.HelpersLib;

namespace Sidebar
{
    /// <summary>
    /// 样式管理器：统一管理所有样式相关的代码，实现样式与逻辑分离
    /// </summary>
    public static class StyleManager
    {
        /// <summary>
        /// 应用 ShareX 主题到窗体
        /// </summary>
        /// <param name="form">要应用主题的窗体</param>
        /// <param name="closeOnEscape">是否支持 ESC 键关闭</param>
        public static void ApplyThemeToForm(Form form, bool closeOnEscape = true)
        {
            if (form == null) return;
            
            ShareXResources.ApplyTheme(form, closeOnEscape);
        }
        
        /// <summary>
        /// 应用 ShareX 主题到控件
        /// </summary>
        /// <param name="control">要应用主题的控件</param>
        public static void ApplyThemeToControl(Control control)
        {
            if (control == null) return;
            
            // 使用反射调用 ShareX 的内部方法（因为 ApplyCustomThemeToControl 是 public static）
            ShareXResources.ApplyCustomThemeToControl(control);
        }
        
        /// <summary>
        /// 配置按钮样式（标准按钮）
        /// </summary>
        /// <param name="button">要配置的按钮</param>
        public static void ConfigureButton(Button button)
        {
            if (button == null) return;
            
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = ShareXResources.Theme.BorderColor;
            button.ForeColor = ShareXResources.Theme.TextColor;
            button.BackColor = ShareXResources.Theme.LightBackgroundColor;
        }
        
        /// <summary>
        /// 配置下拉框样式
        /// </summary>
        /// <param name="comboBox">要配置的下拉框</param>
        public static void ConfigureComboBox(ComboBox comboBox)
        {
            if (comboBox == null) return;
            
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.ForeColor = ShareXResources.Theme.TextColor;
            comboBox.BackColor = ShareXResources.Theme.LightBackgroundColor;
        }
        
        /// <summary>
        /// 配置文本框样式
        /// </summary>
        /// <param name="textBox">要配置的文本框</param>
        public static void ConfigureTextBox(TextBox textBox)
        {
            if (textBox == null) return;
            
            textBox.ForeColor = ShareXResources.Theme.TextColor;
            textBox.BackColor = ShareXResources.Theme.LightBackgroundColor;
            textBox.BorderStyle = BorderStyle.FixedSingle;
        }
        
        /// <summary>
        /// 配置标签样式
        /// </summary>
        /// <param name="label">要配置的标签</param>
        public static void ConfigureLabel(Label label)
        {
            if (label == null) return;
            
            label.ForeColor = ShareXResources.Theme.TextColor;
            label.BackColor = ShareXResources.Theme.BackgroundColor;
        }
        
        /// <summary>
        /// 配置复选框样式
        /// </summary>
        /// <param name="checkBox">要配置的复选框</param>
        public static void ConfigureCheckBox(CheckBox checkBox)
        {
            if (checkBox == null) return;
            
            checkBox.ForeColor = ShareXResources.Theme.TextColor;
            checkBox.BackColor = ShareXResources.Theme.BackgroundColor;
        }
        
        /// <summary>
        /// 配置特殊按钮样式（如录制按钮，无边框，透明背景）
        /// </summary>
        /// <param name="button">要配置的按钮</param>
        /// <param name="iconColor">图标颜色</param>
        /// <param name="iconFont">图标字体</param>
        /// <param name="transparentBackground">是否透明背景</param>
        public static void ConfigureSpecialButton(Button button, Color iconColor, Font iconFont = null, bool transparentBackground = true)
        {
            if (button == null) return;
            
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            
            if (transparentBackground)
            {
                button.BackColor = button.Parent?.BackColor ?? ShareXResources.Theme.BackgroundColor;
                button.FlatAppearance.MouseOverBackColor = button.BackColor;
                button.FlatAppearance.MouseDownBackColor = button.BackColor;
            }
            else
            {
                button.BackColor = ShareXResources.Theme.LightBackgroundColor;
            }
            
            button.ForeColor = iconColor;
            
            if (iconFont != null)
            {
                button.Font = iconFont;
            }
        }
        
        /// <summary>
        /// 配置数值输入框样式
        /// </summary>
        /// <param name="numericUpDown">要配置的数值输入框</param>
        public static void ConfigureNumericUpDown(NumericUpDown numericUpDown)
        {
            if (numericUpDown == null) return;
            
            numericUpDown.ForeColor = ShareXResources.Theme.TextColor;
            numericUpDown.BackColor = ShareXResources.Theme.LightBackgroundColor;
        }
        
        /// <summary>
        /// 配置面板样式
        /// </summary>
        /// <param name="panel">要配置的面板</param>
        public static void ConfigurePanel(Panel panel)
        {
            if (panel == null) return;
            
            panel.ForeColor = ShareXResources.Theme.TextColor;
            panel.BackColor = ShareXResources.Theme.BackgroundColor;
        }
        
        /// <summary>
        /// 批量应用主题到控件集合
        /// </summary>
        /// <param name="controls">控件集合</param>
        public static void ApplyThemeToControls(Control.ControlCollection controls)
        {
            if (controls == null) return;
            
            foreach (Control control in controls)
            {
                ApplyThemeToControl(control);
            }
        }
        
        /// <summary>
        /// 获取主题颜色
        /// </summary>
        public static class ThemeColors
        {
            public static Color BackgroundColor => ShareXResources.Theme.BackgroundColor;
            public static Color LightBackgroundColor => ShareXResources.Theme.LightBackgroundColor;
            public static Color TextColor => ShareXResources.Theme.TextColor;
            public static Color BorderColor => ShareXResources.Theme.BorderColor;
            
            /// <summary>
            /// 录制按钮的红色
            /// </summary>
            public static Color RecordButtonRed => Color.Red;
        }
        
        /// <summary>
        /// 获取主题字体
        /// </summary>
        public static class ThemeFonts
        {
            /// <summary>
            /// 录制按钮的 Emoji 字体
            /// </summary>
            public static Font RecordButtonEmoji => new Font("Segoe UI Emoji", 24F, FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }
}

