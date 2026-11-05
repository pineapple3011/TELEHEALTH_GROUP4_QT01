-- Chạy script này MỘT LẦN trên database TELEHEALTH (SSMS) trước khi chạy ứng dụng.
USE TELEHEALTH;
GO

IF OBJECT_ID(N'dbo.UserAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserAccounts
    (
        UserID            INT IDENTITY(1,1) NOT NULL,
        Email             VARCHAR(100)  NOT NULL,
        PasswordHash      VARCHAR(256)  NOT NULL,   -- PBKDF2, không bao giờ lưu mật khẩu gốc
        [Role]            VARCHAR(20)   NOT NULL,
        PatientID         INT NULL,
        DoctorID          INT NULL,
        AdminID           INT NULL,
        IsActive          BIT NOT NULL CONSTRAINT DF_UserAccounts_IsActive DEFAULT (1),
        FailedLoginCount  INT NOT NULL CONSTRAINT DF_UserAccounts_Failed DEFAULT (0),
        LockoutEndUtc     DATETIME2 NULL,
        SecurityStamp     VARCHAR(64) NOT NULL,
        CreatedAtUtc      DATETIME2 NOT NULL CONSTRAINT DF_UserAccounts_Created DEFAULT (SYSUTCDATETIME()),
        LastLoginUtc      DATETIME2 NULL,

        CONSTRAINT PK_UserAccounts PRIMARY KEY (UserID),
        CONSTRAINT UQ_UserAccounts_Email UNIQUE (Email),
        CONSTRAINT CK_UserAccounts_Role CHECK ([Role] IN ('Patient','Doctor','Admin')),
        -- Mỗi tài khoản gắn đúng MỘT hồ sơ, khớp với vai trò
        CONSTRAINT CK_UserAccounts_Link CHECK (
              ([Role]='Patient' AND PatientID IS NOT NULL AND DoctorID IS NULL AND AdminID IS NULL)
           OR ([Role]='Doctor'  AND DoctorID  IS NOT NULL AND PatientID IS NULL AND AdminID IS NULL)
           OR ([Role]='Admin'   AND AdminID   IS NOT NULL AND PatientID IS NULL AND DoctorID IS NULL)),
        CONSTRAINT FK_UserAccounts_Patients FOREIGN KEY (PatientID) REFERENCES dbo.Patients(PatientID),
        CONSTRAINT FK_UserAccounts_Doctors  FOREIGN KEY (DoctorID)  REFERENCES dbo.Doctors(DoctorID),
        CONSTRAINT FK_UserAccounts_Admin    FOREIGN KEY (AdminID)   REFERENCES dbo.ADMIN(AdminID)
    );

    -- Một hồ sơ chỉ có tối đa một tài khoản
    CREATE UNIQUE INDEX UX_UserAccounts_PatientID ON dbo.UserAccounts(PatientID) WHERE PatientID IS NOT NULL;
    CREATE UNIQUE INDEX UX_UserAccounts_DoctorID  ON dbo.UserAccounts(DoctorID)  WHERE DoctorID  IS NOT NULL;
    CREATE UNIQUE INDEX UX_UserAccounts_AdminID   ON dbo.UserAccounts(AdminID)   WHERE AdminID   IS NOT NULL;
END
GO
