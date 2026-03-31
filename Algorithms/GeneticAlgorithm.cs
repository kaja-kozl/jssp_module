using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;

class GeneticAlgorithm : IAlgorithm
{
    const int POP_SIZE = 40; // Repersents search space
    const int GENERATIONS = 100;
    const double X_MIN = -1.0;
    const double X_MAX = 2.0;
    const double MUTATION_PROB = 0.2;
    const double MUTATION_STD = 0.1;
    
    public bool Solve()
    {
        return true;
    }

    // Fitness function
    public bool fitness_function(Array chromosome)
    {
        return true;
    }

    // Improve robustness
    public Array tournament_selection(int pop, int fitness, int k=3)
    {
        string[] cars = {};
        return cars;
    }

    // Blending real values
    public int arithmetic_crossover(int p1, int p2)
    {
        return 1;
    }
}