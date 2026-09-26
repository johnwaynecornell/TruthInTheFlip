using System.Globalization;
using FluentCommandLine;
using JWCFarm;
using TruthInTheFlip.Farm.Format;
using TruthInTheFlip.Format;
using Xunit;

namespace TruthInTheFlip.Farm.Tests;

public sealed class SamePersistenceAlgorithmicNullTests
{
    private static string CreateTestTrackerFile(
        int recordCount = 10,
        long stepTotal = 200_000_000L,
        double historicalSameRate = 0.52,
        double historicalHeadsRate = 0.51)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_SamePersistenceAlgNull_Test_{Guid.NewGuid():N}.tkr");

        TrackerStore store = TrackerStore.Default(path);
        Tracker tracker = (Tracker)store.LoadOrCreate(true);

        DateTimeOffset begin = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

        for (int i = 1; i <= recordCount; i++)
        {
            tracker.total = i * stepTotal;
            tracker.heads = (long)(tracker.total * historicalHeadsRate);
            tracker.tails = tracker.total - tracker.heads;
            tracker.betHeads = (long)(tracker.total * 0.53);
            tracker.anticipated = (long)(tracker.total * 0.505);
            tracker.baseAnticipated = tracker.anticipated;
            tracker.anticipatedHeads = (long)(tracker.anticipated * 0.52);
            tracker.anticipatedTails = tracker.anticipated - tracker.anticipatedHeads;
            tracker.betSame = tracker.total;
            tracker.anticipatedSame = (long)(tracker.total * historicalSameRate);

            tracker.wallclockTimeNs = TimeSpan.FromMinutes(i * 10).Ticks * 100;
            tracker.utcBeginTimeMs = begin.AddMinutes((i - 1) * 10).ToUnixTimeMilliseconds();
            tracker.utcEndTimeMs = begin.AddMinutes(i * 10).ToUnixTimeMilliseconds();
            tracker.batchTotal = stepTotal;

            store.Save(tracker, true);
        }

        return path;
    }

    [Fact]
    public void SameSeed_ProducesIdenticalAlgorithmicTrial()
    {
        string path = CreateTestTrackerFile(recordCount: 8, stepTotal: 200_000_000L);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new SamePersistenceAlgorithmicNullSpec(source, strategyWindowFlips: 1_000_000_000L);

            ulong seed = 20260925UL;

            var stream1 = spec.CreateSyntheticStream(seed);
            var records1 = stream1.Records.Cast<Tracker>().ToList();

            var stream2 = spec.CreateSyntheticStream(seed);
            var records2 = stream2.Records.Cast<Tracker>().ToList();

            Assert.Equal(records1.Count, records2.Count);
            Assert.True(records1.Count > 0);

            for (int i = 0; i < records1.Count; i++)
            {
                var r1 = records1[i];
                var r2 = records2[i];

                Assert.Equal(r1.total, r2.total);
                Assert.Equal(r1.heads, r2.heads);
                Assert.Equal(r1.tails, r2.tails);
                Assert.Equal(r1.anticipated, r2.anticipated);
                Assert.Equal(r1.anticipatedHeads, r2.anticipatedHeads);
                Assert.Equal(r1.anticipatedTails, r2.anticipatedTails);
                Assert.Equal(r1.betHeads, r2.betHeads);
                Assert.Equal(r1.betSame, r2.betSame);
                Assert.Equal(r1.anticipatedSame, r2.anticipatedSame);
                Assert.Equal(r1.same, r2.same);
                Assert.Equal(r1.diff, r2.diff);
                Assert.Equal(r1.utcEndTimeMs, r2.utcEndTimeMs);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void DifferentSeed_ChangesSyntheticPath()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 200_000_000L);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new SamePersistenceAlgorithmicNullSpec(source, strategyWindowFlips: 1_000_000_000L);

            var records1 = spec.CreateSyntheticStream(11111UL).Records.Cast<Tracker>().ToList();
            var records2 = spec.CreateSyntheticStream(99999UL).Records.Cast<Tracker>().ToList();

            Assert.Equal(records1.Count, records2.Count);

            bool hasDifference = false;
            for (int i = 0; i < records1.Count; i++)
            {
                var r1 = records1[i];
                var r2 = records2[i];

                if (r1.heads != r2.heads || r1.anticipated != r2.anticipated || r1.same != r2.same)
                {
                    hasDifference = true;
                    break;
                }
            }

            Assert.True(hasDifference, "Different seeds should produce different synthetic trial paths.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HistoricalOutcomesAndDecisions_AreNotCopied()
    {
        // Historical tracker has high Same rate (0.52) and Heads rate (0.51)
        string path = CreateTestTrackerFile(recordCount: 20, stepTotal: 200_000_000L, historicalSameRate: 0.52, historicalHeadsRate: 0.51);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            using var histStream = source.Source();
            var histRecords = histStream.Records.Cast<Tracker>().ToList();

            var spec = new SamePersistenceAlgorithmicNullSpec(source, strategyWindowFlips: 1_000_000_000L);
            var syntheticRecords = spec.CreateSyntheticStream(12345UL).Records.Cast<Tracker>().ToList();

            Assert.Equal(histRecords.Count, syntheticRecords.Count);

            for (int i = 0; i < syntheticRecords.Count; i++)
            {
                var h = histRecords[i];
                var s = syntheticRecords[i];

                // Historical values should NOT match synthetic values
                Assert.NotEqual(h.heads, s.heads);
                Assert.NotEqual(h.tails, s.tails);
                Assert.NotEqual(h.anticipated, s.anticipated);
                Assert.NotEqual(h.anticipatedSame, s.anticipatedSame);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HistoricalDeltaTotalCadence_IsPreservedExactly()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"TruthInTheFlip_SamePersistenceCadence_Test_{Guid.NewGuid():N}.tkr");

        try
        {
            TrackerStore store = TrackerStore.Default(path);
            Tracker tracker = (Tracker)store.LoadOrCreate(true);

            long[] stepDeltas = [150_000_000L, 200_000_000L, 75_000_000L, 300_000_000L, 10_000_000L];
            long runningTotal = 0;

            for (int i = 0; i < stepDeltas.Length; i++)
            {
                runningTotal += stepDeltas[i];
                tracker.total = runningTotal;
                tracker.heads = runningTotal / 2;
                tracker.tails = runningTotal - tracker.heads;
                tracker.batchTotal = stepDeltas[i];
                tracker.wallclockTimeNs = (i + 1) * 1000L;
                tracker.utcBeginTimeMs = i * 1000L;
                tracker.utcEndTimeMs = (i + 1) * 1000L;
                tracker.cumulativeTicks = (i + 1) * 500L;
                tracker.IsComplete = true;

                store.Save(tracker, true);
            }

            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new SamePersistenceAlgorithmicNullSpec(source, strategyWindowFlips: 500_000_000L);
            var syntheticRecords = spec.CreateSyntheticStream(42UL).Records.Cast<Tracker>().ToList();

            Assert.Equal(stepDeltas.Length, syntheticRecords.Count);

            long prevSyntheticTotal = 0;
            for (int i = 0; i < syntheticRecords.Count; i++)
            {
                var s = syntheticRecords[i];
                long syntheticDelta = s.total - prevSyntheticTotal;
                Assert.Equal(stepDeltas[i], syntheticDelta);
                Assert.Equal(stepDeltas[i], s.batchTotal);
                Assert.Equal((i + 1) * 1000L, s.wallclockTimeNs);
                Assert.Equal(i * 1000L, s.utcBeginTimeMs);
                Assert.Equal((i + 1) * 1000L, s.utcEndTimeMs);
                Assert.Equal((i + 1) * 500L, s.cumulativeTicks);
                Assert.True(s.IsComplete);

                prevSyntheticTotal = s.total;
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TrackerFieldInvariants_HoldAcrossAllSnapshots()
    {
        string path = CreateTestTrackerFile(recordCount: 25, stepTotal: 200_000_000L);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new SamePersistenceAlgorithmicNullSpec(source, strategyWindowFlips: 1_000_000_000L);

            var records = spec.CreateSyntheticStream(20260925UL).Records.Cast<Tracker>().ToList();

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];

                // 1. total == heads + tails
                Assert.Equal(r.total, r.heads + r.tails);

                // 2. same + diff == total
                Assert.Equal(r.total, r.same + r.diff);

                // 3. anticipated <= total
                Assert.True(r.anticipated <= r.total);

                // 4. anticipatedHeads + anticipatedTails == anticipated
                Assert.Equal(r.anticipated, r.anticipatedHeads + r.anticipatedTails);

                // 5. anticipatedSame <= betSame
                Assert.True(r.anticipatedSame <= r.betSame);

                // 6. betHeads <= total
                Assert.True(r.betHeads <= r.total);

                // 7. baseAnticipated == anticipated
                Assert.Equal(r.anticipated, r.baseAnticipated);

                // 8. Non-negative counts
                Assert.True(r.heads >= 0);
                Assert.True(r.tails >= 0);
                Assert.True(r.anticipated >= 0);
                Assert.True(r.anticipatedHeads >= 0);
                Assert.True(r.anticipatedTails >= 0);
                Assert.True(r.betHeads >= 0);
                Assert.True(r.betSame >= 0);
                Assert.True(r.anticipatedSame >= 0);
                Assert.True(r.same >= 0);
                Assert.True(r.diff >= 0);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PredictSame_WarmupAndDecisionLag_BehaviorIsCorrect()
    {
        // 10 snapshots of 200M = 2B flips total.
        // Strategy window = 1B (5 snapshots).
        string path = CreateTestTrackerFile(recordCount: 15, stepTotal: 200_000_000L);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            long strategyWindowFlips = 1_000_000_000L; // 5 snapshots
            var spec = new SamePersistenceAlgorithmicNullSpec(source, strategyWindowFlips);

            var records = spec.CreateSyntheticStream(88888UL).Records.Cast<Tracker>().ToList();

            long prevBetSame = 0;
            long prevTotal = 0;

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                long deltaBetSame = r.betSame - prevBetSame;
                long deltaTotal = r.total - prevTotal;

                // During warmup (i < 5, total <= 1B), predictSame must be true => deltaBetSame == deltaTotal
                if (r.total <= strategyWindowFlips)
                {
                    Assert.Equal(deltaTotal, deltaBetSame);
                }
                else
                {
                    // Either predictSame (deltaBetSame == deltaTotal) or predictDiff (deltaBetSame == 0)
                    Assert.True(deltaBetSame == deltaTotal || deltaBetSame == 0);
                }

                prevBetSame = r.betSame;
                prevTotal = r.total;
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StrategyWindow_IsIndependentFromDownstreamReportWindow()
    {
        string path = CreateTestTrackerFile(recordCount: 20, stepTotal: 200_000_000L);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new SamePersistenceAlgorithmicNullSpec(source, strategyWindowFlips: 600_000_000L); // 3 snapshots window

            // Downstream windowing at 1B
            var syntheticSelector = spec.CreateSyntheticSelector(12345UL);
            var downstreamWindowed = TrackerWindows.Window(
                TrackerWindows.TrackerWindow.ByTotal(new Count(1_000_000_000L)),
                syntheticSelector);

            using var stream = downstreamWindowed.Source();
            var windowedRecords = stream.Records.Cast<Tracker>().ToList();

            Assert.True(windowedRecords.Count > 0);
            foreach (var r in windowedRecords)
            {
                Assert.Equal(r.total, r.heads + r.tails);
                Assert.Equal(r.total, r.same + r.diff);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AlgorithmicNullSpec_UsesOnlyExperimentGeometry()
    {
        string path = CreateTestTrackerFile(recordCount: 12, stepTotal: 200_000_000L);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            var spec = new SamePersistenceAlgorithmicNullSpec(source, strategyWindowFlips: 1_000_000_000L);

            INullSchedule schedule = spec.MaterializeSchedule();
            Assert.IsType<SamePersistenceAlgorithmicNullSchedule>(schedule);

            var algSchedule = (SamePersistenceAlgorithmicNullSchedule)schedule;
            Assert.Equal(12, algSchedule.StepCount);
            Assert.Equal(1_000_000_000L, algSchedule.StrategyWindowFlips);

            for (int i = 0; i < algSchedule.StepCount; i++)
            {
                SamePersistenceAlgorithmicNullStep step = algSchedule.Steps[i];
                Assert.Equal((i + 1) * 200_000_000L, step.Total);
                Assert.Equal(200_000_000L, step.DeltaTotal);
                Assert.True(step.IsComplete);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Schedule_PreservesSamePersistenceStrategyWarmupAndDecisionLag()
    {
        // Custom deterministic mock sampler:
        // First batch: force S = 120M (> 50%) -> predictSame stays true
        // Second batch: force S = 80M (< 50%) -> window average drops < 50%
        // Third batch: should see predictDiff take effect
        var customSampler = new MockBinomialSampler(
            samples: [
                120_000_000L, // D = 120M, S = 80M (< 50%)
                130_000_000L, // D = 130M, S = 70M (< 50%)
                100_000_000L  // D = 100M, S = 100M
            ]
        );

        string path = CreateTestTrackerFile(recordCount: 3, stepTotal: 200_000_000L);
        try
        {
            var source = TruthInTheFlip_Fluent.Tracker(path);
            // Window of 100M so warmup finishes after 1st snapshot (200M > 100M)
            var spec = new SamePersistenceAlgorithmicNullSpec(source, strategyWindowFlips: 100_000_000L)
            {
                SamplerFactory = _ => customSampler
            };

            var records = spec.CreateSyntheticStream(1UL).Records.Cast<Tracker>().ToList();

            Assert.Equal(3, records.Count);

            // Batch 1 (Warmup): predictSame = true
            // S = 80M, D = 120M
            Assert.Equal(200_000_000L, records[0].total);
            Assert.Equal(200_000_000L, records[0].betSame);
            Assert.Equal(80_000_000L, records[0].anticipated);
            Assert.Equal(80_000_000L, records[0].anticipatedSame);

            // After Batch 1, window (size 200M) has SamePercentage = 80M / 200M = 40% < 50%
            // So Batch 2 should predict Different!
            // Batch 2: D = 130M, S = 70M
            long deltaBetSame2 = records[1].betSame - records[0].betSame;
            long deltaAnticipated2 = records[1].anticipated - records[0].anticipated;
            long deltaAnticipatedSame2 = records[1].anticipatedSame - records[0].anticipatedSame;

            Assert.Equal(0L, deltaBetSame2);              // Predicted Different => betSame += 0
            Assert.Equal(130_000_000L, deltaAnticipated2); // Predicted Different => anticipated += D (130M)
            Assert.Equal(0L, deltaAnticipatedSame2);       // Predicted Different => anticipatedSame += 0
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StableFarmEnvironment_DoesNotExposeAlgorithmic()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 200_000_000L);
        try
        {
            var env = new FluentEnvironment();
            env.AddModule<TruthInTheFlip_Fluent>();
            env.ServeTypes = new[] { typeof(FarmCommand) };

            List<string> args = new()
            {
                "null_trial",
                "20260925",
                "same_persistence_algorithmic",
                "file",
                path,
                "10B",
                "by_total",
                "10B",
                "by_total",
                "10B"
            };

            int cursor = 0;
            var res = env.ParseOne(args, ref cursor);

            Assert.Null(res);
            Assert.Equal(0, cursor);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExperimentalFarmEnvironment_ExposesAlgorithmic()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 200_000_000L);
        try
        {
            var env = new FluentEnvironment();
            env.AddModule<TruthInTheFlip_Fluent>();
            env.AddModule<SamePersistenceAlgorithmicNullSpec>();
            env.AddModule<NullTrialCommand>();
            env.ServeTypes = new[] { typeof(FarmCommand) };

            List<string> args = new()
            {
                "null_trial",
                "20260925",
                "same_persistence_algorithmic",
                "file",
                path,
                "10B",
                "by_total",
                "10B",
                "by_total",
                "10B"
            };

            int cursor = 0;
            var res = env.ParseOne(args, ref cursor);

            Assert.NotNull(res);
            Assert.IsAssignableFrom<FarmCommand>(res.Result);
            Assert.Equal(args.Count, cursor);

            var cmd = (FarmCommand)res.Result!;
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            using var errorOutput = new StringWriter(CultureInfo.InvariantCulture);
            var ctx = new FarmContext { Output = output, ErrorOutput = errorOutput };

            cmd.Execute(ctx);

            string outStr = output.ToString();
            Assert.Contains("=== Null Trial ===", outStr);
            Assert.Contains("Seed                  : 20260925", outStr);
            Assert.Contains("Edge Excursion Score", outStr);
            Assert.Contains("Edge Settlement Score", outStr);
            Assert.Contains("Edge Persistence Index", outStr);
            Assert.Contains("avgMeanA", outStr);
            Assert.Contains("avgMeanZHeads", outStr);
            Assert.Contains("Retained Anticipation", outStr);
            Assert.Contains("Settlement Adjusted A", outStr);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SamePersistenceAlgorithmic_ThrowsFarmInputException_WhenSourceNotAccumulated()
    {
        string path = CreateTestTrackerFile(recordCount: 10, stepTotal: 200_000_000L);
        try
        {
            var rawSource = TruthInTheFlip_Fluent.Tracker(path);
            var windowedSource = TrackerWindows.Window(
                TrackerWindows.TrackerWindow.ByTotal(new Count(500_000_000L)),
                rawSource);

            Assert.False(windowedSource.IsAccumulated);

            FarmInputException ex = Assert.Throws<FarmInputException>(() =>
            {
                SamePersistenceAlgorithmicNullSpec.SamePersistenceAlgorithmic(windowedSource, new Count(10_000_000_000L));
            });

            Assert.Contains("accumulated historical tracker source", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Small-scale reference test:
    /// For small M (M = 8, 10, 12), enumerates all 2^M binary paths for fixed F0,
    /// and verifies exact combinatorial distributions and contingency cell identities.
    /// </summary>
    [Theory]
    [InlineData(6, false)]
    [InlineData(6, true)]
    [InlineData(8, false)]
    [InlineData(8, true)]
    [InlineData(10, false)]
    [InlineData(10, true)]
    [InlineData(12, false)]
    [InlineData(12, true)]
    public void SmallScaleCombinatorialReference_MatchesExhaustiveEnumeration(int M, bool f0Bit)
    {
        int totalPaths = 1 << M;
        int f0 = f0Bit ? 1 : 0;

        // Collect statistics by D = number of transitions
        var countByD = new int[M + 1];
        var headsSumByD = new long[M + 1];
        var headsSqSumByD = new long[M + 1];

        for (int mask = 0; mask < totalPaths; mask++)
        {
            int prior = f0;
            int transitions = 0;
            int heads = 0;
            int n00 = 0, n01 = 0, n10 = 0, n11 = 0;

            for (int bitIdx = 0; bitIdx < M; bitIdx++)
            {
                int currentBit = (mask >> (M - 1 - bitIdx)) & 1;
                if (currentBit == 1) heads++;

                if (prior == 0 && currentBit == 0) n00++;
                else if (prior == 0 && currentBit == 1) n01++;
                else if (prior == 1 && currentBit == 0) n10++;
                else if (prior == 1 && currentBit == 1) n11++;

                if (currentBit != prior) transitions++;
                prior = currentBit;
            }

            int fm = prior;
            int D = transitions;
            int S = M - D;

            // Verify contingency identities on EVERY path:
            Assert.Equal(n00 + n01 + n10 + n11, M);
            Assert.Equal(n01 + n10, D);
            Assert.Equal(n00 + n11, S);
            Assert.Equal(n01 + n11, heads);
            Assert.Equal(n00 + n10, M - heads);

            int expectedN01 = (D + fm - f0) / 2;
            int expectedN10 = (D - fm + f0) / 2;
            Assert.Equal(expectedN01, n01);
            Assert.Equal(expectedN10, n10);

            // fm == f0 ^ (D % 2)
            Assert.Equal(fm, f0 ^ (D % 2));

            countByD[D]++;
            headsSumByD[D] += heads;
            headsSqSumByD[D] += (long)heads * heads;
        }

        // Verify Binomial(M, 0.5) marginal distribution of D
        for (int D = 0; D <= M; D++)
        {
            long binomCoeff = BinomialCoefficient(M, D);
            Assert.Equal(binomCoeff, countByD[D]);

            if (countByD[D] > 0)
            {
                int S = M - D;
                long k = f0 == 1 ? (D / 2) + 1 : (D + 1) / 2;
                long n = D + 1;

                // Combinatorial formula for E[X | D, F0]
                double expectedX = (double)S * k / n;
                int fm = f0 ^ (D % 2);
                int n01 = (D + fm - f0) / 2;
                double expectedH = n01 + expectedX;

                double empiricalMeanH = (double)headsSumByD[D] / countByD[D];
                Assert.True(Math.Abs(empiricalMeanH - expectedH) < 1e-9,
                    $"E[H | D={D}] mismatch: expected {expectedH}, got {empiricalMeanH}");

                // Combinatorial formula for Var(X | D, F0)
                double expectedVarX = (double)S * k * (n - k) / ((double)n * n) * ((double)(M + 1) / (D + 2));
                double empiricalVarH = ((double)headsSqSumByD[D] / countByD[D]) - (empiricalMeanH * empiricalMeanH);

                Assert.True(Math.Abs(empiricalVarH - expectedVarX) < 1e-9,
                    $"Var(H | D={D}] mismatch: expected {expectedVarX}, got {empiricalVarH}");
            }
        }
    }

    [Fact]
    public void LargeM_ConditionalGaussianSampler_MomentSanityTest()
    {
        long M = 200_000_000L;
        ulong seed = 987654321UL;
        var sampler = new FastBinomialSampler(seed);

        int sampleCount = 5000;
        double sumNormalizedZ = 0;
        double sumSqNormalizedZ = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            long D = sampler.Sample(M, 0.5);
            long S = M - D;
            bool f0Bit = (i % 2 == 1);
            int f0 = f0Bit ? 1 : 0;

            long k = f0 == 1 ? (D / 2) + 1 : (D + 1) / 2;
            long n = D + 1;

            double muX = (double)S * k / n;
            double varX = (double)S * k * (n - k) / ((double)n * n) * ((double)(M + 1) / (D + 2));
            double sigmaX = Math.Sqrt(Math.Max(0.0, varX));

            double z = sampler.NextGaussian();
            long X = Math.Clamp((long)Math.Round(muX + sigmaX * z), 0L, S);

            double normalizedZ = (X - muX) / sigmaX;
            sumNormalizedZ += normalizedZ;
            sumSqNormalizedZ += normalizedZ * normalizedZ;
        }

        double sampleMeanZ = sumNormalizedZ / sampleCount;
        double sampleVarZ = (sumSqNormalizedZ / sampleCount) - (sampleMeanZ * sampleMeanZ);

        // Standard error for mean with N=5000 is 1/sqrt(5000) ~ 0.014
        Assert.True(Math.Abs(sampleMeanZ) < 0.08, $"Sample mean of normalized Z {sampleMeanZ} deviates from 0.");
        Assert.True(Math.Abs(sampleVarZ - 1.0) < 0.10, $"Sample variance of normalized Z {sampleVarZ} deviates from 1.");
    }

    private static long BinomialCoefficient(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        if (k == 0 || k == n) return 1;
        if (k > n / 2) k = n - k;

        long result = 1;
        for (int i = 1; i <= k; i++)
        {
            result = result * (n - i + 1) / i;
        }
        return result;
    }

    private sealed class MockBinomialSampler : IBinomialSampler
    {
        private readonly long[] _samples;
        private int _index;

        public MockBinomialSampler(long[] samples)
        {
            _samples = samples;
        }

        public long Sample(long n, double p = 0.5)
        {
            if (_index < _samples.Length)
            {
                return _samples[_index++];
            }
            return (long)(n * p);
        }

        public double NextGaussian() => 0.0;
    }
}
