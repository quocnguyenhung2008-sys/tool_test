namespace ModernSalesApp.Models;

public sealed record PawnRecordListItem(
    long Id,
    string CustomerName,
    string Cccd,
    string RecordNote,
    long TotalAmountVnd,
    DateOnly DatePawn,
    DateTimeOffset CreatedAt,
    string ItemsSummary,
    long ItemCount,
    long RedeemedCount
);
