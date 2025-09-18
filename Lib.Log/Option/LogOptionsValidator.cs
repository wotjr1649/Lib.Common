namespace Lib.Log.Option;

using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

internal sealed class LogOptionsValidator : IValidateOptions<LogOptions>
{
    private static readonly Regex TemplateTokenRegex = new("\\{([^}]+)\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AllowedTemplateTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "Root",
        "RootByLevel",
        "Project",
        "Category",
        "DeviceId",
        "DeviceId?",
        "yyyy",
        "MM",
        "dd",
        "HH",
        "mm",
        "ss"
    };

    public ValidateOptionsResult Validate(string? name, LogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        ValidateFormatting(options, failures);
        ValidateRouting(options, failures);
        ValidatePartitions(options, failures);
        ValidateSampling(options, failures);
        ValidateLocalSink(options, failures);
        ValidateDatabaseSink(options, failures);
        ValidateFtpSink(options, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateFormatting(LogOptions options, List<string> failures)
    {
        if (options.Formatting.MaxMessageLength <= 0)
        {
            failures.Add("Formatting.MaxMessageLength must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.Formatting.TimestampFormat))
        {
            failures.Add("Formatting.TimestampFormat must be provided.");
        }
    }

    private static void ValidateRouting(LogOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.Routing.DeviceKeyField))
        {
            failures.Add("Routing.DeviceKeyField must be provided.");
        }

        if (options.Routing.CategoryGroups.Count == 0)
        {
            failures.Add("At least one routing category group must be configured.");
        }
    }

    private static void ValidatePartitions(LogOptions options, List<string> failures)
    {
        if (options.Partitions.Count == 0)
        {
            return;
        }

        foreach (var (name, partition) in options.Partitions)
        {
            if (partition.Shards <= 0)
            {
                failures.Add($"Partition '{name}' has invalid Shards value '{partition.Shards}'. Shards must be >= 1.");
            }

            if (partition.QueueCapacity <= 0)
            {
                failures.Add($"Partition '{name}' has invalid QueueCapacity '{partition.QueueCapacity}'. QueueCapacity must be >= 1.");
            }

            if (partition.BatchSize <= 0)
            {
                failures.Add($"Partition '{name}' has invalid BatchSize '{partition.BatchSize}'. BatchSize must be >= 1.");
            }

            if (partition.FlushIntervalMs <= 0)
            {
                failures.Add($"Partition '{name}' has invalid FlushIntervalMs '{partition.FlushIntervalMs}'. FlushIntervalMs must be >= 1.");
            }
        }
    }

    private static void ValidateSampling(LogOptions options, List<string> failures)
    {
        if (!options.Sampling.Enabled)
        {
            return;
        }

        if (options.Sampling.DebugSamplingPercentage is < 0 or > 100)
        {
            failures.Add("Sampling.DebugSamplingPercentage must be between 0 and 100 when sampling is enabled.");
        }
    }

    private static void ValidateLocalSink(LogOptions options, List<string> failures)
    {
        if (!options.Local.Enabled)
        {
            return;
        }

        ValidateTemplateTokens(options.Local.FileTemplate, "Local.FileTemplate", failures);

        if (options.Local.RetentionDays < 1)
        {
            failures.Add("Local.RetentionDays must be >= 1 when the local sink is enabled.");
        }

        if (options.Local.Rollover.MaxSizeMB < 1)
        {
            failures.Add("Local.Rollover.MaxSizeMB must be >= 1 when the local sink is enabled.");
        }

        ValidateCircuitBreaker("Local", options.Local.CircuitBreaker, failures);
    }

    private static void ValidateDatabaseSink(LogOptions options, List<string> failures)
    {
        if (!options.Database.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Database.ConnectionString))
        {
            failures.Add("Database.ConnectionString must be provided when the database sink is enabled.");
        }

        if (options.Database.BatchSize <= 0)
        {
            failures.Add("Database.BatchSize must be >= 1 when the database sink is enabled.");
        }

        if (options.Database.MaxConcurrency <= 0)
        {
            failures.Add("Database.MaxConcurrency must be >= 1 when the database sink is enabled.");
        }

        ValidateCircuitBreaker("Database", options.Database.CircuitBreaker, failures);
    }

    private static void ValidateFtpSink(LogOptions options, List<string> failures)
    {
        if (!options.Ftp.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Ftp.Host))
        {
            failures.Add("Ftp.Host must be provided when the FTP sink is enabled.");
        }

        if (options.Ftp.MaxConcurrentUploads <= 0)
        {
            failures.Add("Ftp.MaxConcurrentUploads must be >= 1 when the FTP sink is enabled.");
        }

        if (options.Ftp.UploadIntervalSec <= 0)
        {
            failures.Add("Ftp.UploadIntervalSec must be >= 1 when the FTP sink is enabled.");
        }

        if (options.Ftp.MaxBatchFiles <= 0)
        {
            failures.Add("Ftp.MaxBatchFiles must be >= 1 when the FTP sink is enabled.");
        }

        if (options.Ftp.MaxDelayMs < 0)
        {
            failures.Add("Ftp.MaxDelayMs must be >= 0 when the FTP sink is enabled.");
        }

        if (options.Ftp.FailureBackoffBaseSeconds < 1)
        {
            failures.Add("Ftp.FailureBackoffBaseSeconds must be >= 1 when the FTP sink is enabled.");
        }

        if (options.Ftp.FailureBackoffExponentCap < 0)
        {
            failures.Add("Ftp.FailureBackoffExponentCap must be >= 0 when the FTP sink is enabled.");
        }

        if (options.Ftp.FailureBackoffMaxSeconds < 0)
        {
            failures.Add("Ftp.FailureBackoffMaxSeconds must be >= 0 when the FTP sink is enabled.");
        }

        if (options.Ftp.FailureBackoffJitterFactor is < 0 or > 1)
        {
            failures.Add("Ftp.FailureBackoffJitterFactor must be between 0 and 1 when the FTP sink is enabled.");
        }

        ValidateTemplateTokens(options.Ftp.RemoteTemplate, "Ftp.RemoteTemplate", failures);
        ValidateCircuitBreaker("Ftp", options.Ftp.CircuitBreaker, failures);
    }

    private static void ValidateTemplateTokens(string? template, string contextName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return;
        }

        foreach (Match match in TemplateTokenRegex.Matches(template))
        {
            var token = match.Groups[1].Value;
            if (!AllowedTemplateTokens.Contains(token))
            {
                failures.Add($"{contextName} contains unsupported token '{token}'.");
            }
        }
    }

    private static void ValidateCircuitBreaker(string name, LogOptions.CircuitBreakerOptions circuitBreaker, List<string> failures)
    {
        if (circuitBreaker.Failures < 1)
        {
            failures.Add($"{name} circuit breaker Failures must be >= 1.");
        }

        if (circuitBreaker.BreakSec < 1)
        {
            failures.Add($"{name} circuit breaker BreakSec must be >= 1.");
        }
    }
}
