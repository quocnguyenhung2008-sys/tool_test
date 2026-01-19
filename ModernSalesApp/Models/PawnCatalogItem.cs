namespace ModernSalesApp.Models;

public sealed record PawnCatalogItem(
    long Id,
    string ItemName,
    double DefaultWeightChi,
    string Note,
    DateTimeOffset CreatedAt
);
