using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AirtightInspection.Utilities
{
    public static class EncodingHelper
    {
        public static int DecodeInt32(ushort[] words, string wordOrder, string byteOrder)
        {
            if (words == null || words.Length < 2) throw new ArgumentException("DINT 至少需要两个寄存器");
            ushort first = SwapIfNeeded(words[0], byteOrder);
            ushort second = SwapIfNeeded(words[1], byteOrder);
            uint low = wordOrder.Equals("HighLow", StringComparison.OrdinalIgnoreCase) ? second : first;
            uint high = wordOrder.Equals("HighLow", StringComparison.OrdinalIgnoreCase) ? first : second;
            return unchecked((int)((high << 16) | low));
        }

        public static ushort[] EncodeInt32(int value, string wordOrder, string byteOrder)
        {
            uint raw = unchecked((uint)value);
            ushort low = SwapIfNeeded((ushort)(raw & 0xffff), byteOrder);
            ushort high = SwapIfNeeded((ushort)(raw >> 16), byteOrder);
            return wordOrder.Equals("HighLow", StringComparison.OrdinalIgnoreCase)
                ? new[] { high, low } : new[] { low, high };
        }

        public static string DecodeString(ushort[] words, int charsPerRegister, string byteOrder, string encodingName, int headerBytes)
        {
            var bytes = new List<byte>();
            foreach (var word in words ?? new ushort[0])
            {
                byte high = (byte)(word >> 8), low = (byte)(word & 0xff);
                if (charsPerRegister <= 1) bytes.Add(low);
                else if (byteOrder.Equals("LowHigh", StringComparison.OrdinalIgnoreCase)) { bytes.Add(low); bytes.Add(high); }
                else { bytes.Add(high); bytes.Add(low); }
            }
            if (headerBytes > 0 && bytes.Count >= headerBytes) bytes.RemoveRange(0, headerBytes);
            Encoding encoding;
            try { encoding = Encoding.GetEncoding(encodingName); } catch { encoding = Encoding.ASCII; }
            var text = encoding.GetString(bytes.ToArray());
            return new string(text.Where(c => c != '\x02' && c != '\x03' && c != '\r' && c != '\n' && c != '\0' && !char.IsControl(c)).ToArray()).Trim();
        }

        private static ushort SwapIfNeeded(ushort value, string byteOrder) =>
            byteOrder.Equals("LittleEndian", StringComparison.OrdinalIgnoreCase)
                ? (ushort)((value >> 8) | (value << 8)) : value;
    }

    public static class ValidationHelper
    {
        private static readonly Regex InvalidProductChars = new Regex("[\\\\/:*?\"<>|]", RegexOptions.Compiled);
        public static string ValidateProductName(string input)
        {
            var value = (input ?? string.Empty).Trim();
            if (value.Length == 0) return "产品名称不能为空";
            if (value.Length > 50) return "产品名称不能超过 50 个字符";
            if (InvalidProductChars.IsMatch(value)) return "产品名称不能包含 \\ / : * ? \" < > |";
            return null;
        }
    }
}
