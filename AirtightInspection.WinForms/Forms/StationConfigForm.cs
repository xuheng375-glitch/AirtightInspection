using System;
using Microsoft.Data.Sqlite;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AirtightInspection.Config;
using AirtightInspection.Data;
using AirtightInspection.Models;
using NLog;
using Sunny.UI;

namespace AirtightInspection.Forms
{
    public sealed class StationConfigForm : UIForm
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly Database _database; private readonly AppConfig _config; private readonly DataGridView _grid;
        public event EventHandler StationsChanged;
        public StationConfigForm(Database database, AppConfig config)
        {
            _database = database; _config = config; Text = "工位配置"; Width = 760; Height = 520; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.None;
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(8), BackColor = IndustrialTheme.Panel };
            var heading = UiFactory.Label("◆ 工位配置"); heading.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold); heading.ForeColor = IndustrialTheme.Accent;
            heading.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); } };
            toolbar.Controls.Add(heading);
            toolbar.Controls.Add(UiFactory.Button("新增", Add)); toolbar.Controls.Add(UiFactory.Button("修改", Edit)); toolbar.Controls.Add(UiFactory.Button("删除", Delete));
            toolbar.Controls.Add(UiFactory.Button("关闭", (_, __) => Close()));
            _grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StationNo", HeaderText = "工位号" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "StationName", HeaderText = "工位名称" });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "Enabled", HeaderText = "启用",
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } },
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Remark", HeaderText = "备注" });
            Controls.Add(_grid); Controls.Add(toolbar); IndustrialTheme.Apply(this); heading.ForeColor = IndustrialTheme.Accent; Load += (_, __) => RefreshData();
        }
        private StationConfig Selected => _grid.CurrentRow?.DataBoundItem as StationConfig;
        private bool Authenticate()
        {
            using (var dialog = new TextInputForm("密码验证", "请输入管理密码：", password: true))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                if (dialog.Value == _config.Password) return true;
                Log.Warn("工位配置密码校验失败"); MessageBox.Show("密码错误，操作未执行", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false;
            }
        }
        private void Add(object sender, EventArgs e)
        {
            if (!Authenticate()) return;
            using (var dialog = new StationEditForm(null)) if (dialog.ShowDialog(this) == DialogResult.OK)
                TrySave(() => _database.AddStation(dialog.Station), "工位新增成功");
        }
        private void Edit(object sender, EventArgs e)
        {
            if (Selected == null) { MessageBox.Show("请先选择工位"); return; } if (!Authenticate()) return;
            var copy = new StationConfig { Id = Selected.Id, StationNo = Selected.StationNo, StationName = Selected.StationName, Enabled = Selected.Enabled, Remark = Selected.Remark };
            using (var dialog = new StationEditForm(copy)) if (dialog.ShowDialog(this) == DialogResult.OK)
                TrySave(() => _database.UpdateStation(dialog.Station), "工位修改成功");
        }
        private void Delete(object sender, EventArgs e)
        {
            if (Selected == null) { MessageBox.Show("请先选择工位"); return; } if (!Authenticate()) return;
            if (MessageBox.Show($"确认删除工位 {Selected.StationNo} - {Selected.StationName}？\n历史检测记录不会删除。", "二次确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            TrySave(() => _database.DeleteStation(Selected.Id), "工位删除成功");
        }
        private void TrySave(Action action, string message)
        {
            try { action(); Log.Info(message); RefreshData(); StationsChanged?.Invoke(this, EventArgs.Empty); }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { MessageBox.Show("工位号已存在，请使用其他编号"); }
            catch (Exception ex) { Log.Error(ex, "工位配置保存失败"); MessageBox.Show("操作失败：" + ex.Message); }
        }
        private void RefreshData() { _grid.DataSource = _database.GetStations(); }
    }
}
