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
            DB.NonQuery(@"CREATE TABLE IF NOT EXISTS data_protection_keys (
                id INT AUTO_INCREMENT PRIMARY KEY,
                xml_data TEXT NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            )");
        }

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            var dt = DB.Select("SELECT xml_data FROM data_protection_keys");
            return dt.Rows()
                .Select(r => XElement.Parse((string)r["xml_data"]))
                .ToList();
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            DB.NonQuery("INSERT INTO data_protection_keys (xml_data) VALUES (@x)",
                DB.P("@x", element.ToString()));
        }
    }
}
