namespace Lib.Log.Pipeline;

using Lib.Log.Model;
using Lib.Log.Option;
using Microsoft.Extensions.Logging;


internal sealed class RandomSamplingStrategy : ILogSamplingStrategy
{
    public bool ShouldSample(LogEntry entry, LogOptions options, LogOptions.PartitionGroupOptions partitionOptions)
    {
        if (!options.Sampling.Enabled)
        {
            return false;
        }

        if (entry.Level > LogLevel.Debug)
        {
            return false;
        }

        var percentage = Math.Clamp(options.Sampling.DebugSamplingPercentage, 0, 100);
        if (percentage <= 0)
        {
            return false;
        }

        if (percentage >= 100)
        {
            return true;
        }

        return Random.Shared.Next(0, 100) < percentage;
    }
}
