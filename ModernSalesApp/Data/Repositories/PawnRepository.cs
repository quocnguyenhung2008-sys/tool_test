using Dapper;
using ModernSalesApp.Core;
using ModernSalesApp.Models;

namespace ModernSalesApp.Data.Repositories;

public sealed class PawnRepository
{
    private readonly DbConnectionFactory _factory;
    private readonly ILogger _logger;

    public PawnRepository(DbConnectionFactory factory, ILogger logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public sealed record PawnFilter(string Search, string SearchField, DateOnly DateFrom, DateOnly DateTo);

    public async Task<long> CreateRecordAsync(string customerName, string cccd, long totalAmountVnd, DateOnly datePawn, string recordNote, IReadOnlyList<PawnItemInput> items)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Vui lòng thêm ít nhất 1 món hàng cầm.");
        }

        foreach (var it in items)
        {
            if (it.Qty <= 0)
            {
                throw new InvalidOperationException("Số lượng phải > 0.");
            }
            if (it.WeightChi < 0)
            {
                throw new InvalidOperationException("Trọng lượng không hợp lệ.");
            }
        }

        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            var now = DateTimeOffset.Now;
            var customerNameSearch = InputParsers.NormalizeSearchText(customerName);
            var recordId = await conn.ExecuteScalarAsync<long>(
                """
                INSERT INTO pawn_records(customer_name, customer_name_search, cccd, note, total_amount_vnd, date_pawn, created_at)
                VALUES (@CustomerName, @CustomerNameSearch, @Cccd, @Note, @TotalAmountVnd, @DatePawn, @CreatedAt);
                SELECT last_insert_rowid();
                """,
                new
                {
                    CustomerName = customerName,
                    CustomerNameSearch = customerNameSearch,
                    Cccd = cccd,
                    Note = recordNote ?? "",
                    TotalAmountVnd = totalAmountVnd,
                    DatePawn = datePawn.ToString("yyyy-MM-dd"),
                    CreatedAt = now.ToString("O")
                },
                tx
            );

            foreach (var it in items)
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO pawn_items(record_id, qty, item_name, item_name_search, weight_chi, note, is_redeemed, redeemed_at)
                    VALUES (@RecordId, @Qty, @ItemName, @ItemNameSearch, @WeightChi, @Note, 0, NULL);
                    """,
                    new
                    {
                        RecordId = recordId,
                        Qty = it.Qty,
                        ItemName = it.ItemName,
                        ItemNameSearch = InputParsers.NormalizeSearchText(it.ItemName),
                        WeightChi = it.WeightChi,
                        Note = it.Note ?? ""
                    },
                    tx
                );
            }

            tx.Commit();
            return recordId;
        }
        catch (Exception ex)
        {
            _logger.Error("PawnRepository.CreateRecordAsync failed", ex);
            throw;
        }
    }

    public async Task<PagedResult<PawnRecordListItem>> GetRecordsPageAsync(PawnFilter filter, int pageIndex, int pageSize)
    {
        try
        {
            var search = (filter.Search ?? string.Empty).Trim();
            var searchField = (filter.SearchField ?? "name").Trim().ToLowerInvariant();

            var dateFrom = filter.DateFrom.ToString("yyyy-MM-dd");
            var dateTo = filter.DateTo.ToString("yyyy-MM-dd");
            var offset = pageIndex * pageSize;

            var amountSearchDigits = new string(search.Where(char.IsDigit).ToArray());
            var rawLike = $"%{search}%";
            var textLike = $"%{InputParsers.NormalizeSearchText(search)}%";
            var amountLike = $"%{amountSearchDigits}%";
            long? amount = null;
            if (searchField == "amount" && search.Length > 0 && InputParsers.TryParseMoneyVnd(search, out var parsedAmount))
            {
                amount = parsedAmount;
            }

            var whereSql = """
                WHERE r.date_pawn BETWEEN @DateFrom AND @DateTo
            """;

            if (search.Length > 0)
            {
                whereSql += searchField switch
                {
                    "cccd" => " AND r.cccd LIKE @RawLike",
                    "item" => " AND EXISTS (SELECT 1 FROM pawn_items i WHERE i.record_id = r.id AND i.item_name_search LIKE @TextLike)",
                    "amount" => amount.HasValue ? " AND r.total_amount_vnd = @Amount" : " AND CAST(r.total_amount_vnd AS TEXT) LIKE @AmountLike",
                    _ => " AND r.customer_name_search LIKE @TextLike"
                };
            }

            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            var total = await conn.ExecuteScalarAsync<int>(
                $"""
                SELECT COUNT(1)
                FROM pawn_records r
                {whereSql};
                """,
                new { DateFrom = dateFrom, DateTo = dateTo, RawLike = rawLike, TextLike = textLike, AmountLike = amountLike, Amount = amount }
            );

            var rows = (await conn.QueryAsync(
                $"""
                SELECT r.id, r.customer_name, r.cccd, IFNULL(r.note, '') AS note, r.total_amount_vnd, r.date_pawn, r.created_at
                FROM pawn_records r
                {whereSql}
                ORDER BY r.date_pawn DESC, r.id DESC
                LIMIT @Limit OFFSET @Offset;
                """,
                new { DateFrom = dateFrom, DateTo = dateTo, RawLike = rawLike, TextLike = textLike, AmountLike = amountLike, Amount = amount, Limit = pageSize, Offset = offset }
            )).ToList();

            var recordIds = rows.Select(r => (long)r.id).ToList();
            var aggByRecordId = new Dictionary<long, (string ItemsSummary, long ItemCount, long RedeemedCount)>();
            if (recordIds.Count > 0)
            {
                var aggRows = await conn.QueryAsync(
                    """
                    SELECT i.record_id,
                           group_concat(CAST(i.qty AS TEXT) || 'x' || i.item_name || '(' || CAST(i.weight_chi AS TEXT) || 'Chỉ)', '; ') AS items_summary,
                           COUNT(1) AS item_count,
                           SUM(CASE WHEN IFNULL(i.is_redeemed, 0) = 1 THEN 1 ELSE 0 END) AS redeemed_count
                    FROM pawn_items i
                    WHERE i.record_id IN @RecordIds
                    GROUP BY i.record_id;
                    """,
                    new { RecordIds = recordIds }
                );

                foreach (var a in aggRows)
                {
                    var recordId = (long)a.record_id;
                    var summary = (string?)(a.items_summary ?? "") ?? "";
                    var itemCount = Convert.ToInt64(a.item_count);
                    var redeemedCount = Convert.ToInt64(a.redeemed_count);
                    aggByRecordId[recordId] = (summary, itemCount, redeemedCount);
                }
            }

            var items = new List<PawnRecordListItem>(rows.Count);
            foreach (var r in rows)
            {
                var recordId = (long)r.id;
                var summary = "";
                long itemCount = 0;
                long redeemedCount = 0;

                if (aggByRecordId.TryGetValue(recordId, out var agg))
                {
                    summary = agg.ItemsSummary ?? "";
                    itemCount = agg.ItemCount;
                    redeemedCount = agg.RedeemedCount;
                }

                items.Add(new PawnRecordListItem(
                    recordId,
                    (string)r.customer_name,
                    (string)r.cccd,
                    (string)r.note,
                    (long)r.total_amount_vnd,
                    DateOnly.Parse((string)r.date_pawn),
                    DateTimeOffset.Parse((string)r.created_at),
                    summary,
                    itemCount,
                    redeemedCount
                ));
            }

            return new PagedResult<PawnRecordListItem>(items, total);
        }
        catch (Exception ex)
        {
            _logger.Error("PawnRepository.GetRecordsPageAsync failed", ex);
            throw;
        }
    }

    public async Task UpdateRecordNoteAsync(long recordId, string recordNote)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            var affected = await conn.ExecuteAsync(
                """
                UPDATE pawn_records
                SET note=@Note
                WHERE id=@Id;
                """,
                new { Id = recordId, Note = recordNote ?? "" }
            );

            if (affected <= 0)
            {
                throw new InvalidOperationException($"Không tìm thấy phiếu cầm ID {recordId} để cập nhật ghi chú.");
            }
        }
        catch (Exception ex)
        {
            _logger.Error("PawnRepository.UpdateRecordNoteAsync failed", ex);
            throw;
        }
    }

    public async Task<IReadOnlyList<(long ItemId, long RecordId, long Qty, string ItemName, double WeightChi, string Note, bool IsRedeemed, DateTimeOffset? RedeemedAt)>> GetItemsByRecordIdAsync(long recordId)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync(
                """
                SELECT id, record_id, qty, item_name, weight_chi, note, IFNULL(is_redeemed, 0) AS is_redeemed, redeemed_at
                FROM pawn_items
                WHERE record_id=@RecordId
                ORDER BY id ASC;
                """,
                new { RecordId = recordId }
            );

            var result = new List<(long ItemId, long RecordId, long Qty, string ItemName, double WeightChi, string Note, bool IsRedeemed, DateTimeOffset? RedeemedAt)>();
            foreach (var r in rows)
            {
                string? redeemedAtText = r.redeemed_at;
                DateTimeOffset? redeemedAt = redeemedAtText == null ? null : DateTimeOffset.Parse(redeemedAtText);

                result.Add((
                    (long)r.id,
                    (long)r.record_id,
                    (long)r.qty,
                    (string)r.item_name,
                    (double)r.weight_chi,
                    (string)r.note,
                    ((long)r.is_redeemed) == 1,
                    redeemedAt
                ));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error("PawnRepository.GetItemsByRecordIdAsync failed", ex);
            throw;
        }
    }

    public async Task<IReadOnlyList<(long ItemId, long RecordId, long Qty, string ItemName, double WeightChi, string Note, bool IsRedeemed, DateTimeOffset? RedeemedAt)>> GetItemsByRecordIdsAsync(IReadOnlyList<long> recordIds)
    {
        if (recordIds.Count == 0)
        {
            return Array.Empty<(long ItemId, long RecordId, long Qty, string ItemName, double WeightChi, string Note, bool IsRedeemed, DateTimeOffset? RedeemedAt)>();
        }

        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync(
                """
                SELECT id, record_id, qty, item_name, weight_chi, note, IFNULL(is_redeemed, 0) AS is_redeemed, redeemed_at
                FROM pawn_items
                WHERE record_id IN @RecordIds
                ORDER BY record_id ASC, id ASC;
                """,
                new { RecordIds = recordIds }
            );

            var result = new List<(long ItemId, long RecordId, long Qty, string ItemName, double WeightChi, string Note, bool IsRedeemed, DateTimeOffset? RedeemedAt)>();
            foreach (var r in rows)
            {
                string? redeemedAtText = r.redeemed_at;
                DateTimeOffset? redeemedAt = redeemedAtText == null ? null : DateTimeOffset.Parse(redeemedAtText);

                result.Add((
                    (long)r.id,
                    (long)r.record_id,
                    (long)r.qty,
                    (string)r.item_name,
                    (double)r.weight_chi,
                    (string)r.note,
                    ((long)r.is_redeemed) == 1,
                    redeemedAt
                ));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error("PawnRepository.GetItemsByRecordIdsAsync failed", ex);
            throw;
        }
    }

    public async Task UpdateItemsRedeemedAsync(long recordId, IReadOnlyList<(long ItemId, bool IsRedeemed)> updates)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            foreach (var u in updates)
            {
                await conn.ExecuteAsync(
                    """
                    UPDATE pawn_items
                    SET is_redeemed=@IsRedeemed,
                        redeemed_at=CASE WHEN @IsRedeemed=1 THEN COALESCE(redeemed_at, @RedeemedAt) ELSE NULL END
                    WHERE id=@Id AND record_id=@RecordId;
                    """,
                    new
                    {
                        Id = u.ItemId,
                        RecordId = recordId,
                        IsRedeemed = u.IsRedeemed ? 1 : 0,
                        RedeemedAt = DateTimeOffset.Now.ToString("O")
                    },
                    tx
                );
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            _logger.Error("PawnRepository.UpdateItemsRedeemedAsync failed", ex);
            throw;
        }
    }

    public async Task<IReadOnlyList<PawnRecordListItem>> GetRecordsForExportAsync(PawnFilter filter)
    {
        try
        {
            var search = (filter.Search ?? string.Empty).Trim();
            var searchField = (filter.SearchField ?? "name").Trim().ToLowerInvariant();

            var dateFrom = filter.DateFrom.ToString("yyyy-MM-dd");
            var dateTo = filter.DateTo.ToString("yyyy-MM-dd");

            var amountSearchDigits = new string(search.Where(char.IsDigit).ToArray());
            var rawLike = $"%{search}%";
            var textLike = $"%{InputParsers.NormalizeSearchText(search)}%";
            var amountLike = $"%{amountSearchDigits}%";
            long? amount = null;
            if (searchField == "amount" && search.Length > 0 && InputParsers.TryParseMoneyVnd(search, out var parsedAmount))
            {
                amount = parsedAmount;
            }

            var whereSql = """
                WHERE r.date_pawn BETWEEN @DateFrom AND @DateTo
            """;

            if (search.Length > 0)
            {
                whereSql += searchField switch
                {
                    "cccd" => " AND r.cccd LIKE @RawLike",
                    "item" => " AND EXISTS (SELECT 1 FROM pawn_items i WHERE i.record_id = r.id AND i.item_name_search LIKE @TextLike)",
                    "amount" => amount.HasValue ? " AND r.total_amount_vnd = @Amount" : " AND CAST(r.total_amount_vnd AS TEXT) LIKE @AmountLike",
                    _ => " AND r.customer_name_search LIKE @TextLike"
                };
            }

            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();

            var rows = await conn.QueryAsync(
                $"""
                SELECT r.id, r.customer_name, r.cccd, IFNULL(r.note, '') AS note, r.total_amount_vnd, r.date_pawn, r.created_at,
                       IFNULL(a.items_summary, '') AS items_summary,
                       IFNULL(a.item_count, 0) AS item_count,
                       IFNULL(a.redeemed_count, 0) AS redeemed_count
                FROM pawn_records r
                LEFT JOIN (
                    SELECT i.record_id,
                           group_concat(CAST(i.qty AS TEXT) || 'x' || i.item_name || '(' || CAST(i.weight_chi AS TEXT) || 'Chỉ)', '; ') AS items_summary,
                           COUNT(1) AS item_count,
                           SUM(CASE WHEN IFNULL(i.is_redeemed, 0) = 1 THEN 1 ELSE 0 END) AS redeemed_count
                    FROM pawn_items i
                    GROUP BY i.record_id
                ) a ON a.record_id = r.id
                {whereSql}
                ORDER BY r.date_pawn DESC, r.id DESC;
                """,
                new { DateFrom = dateFrom, DateTo = dateTo, RawLike = rawLike, TextLike = textLike, AmountLike = amountLike, Amount = amount }
            );

            return rows.Select(r => new PawnRecordListItem(
                (long)r.id,
                (string)r.customer_name,
                (string)r.cccd,
                (string)r.note,
                (long)r.total_amount_vnd,
                DateOnly.Parse((string)r.date_pawn),
                DateTimeOffset.Parse((string)r.created_at),
                (string?)(r.items_summary ?? "") ?? "",
                Convert.ToInt64(r.item_count),
                Convert.ToInt64(r.redeemed_count)
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error("PawnRepository.GetRecordsForExportAsync failed", ex);
            throw;
        }
    }

    public async Task DeleteRecordAsync(long recordId)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            await conn.ExecuteAsync(
                "DELETE FROM pawn_items WHERE record_id=@RecordId;",
                new { RecordId = recordId },
                tx
            );

            var affected = await conn.ExecuteAsync(
                "DELETE FROM pawn_records WHERE id=@RecordId;",
                new { RecordId = recordId },
                tx
            );

            if (affected <= 0)
            {
                throw new InvalidOperationException($"Không tìm thấy phiếu cầm ID {recordId} để xóa.");
            }

            tx.Commit();
        }
        catch (Exception ex)
        {
            _logger.Error("PawnRepository.DeleteRecordAsync failed", ex);
            throw;
        }
    }
}
