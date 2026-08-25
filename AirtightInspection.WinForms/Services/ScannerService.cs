using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirtightInspection.Config;
using NLog;

namespace AirtightInspection.Services
{
    public sealed class ScannerService : IDisposable
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly AppConfig _config;
        private readonly StringBuilder _serialBuffer = new StringBuilder();
        private SerialPort _port;
        private CancellationTokenSource _retryCts;
        public event EventHandler<string> BarcodeReceived;
        public event EventHandler<string> Message;
        public bool IsKeyboardMode => _config.ScannerMode.Equals("Keyboard", StringComparison.OrdinalIgnoreCase);

        public ScannerService(AppConfig config) { _config = config; }
        public void Start()
        {
            if (IsKeyboardMode) return;
            _retryCts = new CancellationTokenSource(); Task.Run(() => ConnectLoop(_retryCts.Token));
        }

        private async Task ConnectLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_port == null || !_port.IsOpen)
                    {
                        var port = new SerialPort(_config.ScannerPort, _config.ScannerBaudRate,
                            ParseParity(_config.ScannerParity), _config.ScannerDataBits, ParseStopBits(_config.ScannerStopBits));
                        port.DataReceived += OnDataReceived; port.Open(); _port = port; Raise("扫码枪串口已连接：" + _config.ScannerPort);
                    }
                    await Task.Delay(3000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log.Error(ex, "扫码枪串口打开失败"); Raise("扫码枪串口不可用，3 秒后重试：" + ex.Message);
                    try { await Task.Delay(3000, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
            }
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                _serialBuffer.Append(_port.ReadExisting());
                var end = (char)_config.ScannerLineEnd;
                while (true)
                {
                    var text = _serialBuffer.ToString(); var index = text.IndexOf(end);
                    if (index < 0) break;
                    var barcode = text.Substring(0, index).Trim('\r', '\n', ' ', '\t');
                    _serialBuffer.Remove(0, index + 1);
                    if (barcode.Length > 0 && barcode.Length <= _config.MaxBarcodeLength) BarcodeReceived?.Invoke(this, barcode);
                }
            }
            catch (Exception ex) { Log.Error(ex, "扫码枪串口接收失败"); Raise("扫码枪接收失败：" + ex.Message); }
        }

        private void Raise(string text) { Message?.Invoke(this, text); }
        private static Parity ParseParity(string value) { Parity result; return Enum.TryParse(value, true, out result) ? result : Parity.None; }
        private static StopBits ParseStopBits(string value)
        {
            if (value == "1") return StopBits.One; if (value == "2") return StopBits.Two;
            StopBits result; return Enum.TryParse(value, true, out result) ? result : StopBits.One;
        }
        public void Dispose() { _retryCts?.Cancel(); _retryCts?.Dispose(); _retryCts = null; if (_port == null) return; try { _port.Close(); _port.Dispose(); } catch { } _port = null; }
    }
}
