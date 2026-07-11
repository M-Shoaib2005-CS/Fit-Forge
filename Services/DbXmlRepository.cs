using System.Linq;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using FitForge.DL;

namespace FitForge.Services
{
    /// <summary>
    /// Render's (and most cloud hosts') container disk is wiped on every restart/redeploy.
    /// The default Data Protection key storage lives on that disk, so every restart
    /// generates new keys and silently invalidates every logged-in session and
    /// antiforgery token. Storing the keys in MySQL instead means they survive restarts,
    /// so users stay logged in across deploys/spin-downs.
    /// </summary>
    public class DbXmlRepository : IXmlRepository
    {
        public DbXmlRepository()
        {
            try
            {
                DB.NonQuery(@"CREATE TABLE IF NOT EXISTS data_protection_keys (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    xml_data TEXT NOT NULL,
                    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
                )");
            }
            catch (Exception ex)
            {
                // Don't let a temporarily-unreachable database take down the whole app at
                // startup. Worst case here is sessions won't survive a restart until the
                // DB is reachable again — annoying, but not fatal like it was before.
                Console.Error.WriteLine($"[DbXmlRepository] Could not reach database at startup: {ex.Message}");
            }
        }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            try
            {
                var dt = DB.Select("SELECT xml_data FROM data_protection_keys");
                return dt.Rows()
                    .Select(r => XElement.Parse((string)r["xml_data"]))
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DbXmlRepository] Could not read keys: {ex.Message}");
                return Array.Empty<XElement>();
            }
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            try
            {
                DB.NonQuery("INSERT INTO data_protection_keys (xml_data) VALUES (@x)",
                    DB.P("@x", element.ToString()));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[DbXmlRepository] Could not store key: {ex.Message}");
            }
        }
    }
}
