using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AirtightInspection.Services;
using NLog;
using Sunny.UI;

namespace AirtightInspection.Forms
{
    public sealed class ManualViewerForm : UIForm
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly List<string> _images;
        private readonly Panel _viewport;
        private readonly PictureBox _picture;
        private readonly Label _page;
        private readonly Label _zoomLabel;
        private readonly Button _previous, _next, _zoomIn, _zoomOut, _fit;
        private int _index; private int _zoom = 100; private Image _current;
        private bool _fitMode = true;
        private bool _dragging; private Point _dragStart; private Point _scrollStart;
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        public ManualViewerForm(string folder, string product)
        {
            Text = "作业指导书 - " + product; Width = 1200; Height = 800; StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1000, 700); WindowState = FormWindowState.Maximized;
            KeyPreview = true; FormBorderStyle = FormBorderStyle.None;
            _images = ManualService.FindImages(folder, product);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(7), BackColor = IndustrialTheme.Panel };
            _previous = UiFactory.Button("上一张", (_, __) => { _index--; ShowImage(); });
            _next = UiFactory.Button("下一张", (_, __) => { _index++; ShowImage(); });
            _zoomOut = UiFactory.Button("缩小", (_, __) => { _fitMode = false; _zoom -= 10; ApplyZoom(); });
            _zoomIn = UiFactory.Button("放大", (_, __) => { _fitMode = false; _zoom += 10; ApplyZoom(); });
            _fit = UiFactory.Button("适应窗口", (_, __) => { _fitMode = true; FitToViewport(); });
            _page = UiFactory.Label(""); _zoomLabel = UiFactory.Label("");
            var heading = UiFactory.Label("◆ 作业指导书 / " + product); heading.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold); heading.ForeColor = IndustrialTheme.Accent;
            toolbar.Controls.AddRange(new Control[] { heading, _previous, _next, _page, _zoomOut, _zoomIn, _fit, _zoomLabel, UiFactory.Button("关闭", (_, __) => Close()) });
            toolbar.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, 0x2, 0); } };
            _viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = IndustrialTheme.Background };
            _picture = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent };
            _picture.DoubleClick += (_, __) => { _fitMode = true; FitToViewport(); };
            _picture.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; _dragStart = e.Location; _scrollStart = new Point(-_viewport.AutoScrollPosition.X, -_viewport.AutoScrollPosition.Y); _picture.Cursor = Cursors.Hand; } };
            _picture.MouseMove += (_, e) => { if (_dragging) _viewport.AutoScrollPosition = new Point(_scrollStart.X + _dragStart.X - e.X, _scrollStart.Y + _dragStart.Y - e.Y); };
            _picture.MouseUp += (_, __) => { _dragging = false; _picture.Cursor = Cursors.Default; };
            _viewport.Resize += (_, __) => { if (_fitMode && _current != null) FitToViewport(); };
            _viewport.Controls.Add(_picture); Controls.Add(_viewport); Controls.Add(toolbar);
            KeyDown += (_, e) => { if (e.KeyCode == Keys.Left && _previous.Enabled) { _index--; ShowImage(); } if (e.KeyCode == Keys.Right && _next.Enabled) { _index++; ShowImage(); } };
            IndustrialTheme.Apply(this); heading.ForeColor = IndustrialTheme.Accent;
            Shown += (_, __) => ShowImage(); FormClosed += (_, __) => DisposeCurrent();
        }

        private void ShowImage()
        {
            DisposeCurrent();
            while (_index >= 0 && _index < _images.Count)
            {
                try
                {
                    using (var source = Image.FromFile(_images[_index])) _current = new Bitmap(source);
                    break;
                }
                catch (Exception ex) { Log.Error(ex, "作业指导书图片加载失败：{0}", _images[_index]); _images.RemoveAt(_index); }
            }
            if (_current == null)
            {
                _picture.Image = null; _picture.Size = _viewport.ClientSize;
                _picture.Paint -= DrawEmpty;
                _picture.Paint += DrawEmpty; _page.Text = "未找到该产品的作业指导书图片";
            }
            else { _picture.Paint -= DrawEmpty; _picture.Image = _current; _fitMode = true; FitToViewport(); }
            UpdateButtons();
        }
        private void DrawEmpty(object sender, PaintEventArgs e)
        {
            using (var brush = new SolidBrush(IndustrialTheme.Muted)) e.Graphics.DrawString("未找到该产品的作业指导书图片", Font, brush, 30, 30);
        }
        private void ApplyZoom()
        {
            if (_current == null) return; _zoom = Math.Max(1, Math.Min(300, _zoom));
            _picture.Size = new Size(Math.Max(1, _current.Width * _zoom / 100), Math.Max(1, _current.Height * _zoom / 100));
            _picture.Location = new Point(Math.Max(0, (_viewport.ClientSize.Width - _picture.Width) / 2), Math.Max(0, (_viewport.ClientSize.Height - _picture.Height) / 2));
            UpdateButtons();
        }
        private void FitToViewport()
        {
            if (_current == null) return;
            var availableWidth = Math.Max(1, _viewport.ClientSize.Width - 24);
            var availableHeight = Math.Max(1, _viewport.ClientSize.Height - 24);
            var scale = Math.Min((double)availableWidth / _current.Width, (double)availableHeight / _current.Height);
            _zoom = Math.Max(1, Math.Min(300, (int)Math.Floor(scale * 100)));
            ApplyZoom();
        }
        private void UpdateButtons()
        {
            _previous.Enabled = _images.Count > 0 && _index > 0; _next.Enabled = _images.Count > 0 && _index < _images.Count - 1;
            _zoomOut.Enabled = _current != null && _zoom > 1; _zoomIn.Enabled = _current != null && _zoom < 300; _fit.Enabled = _current != null;
            if (_images.Count > 0) _page.Text = $"第 {_index + 1} / {_images.Count} 页"; _zoomLabel.Text = $"缩放 {_zoom}%";
        }
        private void DisposeCurrent() { _picture.Image = null; _current?.Dispose(); _current = null; }
    }
}
