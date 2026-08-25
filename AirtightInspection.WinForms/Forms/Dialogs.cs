using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AirtightInspection.Models;
using Sunny.UI;

namespace AirtightInspection.Forms
{
    internal static class UiFactory
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        public static Button Button(string text, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(88, 32), Height = 32, Margin = new Padding(4) };
            if (click != null) button.Click += click;
            IndustrialTheme.StyleButton(button);
            return button;
        }
        public static Label Label(string text) => new Label { Text = text, AutoSize = true, ForeColor = IndustrialTheme.Text, Anchor = AnchorStyles.Left, Margin = new Padding(4, 9, 4, 4) };
        public static Label Heading(string text, Form owner)
        {
            var label = new Label { Text = "▌ " + text, Dock = DockStyle.Top, Height = 38, ForeColor = IndustrialTheme.Accent,
                BackColor = IndustrialTheme.Header, Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
            label.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(owner.Handle, 0xA1, 0x2, 0); } };
            return label;
        }
    }

    public sealed class TextInputForm : UIForm
    {
        private readonly TextBox _text;
        public string Value => _text.Text.Trim();
        public TextInputForm(string title, string label, string initial = "", bool password = false)
        {
            Text = title; Width = 420; Height = 180; StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false; MinimizeBox = false;
            FormBorderStyle = FormBorderStyle.None;
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(18), BackColor = IndustrialTheme.Panel };
            panel.Controls.Add(UiFactory.Label(label), 0, 0);
            _text = new TextBox { Dock = DockStyle.Top, Text = initial, UseSystemPasswordChar = password, MaxLength = 200 };
            panel.Controls.Add(_text, 0, 1);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(UiFactory.Button("取消", (_, __) => { DialogResult = DialogResult.Cancel; Close(); }));
            buttons.Controls.Add(UiFactory.Button("确定", (_, __) => { DialogResult = DialogResult.OK; Close(); }));
            panel.Controls.Add(buttons, 0, 2); Controls.Add(panel); Controls.Add(UiFactory.Heading(title, this)); AcceptButton = (Button)buttons.Controls[1];
            IndustrialTheme.Apply(this);
            Shown += (_, __) => { _text.Focus(); _text.SelectAll(); };
        }
    }

    public sealed class ScanStationForm : UIForm
    {
        private readonly List<Button> _stationButtons = new List<Button>();
        private StationConfig _selectedStation;
        public StationConfig SelectedStation => _selectedStation;

        public ScanStationForm(string barcode, IList<StationConfig> stations)
        {
            Text = "扫码工位选择"; Width = 760; Height = 520; StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(680, 460);
            FormBorderStyle = FormBorderStyle.None;

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1,
                Padding = new Padding(24, 20, 24, 18),
                BackColor = IndustrialTheme.Panel
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

            var barcodeCard = new IndustrialCard { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
            barcodeCard.Controls.Add(new Label
            {
                Text = barcode,
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = new Font("Consolas", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(106, 0, 12, 0)
            });
            barcodeCard.Controls.Add(new Label
            {
                Text = "扫描条码",
                Dock = DockStyle.Left,
                Width = 100,
                ForeColor = IndustrialTheme.Accent,
                Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            });
            panel.Controls.Add(barcodeCard, 0, 0);

            panel.Controls.Add(new Label
            {
                Text = "● 已检测到扫码枪输入，请选择检测工位",
                Dock = DockStyle.Fill,
                ForeColor = IndustrialTheme.Success,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            var stationPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8),
                Margin = new Padding(0),
                BackColor = IndustrialTheme.Background
            };
            foreach (var station in stations)
            {
                var stationButton = CreateStationButton(station);
                _stationButtons.Add(stationButton);
                stationPanel.Controls.Add(stationButton);
            }
            panel.Controls.Add(stationPanel, 0, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 12, 0, 0),
                Margin = new Padding(0)
            };
            var cancelButton = UiFactory.Button("取消", (_, __) => { DialogResult = DialogResult.Cancel; Close(); });
            var confirmButton = UiFactory.Button("确认选择", ConfirmSelection);
            cancelButton.AutoSize = false; cancelButton.Size = new Size(132, 44);
            confirmButton.AutoSize = false; confirmButton.Size = new Size(132, 44);
            buttons.Controls.Add(cancelButton);
            buttons.Controls.Add(confirmButton);
            panel.Controls.Add(buttons, 0, 3);

            Controls.Add(panel);
            Controls.Add(UiFactory.Heading("扫码工位选择", this));
            AcceptButton = confirmButton;
            CancelButton = cancelButton;
            IndustrialTheme.Apply(this);

            if (_stationButtons.Count > 0)
                SelectStation((StationConfig)_stationButtons[0].Tag);
        }

        private Button CreateStationButton(StationConfig station)
        {
            var button = new Button
            {
                Text = $"{station.StationNo:00}  {station.StationName}",
                Tag = station,
                Size = new Size(214, 72),
                Margin = new Padding(8),
                FlatStyle = FlatStyle.Flat,
                BackColor = IndustrialTheme.Surface,
                ForeColor = IndustrialTheme.Text,
                Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(64, 88, 101);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(23, 77, 90);
            button.Click += (_, __) => SelectStation(station);
            button.DoubleClick += (_, __) => { SelectStation(station); ConfirmSelection(button, EventArgs.Empty); };
            return button;
        }

        private void SelectStation(StationConfig station)
        {
            _selectedStation = station;
            foreach (var button in _stationButtons)
            {
                var selected = ReferenceEquals(button.Tag, station);
                button.BackColor = selected ? IndustrialTheme.AccentDark : IndustrialTheme.Surface;
                button.ForeColor = selected ? Color.White : IndustrialTheme.Text;
                button.FlatAppearance.BorderColor = selected ? IndustrialTheme.Accent : Color.FromArgb(64, 88, 101);
                button.FlatAppearance.BorderSize = selected ? 2 : 1;
            }
        }

        private void ConfirmSelection(object sender, EventArgs e)
        {
            if (_selectedStation == null)
            {
                MessageBox.Show(this, "请选择检测工位。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    public sealed class StationEditForm : UIForm
    {
        private readonly NumericUpDown _number;
        private readonly TextBox _name;
        private readonly CheckBox _enabled;
        private readonly TextBox _remark;
        public StationConfig Station { get; }

        public StationEditForm(StationConfig station)
        {
            Station = station ?? new StationConfig { Enabled = true };
            Text = Station.Id == 0 ? "新增工位" : "修改工位"; Width = 450; Height = 320; StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(18), BackColor = IndustrialTheme.Panel };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _number = new NumericUpDown { Minimum = 1, Maximum = 999999, Value = Math.Max(1, Station.StationNo), Dock = DockStyle.Top };
            _name = new TextBox { Text = Station.StationName, Dock = DockStyle.Top, MaxLength = 100 };
            _enabled = new CheckBox { Text = "启用", Checked = Station.Enabled, AutoSize = true };
            _remark = new TextBox { Text = Station.Remark, Dock = DockStyle.Fill, Multiline = true, MaxLength = 500 };
            grid.Controls.Add(UiFactory.Label("工位号"), 0, 0); grid.Controls.Add(_number, 1, 0);
            grid.Controls.Add(UiFactory.Label("工位名称"), 0, 1); grid.Controls.Add(_name, 1, 1);
            grid.Controls.Add(UiFactory.Label("状态"), 0, 2); grid.Controls.Add(_enabled, 1, 2);
            grid.Controls.Add(UiFactory.Label("备注"), 0, 3); grid.Controls.Add(_remark, 1, 3);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(UiFactory.Button("取消", (_, __) => { DialogResult = DialogResult.Cancel; Close(); }));
            buttons.Controls.Add(UiFactory.Button("保存", Save)); grid.Controls.Add(buttons, 1, 4); Controls.Add(grid); Controls.Add(UiFactory.Heading(Text, this)); IndustrialTheme.Apply(this);
        }
        private void Save(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_name.Text)) { MessageBox.Show("工位名称不能为空"); return; }
            Station.StationNo = (int)_number.Value; Station.StationName = _name.Text.Trim();
            Station.Enabled = _enabled.Checked; Station.Remark = _remark.Text.Trim(); DialogResult = DialogResult.OK; Close();
        }
    }
}
