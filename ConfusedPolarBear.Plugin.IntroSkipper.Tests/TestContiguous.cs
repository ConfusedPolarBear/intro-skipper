using Xunit;

namespace ConfusedPolarBear.Plugin.IntroSkipper.Tests;

public class TestTimeRanges
{
    [Fact]
    public void TestSmallRange()
    {
        var times = new double[]{
            1, 1.5, 2, 2.5, 3, 3.5, 4,
            100, 100.5, 101, 101.5
        };

        var expected = new TimeRange(1, 4);
        var actual = TimeRangeHelpers.FindContiguous(times, 3.25);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TestLargeRange()
    {
        var times = new double[]{
            1, 1.5, 2,
            2.8, 2.9, 2.995, 3.0, 3.01, 3.02, 3.4, 3.45, 3.48, 3.7, 3.77, 3.78, 3.781, 3.782, 3.789, 3.85,
            4.5, 5.3122, 5.3123, 5.3124, 5.3125, 5.3126, 5.3127, 5.3128,
            55, 55.5, 55.6, 55.7
        };

        var expected = new TimeRange(1, 5.3128);
        var actual = TimeRangeHelpers.FindContiguous(times, 3.25);

        Assert.Equal(expected, actual);
    }
}
