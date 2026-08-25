using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AirtightInspection.Config
{
    public sealed class IniFile
    {
        private readonly string _path;
        private readonly Dictionary<string, Dictionary<string, string>> _data =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public IniFile(string path) { _path = path; Load(); }

        private void Load()
        {
            if (!File.Exists(_path)) return;
            string section = string.Empty;
            foreach (var raw in File.ReadAllLines(_path, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    if (!_data.ContainsKey(section)) _data[section] = NewSection();
                    continue;
                }
                var index = line.IndexOf('=');
                if (index <= 0) continue;
                if (!_data.ContainsKey(section)) _data[section] = NewSection();
                _data[section][line.Substring(0, index).Trim()] = line.Substring(index + 1).Trim();
            }
        }

        private static Dictionary<string, string> NewSection() =>
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Get(string section, string key, string fallback)
        {
            Dictionary<string, string> values;
            string value;
            return _data.TryGetValue(section, out values) && values.TryGetValue(key, out value) ? value : fallback;
        }

        public int GetInt(string section, string key, int fallback)
        {
            int value;
            return int.TryParse(Get(section, key, string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        public bool GetBool(string section, string key, bool fallback) => GetInt(section, key, fallback ? 1 : 0) != 0;
    }
}
