using System;
using System.Text.Json;
using System.Text;
using Xunit;

namespace ConfusedPolarBear.Plugin.IntroSkipper.Tests;

public class TestStatistics
{
    [Fact]
    public void TestTSISerialization()
    {
        var expected = "\"TotalAnalyzedEpisodes\":42,";

        var stats = new AnalysisStatistics();
        stats.TotalAnalyzedEpisodes.Add(42);

        var actual = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(stats));

        Assert.Contains(expected, actual, StringComparison.OrdinalIgnoreCase);
    }
}
