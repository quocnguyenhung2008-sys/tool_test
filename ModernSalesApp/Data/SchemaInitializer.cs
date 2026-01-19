using Dapper;
using ModernSalesApp.Core;

namespace ModernSalesApp.Data;

public static class SchemaInitializer
{
    public static async Task EnsureCreatedAsync(DbConnectionFactory factory, ILogger logger)
    {
        try
        {
            using var conn = factory.CreateConnection();
            await conn.OpenAsync();

            await conn.ExecuteAsync(
                """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA foreign_keys=ON;
                """
            );

            await conn.ExecuteAsync(
                """
                CREATE TABLE IF NOT EXISTS pawn_records (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    customer_name TEXT NOT NULL,
                    cccd TEXT NOT NULL,
                    note TEXT NOT NULL DEFAULT '',
                    total_amount_vnd INTEGER NOT NULL,
                    date_pawn TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_pawn_records_date_pawn ON pawn_records(date_pawn);
                CREATE INDEX IF NOT EXISTS ix_pawn_records_customer_name ON pawn_records(customer_name);
                CREATE INDEX IF NOT EXISTS ix_pawn_records_cccd ON pawn_records(cccd);
                CREATE INDEX IF NOT EXISTS ix_pawn_records_total_amount_vnd ON pawn_records(total_amount_vnd);

                CREATE TABLE IF NOT EXISTS pawn_items (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    record_id INTEGER NOT NULL,
                    qty INTEGER NOT NULL,
                    item_name TEXT NOT NULL,
                    weight_chi REAL NOT NULL,
                    note TEXT NOT NULL,
                    FOREIGN KEY (record_id) REFERENCES pawn_records(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ix_pawn_items_record_id ON pawn_items(record_id);
                CREATE INDEX IF NOT EXISTS ix_pawn_items_item_name ON pawn_items(item_name);

                CREATE TABLE IF NOT EXISTS pawn_catalog (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    item_name TEXT NOT NULL,
                    default_weight_chi REAL NOT NULL,
                    note TEXT NOT NULL,
                    created_at TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ix_pawn_catalog_item_name ON pawn_catalog(item_name);
                """
            );

            if (!await ColumnExistsAsync(conn, "pawn_items", "is_redeemed"))
            {
                await conn.ExecuteAsync("""ALTER TABLE pawn_items ADD COLUMN is_redeemed INTEGER NOT NULL DEFAULT 0;""");
                await conn.ExecuteAsync("""CREATE INDEX IF NOT EXISTS ix_pawn_items_is_redeemed ON pawn_items(is_redeemed);""");
            }

            if (!await ColumnExistsAsync(conn, "pawn_items", "redeemed_at"))
            {
                await conn.ExecuteAsync("""ALTER TABLE pawn_items ADD COLUMN redeemed_at TEXT NULL;""");
            }

            if (!await ColumnExistsAsync(conn, "pawn_records", "note"))
            {
                await conn.ExecuteAsync("""ALTER TABLE pawn_records ADD COLUMN note TEXT NOT NULL DEFAULT '';""");
            }

            if (!await ColumnExistsAsync(conn, "pawn_records", "customer_name_search"))
            {
                await conn.ExecuteAsync("""ALTER TABLE pawn_records ADD COLUMN customer_name_search TEXT NOT NULL DEFAULT '';""");
                await conn.ExecuteAsync("""CREATE INDEX IF NOT EXISTS ix_pawn_records_customer_name_search ON pawn_records(customer_name_search);""");
            }

            if (!await ColumnExistsAsync(conn, "pawn_items", "item_name_search"))
            {
                await conn.ExecuteAsync("""ALTER TABLE pawn_items ADD COLUMN item_name_search TEXT NOT NULL DEFAULT '';""");
                await conn.ExecuteAsync("""CREATE INDEX IF NOT EXISTS ix_pawn_items_item_name_search ON pawn_items(item_name_search);""");
            }

            await BackfillSearchColumnsAsync(conn, logger);
        }
        catch (Exception ex)
        {
            logger.Error("SchemaInitializer.EnsureCreatedAsync failed", ex);
            throw;
        }
    }

    private static async Task BackfillSearchColumnsAsync(System.Data.Common.DbConnection conn, ILogger logger)
    {
        try
        {
            if (await ColumnExistsAsync(conn, "pawn_records", "customer_name_search"))
            {
                const int batchSize = 2000;
                while (true)
                {
                    var rows = (await conn.QueryAsync(
                        """
                        SELECT id, customer_name
                        FROM pawn_records
                        WHERE IFNULL(customer_name_search, '') = ''
                        LIMIT @Limit;
                        """,
                        new { Limit = batchSize }
                    )).ToList();

                    if (rows.Count == 0)
                    {
                        break;
                    }

                    using var tx = conn.BeginTransaction();
                    foreach (var r in rows)
                    {
                        var id = (long)r.id;
                        var name = (string)r.customer_name;
                        var norm = InputParsers.NormalizeSearchText(name);
                        await conn.ExecuteAsync(
                            "UPDATE pawn_records SET customer_name_search=@Search WHERE id=@Id;",
                            new { Id = id, Search = norm },
                            tx
                        );
                    }
                    tx.Commit();
                }
            }

            if (await ColumnExistsAsync(conn, "pawn_items", "item_name_search"))
            {
                const int batchSize = 3000;
                while (true)
                {
                    var rows = (await conn.QueryAsync(
                        """
                        SELECT id, item_name
                        FROM pawn_items
                        WHERE IFNULL(item_name_search, '') = ''
                        LIMIT @Limit;
                        """,
                        new { Limit = batchSize }
                    )).ToList();

                    if (rows.Count == 0)
                    {
                        break;
                    }

                    using var tx = conn.BeginTransaction();
                    foreach (var r in rows)
                    {
                        var id = (long)r.id;
                        var name = (string)r.item_name;
                        var norm = InputParsers.NormalizeSearchText(name);
                        await conn.ExecuteAsync(
                            "UPDATE pawn_items SET item_name_search=@Search WHERE id=@Id;",
                            new { Id = id, Search = norm },
                            tx
                        );
                    }
                    tx.Commit();
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error("SchemaInitializer.BackfillSearchColumnsAsync failed", ex);
            throw;
        }
    }

    private static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection conn, string tableName, string columnName)
    {
        var rows = await conn.QueryAsync($"PRAGMA table_info({tableName});");
        foreach (var r in rows)
        {
            var name = (string)r.name;
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
