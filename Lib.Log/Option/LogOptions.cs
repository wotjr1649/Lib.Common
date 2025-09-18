namespace Lib.Log.Option;

using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

public sealed class LogOptions
{
    public bool MaskSecrets { get; set; } = true;

    public FormattingOptions Formatting { get; set; } = new();
    public RoutingOptions Routing { get; set; } = new();
    public Dictionary<string, PartitionGroupOptions> Partitions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public RootingOptions Rooting { get; set; } = new();
    public SamplingOptions Sampling { get; set; } = new();

    public LocalSinkOptions Local { get; set; } = new();
    public DbSinkOptions Database { get; set; } = new();
    public FtpSinkOptions Ftp { get; set; } = new();

    public sealed class FormattingOptions
    {
        public bool Text { get; set; } = true;
        public bool Json { get; set; } = false;
        public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";
        public bool UseUtcTimestamp { get; set; } = false;
        public int MaxMessageLength { get; set; } = 64 * 1024;
    }

    public sealed class RoutingOptions
    {
        public Dictionary<string, string[]> CategoryGroups { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Default"] = new[] { "*" }
        };

        public string DeviceKeyField { get; set; } = "DeviceId";
    }

    public sealed class PartitionGroupOptions
    {
        public int Shards { get; set; } = 4;
        public int QueueCapacity { get; set; } = 256;
        public int BatchSize { get; set; } = 128; // tuned: 파일/DB 공통 권장 배치(64~256 중간값)
        public int FlushIntervalMs { get; set; } = 150; // tuned: 지연/IO 타협(100~300ms 권장)
        public BackpressurePolicy Backpressure { get; set; } = BackpressurePolicy.Block;
    }

    public enum BackpressurePolicy { Block, DropLatest, Sample1Percent }

    public sealed class RolloverOptions
    {
        public RolloverType Type { get; set; } = RolloverType.Size; // tuned: 용량 기반 회전 기본값
        public long MaxSizeMB { get; set; } = 64; // tuned: 64MB 기본(환경에 따라 128MB 권장)
    }

    public enum RolloverType { Date, Size, Both }

    public sealed class LocalSinkOptions
    {
        public bool Enabled { get; set; } = true;
        public string Directory { get; set; } = string.Empty;
        public string FileTemplate { get; set; } = "{Root}/{yyyy}-{MM}-{dd}/{Category}{DeviceId?}.log"; // tuned: 날짜별 폴더 + 카테고리/디바이스 분리
        public RolloverOptions Rollover { get; set; } = new();
        public int RetentionDays { get; set; } = 14;
        public bool CompressOnRoll { get; set; } = false;
        public long MinFreeSpaceMB { get; set; } = 512; // tuned: 최소 여유공간 512MB
        public bool FlushOnError { get; set; } = true;
        public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
    }

    public sealed class DbSinkOptions
    {
        public bool Enabled { get; set; } = false;
        public string? ConnectionStringName { get; set; }

        [Required]
        public string ConnectionString { get; set; } = string.Empty;

        public string TableName { get; set; } = "IF_LOG";
        public bool AutoCreateTable { get; set; } = true;
        public int BatchSize { get; set; } = 500;
        public int MaxConcurrency { get; set; } = 2;
        public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
    }

    public sealed class SamplingOptions
    {
        public bool Enabled { get; set; } = true;
        public int DebugSamplingPercentage { get; set; } = 99;
    }

    public sealed class FtpSinkOptions
    {
        public bool Enabled { get; set; } = false;

        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 21;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        public string RemoteDirectory { get; set; } = "/";
        public string? RemoteTemplate { get; set; } = "{Project}/{yyyy}/{MM}/{dd}/{Category}{DeviceId?}.log";
        public bool ValidateCert { get; set; } = true;
        public bool AtomicUpload { get; set; } = true;
        public bool DeleteLocalAfterSuccess { get; set; } = false;
        public int MaxConcurrentUploads { get; set; } = 2;
        public int UploadIntervalSec { get; set; } = 5;
        public int MaxBatchFiles { get; set; } = 200;
        public int MaxDelayMs { get; set; } = 15_000;
        public int FailureBackoffBaseSeconds { get; set; } = 5;
        public int FailureBackoffExponentCap { get; set; } = 8;
        public int FailureBackoffMaxSeconds { get; set; } = 600;
        public double FailureBackoffJitterFactor { get; set; } = 0.25;
        public CircuitBreakerOptions CircuitBreaker { get; set; } = new();
    }

    public sealed class CircuitBreakerOptions
    {
        public int Failures { get; set; } = 5;
        public int BreakSec { get; set; } = 30;
    }

    public sealed class RootingOptions
    {
        public string DefaultRoot { get; set; } = "log";
        public bool AllowScopeOverride { get; set; } = true;
        public List<RootRule> Rules { get; set; } = new()
        {
            new RootRule
            {
                Root = "Error", // default: 에러는 Error 루트\r\n                MinLevel = LogLevel.Error
            },
            new RootRule
            {
                Root = "Result",
                Categories = new[] { "result", "Result", "job.result", "ui.result" }
            },
            new RootRule
            {
                Root = "Log", // default: 그 외 일반 로그\r\n                Categories = new[] { "Application", "Db", "Jb", "*" }
            }
        };
    }

    public sealed class RootRule
    {
        public string Root { get; set; } = "log";
        public LogLevel? MinLevel { get; set; }
        public LogLevel? MaxLevel { get; set; }
        public string[]? Categories { get; set; }
        public string[]? CategoryStartsWith { get; set; }
        public string[]? DeviceIdPatterns { get; set; }
        public Dictionary<string, string>? ScopeEquals { get; set; }
    }
}

