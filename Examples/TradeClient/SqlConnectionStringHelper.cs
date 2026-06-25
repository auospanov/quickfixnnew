using System;
using System.Text.RegularExpressions;

namespace TradeClient
{
    internal static class SqlConnectionStringHelper
    {
        public static string Clean(string connectionString)
        {
            var cleaned = connectionString;

            if (cleaned.Contains("Trust Server Certificate", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = Regex.Replace(
                    cleaned,
                    @"Trust\s+Server\s+Certificate\s*=\s*([^;]+)",
                    "TrustServerCertificate=$1",
                    RegexOptions.IgnoreCase);
            }

            if (cleaned.Contains("Pooling", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = Regex.Replace(
                    cleaned,
                    @"Pooling\s*=\s*[^;]+",
                    "Pooling=false",
                    RegexOptions.IgnoreCase);
            }
            else
            {
                cleaned = cleaned.TrimEnd(';', ' ') + ";Pooling=false";
            }

            if (!cleaned.Contains("MultipleActiveResultSets", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.TrimEnd(';', ' ') + ";MultipleActiveResultSets=True";
            }

            cleaned = Regex.Replace(cleaned, @"Max\s+Pool\s+Size\s*=\s*[^;]+;?", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"Min\s+Pool\s+Size\s*=\s*[^;]+;?", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"Connection\s+Lifetime\s*=\s*[^;]+;?", "", RegexOptions.IgnoreCase);

            return cleaned.Replace(";;", ";").TrimEnd(';', ' ');
        }
    }
}
