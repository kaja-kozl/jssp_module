using JSSP.Algorithms.Genetic;
using JSSP.Core.Models;

namespace JSSP.Tests.Algorithms;

[TestClass]
public class ChromosomeTests
{
    // -------------------------------------------------------------------------
    // Helper: build a minimal two-job job list for tests.
    // Job 0: 3 operations. Job 1: 2 operations.
    // -------------------------------------------------------------------------
    private static List<Job> MakeJobs()
    {
        var ops0 = new List<Operation>
        {
            new(0, 0, "A", 1),
            new(0, 1, "B", 1),
            new(0, 2, "C", 1),
        };
        var ops1 = new List<Operation>
        {
            new(1, 0, "A", 1),
            new(1, 1, "B", 1),
        };
        return [new Job(0, ops0), new Job(1, ops1)];
    }

    [TestMethod]
    public void CreateRandom_LengthEqualsTotalOperations()
    {
        // Arrange: 3 + 2 = 5 total operations.
        var jobs = MakeJobs();

        // Act
        var chromosome = Chromosome.CreateRandom(jobs, new Random(0));

        // Assert
        Assert.AreEqual(5, chromosome.Length);
    }

    [TestMethod]
    public void CreateRandom_JobCountsPreserved()
    {
        // Arrange: job 0 has 3 ops, job 1 has 2 ops.
        var jobs = MakeJobs();

        // Act
        var chromosome = Chromosome.CreateRandom(jobs, new Random(42));
        var genes = chromosome.ToArray();

        // Assert: every gene is a valid job id and counts are correct.
        Assert.AreEqual(3, genes.Count(g => g == 0));
        Assert.AreEqual(2, genes.Count(g => g == 1));
    }

    [TestMethod]
    public void HashKey_SameGenes_ProducesSameKey()
    {
        // Arrange
        var c1 = Chromosome.FromArray(new[] { 0, 1, 0, 1 });
        var c2 = Chromosome.FromArray(new[] { 0, 1, 0, 1 });

        // Act & Assert
        Assert.AreEqual(c1.HashKey, c2.HashKey);
    }

    [TestMethod]
    public void HashKey_DifferentGenes_ProducesDifferentKey()
    {
        // Arrange
        var c1 = Chromosome.FromArray(new[] { 0, 1, 0, 1 });
        var c2 = Chromosome.FromArray(new[] { 1, 0, 0, 1 });

        // Act & Assert
        Assert.AreNotEqual(c1.HashKey, c2.HashKey);
    }

    [TestMethod]
    public void ToArray_ReturnsCopy_MutatingCopyDoesNotAffectChromosome()
    {
        // Arrange
        var chromosome = Chromosome.FromArray(new[] { 0, 1, 0 });
        var copy = chromosome.ToArray();

        // Act: mutate the copy.
        copy[0] = 99;

        // Assert: original chromosome is unchanged.
        Assert.AreEqual(0, chromosome[0]);
    }

    [TestMethod]
    public void FromArray_DefensivelyCopiesInput()
    {
        // Arrange
        var source = new[] { 0, 1, 0 };
        var chromosome = Chromosome.FromArray(source);

        // Act: mutate the source array after construction.
        source[0] = 99;

        // Assert: chromosome is unaffected.
        Assert.AreEqual(0, chromosome[0]);
    }
}
