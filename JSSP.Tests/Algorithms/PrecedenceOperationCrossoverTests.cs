using JSSP.Algorithms.Genetic;

namespace JSSP.Tests.Algorithms;

[TestClass]
public class PrecedenceOperationCrossoverTests
{
    // Shared fixture: 2 jobs × 2 ops each → 4 genes total.
    private static readonly IReadOnlyList<int> JobIds = [0, 1];
    private static readonly Chromosome Parent1 = Chromosome.FromArray(new[] { 0, 1, 0, 1 });
    private static readonly Chromosome Parent2 = Chromosome.FromArray(new[] { 1, 0, 1, 0 });

    [TestMethod]
    public void Cross_ChildLength_EqualsParentLength()
    {
        // Arrange
        var rng = new Random(0);

        // Act
        var child = PrecedenceOperationCrossover.Cross(Parent1, Parent2, JobIds, rng);

        // Assert
        Assert.AreEqual(Parent1.Length, child.Length);
    }

    [TestMethod]
    public void Cross_JobCountsInChild_MatchParents()
    {
        // Arrange — run many times to exercise different random partitions.
        var rng = new Random(7);

        for (int i = 0; i < 100; i++)
        {
            // Act
            var child = PrecedenceOperationCrossover.Cross(Parent1, Parent2, JobIds, rng);
            var genes = child.ToArray();

            // Assert: each job must appear exactly twice (2 ops per job).
            Assert.AreEqual(2, genes.Count(g => g == 0),
                $"Job 0 count mismatch on iteration {i}");
            Assert.AreEqual(2, genes.Count(g => g == 1),
                $"Job 1 count mismatch on iteration {i}");
        }
    }

    [TestMethod]
    public void Cross_SingleJobList_ReturnsParent1Unchanged()
    {
        // Arrange: only one job — POX cannot partition; should return parent1.
        var singleJobIds = new List<int> { 0 };
        var p1 = Chromosome.FromArray(new[] { 0, 0, 0 });
        var p2 = Chromosome.FromArray(new[] { 0, 0, 0 });

        // Act
        var child = PrecedenceOperationCrossover.Cross(p1, p2, singleJobIds, new Random(0));

        // Assert: child == parent1 (same hash key).
        Assert.AreEqual(p1.HashKey, child.HashKey);
    }

    [TestMethod]
    public void Cross_ThreeJobs_ChildPreservesAllJobCounts()
    {
        // Arrange: 3 jobs × 2 ops each → 6 genes.
        var threeJobIds = new List<int> { 0, 1, 2 };
        var genes1 = new[] { 0, 1, 2, 0, 1, 2 };
        var genes2 = new[] { 2, 1, 0, 2, 1, 0 };
        var p1 = Chromosome.FromArray(genes1);
        var p2 = Chromosome.FromArray(genes2);
        var rng = new Random(99);

        for (int i = 0; i < 50; i++)
        {
            // Act
            var child = PrecedenceOperationCrossover.Cross(p1, p2, threeJobIds, rng);
            var childGenes = child.ToArray();

            // Assert: each job appears exactly twice.
            for (int jobId = 0; jobId < 3; jobId++)
                Assert.AreEqual(2, childGenes.Count(g => g == jobId),
                    $"Job {jobId} count mismatch on iteration {i}");
        }
    }
}
