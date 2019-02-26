using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Loazit
{
    internal class Database : IDisposable
    {
        private SQLiteConnection _connection;

        public Database(string path, bool readOnly)
        {
            _connection =
                new SQLiteConnection($"Data Source={path};Version=3;Read Only={(readOnly ? "True" : "False")};");
            _connection.Open();
        }

        public void Dispose()
        {
            _connection?.Close();
            _connection?.Dispose();
            _connection = null;
        }

        public IList<IDictionary<string, object>> ReadTable(string table)
        {
            var result = new List<IDictionary<string, object>>();

            using (var command = new SQLiteCommand($"SELECT * FROM {table}", _connection))
            using (var reader = command.ExecuteReader())
            {
                var numColumns = reader.FieldCount;
                var columnNames = Enumerable.Range(0, numColumns).Select(index => reader.GetName(index)).ToArray();

                while (reader.Read())
                {
                    var values = new object[numColumns];
                    var returned = reader.GetValues(values);
                    Debug.Assert(returned == numColumns);

                    var row = columnNames
                        .Zip(values, (column, value) => new KeyValuePair<string, object>(column, value))
                        .ToDictionary(pair => pair.Key, pair => pair.Value);

                    result.Add(row);
                }
            }

            return result;
        }

        public Encoding Encoding
        {
            get
            {
                string encoding;
                using (var command = new SQLiteCommand("PRAGMA encoding", _connection))
                {
                    encoding = (string) command.ExecuteScalar();
                }

                switch (encoding.ToUpperInvariant())
                {
                    case "UTF-8":
                        return Encoding.UTF8;

                    case "UTF-16":
                        return BitConverter.IsLittleEndian ? Encoding.Unicode : Encoding.BigEndianUnicode;

                    case "UTF-16LE":
                        return Encoding.Unicode;

                    case "UTF-16BE":
                        return Encoding.BigEndianUnicode;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
