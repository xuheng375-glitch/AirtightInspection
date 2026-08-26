using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AirtightInspection.Config;
using AirtightInspection.Data;
using AirtightInspection.Models;
using AirtightInspection.Services;
using NLog;
using Sunny.UI;

namespace AirtightInspection.Forms
{
    public sealed class MainForm : UIForm
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly AppConfig _config; private readonly Database _database;
        private readonly PendingRecordService _pending; private readonly ModbusService _modbus;
        private readonly ScannerService _scanner; private readonly InspectionService _inspection; private readonly DatabaseMaintenanceService _maintenance;
        private readonly ComboBox _products; private readonly IndustrialCard _productCard; private readonly DataGridView _records, _pendingGrid;
        private readonly ListBox _logList; private readonly Label _plcStatus, _stationStatus, _focusHint, _pendingStatus, _clockStatus, _dateStatus;
        private readonly StringBuilder _keyboardBuffer = new StringBuilder();
        private readonly Timer _keyboardFinalizeTimer;
        private List<StationConfig> _enabledStations = new List<StationConfig>();
        private DateTime _lastKeyTime;
        private bool _keyboardOverflow;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public MainForm(AppConfig config, Database database)
        {
            _config = config; _database = database; _pending = new PendingRecordService();
            _modbus = new ModbusService(config); _scanner = new ScannerService(config);
            _inspection = new InspectionService(database, _pending, _modbus);
            _maintenance = new DatabaseMaintenanceService(config, database);
            _keyboardFinalizeTimer = new Timer
            {
                Interval = Math.Min(800, Math.Max(350, config.KeyboardCharTimeoutMs * 4))
            };
            _keyboardFinalizeTimer.Tick += (_, __) =>
            {
                _keyboardFinalizeTimer.Stop();
                if (_keyboardBuffer.Length >= _config.MinimumBarcodeLength || _keyboardOverflow) CompleteKeyboardBarcode();
                else { _keyboardBuffer.Clear(); SetScanHint("● 已忽略过短的扫码输入", IndustrialTheme.Warning); }
            };
            Text = "气密检测数据采集系统"; Width = 1380; Height = 850; StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None; KeyPreview = true; MinimumSize = new Size(1100, 700); WindowState = FormWindowState.Maximized;
            IndustrialTheme.ApplyWindowIcon(this);
            Image brandImage = null;
            var brandIconPath = Path.Combine(_config.BaseDirectory, "BrandIcon.png");
            if (File.Exists(brandIconPath)) try
            {
                using (var source = Image.FromFile(brandIconPath)) brandImage = new Bitmap(source);
            }
            catch (Exception ex) { Log.Warn(ex, "左侧品牌图标加载失败"); }
            if (brandImage == null && Icon != null) brandImage = Icon.ToBitmap();

            MouseEventHandler drag = (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); } };

            // 左侧工业控制栏
            var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.FromArgb(14, 23, 30), Padding = new Padding(10) };
            sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 132)); sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
            var brand = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 23, 30), Cursor = Cursors.SizeAll };
            var brandIcon = new PictureBox { Location = new Point(8, 10), Size = new Size(58, 58), SizeMode = PictureBoxSizeMode.Zoom, Image = brandImage, BackColor = Color.Transparent };
            var brandTitle = new Label { Text = "气密检测", Location = new Point(76, 11), AutoSize = true, ForeColor = IndustrialTheme.Accent, Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold) };
            var brandSub = new Label { Text = "数据采集系统", Location = new Point(78, 43), AutoSize = true, ForeColor = IndustrialTheme.Muted, Font = new Font("Microsoft YaHei UI", 8F) };
            var brandLine = new Panel { Location = new Point(8, 82), Height = 1, Width = 180, BackColor = IndustrialTheme.AccentDark };
            var modeLabel = new Label { Text = "生产监控终端 / 01", Location = new Point(8, 94), AutoSize = true, ForeColor = Color.FromArgb(92, 122, 136), Font = new Font("Microsoft YaHei UI", 8F) };
            brand.Controls.AddRange(new Control[] { brandIcon, brandTitle, brandSub, brandLine, modeLabel }); brand.MouseDown += drag; brandTitle.MouseDown += drag; brandSub.MouseDown += drag;
            sidebar.Controls.Add(brand, 0, 0);

            var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 14, 0, 0), BackColor = Color.FromArgb(14, 23, 30) };
            navigation.Controls.Add(NavigationButton("01  工位配置", OpenStations));
            navigation.Controls.Add(NavigationButton("02  产品配置", OpenProducts));
            navigation.Controls.Add(NavigationButton("03  作业指导书", OpenManual));
            navigation.Controls.Add(NavigationButton("04  数据查询", OpenRecordQuery));
            navigation.Controls.Add(NavigationButton("05  导出检测记录", ExportCsv));
            sidebar.Controls.Add(navigation, 0, 1);

            var sidebarFooter = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(14, 23, 30) };
            _clockStatus = new Label { Text = "--:--:--", Dock = DockStyle.Top, Height = 34, ForeColor = IndustrialTheme.Text, Font = new Font("Consolas", 16F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            _dateStatus = new Label { Text = DateTime.Now.ToString("yyyy-MM-dd"), Dock = DockStyle.Top, Height = 24, ForeColor = IndustrialTheme.Muted, Font = new Font("Consolas", 9F), TextAlign = ContentAlignment.MiddleLeft };
            var versionLabel = new Label { Text = "本地采集节点  ·  版本 1.0", Dock = DockStyle.Bottom, Height = 24, ForeColor = Color.FromArgb(80, 109, 122), Font = new Font("Microsoft YaHei UI", 8F) };
            sidebarFooter.Controls.Add(versionLabel); sidebarFooter.Controls.Add(_dateStatus); sidebarFooter.Controls.Add(_clockStatus); sidebar.Controls.Add(sidebarFooter, 0, 2);

            // 顶部设备状态卡
            var statusGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = IndustrialTheme.Background, Padding = new Padding(2) };
            for (var i = 0; i < 4; i++) statusGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            _plcStatus = StatusValue("离线", IndustrialTheme.Danger);
            _stationStatus = StatusValue("--", IndustrialTheme.Text);
            _pendingStatus = StatusValue("00", IndustrialTheme.Text);
            _products = new ComboBox { Height = 38, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold) };
            _productCard = ProductCard(_products);
            statusGrid.Controls.Add(_productCard, 0, 0);
            statusGrid.Controls.Add(StatusCard("PLC当前的工位号", _stationStatus, IndustrialTheme.Accent), 1, 0);
            statusGrid.Controls.Add(StatusCard("控制器连接状态", _plcStatus, IndustrialTheme.Danger), 2, 0);
            statusGrid.Controls.Add(StatusCard("待检测记录数量", _pendingStatus, IndustrialTheme.Success), 3, 0);
            statusGrid.MouseDown += drag;

            // 扫码提示与窗口命令区
            var productBar = new Panel { Dock = DockStyle.Fill, BackColor = IndustrialTheme.Panel, Padding = new Padding(10, 7, 8, 6) };
            var productCommands = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = IndustrialTheme.Panel, Padding = new Padding(2, 1, 0, 0) };
            _focusHint = UiFactory.Label(_scanner.IsKeyboardMode ? "● 请点击本窗口后扫码" : "● 串口扫码枪已启用"); _focusHint.ForeColor = IndustrialTheme.Warning;
            _focusHint.Margin = new Padding(4, 7, 0, 0);
            productCommands.Controls.Add(_focusHint);
            var windowButtons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 100, Padding = new Padding(0, 2, 0, 0), FlowDirection = FlowDirection.LeftToRight, BackColor = IndustrialTheme.Panel };
            var minimizeButton = new Button { Text = "—", Width = 42, Height = 32, Margin = new Padding(2) };
            var closeButton = new Button { Text = "×", Width = 42, Height = 32, Margin = new Padding(2) };
            IndustrialTheme.StyleButton(minimizeButton); IndustrialTheme.StyleButton(closeButton, true);
            minimizeButton.Click += (_, __) => WindowState = FormWindowState.Minimized; closeButton.Click += (_, __) => Close();
            windowButtons.Controls.Add(minimizeButton); windowButtons.Controls.Add(closeButton); productBar.Controls.Add(productCommands); productBar.Controls.Add(windowButtons);

            // 实时数据与事件流
            var upper = new SplitContainer { Dock = DockStyle.Fill, BackColor = IndustrialTheme.Background, SplitterWidth = 6 };
            upper.Resize += (_, __) =>
            {
                if (upper.Width < 800) return;
                var target = Math.Max(500, Math.Min(upper.Width - 280, (int)(upper.Width * 0.68)));
                if (target > 0 && target < upper.Width) upper.SplitterDistance = target;
            };
            _records = CreateRecordsGrid(); _pendingGrid = CreatePendingGrid();
            _records.CellDoubleClick += (_, e) => ShowRecordDetail(e.RowIndex);
            upper.Panel1.Controls.Add(WrapWithTitle("检测记录", _records)); upper.Panel2.Controls.Add(WrapWithTitle("待检测记录（仅内存）", _pendingGrid));
            _logList = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, BackColor = IndustrialTheme.Background, Padding = new Padding(7) };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 92)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 145));
            content.Controls.Add(statusGrid, 0, 0); content.Controls.Add(productBar, 0, 1); content.Controls.Add(upper, 0, 2); content.Controls.Add(WrapWithTitle("运行事件日志", _logList), 0, 3);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, ColumnCount = 2, BackColor = IndustrialTheme.Background, Padding = new Padding(1) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 218)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(sidebar, 0, 0); root.Controls.Add(content, 1, 0); Controls.Add(root);

            IndustrialTheme.Apply(this);
            IndustrialTheme.StyleButton(closeButton, true);
            brandTitle.ForeColor = IndustrialTheme.Accent; _focusHint.ForeColor = IndustrialTheme.Warning;
            _plcStatus.ForeColor = IndustrialTheme.Danger;

            _pending.Changed += (_, __) => SafeUi(RefreshPending);
            _modbus.ConnectionChanged += (_, connected) => SafeUi(() => SetConnection(connected));
            _modbus.StationChanged += (_, station) => SafeUi(() => _stationStatus.Text = station.ToString("00"));
            _modbus.Message += (_, text) => AddLog(text); _scanner.Message += (_, text) => AddLog(text);
            _scanner.BarcodeReceived += (_, barcode) => SafeUi(() => HandleBarcode(barcode));
            _inspection.Message += (_, text) => AddLog(text);
            _maintenance.Message += (_, text) => AddLog(text);
            _maintenance.Warning += (_, text) => SafeUi(() =>
            {
                AddLog(text);
                IndustrialMessageBox.Show(this, text, "数据库维护告警", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            });
            _inspection.RecordsChanged += (_, __) => SafeUi(() =>
            {
                RefreshRecords();
                SetScanHint("● 上位机已入库", IndustrialTheme.Success);
            });
            _products.SelectedIndexChanged += (_, __) => UpdateProductStatus();
            KeyPress += OnKeyPress; Shown += OnShown; FormClosing += OnClosing;
        }

        private static Button NavigationButton(string text, EventHandler click)
        {
            var button = new Button { Text = text, Width = 190, Height = 46, Margin = new Padding(0, 0, 0, 8),
                TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) };
            IndustrialTheme.StyleButton(button); button.FlatAppearance.BorderSize = 0; button.BackColor = IndustrialTheme.Surface;
            button.Click += click; return button;
        }

        private static Label StatusValue(string text, Color color) => new Label
        {
            Text = text, Dock = DockStyle.Fill, ForeColor = color, TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Microsoft YaHei UI", 16F, FontStyle.Bold), AutoEllipsis = true
        };

        private static Control StatusCard(string caption, Label value, Color accent)
        {
            var card = new IndustrialCard { Dock = DockStyle.Fill, AccentColor = accent };
            var title = new Label { Text = caption, Dock = DockStyle.Top, Height = 24, ForeColor = IndustrialTheme.Muted,
                Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            card.Controls.Add(value); card.Controls.Add(title); return card;
        }

        private static IndustrialCard ProductCard(ComboBox products)
        {
            var card = new IndustrialCard { Dock = DockStyle.Fill, AccentColor = IndustrialTheme.Warning };
            var title = new Label
            {
                Text = "当前产品名称  /  点击切换",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = IndustrialTheme.Muted,
                Font = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var selectorHost = new Panel { Dock = DockStyle.Fill, BackColor = IndustrialTheme.Panel, Padding = new Padding(0, 2, 0, 0) };
            products.Dock = DockStyle.Top;
            products.Margin = Padding.Empty;
            selectorHost.Controls.Add(products);
            card.Controls.Add(selectorHost);
            card.Controls.Add(title);
            return card;
        }

        private static Control WrapWithTitle(string title, Control content)
        {
            var group = new IndustrialGroupBox { Text = "  " + title.ToUpperInvariant() + "  ", Dock = DockStyle.Fill, Padding = new Padding(8),
                BackColor = IndustrialTheme.Panel, ForeColor = IndustrialTheme.Accent }; group.Controls.Add(content); return group;
        }
        private static DataGridView BaseGrid() => new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = IndustrialTheme.Background };
        private static DataGridView CreateRecordsGrid()
        {
            var grid = BaseGrid();
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "DetectTime", HeaderText = "时间", FillWeight = 78, MinimumWidth = 135, DefaultCellStyle = new DataGridViewCellStyle { Format = "yy-MM-dd HH:mm:ss" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StationNo", HeaderText = "工位", FillWeight = 32, MinimumWidth = 55 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StationName", HeaderText = "工位名称", FillWeight = 48, MinimumWidth = 75 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductName", HeaderText = "产品名称", FillWeight = 62, MinimumWidth = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Barcode", HeaderText = "条码", FillWeight = 125, MinimumWidth = 170 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProgramDisplay", HeaderText = "程序号", FillWeight = 42, MinimumWidth = 65 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "PressureDisplay", HeaderText = "测试压力", FillWeight = 68, MinimumWidth = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "LeakDisplay", HeaderText = "泄漏值", FillWeight = 70, MinimumWidth = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InstrumentStatusText", HeaderText = "仪器状态", FillWeight = 90, MinimumWidth = 125 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StatusText", HeaderText = "入库状态", FillWeight = 64, MinimumWidth = 105 });
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
        private static DataGridView CreatePendingGrid()
        {
            var grid = BaseGrid();
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StationNo", HeaderText = "工位号", FillWeight = 40, MinimumWidth = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductName", HeaderText = "产品名", FillWeight = 70, MinimumWidth = 90 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Barcode", HeaderText = "条码", FillWeight = 180, MinimumWidth = 200 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ScanTime", HeaderText = "扫码时间", FillWeight = 76, MinimumWidth = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "yy-MM-dd HH:mm:ss" } }); return grid;
        }

        private void OnShown(object sender, EventArgs e)
        {
            SwitchToEnglishInputLanguage();
            RefreshProducts(); RefreshStations(); RefreshRecords(); RefreshPending(); _scanner.Start(); _modbus.Start(); _maintenance.Start();
            var timer = new Timer { Interval = 1000 }; timer.Tick += (_, __) => { var now = DateTime.Now; _clockStatus.Text = now.ToString("HH:mm:ss"); _dateStatus.Text = now.ToString("yyyy-MM-dd"); CheckTimeouts(); }; timer.Start(); Tag = timer;
            AddLog("系统启动完成"); Activate();
        }
        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (_pending.Snapshot().Count > 0 && IndustrialMessageBox.Show(this, "仍有待检测记录，退出后这些内存数据将丢失。确认退出？", "退出确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { e.Cancel = true; return; }
            (Tag as Timer)?.Stop();
            _keyboardFinalizeTimer.Stop(); _keyboardFinalizeTimer.Dispose();
            _maintenance.Dispose();
            _scanner.Dispose(); _modbus.Dispose();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            var keyCode = keyData & Keys.KeyCode;
            if (CanCaptureKeyboardScan() && keyCode == Keys.Enter)
            {
                CompleteKeyboardBarcode();
                return true;
            }
            if (CanCaptureKeyboardScan() && keyCode == Keys.Tab)
            {
                AppendKeyboardCharacter('\t');
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (!CanCaptureKeyboardScan()) return;
            if (e.KeyChar == '\r' || e.KeyChar == '\n')
            {
                CompleteKeyboardBarcode(); e.Handled = true; return;
            }
            var resetThreshold = Math.Max(2000, _keyboardFinalizeTimer.Interval * 4);
            if ((DateTime.Now - _lastKeyTime).TotalMilliseconds > resetThreshold) { _keyboardBuffer.Clear(); _keyboardOverflow = false; }
            if (!char.IsControl(e.KeyChar) || IsBarcodeSeparator(e.KeyChar))
            {
                AppendKeyboardCharacter(e.KeyChar);
                e.Handled = true;
            }
        }

        private static bool IsBarcodeSeparator(char value) =>
            value == '\t' || value == '\x1C' || value == '\x1D' || value == '\x1E' || value == '\x1F';

        private void AppendKeyboardCharacter(char value)
        {
            if (_keyboardBuffer.Length >= _config.MaxBarcodeLength) { _keyboardOverflow = true; return; }
            _keyboardBuffer.Append(value);
            _lastKeyTime = DateTime.Now;
            _keyboardFinalizeTimer.Stop(); _keyboardFinalizeTimer.Start();
        }

        private bool CanCaptureKeyboardScan() =>
            _scanner.IsKeyboardMode && ContainsFocus && Application.OpenForms.Count == 1;

        private void CompleteKeyboardBarcode()
        {
            _keyboardFinalizeTimer.Stop();
            var barcode = _keyboardBuffer.ToString().Trim();
            var overflow = _keyboardOverflow;
            _keyboardBuffer.Clear();
            _keyboardOverflow = false;
            if (barcode.Length == 0) return;
            if (overflow || barcode.Length > _config.MaxBarcodeLength)
            {
                SetScanHint("● 扫码输入超过最大长度，已拒绝", IndustrialTheme.Danger);
                AddLog("扫码输入超过最大长度，为防止条码被截断未进入队列");
                return;
            }
            if (barcode.Length < _config.MinimumBarcodeLength)
            {
                SetScanHint($"● 扫码输入少于 {_config.MinimumBarcodeLength} 个字符，已忽略", IndustrialTheme.Warning);
                return;
            }
            Log.Info("扫码枪输入已接收，条码长度：{0}", barcode.Length);
            AddLog($"已接收扫码输入（{barcode.Length} 字符），请选择工位");
            HandleBarcode(barcode);
        }
        private void HandleBarcode(string barcode)
        {
            SetScanHint($"● 已检测到扫码枪输入（{barcode.Length} 字符）", IndustrialTheme.Success);
            var product = _products.SelectedItem as ProductConfig;
            if (product == null)
            {
                SetScanHint("● 扫码未入队：请先选择生产产品", IndustrialTheme.Danger);
                IndustrialMessageBox.Show(this, "请先配置并选择当前生产产品。", "扫码未入队", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_enabledStations.Count == 0) RefreshStations();
            if (_enabledStations.Count == 0)
            {
                SetScanHint("● 扫码未入队：没有启用中的工位", IndustrialTheme.Danger);
                IndustrialMessageBox.Show(this, "没有启用中的工位，请先配置工位", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
            }
            var preparation = Stopwatch.StartNew();
            using (var dialog = new ScanStationForm(barcode, _enabledStations))
            {
                preparation.Stop();
                Log.Info("工位选择窗口准备耗时：{0} ms", preparation.ElapsedMilliseconds);
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.SelectedStation == null)
                {
                    SetScanHint("● 已取消工位选择，扫码未进入待检测队列", IndustrialTheme.Warning);
                    return;
                }
                PendingRecord old; var station = dialog.SelectedStation;
                if (_pending.TryGet(station.StationNo, out old) && IndustrialMessageBox.Show(this, $"该工位已有待检测条码 [{old.Barcode}]，是否用新条码 [{barcode}] 覆盖？", "覆盖确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    SetScanHint("● 已取消覆盖，扫码未进入待检测队列", IndustrialTheme.Warning);
                    return;
                }
                _pending.AddOrReplace(new PendingRecord { StationNo = station.StationNo, StationName = station.StationName,
                    Barcode = barcode, ProductName = product.ProductName, ScanTime = DateTime.Now });
                SetScanHint($"● 已进入待检测队列 · 工位 {station.StationNo:00}", IndustrialTheme.Success);
                Log.Info("扫码 {0} 已分配到工位 {1}，产品 {2}", barcode, station.StationNo, product.ProductName);
                AddLog($"扫码 {barcode} 已分配到工位 {station.StationNo}");
            }
        }

        private void SetScanHint(string text, Color color)
        {
            _focusHint.Text = text;
            _focusHint.ForeColor = color;
            _focusHint.Refresh();
        }
        private void CheckTimeouts()
        {
            var expired = _pending.RemoveExpired(_config.WaitTimeoutSec);
            foreach (var item in expired) AddLog($"工位 {item.StationNo} 待检测条码 {item.Barcode} 已超时作废");
            if (expired.Count > 0) IndustrialMessageBox.Show(this, $"已有 {expired.Count} 条待检测记录超时作废，请查看运行日志。", "超时提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        private void OpenManual(object sender, EventArgs e)
        {
            var product = _products.SelectedItem as ProductConfig; if (product == null) { IndustrialMessageBox.Show(this, "请先选择产品", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (var form = new ManualViewerForm(_config.ManualFolder, product.ProductName)) form.ShowDialog(this);
        }
        private void OpenStations(object sender, EventArgs e)
        {
            using (var form = new StationConfigForm(_database, _config)) form.ShowDialog(this);
            RefreshStations();
        }
        private void OpenProducts(object sender, EventArgs e)
        {
            var selected = (_products.SelectedItem as ProductConfig)?.ProductName;
            using (var form = new ProductConfigForm(_database, _config)) form.ShowDialog(this);
            RefreshProducts(selected);
        }
        private void OpenRecordQuery(object sender, EventArgs e)
        {
            using (var form = new RecordQueryForm(_database)) form.ShowDialog(this);
        }
        private async void ExportCsv(object sender, EventArgs e)
        {
            using (var dialog = new SaveFileDialog { Filter = "CSV 文件|*.csv", FileName = "检测记录_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { AddLog("正在流式导出 CSV..."); await CsvExportService.ExportAsync(dialog.FileName, _database.EnumerateRecords()); AddLog("CSV 导出成功：" + dialog.FileName); IndustrialMessageBox.Show(this, "检测记录已成功导出。", "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                catch (Exception ex) { Log.Error(ex, "CSV 导出失败"); AddLog("CSV 导出失败：" + ex.Message); IndustrialMessageBox.Show(this, "导出失败：" + ex.Message, "导出失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }
        private void RefreshProducts(string selectName = null)
        {
            var products = _database.GetProducts(); _products.DataSource = products;
            if (!string.IsNullOrEmpty(selectName)) for (var i = 0; i < products.Count; i++) if (products[i].ProductName == selectName) { _products.SelectedIndex = i; break; }
            UpdateProductStatus();
        }
        private void UpdateProductStatus()
        {
            var product = _products.SelectedItem as ProductConfig;
            _productCard.AccentColor = product == null ? IndustrialTheme.Danger : IndustrialTheme.Warning;
            _products.ForeColor = product == null ? IndustrialTheme.Danger : IndustrialTheme.Text;
            _productCard.Invalidate();
        }
        private void RefreshStations() => _enabledStations = _database.GetStations(true);

        private static void SwitchToEnglishInputLanguage()
        {
            foreach (InputLanguage language in InputLanguage.InstalledInputLanguages)
            {
                if (!language.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase)) continue;
                InputLanguage.CurrentInputLanguage = language;
                break;
            }
        }
        private void RefreshRecords() { _records.DataSource = _database.GetRecords(_config.DisplayRecordLimit); }
        private void RefreshPending()
        {
            var items = new List<PendingRecord>(_pending.Snapshot()); _pendingGrid.DataSource = items;
            _pendingStatus.Text = items.Count.ToString("00"); _pendingStatus.ForeColor = items.Count == 0 ? IndustrialTheme.Text : IndustrialTheme.Warning;
        }
        private void ShowRecordDetail(int rowIndex)
        {
            if (rowIndex < 0) return; var item = _records.Rows[rowIndex].DataBoundItem as ScanRecord; if (item == null) return;
            IndustrialMessageBox.Show(this, $"检测时间：{item.DetectTime:yyyy-MM-dd HH:mm:ss.fff}\n工位：{item.StationNo} - {item.StationName}\n产品：{item.ProductName}\n条码：{item.Barcode}\n程序号：{item.ProgramDisplay}\n测试压力：{item.PressureDisplay}\n泄漏值：{item.LeakDisplay}\n仪器状态：{item.InstrumentStatusText}\n气密原始字符串：{item.AirtightString}\n入库状态：{item.StatusText}", "检测记录详情", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void SetConnection(bool connected)
        {
            _plcStatus.Text = connected ? "在线" : "离线"; _plcStatus.ForeColor = connected ? IndustrialTheme.Success : IndustrialTheme.Danger;
            var card = _plcStatus.Parent as IndustrialCard; if (card != null) { card.AccentColor = _plcStatus.ForeColor; card.Invalidate(); }
        }
        private void AddLog(string text) => SafeUi(() => { _logList.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + text); while (_logList.Items.Count > 300) _logList.Items.RemoveAt(_logList.Items.Count - 1); });
        private void SafeUi(Action action) { if (IsDisposed || !IsHandleCreated) return; if (InvokeRequired) BeginInvoke(action); else action(); }
    }
}
