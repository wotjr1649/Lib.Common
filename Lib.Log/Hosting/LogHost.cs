namespace Lib.Log.Hosting;

using Lib.Log.Option;
using Lib.Log.Provider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

public static class LogHost
{
    public static IHostApplicationBuilder UseLibLogFromConfig(
        this IHostApplicationBuilder builder,
        string sectionName = "LibLog",
        IEnumerable<string>? providerOnlySuppressCategories = null,
        bool throwIfMissing = true)
    {
        var section = builder.Configuration.GetSection(sectionName);
        if (!section.Exists())
        {
            if (throwIfMissing)
            {
                throw new InvalidOperationException($"Configuration section '{sectionName}' was not found.");
            }

            return builder;
        }

        return builder.UseLibLog(section, providerOnlySuppressCategories);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Option types are preserved via DynamicDependency.")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.FormattingOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.RoutingOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.PartitionGroupOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.RolloverOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.LocalSinkOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.DbSinkOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.FtpSinkOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.CircuitBreakerOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.RootingOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LogOptions.RootRule))]
    public static IHostApplicationBuilder UseLibLog(
        this IHostApplicationBuilder builder,
        IConfigurationSection section,
        IEnumerable<string>? providerOnlySuppressCategories = null)
    {
        ArgumentNullException.ThrowIfNull(section);

        builder.Services.AddOptions<LogOptions>()
            .BindConfiguration(section.Path)
            .ValidateOnStart();

        builder.Services.AddLibLog();

        if (providerOnlySuppressCategories is not null)
        {
            builder.Services.Configure<LoggerFilterOptions>(opts =>
            {
                foreach (var cat in providerOnlySuppressCategories)
                {
                    opts.Rules.Add(new LoggerFilterRule(
                        providerName: typeof(LoggerProvider).FullName,
                        categoryName: cat,
                        logLevel: LogLevel.None,
                        filter: null));
                }
            });
        }

        return builder;
    }

    public static IHostApplicationBuilder UseLibLog(
        this IHostApplicationBuilder builder,
        Action<LogOptions> configure,
        IEnumerable<string>? providerOnlySuppressCategories = null)
    {
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<LogOptions>()
            .Configure(configure)
            .ValidateOnStart();

        builder.Services.AddLibLog();

        if (providerOnlySuppressCategories is not null)
        {
            builder.Services.Configure<LoggerFilterOptions>(opts =>
            {
                foreach (var cat in providerOnlySuppressCategories)
                {
                    opts.Rules.Add(new LoggerFilterRule(
                        providerName: typeof(LoggerProvider).FullName,
                        categoryName: cat,
                        logLevel: LogLevel.None,
                        filter: null));
                }
            });
        }

        return builder;
    }
}



