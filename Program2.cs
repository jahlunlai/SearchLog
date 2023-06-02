using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    static void Main()
    {
        // Read the config file
        string configFilePath = "config.txt"; // Specify the path to your config file
        List<string> directoryPaths = ReadConfigFile(configFilePath);

        // Iterate through each directory path
        foreach (string directoryPath in directoryPaths)
        {
            // List all files in the directory and its subfolders
            List<string> fileNames = ListFiles(directoryPath, ".zip");

            // Display the file names
            Console.WriteLine($"ZIP files in directory '{directoryPath}':");
            foreach (string fileName in fileNames)
            {
                Console.WriteLine(fileName);
            }
            Console.WriteLine();
        }

        Console.WriteLine("Press any key to exit.");
        Console.ReadKey();
    }

    // Read the config file and return a list of directory paths
    static List<string> ReadConfigFile(string filePath)
    {
        List<string> directoryPaths = new List<string>();

        try
        {
            string[] lines = File.ReadAllLines(filePath);

            foreach (string line in lines)
            {
                // Skip empty lines or lines starting with a comment character (e.g., '#')
                if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#"))
                    continue;

                // Add the directory path to the list
                directoryPaths.Add(line.Trim());
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error reading config file: {ex.Message}");
        }

        return directoryPaths;
    }

    // Recursively list all files with the specified extension in a directory and its subfolders
    static List<string> ListFiles(string directoryPath, string fileExtension)
    {
        List<string> fileNames = new List<string>();

        try
        {
            // Get all files in the current directory with the specified extension
            string[] files = Directory.GetFiles(directoryPath, "*" + fileExtension);
            foreach (string file in files)
            {
                fileNames.Add(Path.GetFileName(file));
            }

            // Recursively process subdirectories
            string[] subDirectories = Directory.GetDirectories(directoryPath);
            foreach (string subDirectory in subDirectories)
            {
                List<string> subDirectoryFiles = ListFiles(subDirectory, fileExtension);
                fileNames.AddRange(subDirectoryFiles);
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error listing files: {ex.Message}");
        }

        return fileNames;
    }
}
