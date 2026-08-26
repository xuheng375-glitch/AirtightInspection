using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AirtightInspection.Models;

namespace AirtightInspection.Services
{
    public static class AirtightResultParser
    {
        private static readonly Regex FrameMarker = new Regex(@"<\s*(?<program>\d+)\s*>\s*:", RegexOptions.Compiled);
        private static readonly Regex ResultCode = new Regex(@"\(\s*(?<code>[A-Za-z. ]+)\s*\)", RegexOptions.Compiled);
        private static readonly Regex NumberWithUnit = new Regex(
            @"(?<value>[+-]?(?:\d+(?:[.,]\d*)?|[.,]\d+))\s*(?<unit>[A-Za-zµμ³%/\.]+)?",
            RegexOptions.Compiled);

        private static readonly Dictionary<string, string> AlarmTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PRESSURE LOW"] = "压力低",
            ["PRESSURE HIGH"] = "压力高",
            ["LARGE LEAK TEST"] = "大泄漏测试异常",
            ["SEALED PART VOL TOO SMALL"] = "密封件容积过小",
            ["SEALED PART VOL TOO LARGE"] = "密封件容积过大",
            ["SEALED PART LEARN ERROR"] = "密封件学习错误",
            ["F.S. TEST"] = "满量程测试异常",
            ["BURST < PMIN"] = "爆破压力低于下限"
        };

        public static AirtightResult Parse(string input)
        {
            var raw = (input ?? string.Empty).Trim();
            var result = new AirtightResult { RawFrame = raw, ResultText = "未解析" };
            if (raw.Length == 0) return result;

            var frame = ExtractFirstFrame(raw);
            result.RawFrame = frame;
            if (frame.IndexOf('\t') >= 0 && TryParseExportFrame(frame, result)) return result;
            TryParsePrinterFrame(frame, result);
            return result;
        }

        private static string ExtractFirstFrame(string raw)
        {
            var first = FrameMarker.Match(raw);
            if (!first.Success) return raw;
            var next = FrameMarker.Match(raw, first.Index + first.Length);
            var length = next.Success ? next.Index - first.Index : raw.Length - first.Index;
            return raw.Substring(first.Index, length).Trim(' ', '\t', ':');
        }

        private static bool TryParsePrinterFrame(string frame, AirtightResult result)
        {
            var marker = FrameMarker.Match(frame);
            if (!marker.Success) return false;
            result.ProgramNo = marker.Groups["program"].Value.PadLeft(2, '0');

            var body = frame.Substring(marker.Index + marker.Length).Trim();
            var codeMatch = ResultCode.Match(body);
            if (!codeMatch.Success)
            {
                result.ResultText = "未解析（缺少结果代码）";
                return false;
            }

            result.ResultCode = NormalizeCode(codeMatch.Groups["code"].Value);
            var beforeCode = body.Substring(0, codeMatch.Index).Trim(' ', '\t', ':');
            var afterCode = body.Substring(codeMatch.Index + codeMatch.Length).Trim(' ', '\t', ':');
            SetPressure(LastNumber(beforeCode), result);

            if (IsNumericMeasurement(afterCode)) SetLeak(FirstNumber(afterCode), result);
            result.ResultText = BuildResultText(result.ResultCode, afterCode, result.LeakValue.HasValue);
            result.IsParsed = true;
            return true;
        }

        private static bool TryParseExportFrame(string frame, AirtightResult result)
        {
            var fields = frame.Split(new[] { '\t' }, StringSplitOptions.None).Select(value => value.Trim()).ToArray();
            var codeIndex = Array.FindIndex(fields, value => ResultCode.IsMatch(value));
            if (codeIndex < 0) return false;

            var program = fields.Take(codeIndex).Reverse().FirstOrDefault(value => Regex.IsMatch(value, @"^\d+$"));
            if (string.IsNullOrWhiteSpace(program)) return false;
            result.ProgramNo = program.PadLeft(2, '0');
            result.ResultCode = NormalizeCode(ResultCode.Match(fields[codeIndex]).Groups["code"].Value);

            var measurements = new List<Match>();
            for (var i = codeIndex + 1; i < fields.Length; i++)
            {
                var number = FirstNumber(fields[i]);
                if (number == null) continue;
                if (string.IsNullOrWhiteSpace(number.Groups["unit"].Value) && i + 1 < fields.Length && Regex.IsMatch(fields[i + 1], @"^[A-Za-zµμ³%/\.]+$"))
                    number = NumberWithUnit.Match(number.Groups["value"].Value + " " + fields[++i]);
                measurements.Add(number);
            }

            var alarm = fields.Skip(codeIndex + 1).FirstOrDefault(IsAlarmText) ?? string.Empty;
            if (!result.ResultCode.Equals("AL", StringComparison.OrdinalIgnoreCase) && measurements.Count > 0)
                SetLeak(measurements[0], result);
            if (measurements.Count > (result.LeakValue.HasValue ? 1 : 0))
                SetPressure(measurements[result.LeakValue.HasValue ? 1 : 0], result);
            result.ResultText = BuildResultText(result.ResultCode, alarm, result.LeakValue.HasValue);
            result.IsParsed = true;
            return true;
        }

        private static Match FirstNumber(string value)
        {
            var match = NumberWithUnit.Match(value ?? string.Empty);
            return match.Success ? match : null;
        }

        private static Match LastNumber(string value)
        {
            var matches = NumberWithUnit.Matches(value ?? string.Empty);
            return matches.Count == 0 ? null : matches[matches.Count - 1];
        }

        private static bool IsNumericMeasurement(string value)
        {
            var match = FirstNumber(value);
            return match != null && match.Index == 0;
        }

        private static bool IsAlarmText(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.Any(char.IsLetter) && FirstNumber(value) == null && !ResultCode.IsMatch(value);

        private static void SetLeak(Match match, AirtightResult result)
        {
            if (match == null || !TryNumber(match.Groups["value"].Value, out var value)) return;
            result.LeakValue = value;
            result.LeakValueText = match.Groups["value"].Value;
            result.LeakUnit = match.Groups["unit"].Value;
        }

        private static void SetPressure(Match match, AirtightResult result)
        {
            if (match == null || !TryNumber(match.Groups["value"].Value, out var value)) return;
            result.PressureValue = value;
            result.PressureValueText = match.Groups["value"].Value;
            result.PressureUnit = match.Groups["unit"].Value;
        }

        private static bool TryNumber(string text, out double value) =>
            double.TryParse((text ?? string.Empty).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static string NormalizeCode(string value) => Regex.Replace((value ?? string.Empty).ToUpperInvariant(), @"\s+", string.Empty);

        private static string BuildResultText(string code, string detail, bool hasLeakValue)
        {
            var cleanDetail = (detail ?? string.Empty).Trim(' ', '\t', ':');
            if (string.Equals(code, "OK", StringComparison.OrdinalIgnoreCase)) return "合格";
            if (string.Equals(code, "AL", StringComparison.OrdinalIgnoreCase))
            {
                var translated = TranslateAlarm(cleanDetail);
                return string.IsNullOrWhiteSpace(translated) ? "报警" : "报警 - " + translated;
            }
            if (!string.IsNullOrWhiteSpace(code))
                return hasLeakValue ? "不合格（" + code + "）" : "异常（" + code + "）";
            return "未解析";
        }

        private static string TranslateAlarm(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var key = Regex.Replace(value.Trim().ToUpperInvariant(), @"\s+", " ");
            return AlarmTranslations.TryGetValue(key, out var translated) ? translated : value.Trim();
        }
    }
}
