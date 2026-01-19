using Dapper;
using ModernSalesApp.Core;
using ModernSalesApp.Models;

namespace ModernSalesApp.Data.Repositories;

public sealed class PawnCatalogRepository
{
    private readonly DbConnectionFactory _factory;
    private readonly ILogger _logger;

    public PawnCatalogRepository(DbConnectionFactory factory, ILogger logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PawnCatalogItem>> GetAllAsync()
    {
        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync(
                """
                SELECT id, item_name, default_weight_chi, note, created_at
                FROM pawn_catalog
                ORDER BY item_name COLLATE NOCASE ASC, id DESC;
                """
            );

            return rows.Select(r => new PawnCatalogItem(
                (long)r.id,
                (string)r.item_name,
                (double)r.default_weight_chi,
                (string)r.note,
                DateTimeOffset.Parse((string)r.created_at)
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error("PawnCatalogRepository.GetAllAsync failed", ex);
            throw;
        }
    }

    public async Task<long> CreateAsync(string itemName, double defaultWeightChi, string note)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            var now = DateTimeOffset.Now;
            var id = await conn.ExecuteScalarAsync<long>(
                """
                INSERT INTO pawn_catalog(item_name, default_weight_chi, note, created_at)
                VALUES (@ItemName, @DefaultWeightChi, @Note, @CreatedAt);
                SELECT last_insert_rowid();
                """,
                new
                {
                    ItemName = itemName,
                    DefaultWeightChi = defaultWeightChi,
                    Note = note ?? "",
                    CreatedAt = now.ToString("O")
                }
            );

            return id;
        }
        catch (Exception ex)
        {
            _logger.Error("PawnCatalogRepository.CreateAsync failed", ex);
            throw;
        }
    }

    public async Task UpdateAsync(long id, string itemName, double defaultWeightChi, string note)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await conn.ExecuteAsync(
                """
                UPDATE pawn_catalog
                SET item_name=@ItemName,
                    default_weight_chi=@DefaultWeightChi,
                    note=@Note
                WHERE id=@Id;
                """,
                new
                {
                    Id = id,
                    ItemName = itemName,
                    DefaultWeightChi = defaultWeightChi,
                    Note = note ?? ""
                }
            );
        }
        catch (Exception ex)
        {
            _logger.Error("PawnCatalogRepository.UpdateAsync failed", ex);
            throw;
        }
    }

    public async Task DeleteAsync(long id)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            await conn.ExecuteAsync(
                "DELETE FROM pawn_catalog WHERE id=@Id;",
                new { Id = id }
            );
        }
        catch (Exception ex)
        {
            _logger.Error("PawnCatalogRepository.DeleteAsync failed", ex);
            throw;
        }
    }
}

