using Talk.Extensions;

namespace Host.Common
{
    public static class AppConfig
    {
        //public static string DbProviderName => ConfigurationManager.GetTryConfig("Quartz:dbProviderName");
        //public static string ConnectionString => ConfigurationManager.GetTryConfig("Quartz:connectionString");
        public static string DbProviderName { get; set; }
        public static string ConnectionString { get; set; }
    }
}
