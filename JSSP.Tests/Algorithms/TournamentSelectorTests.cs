using JSSP.Algorithms.Genetic;
using JSSP.Core.Models;

namespace JSSP.Tests.Algorithms;

[TestClass]
public class TournamentSelectorTests
{
    // -------------------------------------------------------------------------
    // Test fixture helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a population of <paramref name="count"/> chromosomes each containing
    /// a single gene equal to job 0. The returned chromosomes are distinct objects
    /// but structurally identical. Paired with <paramref name="fitnesses"/> for tests
    /// that only care about the fitness array, not the chromosome content.
    /// </summary>
    private static (List<Chromosome> Population, List<int> Fitnesses) MakePopulation(
        int count, int[] fitnesses)
    {
        var jobs = new List<Job> { new Job(0, new List<Operation> { new(0, 0, "M", 1) }) };
        var population = Enumerable
            .Range(0, count)
            .Select(_ => Chromosome.CreateRandom(jobs, new Random(0)))
            .ToList();
        return (population, fitnesses.ToList());
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [TestMethod]
    public void Select_WholePopulationTournament_ReturnsBestFitness()
    {
        // Arrange: 5 chromosomes with distinct fitnesses; k=100 forces deterministic
        // selection — the population is small so all members are sampled repeatedly and
        // the minimum-fitness chromosome always wins.
        var (population, fitnesses) = MakePopulation(5, new[] { 15, 8, 20, 12, 17 });
        int minFitness = fitnesses.Min();

        // Act: large k guarantees the best is seen.
        var selected = TournamentSelector.Select(population, fitnesses, tournamentSize: 100, new Random(0));

        // Assert: the selected chromosome must have the minimum fitness.
        int selectedIdx = population.IndexOf(selected);
        Assert.AreEqual(minFitness, fitnesses[selectedIdx],
            $"Expected fitness {minFitness} but got fitness {fitnesses[selectedIdx]}.");
    }

    [TestMethod]
    public void Select_WithFixedSeed_IsDeterministic()
    {
        // Arrange: 5 chromosomes with distinct fitnesses; k=2.
        var (population, fitnesses) = MakePopulation(5, new[] { 10, 5, 8, 3, 7 });

        // Act: two separate calls each with the same seed should return the same chromosome.
        var selected1 = TournamentSelector.Select(population, fitnesses, tournamentSize: 2, new Random(42));
        var selected2 = TournamentSelector.Select(population, fitnesses, tournamentSize: 2, new Random(42));

        // Assert
        Assert.AreSame(selected1, selected2,
            "Same seed must produce the same tournament winner.");
    }

    [TestMethod]
    public void Select_TournamentSizeOne_ReturnsAChromosome()
    {
        // Arrange: k=1 degrades to uniform random selection; any chromosome may be returned.
        var (population, fitnesses) = MakePopulation(4, new[] { 9, 9, 9, 9 });

        // Act
        var selected = TournamentSelector.Select(population, fitnesses, tournamentSize: 1, new Random(7));

        // Assert: result is a member of the population (not null, and is one of the instances).
        Assert.IsNotNull(selected);
        Assert.Contains(selected, population,
            "Selected chromosome must be a member of the population.");
    }

    [TestMethod]
    public void Select_AllSameFitness_StillReturnsPopulationMember()
    {
        // Arrange: ties everywhere — selection pressure is irrelevant; any is acceptable.
        var (population, fitnesses) = MakePopulation(5, new[] { 42, 42, 42, 42, 42 });

        // Act
        var selected = TournamentSelector.Select(population, fitnesses, tournamentSize: 3, new Random(99));

        // Assert
        Assert.Contains(selected, population,
            "Result must be a member of the input population.");
    }

    [TestMethod]
    public void Select_SingleElementPopulation_ReturnsThatElement()
    {
        // Arrange: only one candidate — it must always win.
        var (population, fitnesses) = MakePopulation(1, new[] { 100 });

        // Act
        var selected = TournamentSelector.Select(population, fitnesses, tournamentSize: 1, new Random(0));

        // Assert
        Assert.AreSame(population[0], selected);
    }
}
