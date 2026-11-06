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
                     new[] { "rang ham mat", "nha khoa", "=rang", "khoa rang", "ham mat" }),

            // ===== Cơ xương khớp / chấn thương chỉnh hình =====
            new Rule(new[] { "dau lung", "dau that lung", "dau co", "cung co", "dau co vai gay", "dau khop", "khop sung", "sung khop", "dau goi", "dau vai", "dau co chan", "dau chan", "dau xuong",
                             "dau co tay", "hoi chung ong co tay", "cot song", "thoat vi dia dem", "thoai hoa", "thoai hoa khop", "gai cot song", "veo cot song", "gay xuong", "nut xuong",
                             "bong gan", "trat khop", "chan thuong", "loang xuong", "gout", "viem khop", "viem khop dang thap", "chuot rut", "co cung", "xuong khop", "chinh hinh" },
                     new[] { "xuong khop", "co xuong khop", "chan thuong chinh hinh", "chinh hinh", "co xuong" }),

            // ===== Thần kinh =====
            new Rule(new[] { "mat ngu", "kho ngu", "ngu khong sau", "ngu khong duoc", "dau nua dau", "migraine", "dau dau", "tuc dau", "chong mat", "hoa mat", "tien dinh", "roi loan tien dinh",
                             "mat thang bang", "quay cuong", "te tay", "te chan", "te bi", "te ran", "te mat", "run tay", "run chan", "hay quen", "suy giam tri nho", "mat tri nho", "dong kinh",
                             "dau day than kinh", "dau than kinh toa", "thoat vi dia dem", "liet mat", "yeu nua nguoi", "than kinh" },
                     new[] { "than kinh", "khoa than kinh", "noi than kinh" }),

            // ===== Tiêu hóa - gan mật =====
            new Rule(new[] { "dau da day", "da day", "dau thuong vi", "dau bao tu", "trao nguoc", "o chua", "o hoi", "nong rat", "nong rat co hong", "day hoi", "day bung", "kho tieu", "an khong tieu",
                             "tieu chay", "di ngoai", "di long", "tao bon", "non mua", "non ra mau", "buon non", "dau bung", "dau quan bung", "dau bung duoi", "vang da", "men gan", "viem gan", "benh gan",
                             "gan nhiem mo", "xo gan", "soi mat", "viem tui mat", "tui mat", "viem tuy", "benh tri", "bi tri", "tri noi", "tri ngoai", "hau mon", "chay mau hau mon", "phan co mau",
                             "phan den", "di cau", "ngo doc thuc pham", "roi loan tieu hoa", "viem dai trang", "hoi mieng", "an khong ngon", "tieu hoa", "noi soi da day", "noi soi dai trang" },
                     new[] { "tieu hoa", "noi tieu hoa", "tieu hoa gan mat", "gan mat", "khoa tieu hoa" }),

            // ===== Sản phụ khoa =====
            new Rule(new[] { "co thai", "mang thai", "dang bau", "kham thai", "sieu am thai", "thai nhi", "thu thai", "que thu thai", "hiem muon", "vo sinh", "sinh con", "sinh no", "sau sinh",
                             "mat sua", "tac tia sua", "kinh nguyet", "tre kinh", "tri hoan kinh", "rong kinh", "vong kinh", "kinh nguyet khong deu", "dau bung kinh", "ra mau bat thuong", "phu khoa",
                             "khi hu", "ngua vung kin", "u xo tu cung", "u nang buong trung", "da nang buong trung", "buong trung", "tu cung", "cat tu cung", "san phu khoa", "san khoa", "mang thai tuan" },
                     new[] { "san phu khoa", "san khoa", "phu khoa", "san" }),

            // ===== Thận - tiết niệu - nam khoa =====
            new Rule(new[] { "tieu buot", "tieu rat", "tieu dau", "tieu ra mau", "tieu dem", "tieu nhieu lan", "tieu khong het", "tieu khong kiem soat", "nuoc tieu duc", "nuoc tieu co bot",
                             "soi than", "soi tiet nieu", "soi bang quang", "dau than", "dau lung vung than", "benh than", "suy than", "viem duong tiet nieu", "nhiem trung tiet nieu", "viem bang quang",
                             "bang quang", "tien liet tuyen", "phi dai tuyen tien liet", "tiet nieu", "nieu khoa", "nam khoa", "roi loan cuong duong", "yeu sinh ly", "xuat tinh som", "cat bao quy dau" },
                     new[] { "than tiet nieu", "tiet nieu", "nieu khoa", "nam khoa", "=than", "khoa than" }),

            // ===== Nội tiết - đái tháo đường =====
            new Rule(new[] { "tieu duong", "dai thao duong", "duong huyet", "duong huyet cao", "duong huyet thap", "khat nuoc nhieu", "tuyen giap", "cuong giap", "suy giap", "bieu co", "buou co", "be phi", "beo phi",
                             "beo bu", "tang can", "tang can nhanh", "giam can nhanh", "sut can", "roi loan noi tiet", "noi tiet", "hoc mon", "man kinh", "tien man kinh", "day thi som" },
                     new[] { "noi tiet", "dai thao duong", "tuyen giap", "khoa noi tiet" }),

            // ===== Tâm thần - tâm lý =====
            new Rule(new[] { "lo au", "roi loan lo au", "roi loan hoang so", "hoang so", "tram cam", "stress", "cang thang", "ap luc", "kiet suc tinh than", "burnout", "buon chan", "chan doi",
                             "mat hung thu", "mat dong luc", "hay khoc", "cau gat", "am anh", "so hai", "roi loan cam xuc", "tam ly", "tam than", "tu ky", "adhd", "tang dong", "nghien", "suy nghi tieu cuc",
                             "mat ngu", "kho ngu", "mat ngu keo dai" },
                     new[] { "tam than", "tam ly", "tam than kinh", "suc khoe tam than" }),

            // ===== Hô hấp =====
            new Rule(new[] { "kho tho", "tho kho", "tho khe khe", "kho khe", "tho gap", "tho dai", "ngat tho", "tuc nguc kho tho", "ho co dom", "ho ra mau", "khac dom", "dom dac", "ho keo dai",
                             "ho ve dem", "ho gio", "ho nhieu", "hen suyen", "suyen", "benh hen", "con hen", "viem phe quan", "phe quan", "viem phoi", "phoi", "lao phoi", "tran dich mang phoi",
                             "ngung tho khi ngu", "ngay khi ngu", "ho hap" },
                     new[] { "ho hap", "phoi", "noi ho hap", "khoa ho hap" }),

            // ===== Truyền nhiễm =====
            new Rule(new[] { "sot xuat huyet", "covid", "cum a", "cum b", "sot ret", "benh lao", "viem gan b", "viem gan c", "hiv", "sot ve", "tay chan mieng", "quai bi", "thuy dau", "vi rut", "nhiem trung", "nhiem khuan", "ngo doc", "lay lan", "benh truyen nhiem", "truyen nhiem" },
                     new[] { "truyen nhiem", "nhiem" }),

            // ===== Ung bướu =====
            new Rule(new[] { "ung thu", "ung buou", "khoi u", "u lanh", "u ac", "noi hach", "hach to", "hoa tri", "xa tri", "tam soat ung thu", "sinh thiet", "u vu", "buou" },
                     new[] { "ung buou", "ung thu", "ung bau" }),

            // ===== Ngoại khoa - phẫu thuật =====
            new Rule(new[] { "phau thuat", "mo ruot thua", "ruot thua", "viem ruot thua", "thoat vi ben", "tri noi", "benh tri", "ap xe", "u bao", "vet mo", "vet thuong", "khau vet thuong", "mo noi soi", "ngoai khoa" },
                     new[] { "ngoai khoa", "ngoai tong quat", "ngoai" }),

            // ===== Dinh dưỡng =====
            new Rule(new[] { "dinh duong", "che do an", "an kieng", "thuc don", "giam can", "tang can", "suy dinh duong", "thieu vitamin", "thieu chat", "an uong", "thua can", "beo phi" },
                     new[] { "dinh duong", "khoa dinh duong" }),

            // ===== Phục hồi chức năng =====
            new Rule(new[] { "phuc hoi chuc nang", "vat ly tri lieu", "tap phuc hoi", "tai bien phuc hoi", "sau dot quy", "sau mo", "di lai kho", "yeu chan", "yeu tay", "liet", "bai liet" },
                     new[] { "phuc hoi chuc nang", "vat ly tri lieu" }),
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
                        Url = Url.Action("Index", "Doctors", new { q = s.Name }) ?? "/Doctors",
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
                Url = Url.Action("Index", "Doctors", new { q = s.Name }) ?? "/Doctors",
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
