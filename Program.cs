// This is the only code (Other code was generated by .NET)
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Management;
using System.Net.NetworkInformation;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        Console.WriteLine("Gathering system information...\n");
        ShowLoadingBar();

        string logFilePath = "system_logs.json";
        var systemInfo = GetSystemInformation();
        PrintSystemInfo(systemInfo);

        // Load previous logs if available
        var previousLogs = LoadPreviousLogs(logFilePath);
        if (previousLogs.Count > 0)
        {
            CompareLogs(previousLogs.Last(), systemInfo);
        }

        Console.Write("\nDo you want to save this log? (y/n): ");
        string saveLogResponse = Console.ReadLine()?.ToLower();

        if (saveLogResponse == "y")
        {
            SaveSystemInformation(systemInfo, logFilePath);
            Console.WriteLine("Log saved successfully.");
        }

        Console.Write("\nPress [Enter] to exit immediately or wait 1 minute... ");
        for (int i = 0; i < 60; i++)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                break;
            Thread.Sleep(1000);
        }
    }

    static void ShowLoadingBar()
    {
        Console.Write("[");
        int totalBlocks = 20;
        for (int i = 0; i < totalBlocks; i++)
        {
            Thread.Sleep(100);
            Console.Write("#");
        }
        Console.WriteLine("] | Fetched Info!");
        Thread.Sleep(500);
    }

    static dynamic GetSystemInformation()
    {
        return new
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            MachineName = Environment.MachineName,
            OSVersion = GetOSVersion(),
            ProcessorCount = Environment.ProcessorCount,
            SystemDirectory = Environment.SystemDirectory,
            UserDomainName = Environment.UserDomainName,
            UserName = Environment.UserName,
            DotNetVersion = Environment.Version.ToString(),
            SystemUptime = GetSystemUptime(),
            Storage = GetStorageInfo(),
            GPUs = GetGPUInfo(),
            NetworkStatus = CheckNetworkStatus()
        };
    }

    static void PrintSystemInfo(dynamic systemInfo)
    {
        Console.WriteLine("\n--- Current System Information ---");
        Console.WriteLine($"Timestamp: {systemInfo.Timestamp}");
        Console.WriteLine($"Machine Name: {systemInfo.MachineName}");
        Console.WriteLine($"OS Version: {systemInfo.OSVersion}");
        Console.WriteLine($"Processor Count: {systemInfo.ProcessorCount}");
        Console.WriteLine($"System Directory: {systemInfo.SystemDirectory}");
        Console.WriteLine($"User Domain Name: {systemInfo.UserDomainName}");
        Console.WriteLine($"User Name: {systemInfo.UserName}");
        Console.WriteLine($".NET Version: {systemInfo.DotNetVersion}");
        Console.WriteLine($"System Uptime: {systemInfo.SystemUptime} minutes");

        Console.WriteLine("\n--- Storage Drives ---");
        foreach (var drive in systemInfo.Storage)
        {
            Console.WriteLine($"- {drive}");
        }

        Console.WriteLine("\n--- GPUs ---");
        foreach (var gpu in systemInfo.GPUs)
        {
            Console.WriteLine($"- {gpu}");
        }

        Console.WriteLine($"\nNetwork Status: {systemInfo.NetworkStatus}");
    }

    static void SaveSystemInformation(dynamic systemInfo, string logFilePath)
    {
        var logs = LoadPreviousLogs(logFilePath);
        logs.Add(systemInfo);
        File.WriteAllText(logFilePath, JsonConvert.SerializeObject(logs, Formatting.Indented));
    }

    static List<dynamic> LoadPreviousLogs(string logFilePath)
    {
        if (File.Exists(logFilePath))
        {
            return JsonConvert.DeserializeObject<List<dynamic>>(File.ReadAllText(logFilePath)) ?? new List<dynamic>();
        }
        return new List<dynamic>();
    }

    static void CompareLogs(dynamic previousLog, dynamic currentLog)
    {
        int totalFields = 0;
        int unchangedFields = 0;

        Console.WriteLine("\n--- System Changes ---");

        foreach (var field in previousLog)
        {
            totalFields++;
            string fieldName = field.Name;
            string previousValue = previousLog[fieldName].ToString();
            string currentValue = currentLog[fieldName].ToString();

            if (previousValue != currentValue)
            {
                Console.WriteLine($"- {fieldName} | Current: {currentValue} | Previous: {previousValue}");
            }
            else
            {
                unchangedFields++;
            }
        }

        double similarity = (double)unchangedFields / totalFields * 100;
        Console.WriteLine($"\nSystem Similarity: {Math.Round(similarity, 2)}%");
    }

    static string GetOSVersion()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            int buildNumber = GetWindowsBuildNumber();
            return buildNumber >= 22000 ? $"Windows 11 (Build {buildNumber})" : RuntimeInformation.OSDescription;
        }
        return Environment.OSVersion.ToString();
    }

    static int GetWindowsBuildNumber()
    {
        try
        {
            return int.Parse(Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")?
                            .GetValue("CurrentBuild")?.ToString() ?? "0");
        }
        catch { return 0; }
    }

    static List<string> GetStorageInfo()
    {
        List<string> drives = new List<string>();
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                drives.Add($"{drive.Name} | Total: {drive.TotalSize / 1_073_741_824} GB | Free: {drive.AvailableFreeSpace / 1_073_741_824} GB");
            }
        }
        return drives;
    }

    static List<string> GetGPUInfo()
    {
        List<string> gpus = new List<string>();

        try
        {
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    gpus.Add(obj["Name"].ToString());
                }
            }
        }
        catch { gpus.Add("Unknown GPU"); }

        return gpus;
    }

    static string CheckNetworkStatus()
    {
        return NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Offline";
    }

    static double GetSystemUptime()
    {
        return Math.Round(Environment.TickCount64 / 60000.0, 2);
    }
}
