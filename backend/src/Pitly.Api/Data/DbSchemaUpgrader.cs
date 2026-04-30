using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Pitly.Api.Data;

public static class DbSchemaUpgrader
{
    public static void EnsureLatestSchema(AppDbContext db)
    {
        TryExecute(db, "ALTER TABLE Sessions ADD COLUMN TaxableFrom TEXT NULL;");
        TryExecute(db, "ALTER TABLE Sessions ADD COLUMN TotalCreditableWithholdingPln TEXT NOT NULL DEFAULT '0';");
        TryExecute(db, "ALTER TABLE Dividends ADD COLUMN CreditableWithholdingTaxPln TEXT NOT NULL DEFAULT '0';");
        TryExecute(db, "ALTER TABLE Dividends ADD COLUMN Isin TEXT NULL;");
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
