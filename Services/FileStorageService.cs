namespace TELEHEALTH_GROUP4_QT01.Services
{
    public sealed record FileInspection(bool Ok, string? Error, string Extension, string ContentType);

    public sealed record StoredFile(Stream Stream, string ContentType, bool IsImage);

    public interface IFileStorageService
    {
        int MaxFilesPerRequest { get; }

        /// <summary>Kiểm tra 1 file tải lên. Trả về thông báo lỗi hoặc null nếu hợp lệ.</summary>
        Task<string?> ValidateAsync(IFormFile file);

        /// <summary>Kiểm tra ảnh đại diện bác sĩ (JPG/PNG/WEBP, tối đa 2 MB, kiểm tra cả nội dung thật). Null nếu hợp lệ.</summary>
        Task<string?> ValidateAvatarAsync(IFormFile file);

        /// <summary>Lưu file (đã kiểm tra) và trả về đường dẫn TƯƠNG ĐỐI, vd "symptoms/abc123.jpg".</summary>
        Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default);

        /// <summary>Mở file đã lưu để trả về cho người dùng được phép. Null nếu không hợp lệ / không tồn tại.</summary>
        StoredFile? Open(string relativePath);
    }

    /// <summary>
    /// Lưu file tải lên một cách an toàn:
    ///  1. Giới hạn kích thước (5 MB) và số lượng file.
    ///  2. Chỉ nhận .jpg .jpeg .png .pdf (whitelist).
    ///  3. Kiểm tra "magic bytes" – nội dung thật phải là JPEG/PNG/PDF và khớp với phần mở rộng.
    ///     (đổi đuôi .exe/.html thành .jpg sẽ bị từ chối)
    ///  4. Tên file lưu là GUID ngẫu nhiên, đuôi lấy theo nội dung – KHÔNG dùng tên người dùng gửi lên
    ///     (chống path traversal, ghi đè file, ký tự lạ).
    ///  5. Lưu NGOÀI wwwroot => không ai truy cập trực tiếp bằng URL; chỉ tải qua FilesController
    ///     sau khi kiểm tra quyền.
    /// </summary>
    public sealed class FileStorageService : IFileStorageService
    {
        private const long MaxBytes = 5 * 1024 * 1024;   // 5 MB
        private static readonly HashSet<string> AllowedFolders = new(StringComparer.Ordinal) { "symptoms", "receipts" };
        private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".pdf" };

        private static readonly (string Ext, string Mime, byte[] Magic)[] Types =
        {
            (".jpg", "image/jpeg",      new byte[] { 0xFF, 0xD8, 0xFF }),
            (".png", "image/png",       new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            (".pdf", "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D })   // "%PDF-"
        };

        private readonly string _root;

        public int MaxFilesPerRequest => 5;

        public FileStorageService(IWebHostEnvironment env, IConfiguration config)
        {
            var configured = config["Storage:UploadRoot"];
            _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "PrivateUploads")
                : configured);
        }

        public async Task<string?> ValidateAsync(IFormFile file) => (await InspectAsync(file)).Error;

        public async Task<string?> ValidateAvatarAsync(IFormFile file)
        {
            if (file.Length <= 0) return "Ảnh bị rỗng.";
            if (file.Length > 2 * 1024 * 1024) return "Ảnh tối đa 2MB";

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) return "Ảnh phải là JPG, PNG hoặc WEBP";

            var header = new byte[12];
            int read;
            await using (var s = file.OpenReadStream())
            {
                read = await s.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
            }

            string? detected = null;
            if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) detected = ".jpg";
            else if (read >= 8 && header.AsSpan(0, 8).SequenceEqual(Types[1].Magic)) detected = ".png";
            else if (read >= 12
                     && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                     && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P')
                detected = ".webp";

            if (detected == null) return "Nội dung file không phải ảnh JPG/PNG/WEBP hợp lệ.";

            var normalizedExt = ext == ".jpeg" ? ".jpg" : ext;
            if (normalizedExt != detected) return "Phần mở rộng không khớp với nội dung ảnh.";
            return null;
        }

        public async Task<string> SaveAsync(IFormFile file, string folder, CancellationToken ct = default)
        {
            if (!AllowedFolders.Contains(folder))
                throw new ArgumentException("Thư mục lưu không hợp lệ.", nameof(folder));

            var check = await InspectAsync(file);
            if (!check.Ok) throw new InvalidOperationException(check.Error);

            var dir = Path.Combine(_root, folder);
            Directory.CreateDirectory(dir);

            var name = $"{Guid.NewGuid():N}{check.Extension}";
            var fullPath = Path.Combine(dir, name);

            await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(fs, ct);

            return $"{folder}/{name}";
        }

        public StoredFile? Open(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;

            // Tương thích dữ liệu cũ: "/uploads/symptoms/abc.jpg" -> "symptoms/abc.jpg"
            relativePath = relativePath.Replace('\\', '/').TrimStart('/');
            if (relativePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                relativePath = relativePath.Substring("uploads/".Length);

            var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));
            var rootWithSep = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;

            // Chống path traversal: đường dẫn sau khi chuẩn hoá phải nằm trong thư mục gốc
            if (!fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)) return null;
            if (!File.Exists(fullPath)) return null;

            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var type = Types.FirstOrDefault(t => t.Ext == ext);
            if (type.Ext == null) return null;

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            return new StoredFile(stream, type.Mime, type.Mime.StartsWith("image/"));
        }

        private static async Task<FileInspection> InspectAsync(IFormFile file)
        {
            var shownName = SafeDisplayName(file.FileName);

            if (file.Length <= 0)
                return Fail($"File \"{shownName}\" bị rỗng.");
            if (file.Length > MaxBytes)
                return Fail($"File \"{shownName}\" vượt quá 5 MB.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext))
                return Fail($"File \"{shownName}\" không hợp lệ. Chỉ nhận .jpg, .png, .pdf.");

            var header = new byte[8];
            int read;
            await using (var s = file.OpenReadStream())
            {
                read = await s.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
            }

            (string Ext, string Mime, byte[] Magic)? detected = null;
            foreach (var t in Types)
            {
                if (read >= t.Magic.Length && header.AsSpan(0, t.Magic.Length).SequenceEqual(t.Magic))
                {
                    detected = t;
                    break;
                }
            }

            if (detected == null)
                return Fail($"Nội dung file \"{shownName}\" không phải ảnh JPG/PNG hoặc PDF hợp lệ.");

            var normalizedExt = ext == ".jpeg" ? ".jpg" : ext;
            if (normalizedExt != detected.Value.Ext)
                return Fail($"Phần mở rộng của file \"{shownName}\" không khớp với nội dung thực tế.");

            return new FileInspection(true, null, detected.Value.Ext, detected.Value.Mime);
        }

        private static FileInspection Fail(string error) => new(false, error, "", "");

        /// <summary>Tên hiển thị trong thông báo lỗi: bỏ đường dẫn, ký tự điều khiển, cắt ngắn.</summary>
        private static string SafeDisplayName(string? name)
        {
            var n = Path.GetFileName(name ?? "") ?? "";
            n = new string(n.Where(c => !char.IsControl(c)).ToArray());
            return n.Length > 60 ? n[..60] + "…" : n;
        }
    }
}
