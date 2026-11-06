-- Chạy script này MỘT LẦN trên database TELEHEALTH (SSMS) trước khi chạy ứng dụng.
-- Bổ sung 3 cột bảo mật cho bảng Accounts CÓ SẴN (không tạo bảng mới, không đụng dữ liệu cũ).
USE TELEHEALTH;
GO

-- Đếm số lần đăng nhập sai liên tiếp (khoá tạm sau 5 lần)
IF COL_LENGTH('dbo.Accounts', 'FailedLoginCount') IS NULL
    ALTER TABLE dbo.Accounts ADD FailedLoginCount INT NOT NULL
        CONSTRAINT DF_Accounts_FailedLoginCount DEFAULT (0);
GO

-- Thời điểm hết khoá tạm (UTC)
IF COL_LENGTH('dbo.Accounts', 'LockoutEndUtc') IS NULL
    ALTER TABLE dbo.Accounts ADD LockoutEndUtc DATETIME2 NULL;
GO

-- Đổi giá trị này => mọi phiên đăng nhập cũ của tài khoản bị vô hiệu (khi reset mật khẩu, khoá tài khoản...)
IF COL_LENGTH('dbo.Accounts', 'SecurityStamp') IS NULL
    ALTER TABLE dbo.Accounts ADD SecurityStamp VARCHAR(64) NOT NULL
        CONSTRAINT DF_Accounts_SecurityStamp DEFAULT (REPLACE(CONVERT(VARCHAR(36), NEWID()), '-', ''));
GO

-- Kiểm tra: phải thấy 3 cột mới
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Accounts'
ORDER BY ORDINAL_POSITION;
GO
