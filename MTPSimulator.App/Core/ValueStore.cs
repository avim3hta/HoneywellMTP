using System;
using Microsoft.Data.Sqlite;

namespace MTPSimulator.App.Core
{
    public sealed class ValueStore : IDisposable
    {
        private readonly string _dbPath;
        private readonly SqliteConnection _conn;

        public ValueStore(string dbPath = "mtp_values.db")
        {
            _dbPath = dbPath;
            _conn = new SqliteConnection($"Data Source={_dbPath}");
            _conn.Open();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS values_store(
                  node_id TEXT PRIMARY KEY,
                  value TEXT,
                  updated_utc TEXT
                );";
            cmd.ExecuteNonQuery();
        }

        public void Upsert(string nodeId, object? value)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO values_store(node_id,value,updated_utc) VALUES($id,$v,$t) ON CONFLICT(node_id) DO UPDATE SET value=$v, updated_utc=$t;";
            cmd.Parameters.AddWithValue("$id", nodeId);
            cmd.Parameters.AddWithValue("$v", value?.ToString() ?? "");
            cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
        }

        public (bool found, string? value) TryGet(string nodeId)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM values_store WHERE node_id=$id";
            cmd.Parameters.AddWithValue("$id", nodeId);
            using var r = cmd.ExecuteReader();
            if (r.Read()) return (true, r.GetString(0));
            return (false, null);
        }

        public void Dispose()
        {
            try { _conn.Dispose(); } catch { }
        }
    }
}

