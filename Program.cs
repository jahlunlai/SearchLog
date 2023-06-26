using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
namespace SearchLog
{
    class Program
    {
        static void Main()
        {
            // Read the directory paths from the config file
            string configFilePath = "config.txt"; // Specify the path to your config file
            List<string> directoryPaths = ReadDirectoryPaths(configFilePath);

            // Get the user input string
            Console.Write("Enter a date-time string (format: yyyyMMdd_HHmmss): ");
            string userInput = Console.ReadLine();

            if (string.IsNullOrEmpty(userInput))
            {
                Console.WriteLine("Invalid date-time input. Please try again.");
                return;
            }

            // Convert user input string to DateTime
            DateTime userInputDateTime = ConvertToDateTime(userInput);
            if (userInputDateTime == DateTime.MinValue)
            {
                Console.WriteLine("Invalid date-time input. Please try again.");
                return;
            }

            TraceLog($"User input search targeted datetime: {userInputDateTime}");


            DateTime startDate;
            DateTime endDate;

            // Prompt the user to enter the start date
            Console.Write("Enter the start date (yyyy-MM-dd): ");
            if (!DateTime.TryParseExact(Console.ReadLine(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDate))
            {
                Console.WriteLine("Invalid date format. Aborting...");
                return;
            }

            // Prompt the user to enter the end date
            Console.Write("Enter the end date (yyyy-MM-dd): ");
            if (!DateTime.TryParseExact(Console.ReadLine(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDate))
            {
                Console.WriteLine("Invalid date format. Aborting...");
                return;
            }


            // Process each directory path concurrently
            Parallel.ForEach(directoryPaths, directoryPath =>
            {
                ProcessDirectory(directoryPath, userInputDateTime, startDate, endDate);
            });

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }


        // Read the directory paths from the config file
        static List<string> ReadDirectoryPaths(string filePath)
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


        // Process a directory and its subdirectories
        static void ProcessDirectory(string directoryPath, DateTime userInputDateTime, DateTime startDate, DateTime endDate)
        {
            try
            {
                // List all files in the directory and its subfolders
                List<string> fileNames = ListAllFiles(directoryPath, startDate, endDate);

                // Process each file
                foreach (string fileName in fileNames)
                {
                    DateTime startTime = DateTime.MinValue;
                    DateTime endTime = DateTime.MinValue;

                    string dateTimeString = ExtractDateTimeFromFileName(Path.GetFileName(fileName));
                    if (!string.IsNullOrEmpty(dateTimeString))
                    {
                        startTime = ConvertToDateTime(dateTimeString);
                    }

                    if (IsZipFile(fileName))
                    {
                        List<string> zipFileEntries = ListFilesInZip(fileName);
                        foreach (string entry in zipFileEntries)
                        {
                            string entryFileName = Path.GetFileName(entry);
                            DateTime entryStartTime = ExtractStartTimeFromEntryFileName(entryFileName);
                            DateTime entryEndTime = GetLastWriteTimeInZipFile(fileName, entry);

                            // Print the file name, StartTime, EndTime, and whether the user input is in range
                            TraceLog($"{entryFileName} - StartTime: {entryStartTime}, EndTime: {entryEndTime} - IsInRange: {IsDateTimeInRange(userInputDateTime, entryStartTime, entryEndTime)}");

                            // Copy the file to the output folder if the user input is in range
                            if (IsDateTimeInRange(userInputDateTime, entryStartTime, entryEndTime))
                            {
                                string outputFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Output_Files");
                                CopyFileToOutputFolder(fileName, entry, outputFolderPath);
                            }
                        }
                    }
                    else
                    {
                        endTime = File.GetLastWriteTime(fileName);

                        // Print the file name, StartTime, EndTime, and whether the user input is in range
                        TraceLog($"{fileName} - StartTime: {startTime}, EndTime: {endTime} - IsInRange: {IsDateTimeInRange(userInputDateTime, startTime, endTime)}");

                        // Copy the file to the output folder if the user input is in range
                        if (IsDateTimeInRange(userInputDateTime, startTime, endTime))
                        {
                            string outputFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "Output_Files");
                            CopyFileToOutputFolder(fileName, null, outputFolderPath);
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error processing directory '{directoryPath}': {ex.Message}");
            }
        }







        // Recursively list all files in a directory and its subfolders, including files inside zip files
        static List<string> ListAllFiles(string directoryPath, DateTime startDate, DateTime endDate)
        {
            List<string> fileList = new List<string>();

            try
            {
                string[] files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);

                foreach (string file in files)
                {
                    DateTime fileModifiedDate = File.GetLastWriteTime(file);

                    if (fileModifiedDate >= startDate && fileModifiedDate <= endDate)
                    {
                        fileList.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing files: {ex.Message}");
            }

            return fileList;
        }


        // Extract and return the file names from a zip file
        static List<string> ListFilesInZip(string zipFilePath)
        {
            List<string> fileNames = new List<string>();

            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        fileNames.Add(entry.FullName);
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error reading zip file: {ex.Message}");
            }

            return fileNames;
        }

        // Extract and return the date and time from the file name
        static string ExtractDateTimeFromFileName(string fileName)
        {
            string pattern = @"\d{8}_\d{6}";
            Match match = Regex.Match(fileName, pattern);
            if (match.Success)
            {
                return match.Value;
            }

            return string.Empty;
        }

        // Convert the extracted date and time to a DateTime object
        static DateTime ConvertToDateTime(string dateTimeString)
        {
            if (DateTime.TryParseExact(dateTimeString, "yyyyMMdd_HHmmss", null, System.Globalization.DateTimeStyles.None, out DateTime dateTime))
            {
                return dateTime;
            }

            return DateTime.MinValue;
        }

        // Check if a file is a zip file
        static bool IsZipFile(string fileName)
        {
            return string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase);
        }

        // Extract the StartTime from the entry file name
        static DateTime ExtractStartTimeFromEntryFileName(string entryFileName)
        {
            string dateTimeString = ExtractDateTimeFromFileName(entryFileName);
            if (!string.IsNullOrEmpty(dateTimeString))
            {
                return ConvertToDateTime(dateTimeString);
            }

            return DateTime.MinValue;
        }

        // Get the last write time inside files in the compressed file of a zip file
        static DateTime GetLastWriteTimeInZipFile(string zipFilePath, string entryFileName)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipFilePath))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (string.Equals(entry.FullName, entryFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            return entry.LastWriteTime.LocalDateTime;
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error reading zip file: {ex.Message}");
            }

            return DateTime.MinValue;
        }

        // Check if a DateTime is within the range of StartTime and EndTime
        static bool IsDateTimeInRange(DateTime dateTime, DateTime startTime, DateTime endTime)
        {
            return dateTime >= startTime && dateTime <= endTime;
        }

        // Trace log function to record the program's process procedure
        static void TraceLog(string message)
        {
            // Generate the log file path based on the thread's managed ID
            string logFilePath = $"log_thread_{Thread.CurrentThread.ManagedThreadId}.txt";

            // Append the message to the log file
            using (StreamWriter writer = File.AppendText(logFilePath))
            {
                writer.WriteLine($"{DateTime.Now} - {message}");
            }

            // Also print the message to the console
            Console.WriteLine(message);
        }


        // Copy a file to the output folder
        static void CopyFileToOutputFolder(string sourceFilePath, string entryFileName, string outputFolderPath)
        {
            try
            {
                

                // Create the output folder if it doesn't exist
                if (!Directory.Exists(outputFolderPath))
                {
                    Directory.CreateDirectory(outputFolderPath);
                }

                if (IsZipFile(sourceFilePath))
                {
                    string outputFilePath = Path.Combine(outputFolderPath, entryFileName);


                    using (ZipArchive archive = ZipFile.OpenRead(sourceFilePath))
                    {
                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            if (entry.FullName.Equals(entryFileName, StringComparison.OrdinalIgnoreCase))
                            {
                                entry.ExtractToFile(outputFilePath, true);
                                Console.WriteLine($"File extracted to output folder: {outputFilePath}");
                                break;
                            }
                        }
                    }
                }
                else
                {
                    //string outputFilePath = Path.Combine(outputFolderPath, sourceFilePath);


                    string filename = System.IO.Path.GetFileName(sourceFilePath);
                    string outputFilePath = Path.Combine(outputFolderPath, filename);

                    File.Copy(sourceFilePath, outputFilePath, true);
                    Console.WriteLine($"File copied to output folder: {outputFilePath}");
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error copying file '{sourceFilePath}': {ex.Message}");
            }
        }







    }
}
