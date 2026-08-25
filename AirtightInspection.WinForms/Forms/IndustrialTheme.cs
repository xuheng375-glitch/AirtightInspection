using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Sunny.UI;

namespace AirtightInspection.Forms
{
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

        private static void ApplyControl(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Button) StyleButton((Button)control);
                else if (control is DataGridView) ApplyGrid((DataGridView)control);
                else if (control is TextBoxBase)
                {
                    control.BackColor = Color.FromArgb(9, 16, 22); control.ForeColor = Text;
                }
                else if (control is ComboBox || control is NumericUpDown)
                {
                    control.BackColor = Color.FromArgb(9, 16, 22); control.ForeColor = Text;
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
                }
                else if (control is Panel || control is FlowLayoutPanel || control is TableLayoutPanel)
                {
                    // Keep deliberately assigned Background/Header/accent colors.
                    // Only replace the WinForms default surface color.
                    if (control.BackColor == SystemColors.Control)
                        control.BackColor = Panel;
                }
                if (control.HasChildren) ApplyControl(control);
            }
        }
    }
}
