namespace ConfusedPolarBear.Plugin.IntroSkipper.Tests;

using System;
using System.Collections.Generic;
using Xunit;

public class TestBlackFrames
{
    [FactSkipFFmpegTests]
    public void TestBlackFrameDetection()
    {
        var expected = new List<BlackFrame>();
        expected.AddRange(CreateFrameSequence(2, 3));
        expected.AddRange(CreateFrameSequence(5, 6));
        expected.AddRange(CreateFrameSequence(8, 9.96));

        var actual = FFmpegWrapper.DetectBlackFrames(
            queueFile("rainbow.mp4"),
            new TimeRange(0, 10)
        );

        for (var i = 0; i < expected.Count; i++)
        {
            var (e, a) = (expected[i], actual[i]);
            Assert.Equal(e.Percentage, a.Percentage);
            Assert.True(Math.Abs(e.Time - a.Time) <= 0.005);
        }
    }

    private QueuedEpisode queueFile(string path)
    {
        return new()
        {
            EpisodeId = Guid.NewGuid(),
            Path = "../../../video/" + path
        };
    }

    private BlackFrame[] CreateFrameSequence(double start, double end)
    {
        var frames = new List<BlackFrame>();

        for (var i = start; i < end; i += 0.04)
        {
            frames.Add(new(100, i));
        }

        return frames.ToArray();
    }
}
