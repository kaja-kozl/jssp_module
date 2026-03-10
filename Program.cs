using System;
using System.Reflection;
using System.IO;

namespace JSSP
{
    class Menu()
    {
        
    }
    internal class Program
    {
        static void Main(string[] args)
        {
            // Get all implementations of an interface (then store their names)
            // Using a dynamic library
            string[] algorithms = [ "Tabu Search", 
                                    "Genetic Algorithm", 
                                    "Hybrid (Memetic, Tabu + Genetic)", 
                                    "Branch & Bound"];
            bool run = true;

            do
            {
                // Input CSV dataset
                Console.Write("Input the name of the CSV dataset: ");
                string? csvFile = Console.ReadLine(); // Input validation

                // File Handling (stick this in a class)
                System.IO.StreamReader fileReader;
                fileReader = new System.IO.StreamReader(csvFile);
                
                // List available algorithms (order by recommended)
                for (int i = 0; algorithms.Length > i; i++)
                {
                    Console.WriteLine("[" + (i + 1) + "]" + " " + algorithms[i]);
                }

                Console.Write("Select an algorithm: ");
                int algorithmChosen = Convert.ToInt32(Console.ReadLine());

                Console.WriteLine(algorithms[algorithmChosen]);
            } while (run == true);

            Console.WriteLine("Hello World!");
        }
    }
}