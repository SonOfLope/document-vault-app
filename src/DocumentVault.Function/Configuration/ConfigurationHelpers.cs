namespace DocumentVault.Function.Configuration
{
    public static class ConfigurationHelpers
    {
        public static string GetConnectionStringValue(string connectionString, string key)
        {
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kvp = part.Split('=', 2);
                if (kvp.Length == 2 && kvp[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp[1].Trim();
                }
            }

            throw new InvalidOperationException($"Key '{key}' not found in the connection string.");
        }
    }
}