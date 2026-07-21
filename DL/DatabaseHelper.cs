using System.Linq;
using MySqlConnector;
using System.Data;
namespace FitForge.DL
{
    public static class DB
    {
        private static string _cs = "";
        public static void Configure(string cs) => _cs = cs;
        public static MySqlConnection Conn() => new MySqlConnection(_cs);
        public static int NonQuery(string sql, params MySqlParameter[] p){
            using var c=Conn(); using var cmd=new MySqlCommand(sql,c);
            foreach(var x in p)cmd.Parameters.Add(x); c.Open(); return cmd.ExecuteNonQuery();
        }
        public static object? Scalar(string sql, params MySqlParameter[] p){
            using var c=Conn(); using var cmd=new MySqlCommand(sql,c);
            foreach(var x in p)cmd.Parameters.Add(x); c.Open(); return cmd.ExecuteScalar();
        }
        // Runs an INSERT and returns LAST_INSERT_ID() from the SAME connection.
        // NonQuery/Scalar each open+close their own connection, and LAST_INSERT_ID()
        // is connection-scoped in MySQL, so calling them back-to-back always yields 0.
        public static long InsertGetId(string sql, params MySqlParameter[] p){
            using var c=Conn(); using var cmd=new MySqlCommand(sql,c);
            foreach(var x in p)cmd.Parameters.Add(x);
            c.Open();
            cmd.ExecuteNonQuery();
            cmd.CommandText="SELECT LAST_INSERT_ID()";
            cmd.Parameters.Clear();
            return Convert.ToInt64(cmd.ExecuteScalar());
        }
        public static DataTable Select(string sql, params MySqlParameter[] p){
            using var c=Conn(); using var cmd=new MySqlCommand(sql,c);
            foreach(var x in p)cmd.Parameters.Add(x);
            c.Open();
            using var reader = cmd.ExecuteReader();
            var t = new DataTable();
            t.Load(reader); // MySqlConnector doesn't ship a MySqlDataAdapter — load straight from the reader instead
            return t;
        }
        public static MySqlParameter P(string n, object? v) => new MySqlParameter(n, v??DBNull.Value);
    }


    /// <summary>
    /// Extension methods for DataTable — replaces System.Data.DataSetExtensions
    /// so we don't need that NuGet package.
    /// </summary>
    public static class DataTableExtensions
    {
        public static IEnumerable<System.Data.DataRow> Rows(this System.Data.DataTable dt)
        {
            foreach (System.Data.DataRow row in dt.Rows)
                yield return row;
        }
    }

}
