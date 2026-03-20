using Microsoft.Data.SqlClient;

namespace JobEntryApp.Infrastructure
{
    public static class SqlReaderValue
    {
        public static int ReadInt32(SqlDataReader reader, int ordinal)
            => Convert.ToInt32(reader.GetValue(ordinal));

        public static int? ReadNullableInt32(SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }
}
