﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32;
using Newtonsoft.Json;

class Program
{
    static void Main()
    {
        Console.WriteLine("Gathering system information...\n");
        ShowLoadingBar();

        string currentLog = GetSystemInformation();
        string logFilePath = "system_info_log.json";

        // Print the current system info
        PrintSystemInfo(currentLog);

        // If a previous log exists, compare with the current system info
        if (File.Exists(logFilePath))
        {
            string previousLog = File.ReadAllText(logFilePath);
            CompareLogs(previousLog, currentLog);
        }

        // Ask user if they want to save the log at the end
        Console.WriteLine("\nDo you want to save the log for this run? (y/n): ");
        string saveLogResponse = Console.ReadLine()?.ToLower();

        if (saveLogResponse == "y")
        {
            SaveSystemInformation(currentLog, logFilePath);
            Console.WriteLine("Log saved.");
        }

        Console.WriteLine("\nExiting in 1 minute...");
        Thread.Sleep(60000); // Pause for 1 minute before exit
    }

    static void ShowLoadingBar()
    {
        Console.Write("[");
        int totalBlocks = 20;
        for (int i = 0; i < totalBlocks; i++)
        {
            Thread.Sleep(500); // Delay 500ms per block
            Console.Write("#");
        }
        Console.WriteLine("] | Fetched Info!");
        Thread.Sleep(1000);
    }

    static string GetSystemInformation()
    {
        var systemInfo = new
        {
            MachineName = Environment.MachineName,
            OSVersion = GetOSVersion(),
            ProcessorCount = Environment.ProcessorCount,
            SystemDirectory = Environment.SystemDirectory,
            UserDomainName = Environment.UserDomainName,
            UserName = Environment.UserName,
            DotNetVersion = Environment.Version.ToString(),
            SystemUptime = GetSystemUptime()
        };

        return JsonConvert.SerializeObject(systemInfo);
    }

    static void PrintSystemInfo(string log)
    {
        var systemInfo = JsonConvert.DeserializeObject<dynamic>(log);

        Console.WriteLine("\n--- Current System Information ---");
        Console.WriteLine($"Machine Name: {systemInfo.MachineName}");
        Console.WriteLine($"OS Version: {systemInfo.OSVersion}");
        Console.WriteLine($"Processor Count: {systemInfo.ProcessorCount}");
        Console.WriteLine($"System Directory: {systemInfo.SystemDirectory}");
        Console.WriteLine($"User Domain Name: {systemInfo.UserDomainName}");
        Console.WriteLine($"User Name: {systemInfo.UserName}");
        Console.WriteLine($".NET Version: {systemInfo.DotNetVersion}");
        Console.WriteLine($"System Uptime: {systemInfo.SystemUptime} minutes");
    }

    static void SaveSystemInformation(string log, string logFilePath)
    {
        File.WriteAllText(logFilePath, log);
    }

    static void CompareLogs(string previousLog, string currentLog)
    {
        var previousInfo = JsonConvert.DeserializeObject<dynamic>(previousLog);
        var currentInfo = JsonConvert.DeserializeObject<dynamic>(currentLog);

        bool changesDetected = false;
        Console.WriteLine("\n--- Changes Detected ---");

        foreach (var field in previousInfo)
        {
            if (previousInfo[field.Name] != currentInfo[field.Name])
            {
                Console.WriteLine($"Changes: - {field.Name} | Current: {currentInfo[field.Name]} | Previous: {previousInfo[field.Name]}");
                changesDetected = true;
            }
        }

        if (!changesDetected)
        {
            Console.WriteLine("No changes detected.");
        }
    }

    static string GetOSVersion()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string version = RuntimeInformation.OSDescription;

            // Check if it's Windows 11 based on the build number
            int buildNumber = GetWindowsBuildNumber();
            if (buildNumber >= 22000) // Windows 11 builds start at 22000+
                return "Windows 11 (Build " + buildNumber + ")";

            return version; // Fallback
        }
        return Environment.OSVersion.ToString(); // Fallback for non-Windows
    }

    static int GetWindowsBuildNumber()
    {
        try
        {
            return int.Parse(Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")?
                            .GetValue("CurrentBuild")?.ToString() ?? "0");
        }
        catch
        {
            return 0;
        }
    }

    static double GetSystemUptime()
    {
        return Math.Round(Environment.TickCount64 / 60000.0, 2);
    }
}
