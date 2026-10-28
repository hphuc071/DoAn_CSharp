using MocCafe.Desktop.Controls;
using MocCafe.Desktop.Preview;
using MocCafe.Presentation.Shared.Models;
namespace MocCafe.Desktop.Features.Admin;

internal static class ManagementPage
{
    public static Control Create(string title, PreviewTable table, string note, params (string Title,string[] Fields)[] actions)
    {
        var buttons=actions.Select((a,i)=>(Control)Ui.Button(a.Title,()=>PreviewDialog.ShowMessage(null,a.Title,note,a.Fields),i==0)).ToArray();
        var grid=Ui.Grid(table,Math.Min(330,table.Rows.Length*44+54));var search=Ui.Input("Tìm kiếm…","",350);
        search.TextChanged+=(_,_)=>{if(grid.DataSource is System.Data.DataTable data){var term=search.Text.Replace("'","''").Replace("[","[[]").Replace("%","[%]").Replace("*","[*]");data.DefaultView.RowFilter=string.Join(" OR ",data.Columns.Cast<System.Data.DataColumn>().Select(c=>$"CONVERT([{c.ColumnName}], 'System.String') LIKE '%{term}%'"));}};
        return Ui.Panel(title,Ui.Row(search,Ui.Select("Mọi trạng thái","Đang hoạt động","Ngừng / khóa")),grid,Ui.Row(buttons));
    }
}
