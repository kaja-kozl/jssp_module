using System;
using System.Reflection;
using System.IO;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using Microsoft.VisualBasic;
using System.Runtime.CompilerServices;

namespace JSSP
{
    class Menu()
    {
        
    }
    internal class Program
    {
        // Reads the job sets line by line
        // Stores the tasks in appropriate data structures
        static Dictionary<int, List<Operation>> FileHandler(string file)
        {
            // Determines if the file exists
            if (!File.Exists(file))
            {
                throw new FileNotFoundException("The specified file does not exist.");
            }

            // Dictionary to organize operations by JobId
            Dictionary<int, List<Operation>> jobs = new Dictionary<int, List<Operation>>();

            // Read all lines using a CSV Parser
            using (TextFieldParser parser = new TextFieldParser(file))
            {
                // Loops through the lines, splitting at ","
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.ReadLine(); // Skip header line

                while (!parser.EndOfData)
                {
                    // Catching any corrupt lines
                    try
                    {
                        string[] job_row = parser.ReadFields();
                        
                        // Parse CSV fields: JobId, OperationId, Subdivision, ProcessingTime
                        int jobId = int.Parse(job_row[0]);
                        int operationId = int.Parse(job_row[1]);
                        string subdivision = job_row[2];
                        int processingTime = int.Parse(job_row[3]);

                        // Create Operation object
                        Operation operation = new Operation(jobId, operationId, subdivision, processingTime);

                        // Add to jobs dictionary, grouped by JobId
                        if (!jobs.ContainsKey(jobId))
                        {
                            jobs[jobId] = new List<Operation>();
                        }
                        jobs[jobId].Add(operation);
                    } 
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            Console.WriteLine($"\nTotal jobs loaded: {jobs.Count}");
            return jobs;
        }

        static void Main(string[] args)
        {
            // Get all implementations of an interface (then store their names)
            // Using a dynamic library
            string[] algorithms = [ "Tabu Search", 
                                    "Genetic Algorithm", 
                                    "Hybrid (Memetic, Tabu + Genetic)", 
                                    "Branch & Bound"];
            bool run = true;
            
            {
                // Input CSV dataset
                Console.Write("Input the name of the CSV dataset: ");
                string? csvFile = Console.ReadLine(); // Input validation

                // Handle file
                Dictionary<int, List<Operation>> jobs = FileHandler(csvFile);
                
                foreach (var job in jobs)
                {
                    Console.WriteLine(job.Key);
                    Console.WriteLine(job.Value);
                }

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