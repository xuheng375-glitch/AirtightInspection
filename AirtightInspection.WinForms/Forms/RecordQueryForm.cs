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
        private readonly DateTimePicker _startDate;
        private readonly DateTimePicker _endDate;
        private readonly ComboBox _station;
        private readonly ComboBox _product;
        private readonly TextBox _barcode;
        private readonly DataGridView _grid;
        private readonly Label _countLabel;
        private readonly Button _queryButton;
        private List<ScanRecord> _results = new List<ScanRecord>();

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

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(8),
                WrapContents = false,
                BackColor = IndustrialTheme.Panel
            };
            var heading = UiFactory.Label("◆ 检测记录查询");
            heading.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            heading.ForeColor = IndustrialTheme.Accent;
            _queryButton = UiFactory.Button("查询", Query);
            toolbar.Controls.Add(heading);
            toolbar.Controls.Add(_queryButton);
            toolbar.Controls.Add(UiFactory.Button("重置条件", (_, __) => ResetFilters(true)));
            toolbar.Controls.Add(UiFactory.Button("导出查询结果", ExportResults));
            toolbar.Controls.Add(UiFactory.Button("关闭", (_, __) => Close()));
            _countLabel = UiFactory.Label("查询结果：0 条");
            _countLabel.ForeColor = IndustrialTheme.Muted;
            _countLabel.Margin = new Padding(18, 9, 4, 4);
            toolbar.Controls.Add(_countLabel);

            var filters = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 76,
                Padding = new Padding(12, 13, 12, 8),
                WrapContents = false,
                BackColor = IndustrialTheme.Surface
            };
            _startDate = DatePicker(DateTime.Today.AddDays(-7));
            _endDate = DatePicker(DateTime.Today);
            _station = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(5, 4, 14, 0) };
            _product = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(5, 4, 14, 0) };
            _barcode = new TextBox { Width = 230, Margin = new Padding(5, 4, 10, 0), MaxLength = 200 };
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
            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), BackColor = IndustrialTheme.Background };
            content.Controls.Add(_grid);

            Controls.Add(content);
            Controls.Add(filters);
            Controls.Add(toolbar);
            AcceptButton = _queryButton;
            IndustrialTheme.Apply(this);
            heading.ForeColor = IndustrialTheme.Accent;
            Load += (_, __) =>
            {
                LoadFilters();
                Query(this, EventArgs.Empty);
            };
        }

        private static DateTimePicker DatePicker(DateTime value) => new DateTimePicker
        {
            Value = value,
            Width = 130,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd",
            Margin = new Padding(5, 4, 14, 0)
        };

        private static Label FilterLabel(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = IndustrialTheme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(5, 8, 0, 0)
        };

        private static DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DetectTime", HeaderText = "检测时间", FillWeight = 110, MinimumWidth = 175, DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss.fff" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StationNo", HeaderText = "工位号", FillWeight = 38, MinimumWidth = 65 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StationName", HeaderText = "工位名称", FillWeight = 55, MinimumWidth = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductName", HeaderText = "产品名称", FillWeight = 75, MinimumWidth = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Barcode", HeaderText = "条码", FillWeight = 145, MinimumWidth = 190 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AirtightString", HeaderText = "气密字符串", FillWeight = 170, MinimumWidth = 230 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StatusText", HeaderText = "状态", FillWeight = 62, MinimumWidth = 110 });
            return grid;
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
            if (_startDate.Value.Date > _endDate.Value.Date)
            {
                IndustrialMessageBox.Show(this, "开始日期不能晚于结束日期。", "查询条件错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _queryButton.Enabled = false;
            _countLabel.Text = "正在查询...";
            try
            {
                var start = _startDate.Value.Date;
                var endExclusive = _endDate.Value.Date.AddDays(1);
                var stationNo = (_station.SelectedItem as StationConfig)?.StationNo;
                var productName = (_product.SelectedItem as ProductConfig)?.ProductName;
                var barcode = _barcode.Text;
                _results = await Task.Run(() => _database.QueryRecords(start, endExclusive, stationNo, productName, barcode));
                _grid.DataSource = null;
                _grid.DataSource = _results;
                _countLabel.Text = $"查询结果：{_results.Count} 条（最多显示 5000 条）";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "检测记录查询失败");
                _countLabel.Text = "查询失败";
                IndustrialMessageBox.Show(this, "查询失败：" + ex.Message, "查询失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { _queryButton.Enabled = true; }
        }

        private void ResetFilters(bool query)
        {
            _startDate.Value = DateTime.Today.AddDays(-7);
            _endDate.Value = DateTime.Today;
            if (_station.Items.Count > 0) _station.SelectedIndex = 0;
            if (_product.Items.Count > 0) _product.SelectedIndex = 0;
            _barcode.Clear();
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
                $"检测时间：{item.DetectTime:yyyy-MM-dd HH:mm:ss.fff}\n工位：{item.StationNo} - {item.StationName}\n产品：{item.ProductName}\n条码：{item.Barcode}\n气密字符串：{item.AirtightString}\n状态：{item.StatusText}",
                "检测记录详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
