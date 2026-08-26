using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AirtightInspection.Data;
using AirtightInspection.Models;
using AirtightInspection.Services;
using NLog;
using Sunny.UI;

namespace AirtightInspection.Forms
{
    public sealed class RecordQueryForm : UIForm
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly Database _database;
        private readonly UIDatePicker _startDate;
        private readonly UIDatePicker _endDate;
        private readonly UIComboBox _station;
        private readonly UIComboBox _product;
        private readonly UITextBox _barcode;
        private readonly UIDataGridView _grid;
        private readonly UILabel _countLabel;
        private readonly UIButton _queryButton;
        private List<ScanRecord> _results = new List<ScanRecord>();
        private int _queryVersion;

        public RecordQueryForm(Database database)
        {
            _database = database;
            Text = "检测记录查询";
            Width = 1320;
            Height = 780;
            MinimumSize = new Size(1100, 680);
            StartPosition = FormStartPosition.CenterParent;
            WindowState = FormWindowState.Maximized;
            FormBorderStyle = FormBorderStyle.None;

            var toolbar = new UIFlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(8),
                WrapContents = false,
                FillColor = IndustrialTheme.Panel,
                RectColor = IndustrialTheme.Panel,
                StyleCustomMode = true
            };
            var heading = new UILabel { Text = "◆ 检测记录查询", AutoSize = true, Margin = new Padding(4, 9, 4, 4) };
            heading.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            heading.ForeColor = IndustrialTheme.Accent;
            _queryButton = SunnyButton("查询", Query);
            toolbar.Controls.Add(heading);
            toolbar.Controls.Add(_queryButton);
            toolbar.Controls.Add(SunnyButton("重置条件", (_, __) => ResetFilters(true)));
            toolbar.Controls.Add(SunnyButton("导出查询结果", ExportResults, 116));
            toolbar.Controls.Add(SunnyButton("关闭", (_, __) => Close()));
            _countLabel = new UILabel { Text = "查询结果：0 条", AutoSize = true };
            _countLabel.ForeColor = IndustrialTheme.Muted;
            _countLabel.Margin = new Padding(18, 9, 4, 4);
            toolbar.Controls.Add(_countLabel);

            var filters = new UIFlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 76,
                Padding = new Padding(12, 13, 12, 8),
                WrapContents = false,
                FillColor = IndustrialTheme.Surface,
                RectColor = IndustrialTheme.Surface,
                StyleCustomMode = true
            };
            _startDate = DateInput(DateTime.Today.AddDays(-7));
            _endDate = DateInput(DateTime.Today);
            _station = new UIComboBox { Width = 150, Height = 32, DropDownStyle = UIDropDownStyle.DropDownList, Margin = new Padding(5, 4, 14, 0) };
            _product = new UIComboBox { Width = 190, Height = 32, DropDownStyle = UIDropDownStyle.DropDownList, Margin = new Padding(5, 4, 14, 0) };
            _barcode = new UITextBox { Width = 230, Height = 32, Margin = new Padding(5, 4, 10, 0), MaxLength = 200, Watermark = "输入条码关键字" };
            filters.Controls.AddRange(new Control[]
            {
                FilterLabel("开始日期"), _startDate,
                FilterLabel("结束日期"), _endDate,
                FilterLabel("工位"), _station,
                FilterLabel("产品"), _product,
                FilterLabel("条码"), _barcode
            });

            _grid = CreateGrid();
            _grid.CellDoubleClick += (_, e) => ShowDetail(e.RowIndex);
            var content = new UIPanel { Dock = DockStyle.Fill, Padding = new Padding(10), FillColor = IndustrialTheme.Background, RectColor = IndustrialTheme.Background, StyleCustomMode = true };
            content.Controls.Add(_grid);

            Controls.Add(content);
            Controls.Add(filters);
            Controls.Add(toolbar);
            AcceptButton = _queryButton;
            IndustrialTheme.Apply(this);
            _grid.StripeOddColor = IndustrialTheme.Panel;
            _grid.StripeEvenColor = IndustrialTheme.Panel;
            _grid.RowsDefaultCellStyle.BackColor = IndustrialTheme.Panel;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = IndustrialTheme.Panel;
            heading.ForeColor = IndustrialTheme.Accent;
            Load += (_, __) =>
            {
                LoadFilters();
                Query(this, EventArgs.Empty);
            };
        }

        private static UIDatePicker DateInput(DateTime value) => new UIDatePicker
        {
            Value = value,
            DateFormat = "yyyy-MM-dd",
            Width = 130,
            Height = 32,
            Margin = new Padding(5, 4, 14, 0)
        };

        private static UILabel FilterLabel(string text) => new UILabel
        {
            Text = text,
            AutoSize = true,
            ForeColor = IndustrialTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(5, 8, 0, 0)
        };

        private static UIDataGridView CreateGrid()
        {
            var grid = new UIDataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DetectTime", HeaderText = "检测时间", FillWeight = 78, MinimumWidth = 135, DefaultCellStyle = new DataGridViewCellStyle { Format = "yy-MM-dd HH:mm:ss" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StationNo", HeaderText = "工位", FillWeight = 32, MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StationName", HeaderText = "工位名称", FillWeight = 45, MinimumWidth = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductName", HeaderText = "产品名称", FillWeight = 60, MinimumWidth = 100 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Barcode", HeaderText = "条码", FillWeight = 120, MinimumWidth = 180 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProgramDisplay", HeaderText = "程序号", FillWeight = 40, MinimumWidth = 65 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PressureDisplay", HeaderText = "测试压力", FillWeight = 65, MinimumWidth = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LeakDisplay", HeaderText = "泄漏值", FillWeight = 68, MinimumWidth = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InstrumentStatusText", HeaderText = "仪器状态", FillWeight = 90, MinimumWidth = 125 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AirtightString", HeaderText = "气密原始字符串", FillWeight = 135, MinimumWidth = 190 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StatusText", HeaderText = "入库状态", FillWeight = 62, MinimumWidth = 105 });
            grid.CellFormatting += FormatInstrumentResult;
            return grid;
        }

        private static void FormatInstrumentResult(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var grid = (DataGridView)sender;
            if (e.RowIndex < 0 || grid.Columns[e.ColumnIndex].DataPropertyName != "InstrumentStatusText") return;
            var item = grid.Rows[e.RowIndex].DataBoundItem as ScanRecord;
            if (item == null) return;
            var statusColor = string.IsNullOrWhiteSpace(item.ResultCode)
                ? IndustrialTheme.Muted
                : string.Equals(item.ResultCode, "OK", StringComparison.OrdinalIgnoreCase)
                    ? IndustrialTheme.Success
                    : string.Equals(item.ResultCode, "NG", StringComparison.OrdinalIgnoreCase)
                        ? IndustrialTheme.Danger
                    : string.Equals(item.ResultCode, "AL", StringComparison.OrdinalIgnoreCase)
                        ? IndustrialTheme.Warning
                        : IndustrialTheme.Danger;
            e.CellStyle.ForeColor = statusColor;
            e.CellStyle.SelectionForeColor = statusColor;
        }

        private static UIButton SunnyButton(string text, EventHandler click, int width = 88)
        {
            var button = new UIButton { Text = text, Width = width, Height = 34, Margin = new Padding(4), Radius = 0 };
            button.Click += click;
            IndustrialTheme.StyleSunnyButton(button);
            return button;
        }

        private void LoadFilters()
        {
            _station.Items.Clear();
            _station.Items.Add("全部工位");
            foreach (var item in _database.GetStations()) _station.Items.Add(item);
            _station.SelectedIndex = 0;

            _product.Items.Clear();
            _product.Items.Add("全部产品");
            foreach (var item in _database.GetProducts()) _product.Items.Add(item);
            _product.SelectedIndex = 0;
        }

        private async void Query(object sender, EventArgs e)
        {
            var start = _startDate.Value.Date;
            var end = _endDate.Value.Date;
            if (start > end)
            {
                IndustrialMessageBox.Show(this, "开始日期不能晚于结束日期。", "查询条件错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var queryVersion = ++_queryVersion;
            _queryButton.Enabled = false;
            _countLabel.Text = "正在查询...";
            try
            {
                var endExclusive = end.AddDays(1);
                var stationNo = (_station.SelectedItem as StationConfig)?.StationNo;
                var productName = (_product.SelectedItem as ProductConfig)?.ProductName;
                var barcode = _barcode.Text;
                var results = await Task.Run(() => _database.QueryRecords(start, endExclusive, stationNo, productName, barcode));
                if (queryVersion != _queryVersion) return;
                _results = results;
                _grid.DataSource = null;
                _grid.DataSource = _results;
                _countLabel.Text = $"查询结果：{_results.Count} 条（最多显示 5000 条）";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "检测记录查询失败");
                if (queryVersion != _queryVersion) return;
                _countLabel.Text = "查询失败";
                IndustrialMessageBox.Show(this, "查询失败：" + ex.Message, "查询失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { if (queryVersion == _queryVersion) _queryButton.Enabled = true; }
        }

        private void ResetFilters(bool query)
        {
            _startDate.Value = DateTime.Today.AddDays(-7);
            _endDate.Value = DateTime.Today;
            if (_station.Items.Count > 0) _station.SelectedIndex = 0;
            if (_product.Items.Count > 0) _product.SelectedIndex = 0;
            _barcode.Text = string.Empty;
            if (query) Query(this, EventArgs.Empty);
        }

        private async void ExportResults(object sender, EventArgs e)
        {
            if (_results.Count == 0)
            {
                IndustrialMessageBox.Show(this, "当前没有可导出的查询结果。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var dialog = new SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                FileName = "查询结果_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    await CsvExportService.ExportAsync(dialog.FileName, _results);
                    IndustrialMessageBox.Show(this, "查询结果已成功导出。", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "查询结果导出失败");
                    IndustrialMessageBox.Show(this, "导出失败：" + ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowDetail(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _grid.Rows.Count) return;
            var item = _grid.Rows[rowIndex].DataBoundItem as ScanRecord;
            if (item == null) return;
            IndustrialMessageBox.Show(this,
                $"检测时间：{item.DetectTime:yyyy-MM-dd HH:mm:ss.fff}\n工位：{item.StationNo} - {item.StationName}\n产品：{item.ProductName}\n条码：{item.Barcode}\n程序号：{item.ProgramDisplay}\n测试压力：{item.PressureDisplay}\n泄漏值：{item.LeakDisplay}\n仪器状态：{item.InstrumentStatusText}\n气密原始字符串：{item.AirtightString}\n入库状态：{item.StatusText}",
                "检测记录详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
