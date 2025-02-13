using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Microsoft.Win32;
using Newtonsoft.Json; // Ensure Newtonsoft.Json is installed for logging

class Program
{
    static void Main()
    {
        Console.WriteLine("Select mode:\n1 - Simple\n2 - Advanced");
        string mode = Console.ReadLine();
        bool isAdvanced = mode == "2";

        Console.WriteLine("\nGathering system information...\n");
        ShowLoadingBar();

        var systemInfo = new
        {
            MachineName = Environment.MachineName,
            OSVersion = GetOSVersion(),
            ProcessorCount = Environment.ProcessorCount,
            SystemDirectory = Environment.SystemDirectory,
            UserDomain = Environment.UserDomainName,
            UserName = Environment.UserName,
            Uptime = GetSystemUptime() + " minutes",
            Drives = GetStorageInfo(),
            GPUs = GetGPUInfo(),
            NetworkStatus = CheckNetworkStatus()
        };

        Console.WriteLine("\n--- System Information ---\n");
        PrintSystemInfo(systemInfo);

        Console.Write("\nWould you like to save a log of this run? (y/n): ");
        if (Console.ReadLine()?.ToLower() == "y")
        {
            SaveLog(systemInfo, isAdvanced);
            Console.WriteLine("Log saved.");
        }

        Console.WriteLine("\nChecking for changes since last run...");
        CompareLogs(isAdvanced);

        Console.WriteLine("\nExiting in 1 minute...");
        Thread.Sleep(60000);
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
        Console.WriteLine("] Done!");
    }

    static void PrintSystemInfo(dynamic systemInfo)
    {
        Console.WriteLine($"Machine Name: {systemInfo.MachineName}");
        Console.WriteLine($"OS Version: {systemInfo.OSVersion}");
        Console.WriteLine($"Processor Count: {systemInfo.ProcessorCount}");
        Console.WriteLine($"System Directory: {systemInfo.SystemDirectory}");
        Console.WriteLine($"User Domain: {systemInfo.UserDomain}");
        Console.WriteLine($"User Name: {systemInfo.UserName}");
        Console.WriteLine($"System Uptime: {systemInfo.Uptime}");

        Console.WriteLine("\n--- Drives ---");
        foreach (var drive in systemInfo.Drives)
            Console.WriteLine($"Drive {drive.Name}: {drive.FreeSpace} GB free / {drive.TotalSpace} GB total");

        Console.WriteLine("\n--- GPUs ---");
        foreach (var gpu in systemInfo.GPUs)
            Console.WriteLine($"GPU: {gpu}");

        Console.WriteLine($"\nNetwork Status: {systemInfo.NetworkStatus}");
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

    static object GetStorageInfo()
    {
        return DriveInfo.GetDrives()
            .Where(d => d.IsReady)
            .Select(d => new
            {
                Name = d.Name,
                TotalSpace = Math.Round(d.TotalSize / (double)(1024 * 1024 * 1024), 2),
                FreeSpace = Math.Round(d.AvailableFreeSpace / (double)(1024 * 1024 * 1024), 2)
            }).ToList();
    }

    static List<string> GetGPUInfo()
    {
        List<string> gpus = new List<string>();
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-Command \"Get-WmiObject Win32_VideoController | Select-Object -ExpandProperty Name\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                while (!process.StandardOutput.EndOfStream)
                    gpus.Add(process.StandardOutput.ReadLine()?.Trim() ?? "");
            }
        }
        catch { gpus.Add("Unknown GPU"); }

        return gpus;
    }

    static string CheckNetworkStatus()
    {
        try
        {
            return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable() ? "Connected" : "Disconnected";
        }
        catch { return "Unknown"; }
    }

    static double GetSystemUptime()
    {
        return Math.Round(Environment.TickCount64 / 60000.0, 2);
    }

    static void SaveLog(object systemInfo, bool isAdvanced)
    {
        string logFile = isAdvanced ? "advanced_log.json" : "simple_log.json";
        File.WriteAllText(logFile, JsonConvert.SerializeObject(systemInfo, Formatting.Indented));
    }

    static void CompareLogs(bool isAdvanced)
    {
        string logFile = isAdvanced ? "advanced_log.json" : "simple_log.json";

        if (!File.Exists(logFile))
        {
            Console.WriteLine("No previous log found.");
            return;
        }

        var previousLog = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(logFile));
        var currentLog = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(new
        {
            MachineName = Environment.MachineName,
            OSVersion = GetOSVersion(),
            ProcessorCount = Environment.ProcessorCount,
            SystemDirectory = Environment.SystemDirectory,
            UserDomain = Environment.UserDomainName,
            UserName = Environment.UserName,
            Uptime = GetSystemUptime() + " minutes",
            Drives = GetStorageInfo(),
            GPUs = GetGPUInfo(),
            NetworkStatus = CheckNetworkStatus()
        }));

        Console.WriteLine("\n--- System Changes ---");
        int totalChanges = 0;

        foreach (var key in currentLog.Keys)
        {
            if (!previousLog.ContainsKey(key) || previousLog[key]?.ToString() != currentLog[key]?.ToString())
            {
                totalChanges++;
                Console.WriteLine($"- {key} | Current: {currentLog[key]} | Previous: {previousLog[key]}");
            }
        }

        double similarity = totalChanges == 0 ? 100 : Math.Round((1 - (double)totalChanges / currentLog.Count) * 100, 2);
        Console.WriteLine($"\nSystem similarity to last run: {similarity}%");
    }
}
