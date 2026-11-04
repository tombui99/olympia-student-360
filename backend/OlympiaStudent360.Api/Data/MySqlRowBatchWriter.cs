using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySql.Data.MySqlClient;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Data;

/// <summary>Parameterized multi-row writes for warehouse and disposable report rows.</summary>
internal static class MySqlRowBatchWriter
{
    public static Task InsertAsync<T>(AppDbContext db, IEnumerable<T> rows, CancellationToken ct)
        where T : BaseEntity => WriteAsync(db, rows, null, ct);

    public static Task UpsertAsync<T>(AppDbContext db, IEnumerable<T> rows,
        IReadOnlySet<string>? updateProperties, CancellationToken ct) where T : BaseEntity =>
        WriteAsync(db, rows, updateProperties ?? AllUpdates(db, typeof(T)), ct);

    private static HashSet<string> AllUpdates(AppDbContext db, Type type) =>
        db.Model.FindEntityType(type)!.GetProperties()
            .Where(p => p.Name is not (nameof(BaseEntity.Id) or nameof(BaseEntity.CreatedAt)))
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

    private static async Task WriteAsync<T>(AppDbContext db, IEnumerable<T> rows,
        IReadOnlySet<string>? updates, CancellationToken ct) where T : BaseEntity
    {
        var entity = db.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Unknown batch entity type.");
        var table = entity.GetTableName()!;
        var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
        var properties = entity.GetProperties().Where(p => p.PropertyInfo is not null &&
            p.GetComputedColumnSql() is null).ToArray();
        var columns = properties.Select(p => Quote(p.GetColumnName(store)!)).ToArray();
        var updateColumns = properties.Select((p, index) => (p, index))
            .Where(item => updates?.Contains(item.p.Name) == true)
            .Select(item => columns[item.index] + "=VALUES(" + columns[item.index] + ")").ToArray();
        var limit = Math.Min(1000, 60000 / properties.Length);
        var batch = new List<object?[]>();
        var bytes = 0;
        foreach (var row in rows)
        {
            var values = properties.Select(p => p.Name == nameof(BaseEntity.Id) && row.Id == 0
                ? null : p.PropertyInfo!.GetValue(row)).ToArray();
            var size = values.Sum(value => value is string text ? Encoding.UTF8.GetByteCount(text) : 32);
            if (batch.Count > 0 && (batch.Count >= limit || bytes + size > 2_000_000))
            {
                await FlushAsync(batch);
                batch.Clear();
                bytes = 0;
            }
            batch.Add(values);
            bytes += size;
        }
        if (batch.Count > 0) await FlushAsync(batch);

        async Task FlushAsync(List<object?[]> values)
        {
            var sql = new StringBuilder($"INSERT INTO {Quote(table)} ({string.Join(',', columns)}) VALUES ");
            var parameters = new List<object>();
            foreach (var record in values)
            {
                if (parameters.Count > 0) sql.Append(',');
                sql.Append('(');
                foreach (var value in record)
                {
                    if (sql[^1] != '(') sql.Append(',');
                    var name = "@batch" + parameters.Count;
                    sql.Append(name);
                    parameters.Add(new MySqlParameter(name, value ?? DBNull.Value));
                }
                sql.Append(')');
            }
            if (updates is not null)
            {
                if (updateColumns.Length == 0) throw new InvalidOperationException("Batch upsert has no update columns.");
                sql.Append(" ON DUPLICATE KEY UPDATE ").AppendJoin(',', updateColumns);
            }
            await db.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, ct);
        }
    }

    private static string Quote(string name) => "`" + name.Replace("`", "``") + "`";
}
