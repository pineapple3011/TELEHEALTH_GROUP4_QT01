using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Models;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    // Trợ lý y tế: phân tích câu người dùng gõ, đối chiếu với danh mục chuyên khoa trong database
    // rồi trả về gợi ý thật (số bác sĩ, giá khám lấy từ DB). Không dùng AI bên ngoài.
    public class ChatController : Controller
    {
        private readonly TelehealthContext _db;
        public ChatController(TelehealthContext db) => _db = db;

        public class AskInput { public string? Message { get; set; } }

        public class Suggestion
        {
            public string Name { get; set; } = "";
            public string Url { get; set; } = "";
            public string Detail { get; set; } = "";
        }

        // Luật: nhóm từ khóa triệu chứng (đã bỏ dấu) -> tên chuyên khoa (cũng bỏ dấu) cần tìm trong DB
        private class Rule
        {
            public string[] Words { get; }
            public string[] SpecKeys { get; }
            public Rule(string[] words, string[] specKeys) { Words = words; SpecKeys = specKeys; }
        }

        private static readonly Rule[] Rules =
        {
            // ===== Nội tổng quát =====
            new Rule(new[] { "sot", "sot cao", "sot nhe", "sot ve chieu", "nong sot", "bi cam", "cam lanh", "cam cum", "cam nang", "cam sot", "cam mao", "cum", "cum a",
                             "met moi", "met nguoi", "kiet suc", "ue oai", "met la", "chong mat", "hoa mat", "xay xam", "ngat xiu", "thieu mau", "xanh xao",
                             "dau dau", "nhuc dau", "dau nguoi", "dau minh", "on lanh", "gai ret", "ret run", "ra mo hoi", "bi ho", "ho khan", "ho nhieu", "ho dai dang",
                             "so mui", "kham tong quat", "kiem tra suc khoe", "kham suc khoe", "dau bung", "buon non", "chan an" },
                     new[] { "noi tong quat", "noi khoa", "da khoa", "tong quat", "kham tong quat" }),

            // ===== Nhi khoa =====
            new Rule(new[] { "tre em", "tre nho", "tre con", "tre so sinh", "so sinh", "tre bi", "be bi", "be nha", "em be", "be yeu", "be khoc", "be bu", "be bieng an",
                             "con toi", "con em", "con minh", "con gai toi", "con trai toi", "chau toi", "chau be", "cho be", "cho con", "tieu nhi", "nhi dong", "nhi khoa", "khoa nhi", "nhi",
                             "bieng an", "cham lon", "cham tang can", "suy dinh duong", "tay chan mieng", "soi tre em", "quai bi", "thuy dau", "viem tieu phe quan", "tiem chung", "tiem phong", "bu sua" },
                     new[] { "nhi", "nhi khoa", "khoa nhi", "nhi dong" }),

            // ===== Tim mạch =====
            new Rule(new[] { "dau nguc", "tuc nguc", "nang nguc", "dau thot nguc", "hoi hop", "tim dap nhanh", "tim dap cham", "tim dap khong deu", "tim dap manh", "nhip tim",
                             "loan nhip", "roi loan nhip tim", "benh tim", "dau tim", "nhoi mau co tim", "suy tim", "tim mach", "huyet ap", "tang huyet ap", "cao huyet ap",
                             "huyet ap cao", "huyet ap thap", "ha huyet ap", "phu chan", "phu mat ca chan", "mach nhanh", "mach cham", "cholesterol", "mo mau", "mo mau cao",
                             "xo vua dong mach", "dong mach vanh", "van tim", "dau than tim", "tim to" },
                     new[] { "tim mach", "tim", "khoa tim" }),

            // ===== Da liễu =====
            new Rule(new[] { "benh ve da", "viem da", "kho da", "ngua da", "da bi", "da nhay cam", "noi man", "man ngua", "man do", "phat ban", "ban do", "me day", "di ung", "ngua", "ngua khap nguoi",
                             "mun", "mun trung ca", "mun coc", "mun boc", "seo", "seo lom", "tham da", "nam da", "nam mong", "nam toc", "nam chan", "nam tay", "ghe ngua", "zona", "ro nuoc",
                             "vay nen", "cham sua", "eczema", "viem da co dia", "rung toc", "hoi nach", "hoi chan", "chay nang", "bong da", "da lieu", "tan nhang", "mun cam", "ngua vung kin" },
                     new[] { "da lieu", "=da", "khoa da", "da lieu tham my" }),

            // ===== Tai mũi họng =====
            new Rule(new[] { "dau tai", "u tai", "viem tai", "chay mu tai", "nghe kem", "nghe khong ro", "dut tai", "ngac tai", "co vat trong tai",
                             "dau hong", "viem hong", "ngua hong", "rat hong", "kho nuot", "nuot dau", "amidan", "viem amidan", "khan tieng", "mat tieng", "viem thanh quan", "kho giong",
                             "ngat mui", "nghet mui", "chay mui", "hat hoi", "viem mui", "viem mui di ung", "polyp mui", "viem xoang", "xoang", "chay mau cam", "tai mui hong", "tmh" },
                     new[] { "tai mui hong", "tmh" }),

            // ===== Mắt =====
            new Rule(new[] { "dau mat", "do mat", "mat do", "mo mat", "mat mo", "ngua mat", "mat ngua", "cay mat", "mat cay", "nhuc mat", "chay nuoc mat", "kho mat", "moi mat", "nhin mo", "nhin khong ro", "nhin doi", "thi luc", "giam thi luc", "can thi", "vien thi", "loan thi", "duc thuy tinh the", "cuom", "mong thit", "mat co ghen",
                             "benh ve mat", "benh mat", "kham mat", "nhan khoa", "khoa mat", "nhin ban dem" },
                     new[] { "=mat", "nhan khoa", "khoa mat", "mat nhan khoa" }),

            // ===== Răng hàm mặt =====
            new Rule(new[] { "dau rang", "sau rang", "nho rang", "nho rang khon", "rang khon", "moc rang khon", "mat rang", "tram rang", "nieng rang", "nieng", "lung lay rang", "e buot", "viem nuou",
                             "chay mau nuou", "nuou rang", "viem loi", "hoi mieng", "nhiet mieng", "loet mieng", "nha khoa", "rang ham mat", "boc rang su", "cao voi rang", "nha chu", "ham mat" },
                     new[] { "rang ham mat", "nha khoa", "=rang", "rang", "ham", "mat", "khoa rang", "ham mat" }),

         
        };

        // Dấu hiệu nguy hiểm: không gợi ý đặt lịch mà hướng dẫn gọi cấp cứu
        private static readonly string[] Emergency =
        {
            "ngung tho", "ngung tim", "tho khong duoc", "tho khong noi", "kho tho nhieu", "kho tho nang", "bat tinh", "hon me", "khong tinh",
            "co giat", "chay mau nhieu", "chay mau khong cam", "dau nguc du doi", "dau nguc lan", "dot quy", "liet nua nguoi", "meo mieng",
            "noi ngong", "ngo doc", "uong thuoc ngu", "tai nan", "bong nang", "tu tu", "muon chet"
        };

        // bỏ dấu tiếng Việt, chữ thường, chỉ giữ chữ + số, gộp khoảng trắng
        private static string Norm(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var d = s.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
            }
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }

        private static bool Has(string paddedText, string word) => paddedText.Contains(" " + word + " ");

        private static string Money(decimal? v) =>
            v.HasValue ? string.Format(new CultureInfo("vi-VN"), "{0:N0}đ", v.Value) : "";

        [HttpPost, IgnoreAntiforgeryToken]
        public async Task<IActionResult> Ask([FromBody] AskInput input)
        {
            var raw = (input?.Message ?? "").Trim();
            if (raw.Length == 0)
                return Json(new { reply = "Bạn hãy nhập triệu chứng hoặc câu hỏi, ví dụ: \"con tôi bị sốt và ho 3 ngày\".", suggestions = new List<Suggestion>() });
            if (raw.Length > 500) raw = raw.Substring(0, 500);

            var text = " " + Norm(raw) + " ";
            var all = new Suggestion { Name = "Xem tất cả bác sĩ", Url = Url.Action("Index", "Doctors") ?? "/Doctors" };

            // 1. Tình huống nguy hiểm
            if (Emergency.Any(e => Has(text, e)))
            {
                return Json(new
                {
                    reply = "Mô tả của bạn có thể là tình huống nguy hiểm. Hãy gọi cấp cứu 115 ngay hoặc đưa người bệnh đến cơ sở y tế gần nhất, không nên chờ đặt lịch khám trực tuyến.",
                    suggestions = new List<Suggestion>
                    {
                        new Suggestion { Name = "Gọi 115", Url = "tel:115" },
                        new Suggestion { Name = "Tổng đài viện 1900 1234", Url = "tel:19001234" }
                    }
                });
            }

            var specs = await _db.Specialties.AsNoTracking()
                .Select(s => new
                {
                    s.SpecId,
                    Name = s.SpecialtyName,
                    Price = (decimal?)s.BasePrice,
                    Doctors = s.Doctors.Count()
                }).ToListAsync();

            string Detail(int doctors, decimal? price)
            {
                var parts = new List<string> { doctors + " bác sĩ" };
                if (price.HasValue) parts.Add(Money(price));
                return string.Join(" · ", parts);
            }

            // 2. Chào hỏi
            if (new[] { "xin chao", "chao", "hello", "hi", "alo", "chao ban" }.Any(g => Norm(raw) == g))
                return Json(new { reply = "Xin chào! Hãy mô tả triệu chứng của bạn, tôi sẽ gợi ý chuyên khoa phù hợp trong hệ thống.", suggestions = new List<Suggestion>() });

            // 2b. Cảm ơn / hỏi trợ lý làm được gì
            if (new[] { "cam on", "cam on ban", "thanks", "thank you", "cam on nhieu" }.Any(g => Has(text, g)))
                return Json(new { reply = "Rất vui được hỗ trợ bạn. Chúc bạn mau khỏe!", suggestions = new List<Suggestion>() });
            if (new[] { "ban la ai", "giup duoc gi", "lam duoc gi", "ban biet gi", "huong dan" }.Any(g => Has(text, g)))
                return Json(new
                {
                    reply = "Mình có thể: (1) gợi ý chuyên khoa theo triệu chứng bạn mô tả, (2) cho biết giá khám từng chuyên khoa, (3) hướng dẫn đặt lịch khám. Bạn thử gõ ví dụ \"đau bụng kèm buồn nôn\".",
                    suggestions = new List<Suggestion>()
                });

            // 3. Hỏi giá khám
            if (new[] { "gia", "gia ca", "gia kham", "gia tien", "bang gia", "chi phi", "phi", "phi kham", "tien kham", "bao nhieu", "bao nhieu tien", "mac khong", "re khong" }.Any(k => Has(text, k)))
            {
                var list = specs.Where(s => s.Price.HasValue).OrderBy(s => s.Price).Take(8)
                    .Select(s => new Suggestion
                    {
                        Name = s.Name,
                        Url = Url.Action("Index", "Doctors", new { specId = s.SpecId }) ?? "/Doctors",
                        Detail = Money(s.Price)
                    }).ToList();
                return Json(new { reply = "Giá khám cơ bản theo từng chuyên khoa (chưa gồm phụ phí nếu có):", suggestions = list });
            }

            // 4. Hỏi cách đặt lịch
            if (new[] { "dat lich", "dat kham", "cach dat", "huong dan dat" }.Any(k => Has(text, k)))
                return Json(new
                {
                    reply = "Để đặt lịch: chọn bác sĩ ở mục Tìm bác sĩ, chọn khung giờ còn trống, điền thông tin và thanh toán tiền cọc. Lịch hẹn sẽ hiện trong mục Lịch khám của tôi.",
                    suggestions = new List<Suggestion> { all }
                });

            // 5. Phân tích triệu chứng: chấm điểm chuyên khoa
            var score = new Dictionary<int, int>();
            foreach (var r in Rules)
            {
                var hits = r.Words.Count(w => Has(text, w));
                if (hits == 0) continue;
                foreach (var s in specs)
                {
                    var nameN = " " + Norm(s.Name) + " ";
                    if (r.SpecKeys.Any(k => k.StartsWith("=") ? nameN.Trim() == k.Substring(1) : Has(nameN, k)))
                        score[s.SpecId] = score.GetValueOrDefault(s.SpecId) + hits;
                }
            }
            // người dùng gõ thẳng tên chuyên khoa có trong DB
            foreach (var s in specs)
            {
                var n = Norm(s.Name);
                // tên ngắn (Mắt, Nhi, Tim...) phải khớp đúng dấu để "mất ngủ" không bị hiểu là "Mắt"
                var direct = n.Length >= 6
                    ? Has(text, n)
                    : raw.ToLowerInvariant().Contains(s.Name.Trim().ToLowerInvariant());
                if (n.Length > 0 && direct) score[s.SpecId] = score.GetValueOrDefault(s.SpecId) + 3;
            }

            var picked = specs.Where(s => score.ContainsKey(s.SpecId))
                .OrderByDescending(s => score[s.SpecId]).ThenByDescending(s => s.Doctors)
                .Take(3).ToList();

            if (picked.Count == 0)
            {
                return Json(new
                {
                    reply = "Mình chưa nhận ra triệu chứng nào từ câu này. Bạn thử mô tả rõ hơn (vị trí đau, thời gian bị, ai bị), ví dụ: \"đau bụng 2 ngày kèm buồn nôn\" hoặc \"con tôi ho và sốt\". Hoặc bạn có thể xem toàn bộ danh sách bác sĩ.",
                    suggestions = new List<Suggestion> { all }
                });
            }

            var sugg = picked.Select(s => new Suggestion
            {
                Name = s.Name,
                Url = Url.Action("Index", "Doctors", new { specId = s.SpecId }) ?? "/Doctors",
                Detail = Detail(s.Doctors, s.Price)
            }).ToList();

            return Json(new
            {
                reply = "Dựa trên mô tả của bạn, bạn có thể cân nhắc đặt khám các chuyên khoa sau. Đây chỉ là gợi ý tham khảo, không thay thế chẩn đoán của bác sĩ.",
                suggestions = sugg
            });
        }
    }
}