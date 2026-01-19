using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Globalization;
using System.IO;

namespace ModernSalesApp.UI;

public sealed class PawnExcelExporter
{
    public async Task ExportAsync(string filePath, Data.Repositories.PawnRepository.PawnFilter filter)
    {
        var records = await AppServices.Pawn.GetRecordsForExportAsync(filter);
        var itemsByRecordId = new Dictionary<long, IReadOnlyList<(long ItemId, long RecordId, long Qty, string ItemName, double WeightChi, string Note, bool IsRedeemed, DateTimeOffset? RedeemedAt)>>();
        var recordIds = records.Select(r => r.Id).ToList();
        const int chunkSize = 500;
        for (var i = 0; i < recordIds.Count; i += chunkSize)
        {
            var chunk = recordIds.Skip(i).Take(chunkSize).ToList();
            var items = await AppServices.Pawn.GetItemsByRecordIdsAsync(chunk);
            foreach (var g in items.GroupBy(x => x.RecordId))
            {
                itemsByRecordId[g.Key] = g.ToList();
            }
        }

        using var package = new ExcelPackage();
        var wsRecords = package.Workbook.Worksheets.Add("PhieuCam");
        var wsItems = package.Workbook.Worksheets.Add("ChiTiet");

        WriteRecords(wsRecords, records, itemsByRecordId);
        WriteItems(wsItems, records, itemsByRecordId);

        var fi = new FileInfo(filePath);
        if (fi.Directory != null)
        {
            fi.Directory.Create();
        }

        await package.SaveAsAsync(fi);
    }

    private static void WriteRecords(
        ExcelWorksheet ws,
        IReadOnlyList<Models.PawnRecordListItem> records,
        IReadOnlyDictionary<long, IReadOnlyList<(long ItemId, long RecordId, long Qty, string ItemName, double WeightChi, string Note, bool IsRedeemed, DateTimeOffset? RedeemedAt)>> itemsByRecordId
    )
    {
        var headers = new[] { "ID", "Khách hàng", "CCCD", "Tổng tiền (VNĐ)", "Ngày cầm", "Món hàng", "Đã chuộc", "Ngày chuộc", "Tổng món" };
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = headers[i];
        }

        using (var rng = ws.Cells[1, 1, 1, headers.Length])
        {
            rng.Style.Font.Bold = true;
            rng.Style.Fill.PatternType = ExcelFillStyle.Solid;
            rng.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(238, 238, 238));
        }

        for (var i = 0; i < records.Count; i++)
        {
            var r = records[i];
            var row = i + 2;
            ws.Cells[row, 1].Value = r.Id;
            ws.Cells[row, 2].Value = r.CustomerName;
            ws.Cells[row, 3].Value = r.Cccd;
            ws.Cells[row, 4].Value = r.TotalAmountVnd;
            ws.Cells[row, 5].Value = r.DatePawn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cells[row, 6].Value = r.ItemsSummary;
            var isFullyRedeemed = r.ItemCount > 0 && r.RedeemedCount >= r.ItemCount;
            ws.Cells[row, 7].Value = isFullyRedeemed ? "Đã chuộc" : "Chưa chuộc";
            if (isFullyRedeemed && itemsByRecordId.TryGetValue(r.Id, out var items))
            {
                var redeemedAt = items
                    .Where(x => x.IsRedeemed && x.RedeemedAt != null)
                    .Select(x => x.RedeemedAt!.Value)
                    .OrderByDescending(x => x)
                    .FirstOrDefault();

                ws.Cells[row, 8].Value = redeemedAt == default ? "" : redeemedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            }
            else
            {
                ws.Cells[row, 8].Value = "";
            }
            ws.Cells[row, 9].Value = r.ItemCount;
        }

        ws.Column(4).Style.Numberformat.Format = "#,##0";
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        ws.View.FreezePanes(2, 1);
    }

    private static void WriteItems(
        ExcelWorksheet ws,
        IReadOnlyList<Models.PawnRecordListItem> records,
        IReadOnlyDictionary<long, IReadOnlyList<(long ItemId, long RecordId, long Qty, string ItemName, double WeightChi, string Note, bool IsRedeemed, DateTimeOffset? RedeemedAt)>> itemsByRecordId
    )
    {
        var headers = new[] { "ID phiếu", "Khách hàng", "CCCD", "SL", "Món hàng", "Trọng lượng (Chỉ)", "Đã chuộc", "Ngày chuộc" };
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = headers[i];
        }

        using (var rng = ws.Cells[1, 1, 1, headers.Length])
        {
            rng.Style.Font.Bold = true;
            rng.Style.Fill.PatternType = ExcelFillStyle.Solid;
            rng.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(238, 238, 238));
        }

        var rowIndex = 2;
        foreach (var rec in records)
        {
            if (!itemsByRecordId.TryGetValue(rec.Id, out var items))
            {
                continue;
            }

            foreach (var it in items)
            {
                ws.Cells[rowIndex, 1].Value = rec.Id;
                ws.Cells[rowIndex, 2].Value = rec.CustomerName;
                ws.Cells[rowIndex, 3].Value = rec.Cccd;
                ws.Cells[rowIndex, 4].Value = it.Qty;
                ws.Cells[rowIndex, 5].Value = it.ItemName;
                ws.Cells[rowIndex, 6].Value = it.WeightChi;
                ws.Cells[rowIndex, 7].Value = it.IsRedeemed ? "Đã chuộc" : "Chưa chuộc";
                ws.Cells[rowIndex, 8].Value = it.RedeemedAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";
                rowIndex++;
            }
        }

        ws.Column(4).Style.Numberformat.Format = "#,##0";
        ws.Column(6).Style.Numberformat.Format = "0.##";
        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        ws.View.FreezePanes(2, 1);
    }
}
