namespace Lib.Log.Pipeline;

using Lib.Log.Model;
using Lib.Log.Option;

public interface ILogSamplingStrategy
{
    bool ShouldSample(LogEntry entry, LogOptions options, LogOptions.PartitionGroupOptions partitionOptions);
}

