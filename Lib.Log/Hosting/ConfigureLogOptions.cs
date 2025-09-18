namespace Lib.Log.Hosting;

using Lib.Log.Option;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

internal sealed class ConfigureLogOptions(IConfiguration configuration) : IConfigureOptions<LogOptions>
{
    private readonly IConfiguration _configuration = configuration;

    public void Configure(LogOptions options)
    {
        if (options.Database.Enabled && !string.IsNullOrWhiteSpace(options.Database.ConnectionStringName))
        {
            var connectionString = _configuration.GetConnectionString(options.Database.ConnectionStringName);

            if (!string.IsNullOrEmpty(connectionString))
            {
                options.Database.ConnectionString = connectionString;
            }
        }
    }
}
