-- ADM-11: bổ sung quyền hẹp cho login ứng dụng hiện hữu; không đổi bảng/cột/dữ liệu.
-- API vẫn yêu cầu Admin, rowversion, kiểm tra tham chiếu và audit nguyên tử.
IF USER_ID(N'fourchang_app') IS NOT NULL
BEGIN
    REVOKE DELETE ON OBJECT::dbo.NhanVien FROM [fourchang_app];
    GRANT DELETE ON OBJECT::dbo.NhanVien TO [fourchang_app];
END;
