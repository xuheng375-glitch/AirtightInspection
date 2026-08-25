using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Sunny.UI;

namespace AirtightInspection.Forms
{
    internal sealed class IndustrialGroupBox : GroupBox
    {
        public IndustrialGroupBox() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            var textSize = TextRenderer.MeasureText(Text, Font);
            var borderTop = Math.Max(8, textSize.Height / 2);
            using (var border = new Pen(Color.FromArgb(56, 79, 92)))
                e.Graphics.DrawRectangle(border, 0, borderTop, Width - 1, Height - borderTop - 1);
            var titleBounds = new Rectangle(10, 0, textSize.Width + 8, textSize.Height);
            using (var background = new SolidBrush(BackColor)) e.Graphics.FillRectangle(background, titleBounds);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Point(14, 0), ForeColor, TextFormatFlags.NoPadding);
        }
    }

    internal sealed class IndustrialCard : Panel
    {
        public Color AccentColor { get; set; } = IndustrialTheme.Accent;
        public IndustrialCard()
        {
            DoubleBuffered = true; BackColor = IndustrialTheme.Panel; Padding = new Padding(14, 10, 12, 10); Margin = new Padding(6);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var border = new Pen(Color.FromArgb(48, 67, 78))) e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            using (var accent = new SolidBrush(AccentColor)) e.Graphics.FillRectangle(accent, 0, 0, 4, Height);
            using (var line = new Pen(Color.FromArgb(70, AccentColor))) e.Graphics.DrawLine(line, 4, Height - 1, Width - 1, Height - 1);
        }
    }

    internal static class IndustrialTheme
    {
        public static readonly Color Background = Color.FromArgb(11, 17, 23);
        public static readonly Color Panel = Color.FromArgb(19, 29, 38);
        public static readonly Color Surface = Color.FromArgb(26, 39, 50);
        public static readonly Color Header = Color.FromArgb(32, 48, 61);
        public static readonly Color Accent = Color.FromArgb(0, 188, 212);
        public static readonly Color AccentDark = Color.FromArgb(0, 109, 128);
        public static readonly Color Text = Color.FromArgb(225, 235, 240);
        public static readonly Color Muted = Color.FromArgb(145, 166, 177);
        public static readonly Color Success = Color.FromArgb(57, 211, 129);
        public static readonly Color Warning = Color.FromArgb(255, 183, 43);
        public static readonly Color Danger = Color.FromArgb(239, 83, 80);

        public static void Apply(Form form)
        {
            ApplyWindowIcon(form);
            if (form is UIForm uiForm)
            {
                // SunnyUI has its own title area; hide it so every window uses the
                // same industrial heading created by UiFactory.Heading.
                uiForm.AllowShowTitle = false;
                uiForm.ShowTitle = false;
                uiForm.ShowTitleIcon = false;
                uiForm.TitleHeight = 0;
                uiForm.ControlBox = false;
            }
            form.FormBorderStyle = FormBorderStyle.None;
            form.BackColor = Background;
            form.ForeColor = Text;
            form.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);
            form.Padding = new Padding(1);
            ApplyControl(form);
        }

        public static void ApplyWindowIcon(Form form)
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Icon.ico");
            if (!File.Exists(iconPath)) return;
            try
            {
                using (var appIcon = new Icon(iconPath))
                    form.Icon = (Icon)appIcon.Clone();
                form.ShowIcon = true;
            }
            catch
            {
                // ApplicationIcon remains the fallback if the external icon is unavailable.
            }
        }

        public static void ApplyGrid(DataGridView grid)
        {
            grid.BackgroundColor = Background;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(46, 62, 73);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Header;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Header;
            grid.ColumnHeadersHeight = 38;
            grid.RowHeadersVisible = false;
            grid.RowTemplate.Height = 34;
            grid.DefaultCellStyle.BackColor = Panel;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = AccentDark;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Surface;
            foreach (DataGridViewColumn column in grid.Columns)
                if (column is DataGridViewCheckBoxColumn checkColumn)
                {
                    checkColumn.FlatStyle = FlatStyle.Flat;
                    checkColumn.DefaultCellStyle.BackColor = Panel;
                    checkColumn.DefaultCellStyle.SelectionBackColor = AccentDark;
                }
        }

        public static void StyleButton(Button button, bool danger = false)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = danger ? Danger : AccentDark;
            button.FlatAppearance.MouseOverBackColor = danger ? Color.FromArgb(124, 44, 45) : Color.FromArgb(23, 77, 90);
            button.FlatAppearance.MouseDownBackColor = danger ? Color.FromArgb(91, 34, 35) : AccentDark;
            button.BackColor = Surface;
            button.ForeColor = danger ? Color.FromArgb(255, 190, 190) : Text;
            button.Cursor = Cursors.Hand;
        }

        public static void StyleSunnyButton(UIButton button, bool danger = false)
        {
            button.StyleCustomMode = true;
            button.FillColor = Surface;
            button.FillHoverColor = danger ? Color.FromArgb(124, 44, 45) : Color.FromArgb(23, 77, 90);
            button.FillPressColor = danger ? Color.FromArgb(91, 34, 35) : AccentDark;
            button.FillDisableColor = Color.FromArgb(30, 40, 47);
            button.RectColor = danger ? Danger : AccentDark;
            button.RectHoverColor = danger ? Danger : Accent;
            button.RectPressColor = danger ? Color.FromArgb(190, 55, 55) : Accent;
            button.RectDisableColor = Color.FromArgb(56, 68, 75);
            button.ForeColor = danger ? Color.FromArgb(255, 190, 190) : Text;
            button.ForeHoverColor = Color.White;
            button.ForePressColor = Color.White;
            button.ForeDisableColor = Muted;
            button.Radius = 0;
            button.Cursor = Cursors.Hand;
        }

        private static void ApplyControl(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is UIButton sunnyButton) StyleSunnyButton(sunnyButton);
                else if (control is UIDatePicker datePicker) StyleSunnyInput(datePicker);
                else if (control is UIComboBox sunnyCombo) StyleSunnyComboBox(sunnyCombo);
                else if (control is UITextBox sunnyTextBox) StyleSunnyInput(sunnyTextBox);
                else if (control is UIPanel sunnyPanel)
                {
                    sunnyPanel.StyleCustomMode = true;
                    if (sunnyPanel.FillColor == Color.White || sunnyPanel.FillColor == SystemColors.Control)
                        sunnyPanel.FillColor = Panel;
                    sunnyPanel.RectColor = sunnyPanel.FillColor;
                    sunnyPanel.Radius = 0;
                }
                else if (control is Button) StyleButton((Button)control);
                else if (control is DataGridView) ApplyGrid((DataGridView)control);
                else if (control is TextBoxBase)
                {
                    control.BackColor = Color.FromArgb(9, 16, 22); control.ForeColor = Text;
                    if (control is TextBox textBox) textBox.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (control is ComboBox combo)
                {
                    StyleComboBox(combo);
                }
                else if (control is NumericUpDown numeric)
                {
                    numeric.BackColor = Color.FromArgb(9, 16, 22); numeric.ForeColor = Text;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    if (numeric.Controls.Count > 0)
                    {
                        numeric.Controls[0].BackColor = Surface;
                        numeric.Controls[0].ForeColor = Text;
                    }
                }
                else if (control is CheckBox checkBox)
                {
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = Text;
                    checkBox.FlatStyle = FlatStyle.Flat;
                    checkBox.FlatAppearance.BorderColor = AccentDark;
                    checkBox.FlatAppearance.CheckedBackColor = AccentDark;
                    checkBox.FlatAppearance.MouseOverBackColor = Surface;
                }
                else if (control is GroupBox)
                {
                    control.BackColor = Panel; control.ForeColor = Accent;
                }
                else if (control is Label)
                {
                    if (control.ForeColor == SystemColors.ControlText) control.ForeColor = Text;
                    if (control.BackColor == SystemColors.Control)
                        control.BackColor = Color.Transparent;
                }
                else if (control is ListBox)
                {
                    control.BackColor = Color.FromArgb(8, 14, 20); control.ForeColor = Color.FromArgb(159, 219, 230);
                    ((ListBox)control).BorderStyle = BorderStyle.None;
                }
                else if (control is Panel || control is FlowLayoutPanel || control is TableLayoutPanel)
                {
                    // Keep deliberately assigned Background/Header/accent colors.
                    // Only replace the WinForms default surface color.
                    if (control.BackColor == SystemColors.Control)
                        control.BackColor = Panel;
                }
                if (control.HasChildren && control is not UITextBox && control is not UIComboBox && control is not UIDatePicker)
                    ApplyControl(control);
            }
        }

        private static void StyleSunnyInput(UITextBox input)
        {
            input.StyleCustomMode = true;
            input.FillColor = Color.FromArgb(9, 16, 22);
            input.FillDisableColor = Color.FromArgb(20, 29, 36);
            input.FillReadOnlyColor = Color.FromArgb(15, 24, 31);
            input.ForeColor = Text;
            input.ForeDisableColor = Muted;
            input.ForeReadOnlyColor = Text;
            input.RectColor = Color.FromArgb(73, 94, 105);
            input.RectDisableColor = Color.FromArgb(50, 64, 72);
            input.RectReadOnlyColor = Color.FromArgb(56, 73, 82);
            input.WatermarkColor = Muted;
            input.WatermarkActiveColor = Muted;
            input.Radius = 0;
        }

        private static void StyleSunnyInput(UIDatePicker input)
        {
            input.StyleCustomMode = true;
            input.FillColor = Color.FromArgb(9, 16, 22);
            input.FillDisableColor = Color.FromArgb(20, 29, 36);
            input.ForeColor = Text;
            input.ForeDisableColor = Muted;
            input.RectColor = Color.FromArgb(73, 94, 105);
            input.RectDisableColor = Color.FromArgb(50, 64, 72);
            input.WatermarkColor = Muted;
            input.WatermarkActiveColor = Muted;
            input.Radius = 0;
        }

        private static void StyleSunnyComboBox(UIComboBox combo)
        {
            combo.StyleCustomMode = true;
            combo.FillColor = Color.FromArgb(9, 16, 22);
            combo.FillDisableColor = Color.FromArgb(20, 29, 36);
            combo.ForeColor = Text;
            combo.ForeDisableColor = Muted;
            combo.RectColor = Color.FromArgb(73, 94, 105);
            combo.RectDisableColor = Color.FromArgb(50, 64, 72);
            combo.ItemFillColor = Color.FromArgb(9, 16, 22);
            combo.ItemForeColor = Text;
            combo.ItemHoverColor = Surface;
            combo.ItemSelectBackColor = AccentDark;
            combo.ItemSelectForeColor = Color.White;
            combo.ItemRectColor = Color.FromArgb(46, 62, 73);
            combo.WatermarkColor = Muted;
            combo.WatermarkActiveColor = Muted;
            combo.Radius = 0;
            combo.ItemHeight = Math.Max(28, combo.Font.Height + 10);
        }

        private static void StyleComboBox(ComboBox combo)
        {
            combo.BackColor = Color.FromArgb(9, 16, 22);
            combo.ForeColor = Text;
            combo.FlatStyle = FlatStyle.Flat;
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = Math.Max(24, combo.Font.Height + 9);
            combo.IntegralHeight = false;
            combo.DropDownHeight = 260;
            combo.DrawItem -= DrawComboItem;
            combo.DrawItem += DrawComboItem;
        }

        private static void DrawComboItem(object sender, DrawItemEventArgs e)
        {
            var combo = (ComboBox)sender;
            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (var background = new SolidBrush(selected ? AccentDark : Color.FromArgb(9, 16, 22)))
                e.Graphics.FillRectangle(background, e.Bounds);
            var text = e.Index >= 0 && e.Index < combo.Items.Count
                ? Convert.ToString(combo.Items[e.Index])
                : combo.Text;
            TextRenderer.DrawText(e.Graphics, text ?? string.Empty, combo.Font,
                new Rectangle(e.Bounds.X + 7, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 10), e.Bounds.Height),
                selected ? Color.White : Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }
}
