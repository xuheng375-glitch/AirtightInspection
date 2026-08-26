using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AirtightInspection.Models;
using Sunny.UI;

namespace AirtightInspection.Forms
{
    public sealed class ExportFilterForm : UIForm
    {
        private readonly UIDatePicker _startDate;
        private readonly UIDatePicker _endDate;
        private readonly UIComboBox _station;
        private readonly UIComboBox _product;

        public DateTime StartDate => _startDate.Value.Date;
        public DateTime EndDate => _endDate.Value.Date;
        public DateTime EndDateExclusive => EndDate.AddDays(1);
        public int? StationNo => _station.SelectedIndex <= 0 ? (int?)null : ((StationConfig)_station.SelectedItem).StationNo;
        public string SelectedProductName => _product.SelectedIndex <= 0 ? null : ((ProductConfig)_product.SelectedItem).ProductName;
        public string FilterSummary => $"{StartDate:yyyy-MM-dd} 至 {EndDate:yyyy-MM-dd}，" +
            (StationNo.HasValue ? $"工位 {StationNo.Value}" : "全部工位") + "，" +
            (string.IsNullOrWhiteSpace(SelectedProductName) ? "全部产品" : "产品 " + SelectedProductName);

        public ExportFilterForm(IList<StationConfig> stations, IList<ProductConfig> products)
        {
            Text = "导出检测记录";
            Width = 620;
            Height = 370;
            MinimumSize = new Size(620, 370);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.None;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(34, 26, 34, 22),
                BackColor = IndustrialTheme.Panel
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _startDate = DateInput(DateTime.Today.AddDays(-7));
            _endDate = DateInput(DateTime.Today);
            _station = ComboInput();
            _product = ComboInput();
            _station.Items.Add("全部工位");
            foreach (var station in stations ?? Array.Empty<StationConfig>()) _station.Items.Add(station);
            _station.SelectedIndex = 0;
            _product.Items.Add("全部产品");
            foreach (var product in products ?? Array.Empty<ProductConfig>()) _product.Items.Add(product);
            _product.SelectedIndex = 0;

            AddRow(layout, 0, "开始日期", _startDate);
            AddRow(layout, 1, "结束日期", _endDate);
            AddRow(layout, 2, "检测工位", _station);
            AddRow(layout, 3, "产品名称", _product);

            var buttons = new UIFlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 14, 0, 0),
                FillColor = IndustrialTheme.Panel,
                RectColor = IndustrialTheme.Panel,
                StyleCustomMode = true
            };
            var cancel = Button("取消", (_, __) => Finish(DialogResult.Cancel));
            var export = Button("选择文件并导出", Confirm, 150);
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(export);
            layout.SetColumnSpan(buttons, 2);
            layout.Controls.Add(buttons, 0, 4);

            Controls.Add(layout);
            Controls.Add(UiFactory.Heading(Text, this));
            AcceptButton = export;
            CancelButton = cancel;
            IndustrialTheme.Apply(this);
        }

        private static UIDatePicker DateInput(DateTime value) => new UIDatePicker
        {
            Value = value,
            DateFormat = "yyyy-MM-dd",
            Dock = DockStyle.Fill,
            Height = 34,
            Margin = new Padding(0, 6, 0, 6)
        };

        private static UIComboBox ComboInput() => new UIComboBox
        {
            Dock = DockStyle.Fill,
            Height = 34,
            DropDownStyle = UIDropDownStyle.DropDownList,
            Margin = new Padding(0, 6, 0, 6)
        };

        private static void AddRow(TableLayoutPanel layout, int row, string label, Control input)
        {
            layout.Controls.Add(new UILabel
            {
                Text = label,
                Dock = DockStyle.Fill,
                ForeColor = IndustrialTheme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            layout.Controls.Add(input, 1, row);
        }

        private static UIButton Button(string text, EventHandler click, int width = 110)
        {
            var button = new UIButton { Text = text, Width = width, Height = 38, Margin = new Padding(8, 0, 0, 0), Radius = 0 };
            button.Click += click;
            IndustrialTheme.StyleSunnyButton(button);
            return button;
        }

        private void Confirm(object sender, EventArgs e)
        {
            if (EndDate < StartDate)
            {
                IndustrialMessageBox.Show(this, "结束日期不能早于开始日期。", "日期范围错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Finish(DialogResult.OK);
        }

        private void Finish(DialogResult result)
        {
            DialogResult = result;
            Close();
        }
    }
}
