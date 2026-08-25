using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AirtightInspection.Config;
using AirtightInspection.Data;
using AirtightInspection.Models;
using AirtightInspection.Utilities;
using Microsoft.Data.Sqlite;
using NLog;
using Sunny.UI;

namespace AirtightInspection.Forms
{
    public sealed class ProductConfigForm : UIForm
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly Database _database;
        private readonly AppConfig _config;
        private readonly DataGridView _grid;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public ProductConfigForm(Database database, AppConfig config)
        {
            _database = database; _config = config;
            Text = "产品配置"; Width = 720; Height = 520; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.None;

            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, Padding = new Padding(8), BackColor = IndustrialTheme.Panel };
            var heading = UiFactory.Label("◆ 产品配置");
            heading.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold); heading.ForeColor = IndustrialTheme.Accent;
            heading.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); } };
            toolbar.Controls.Add(heading);
            toolbar.Controls.Add(UiFactory.Button("新增产品", Add));
            toolbar.Controls.Add(UiFactory.Button("重命名", Rename));
            toolbar.Controls.Add(UiFactory.Button("删除产品", Delete));
            toolbar.Controls.Add(UiFactory.Button("关闭", (_, __) => Close()));

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductName", HeaderText = "产品名称", FillWeight = 70 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CreateTime", HeaderText = "创建时间", FillWeight = 30,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss" }
            });

            Controls.Add(_grid); Controls.Add(toolbar); IndustrialTheme.Apply(this); heading.ForeColor = IndustrialTheme.Accent;
            Load += (_, __) => RefreshData();
        }

        private ProductConfig Selected => _grid.CurrentRow?.DataBoundItem as ProductConfig;

        private bool Authenticate(string operation)
        {
            using (var dialog = new TextInputForm("密码验证", "请输入管理密码：", password: true))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                if (dialog.Value == _config.Password) return true;
                Log.Warn("{0}密码校验失败", operation);
                IndustrialMessageBox.Show(this, "密码错误，操作未执行", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }

        private void Add(object sender, EventArgs e)
        {
            using (var input = new TextInputForm("新增产品", "请输入产品名称："))
            {
                if (input.ShowDialog(this) != DialogResult.OK) return;
                var error = ValidationHelper.ValidateProductName(input.Value);
                if (error != null) { IndustrialMessageBox.Show(this, error, "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (!Authenticate("新增产品")) return;
                TryChange(() => _database.AddProduct(input.Value), "产品新增成功：" + input.Value);
            }
        }

        private void Rename(object sender, EventArgs e)
        {
            var product = Selected; if (product == null) { IndustrialMessageBox.Show(this, "请先选择产品", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (var input = new TextInputForm("重命名产品", "请输入新产品名称：", product.ProductName))
            {
                if (input.ShowDialog(this) != DialogResult.OK) return;
                var error = ValidationHelper.ValidateProductName(input.Value);
                if (error != null) { IndustrialMessageBox.Show(this, error, "输入错误", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (!Authenticate("重命名产品")) return;
                TryChange(() => { _database.RenameProduct(product.Id, input.Value); return 0L; }, "产品重命名成功：" + input.Value);
            }
        }

        private void Delete(object sender, EventArgs e)
        {
            var product = Selected; if (product == null) { IndustrialMessageBox.Show(this, "请先选择产品", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (!Authenticate("删除产品")) return;
            if (IndustrialMessageBox.Show(this, $"确认删除产品 {product.ProductName}？\n历史检测记录和指导书图片不会删除。", "二次确认",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            TryChange(() => { _database.DeleteProduct(product.Id); return 0L; }, "产品删除成功：" + product.ProductName);
        }

        private void TryChange(Func<long> action, string message)
        {
            try { action(); Log.Info(message); RefreshData(); }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { IndustrialMessageBox.Show(this, "产品名称已存在", "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            catch (Exception ex) { Log.Error(ex, "产品配置操作失败"); IndustrialMessageBox.Show(this, "操作失败：" + ex.Message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void RefreshData() { _grid.DataSource = _database.GetProducts(); }
    }
}
