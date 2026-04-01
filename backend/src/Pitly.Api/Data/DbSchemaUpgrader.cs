using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Pitly.Api.Data;

public static class DbSchemaUpgrader
{
    public static void EnsureLatestSchema(AppDbContext db)
    {
        TryExecute(db, "ALTER TABLE Sessions ADD COLUMN TaxableFrom TEXT NULL;");
    }

    private static void TryExecute(AppDbContext db, string sql)
    {
        try
        {
            db.Database.ExecuteSqlRaw(sql);
        }
        catch (SqliteException ex) when (
            ex.SqliteErrorCode == 1 &&
            ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
        }
    }
}
