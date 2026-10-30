using System.Text;

namespace TELEHEALTH_GROUP4_QT01.Areas.Admin.Helpers
{
    // Tạo file CSV có BOM UTF-8 để Excel đọc đúng tiếng Việt
    public static class CsvBuilder
    {
        public static byte[] Build(string[] header, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", header.Select(Esc)));
            foreach (var r in rows) sb.AppendLine(string.Join(",", r.Select(Esc)));
            return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        }

        private static string Esc(string? v)
        {
            v ??= "";
            if (v.Length > 0 && "=+-@".Contains(v[0]) && !decimal.TryParse(v, out _)) v = "'" + v; // chống CSV injection
            return v.Contains(',') || v.Contains('"') || v.Contains('\n') ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }
    }
}
