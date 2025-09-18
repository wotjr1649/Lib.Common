namespace Lib.Log.Sink;

using FluentFTP;
using Lib.Log.Model;
using Lib.Log.Option;
using Log.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed class FtpUpload(
    IOptionsMonitor<LogOptions> options,
    ILogger<FtpUpload> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private const string FailedExtension = ".uploadfailed";

    private readonly IOptionsMonitor<LogOptions> _options = options;
    private readonly ILogger<FtpUpload> _logger = logger;
    private readonly TimeProvider _timeProvider = timeProvider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.Ftp.Enabled)
        {
            _logger.LogInformation("FtpUploadService disabled.");
            return;
        }

        var opt = _options.CurrentValue;
        _logger.LogInformation("FtpUploadService started: {Host}:{Port}", opt.Ftp.Host, opt.Ftp.Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UploadCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FtpUploadService cycle failed.");
            }

            try
            {
                var sec = Math.Max(1, _options.CurrentValue.Ftp.UploadIntervalSec);
                await Task.Delay(TimeSpan.FromSeconds(sec), stoppingToken).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private async Task UploadCycleAsync(CancellationToken ct)
    {
        var opt = _options.CurrentValue;

        var baseDir = string.IsNullOrWhiteSpace(opt.Local.Directory)
            ? AppContext.BaseDirectory
            : opt.Local.Directory;
        Directory.CreateDirectory(baseDir);

        var nowUtc = _timeProvider.GetUtcNow();
        var candidates = Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories)
            .Where(path => IsEligible(path, nowUtc, opt))
            .Take(Math.Max(1, opt.Ftp.MaxBatchFiles))
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var sem = new SemaphoreSlim(Math.Max(1, opt.Ftp.MaxConcurrentUploads));
        var tasks = new List<Task>(candidates.Count);

        foreach (var path in candidates)
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            tasks.Add(Task.Run(async () =>
            {
                var attempt = _timeProvider.GetUtcNow();
                try
                {
                    var (success, error) = await UploadOneAsync(path, baseDir, opt, ct).ConfigureAwait(false);
                    if (success)
                    {
                        ClearFailureMarker(path);
                    }
                    else
                    {
                        ScheduleRetry(path, attempt, opt);
                        if (error is not null)
                        {
                            _logger.LogWarning(error, "FTP upload failed for {Path}", path);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    ScheduleRetry(path, attempt, opt);
                    _logger.LogWarning(ex, "FTP upload failed for {Path}", path);
                }
                finally
                {
                    sem.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private bool IsEligible(string path, DateTimeOffset nowUtc, LogOptions options)
    {
        if (!path.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        DateTimeOffset lastWriteUtc;
        try
        {
            lastWriteUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(path));
        }
        catch
        {
            return false;
        }

        if (nowUtc - lastWriteUtc <= TimeSpan.FromMinutes(2))
        {
            return false;
        }

        var marker = ReadFailureMetadata(path);
        return marker is null || marker.NextAttemptUtc <= nowUtc;
    }

    private static FailureMetadata? ReadFailureMetadata(string localPath)
    {
        var markerPath = GetFailureMarkerPath(localPath);
        if (!File.Exists(markerPath))
        {
            return null;
        }

        try
        {
            var text = File.ReadAllText(markerPath);
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var parts = text.Split('|');
            if (parts.Length != 2)
            {
                return null;
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                return null;
            }

            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                return null;
            }

            return new FailureMetadata(count, new DateTimeOffset(ticks, TimeSpan.Zero));
        }
        catch
        {
            return null;
        }
    }

    private static void ScheduleRetry(string localPath, DateTimeOffset attemptUtc, LogOptions options)
    {
        var markerPath = GetFailureMarkerPath(localPath);
        var metadata = ReadFailureMetadata(localPath) ?? new FailureMetadata(0, attemptUtc);
        var nextCount = metadata.Count + 1;
        var delay = ComputeBackoff(nextCount, options);
        var nextAttempt = attemptUtc + delay;

        var payload = string.Create(CultureInfo.InvariantCulture, $"{nextCount}|{nextAttempt.UtcTicks}");
        try
        {
            File.WriteAllText(markerPath, payload);
        }
        catch
        {
        }
    }

    private static void ClearFailureMarker(string localPath)
    {
        var markerPath = GetFailureMarkerPath(localPath);
        if (File.Exists(markerPath))
        {
            try
            {
                File.Delete(markerPath);
            }
            catch
            {
            }
        }
    }

    private static string GetFailureMarkerPath(string localPath)
        => localPath + FailedExtension;

    private static TimeSpan ComputeBackoff(int failureCount, LogOptions options)
    {
        var ftp = options.Ftp;
        var baseSeconds = Math.Max(1, ftp.FailureBackoffBaseSeconds);
        var exponent = Math.Min(failureCount, ftp.FailureBackoffExponentCap);
        var delaySeconds = baseSeconds * Math.Pow(2, exponent);

        if (ftp.FailureBackoffMaxSeconds > 0)
        {
            delaySeconds = Math.Min(delaySeconds, ftp.FailureBackoffMaxSeconds);
        }

        var jitterFactor = Math.Clamp(ftp.FailureBackoffJitterFactor, 0d, 1d);
        if (jitterFactor > 0)
        {
            var jitter = delaySeconds * jitterFactor;
            var min = Math.Max(baseSeconds, delaySeconds - jitter);
            var max = delaySeconds + jitter;
            delaySeconds = min + (Random.Shared.NextDouble() * (max - min));
        }

        delaySeconds = Math.Max(baseSeconds, delaySeconds);
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private static async Task<(bool Success, Exception? Error)> UploadOneAsync(string localPath, string baseDir, LogOptions opt, CancellationToken ct)
    {
        try
        {
            var fileNameNoExt = Path.GetFileNameWithoutExtension(localPath);
            string category = fileNameNoExt;
            string? device = null;

            var usIdx = fileNameNoExt.IndexOf('_');
            if (usIdx > 0)
            {
                category = fileNameNoExt[..usIdx];
                device = fileNameNoExt[usIdx..];
            }

            var rk = new RouteKey("Remote", category, device);

            string remotePath;

            if (string.IsNullOrWhiteSpace(opt.Ftp.RemoteTemplate))
            {
                var rel = Path.GetRelativePath(baseDir, localPath).Replace('\\', '/');
                remotePath = CombineRemote(opt.Ftp.RemoteDirectory, rel);
            }
            else
            {
                var tmpl = opt.Ftp.RemoteTemplate!;
                if (!tmpl.StartsWith("/", StringComparison.Ordinal) && !tmpl.Contains(':'))
                {
                    tmpl = CombineRemote(opt.Ftp.RemoteDirectory, tmpl);
                }

                var ts = File.GetLastWriteTime(localPath);
                remotePath = TemplateRenderer.RenderPath(string.Empty, tmpl, rk, ts, opt).Replace('\\', '/');
            }

            var remoteDir = remotePath.Contains('/') ? remotePath[..remotePath.LastIndexOf('/')] : "/";

            using var ftp = new AsyncFtpClient(opt.Ftp.Host, opt.Ftp.Username, opt.Ftp.Password, opt.Ftp.Port);
            ftp.Config.ValidateAnyCertificate = !opt.Ftp.ValidateCert;
            await ftp.Connect(ct).ConfigureAwait(false);

            if (!await ftp.DirectoryExists(remoteDir, ct).ConfigureAwait(false))
            {
                await ftp.CreateDirectory(remoteDir, ct).ConfigureAwait(false);
            }

            var target = opt.Ftp.AtomicUpload ? remotePath + ".uploading" : remotePath;
            var status = await ftp.UploadFile(localPath, target,
                FtpRemoteExists.Overwrite, createRemoteDir: true, token: ct).ConfigureAwait(false);

            if (status is FtpStatus.Success && opt.Ftp.AtomicUpload)
            {
                await ftp.Rename(target, remotePath, ct).ConfigureAwait(false);
            }

            await ftp.Disconnect(ct).ConfigureAwait(false);

            if (status is FtpStatus.Success)
            {
                if (opt.Ftp.DeleteLocalAfterSuccess)
                {
                    try
                    {
                        File.Delete(localPath);
                    }
                    catch
                    {
                    }
                }

                return (true, null);
            }

            return (false, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (false, ex);
        }
    }

    private static string CombineRemote(string root, string tail)
    {
        root ??= "/";
        tail ??= string.Empty;
        var left = root.Replace('\\', '/').TrimEnd('/');
        var right = tail.Replace('\\', '/').TrimStart('/');
        var joined = string.IsNullOrEmpty(left) ? "/" + right : left + "/" + right;
        return string.IsNullOrEmpty(joined) ? "/" : joined;
    }

    private sealed record FailureMetadata(int Count, DateTimeOffset NextAttemptUtc);
}

