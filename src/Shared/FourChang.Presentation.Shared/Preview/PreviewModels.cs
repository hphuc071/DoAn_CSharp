namespace FourChang.Presentation.Shared.Preview;

public sealed record ProductPreview(string Id, string Name, string Category, decimal Price, int ImageIndex, bool Available = true, string Badge = "");
public sealed record PreviewTable(string[] Columns, string[][] Rows);
