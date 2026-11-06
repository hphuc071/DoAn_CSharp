-- Đã được chủ dự án cho phép: thêm địa chỉ liên hệ, giữ nguyên 35 bảng và toàn bộ mã/dữ liệu cũ.
IF COL_LENGTH(N'dbo.NhanVien',N'DiaChiLienHe') IS NULL
    ALTER TABLE dbo.NhanVien ADD DiaChiLienHe nvarchar(500) NULL;
