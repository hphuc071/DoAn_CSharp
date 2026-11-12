using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using FourChang.Presentation.Shared.Contracts.Admin;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace FourChang.Api.Shared.Documents;

/// <summary>Xuất từ snapshot, không truy vấn lại menu/tiền. Excel dùng OOXML; PDF dùng PDFsharp nhúng font tiếng Việt.</summary>
public static class ReportDocumentWriter
{
    private static readonly object fontGate = new();
    /// <summary>Tạo workbook thật: tiền kiểu số, mã và nội dung người dùng là inline string, không tạo công thức.</summary>
    public static byte[] Excel(ReportSnapshot snapshot, CancellationToken ct = default)
    {
        XNamespace s = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var tables = Tables(snapshot);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            WriteXml(zip, "[Content_Types].xml", new XElement(XName.Get("Types", "http://schemas.openxmlformats.org/package/2006/content-types"),
                new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                tables.Select((_, i) => new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            WriteXml(zip, "_rels/.rels", Relationships([( "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml" )]));
            WriteXml(zip, "xl/workbook.xml", new XElement(s + "workbook", new XAttribute(XNamespace.Xmlns + "r", rel), new XElement(s + "sheets", tables.Select((t, i) => new XElement(s + "sheet", new XAttribute("name", (i + 1) + " " + t.Ten[..Math.Min(25, t.Ten.Length)]), new XAttribute("sheetId", i + 1), new XAttribute(rel + "id", "rId" + (i + 1)))))));
            WriteXml(zip, "xl/_rels/workbook.xml.rels", Relationships(tables.Select((_, i) => ("rId" + (i + 1), "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", $"worksheets/sheet{i + 1}.xml"))));
            for (var i = 0; i < tables.Length; i++)
            {
                ct.ThrowIfCancellationRequested(); var table = tables[i];
                var rows = new List<XElement> { new(s + "row", table.Cot.Select(c => TextCell(s, c.Ten))) };
                foreach (var row in table.Dong)
                {
                    ct.ThrowIfCancellationRequested();
                    rows.Add(new XElement(s + "row", row.Select((value, column) => table.Cot[column].Kieu == "number" && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                        ? new XElement(s + "c", new XElement(s + "v", number.ToString(CultureInfo.InvariantCulture))) : TextCell(s, value ?? ""))));
                }
                WriteXml(zip, $"xl/worksheets/sheet{i + 1}.xml", new XElement(s + "worksheet", new XElement(s + "sheetViews", new XElement(s + "sheetView", new XAttribute("workbookViewId", 0),
                    new XElement(s + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))),
                    new XElement(s + "cols", table.Cot.Select((_, j) => new XElement(s + "col", new XAttribute("min", j + 1), new XAttribute("max", j + 1), new XAttribute("width", j == 1 ? 38 : 24), new XAttribute("customWidth", 1)))), new XElement(s + "sheetData", rows)));
            }
        }
        return output.ToArray();
    }

    /// <summary>PDF phân trang theo chiều cao thật, lặp đầu bảng và đánh số trang; font được nhúng để giữ dấu trên máy khác.</summary>
    public static byte[] Pdf(ReportSnapshot snapshot, string? fontPath = null, CancellationToken ct = default)
    {
        lock (fontGate) GlobalFontSettings.FontResolver ??= new VietnameseFontResolver(fontPath);
        using var pdf = new PdfDocument(); pdf.Info.Title = "4CHANG LINH NGU LAM";
        var font = new XFont("FourChangReport", 9, XFontStyleEx.Regular); var heading = new XFont("FourChangReport", 14, XFontStyleEx.Bold);
        var tables = Tables(snapshot);
        foreach (var table in tables)
        {
            PdfPage? page = null; XGraphics? graphics = null; double y = 0;
            void NewPage()
            {
                graphics?.Dispose(); page = pdf.AddPage(); page.Width = XUnit.FromPoint(842); page.Height = XUnit.FromPoint(595); graphics = XGraphics.FromPdfPage(page); y = 36;
                graphics.DrawString("4CHANG LINH NGU LAM", heading, XBrushes.Black, new XPoint(32, y)); y += 20;
                graphics.DrawString(table.Ten + " · " + snapshot.MocDuLieu.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"), font, XBrushes.Black, new XPoint(32, y)); y += 24;
                for (var c = 0; c < table.Cot.Count; c++) DrawCell(graphics, table.Cot[c].Ten, 32 + c * 778d / table.Cot.Count, y, 778d / table.Cot.Count, font);
                y += 42;
            }
            NewPage();
            if (table.Dong.Count == 0) graphics!.DrawString("Không có phát sinh trong kỳ theo bộ lọc.", font, XBrushes.Black, new XPoint(36, y));
            foreach (var row in table.Dong)
            {
                ct.ThrowIfCancellationRequested(); var width = 778d / table.Cot.Count;
                var wrapped = row.Select(text => Wrap(graphics!, text ?? "", font, width - 12)).ToArray();
                var maxLines = wrapped.Length == 0 ? 1 : wrapped.Max(x => x.Count);
                for (var offset = 0; offset < maxLines;)
                {
                    if (y + 16 > 548) NewPage();
                    var count = Math.Max(1, Math.Min(maxLines - offset, (int)((548 - y) / 14)));
                    for (var c = 0; c < wrapped.Length; c++)
                        for (var line = 0; line < count && line + offset < wrapped[c].Count; line++)
                            graphics!.DrawString(wrapped[c][line + offset], font, XBrushes.Black, new XPoint(36 + c * width, y + line * 14));
                    y += count * 14 + 8; offset += count;
                }
                graphics!.DrawLine(XPens.LightGray, 32, y - 12, 810, y - 12);
            }
            graphics?.Dispose();
        }
        for (var i = 0; i < pdf.PageCount; i++)
        {
            using var graphics = XGraphics.FromPdfPage(pdf.Pages[i], XGraphicsPdfPageOptions.Append);
            graphics.DrawString($"Trang {i + 1}/{pdf.PageCount}", font, XBrushes.Black, new XPoint(740, 574));
        }
        using var stream = new MemoryStream(); pdf.Save(stream, false); return stream.ToArray();
    }

    /// <summary>Metadata chứa kỳ, mốc và định nghĩa chỉ tiêu để người nhận không nhầm doanh thu với dòng tiền.</summary>
    private static ReportTable Metadata(ReportSnapshot x) => new("Thông tin báo cáo", [new("Nội dung"), new("Giá trị")],
    [new string?[] { "Kỳ UTC", x.BoLoc.TuLuc.ToString("O") + " — " + x.BoLoc.DenLuc.ToString("O") }, new string?[] { "Mốc", x.MocDuLieu.ToString("O") },
     new string?[] { "Múi giờ", x.MuiGio }, new string?[] { "Bộ lọc", System.Text.Json.JsonSerializer.Serialize(x.BoLoc) }, new string?[] { "Định nghĩa", x.DinhNghia }]);

    /// <summary>Đưa cả chỉ tiêu tổng hợp vào file; tài liệu hóa đơn/nhật ký không thêm KPI không liên quan.</summary>
    private static ReportTable[] Tables(ReportSnapshot snapshot)
    {
        if (snapshot.Loai != "REPORT") return new[] { Metadata(snapshot) }.Concat(snapshot.Bang).ToArray();
        var x = snapshot.ChiSo;
        var pairs = new (string Name, decimal? Value, string Unit)[] { ("Tiền hàng", x.TienHang, "đ"), ("Phí giao", x.PhiGiaoHang, "đ"),
            ("Điều chỉnh khiếu nại", x.DieuChinhKhieuNai, "đ"), ("Doanh thu thuần", x.DoanhThuThuan, "đ"), ("Thu tiền mặt", x.ThuTienMat, "đ"),
            ("Thu chuyển khoản", x.ThuChuyenKhoan, "đ"), ("Hoàn tiền mặt", x.HoanTienMat, "đ"), ("Hoàn chuyển khoản", x.HoanChuyenKhoan, "đ"),
            ("Dòng tiền ròng", x.DongTienRong, "đ"), ("Đơn tạo trong kỳ", x.TongDon, "đơn"), ("Hoàn tất trong kỳ", x.HoanTat, "đơn"),
            ("Hủy toàn đơn", x.HuyToanDon, "đơn"), ("Hết hạn", x.HetHan, "đơn"), ("Hủy một phần", x.HuyMotPhan, "đơn"),
            ("Số ly hủy", x.SoLyHuy, "ly"), ("Tỷ lệ hoàn tất của đơn tạo trong kỳ", x.TyLeHoanTat, "tỷ lệ 0–1") };
        var table = new ReportTable("Chỉ tiêu tổng hợp", [new("Chỉ tiêu"), new("Giá trị", "number"), new("Đơn vị")],
            pairs.Select(p => (IReadOnlyList<string?>)new string?[] { p.Name, p.Value?.ToString(CultureInfo.InvariantCulture) ?? "Không áp dụng", p.Unit }).ToList());
        return new[] { Metadata(snapshot), table }.Concat(snapshot.Bang).ToArray();
    }
    /// <summary>Ô text OOXML luôn inlineStr; ký tự =,+,-,@ từ tên/lý do không trở thành công thức Excel.</summary>
    private static XElement TextCell(XNamespace ns, string value) => new(ns + "c", new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value)));
    /// <summary>Viết XML UTF-8 qua XElement để mọi nội dung người dùng được escape an toàn.</summary>
    private static void WriteXml(ZipArchive zip, string name, XElement content) { using var stream = zip.CreateEntry(name).Open(); content.Save(stream); }
    /// <summary>Tạo relationship trong workbook mà không dùng đường dẫn do người dùng gửi.</summary>
    private static XElement Relationships(IEnumerable<(string Id, string Type, string Target)> items)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new(ns + "Relationships", items.Select(x => new XElement(ns + "Relationship", new XAttribute("Id", x.Id), new XAttribute("Type", x.Type), new XAttribute("Target", x.Target))));
    }
    /// <summary>Ngắt dòng theo đo font thực tế, kể cả chuỗi dài không có khoảng trắng.</summary>
    private static List<string> Wrap(XGraphics graphics, string text, XFont font, double width)
    {
        var result = new List<string>(); var line = new StringBuilder();
        foreach (var c in text.Replace("\r", ""))
        {
            if (c == '\n' || graphics.MeasureString(line.ToString() + c, font).Width > width)
            { result.Add(line.ToString()); line.Clear(); if (c == '\n') continue; }
            line.Append(char.IsControl(c) ? ' ' : c);
        }
        result.Add(line.ToString()); return result;
    }
    /// <summary>Vẽ đầu cột không tràn sang cột bên cạnh.</summary>
    private static void DrawCell(XGraphics graphics, string value, double x, double y, double width, XFont font)
    {
        var lines = Wrap(graphics, value, font, width - 12);
        for (var i = 0; i < Math.Min(3, lines.Count); i++) graphics.DrawString(lines[i], font, XBrushes.Black, new XPoint(x + 4, y + 14 * i));
    }
}

/// <summary>Font resolver cấu hình được cho Windows/Linux; thiếu font báo lỗi xuất thay vì âm thầm mất dấu.</summary>
internal sealed class VietnameseFontResolver(string? configuredPath) : IFontResolver
{
    /// <summary>Ánh xạ duy nhất family báo cáo sang font Unicode đã chọn.</summary>
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new("FourChangUnicode", bold, italic);
    /// <summary>Đọc font từ cấu hình hoặc vị trí hệ điều hành; không tải font từ mạng và không thay logo.</summary>
    public byte[] GetFont(string faceName)
    {
        var candidates = new[] { configuredPath, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"), "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" };
        var file = candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && File.Exists(x));
        return file is null ? throw new InvalidOperationException("Chưa có font PDF tiếng Việt. Cấu hình Reports:FontPath.") : File.ReadAllBytes(file);
    }
}
