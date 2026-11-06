-- TUỲ CHỌN – chỉ chạy nếu bạn đã chuyển thư mục file cũ (xem hướng dẫn):
--   wwwroot\uploads\symptoms  ->  PrivateUploads\symptoms
--   wwwroot\uploads\receipts  ->  PrivateUploads\receipts
-- Script đổi đường dẫn dạng "/uploads/receipts/abc.jpg" thành "receipts/abc.jpg".
-- (Không chạy cũng không sao: ứng dụng vẫn đọc được đường dẫn cũ.)
USE TELEHEALTH;
GO

UPDATE dbo.AppointmentFiles
SET FilePath = STUFF(FilePath, 1, LEN('/uploads/'), '')
WHERE FilePath LIKE '/uploads/%';
GO

SELECT TOP 20 FileID, FilePath, FileType FROM dbo.AppointmentFiles ORDER BY FileID DESC;
GO
