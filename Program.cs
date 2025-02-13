using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using Microsoft.Win32;
using Newtonsoft.Json;
using System.Management;

class Program
{
    static void Main()
    {
        Console.WriteLine("Gathering system information...\n");
        ShowLoadingBar();

        var systemInfo = new
        {
            MachineName = Environment.MachineName,
            OS = GetOSVersion(),
            ProcessorCount = Environment.ProcessorCount,
            UserDomainName = Environment.UserDomainName,
            UserName = Environment.UserName,
            SystemUptime = GetSystemUptime() + " minutes",
            Storage = GetStorageInfo(),
            GPU = GetGPUInfo(),
            NetworkStatus = CheckNetworkStatus()
        };

        Console.WriteLine("\n--- System Information ---");
        PrintSystemInfo(systemInfo);

        // Check for previous log and compare
        if (File.Exists("system_log.json"))
        {
            Console.WriteLine("\nComparing with previous log...");
            var previousLog = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("system_log.json"));
            CompareLogs(previousLog, systemInfo);
        }
        
        // Ask user if they want to save a log
        Console.Write("\nWould you like to save a log of this run? (y/n): ");
        if (Console.ReadLine()?.ToLower() == "y")
        {
            SaveLog(systemInfo);
        }

        Console.WriteLine("\nExiting in 1 minute...");
        Thread.Sleep(60000);
    }

    static void ShowLoadingBar()
    {
        Console.Write("[");
        int totalBlocks = 20;
        for (int i = 0; i < totalBlocks; i++)
        {
            Thread.Sleep(200);
            Console.Write("#");
        }
        Console.WriteLine("] | Fetched Info!");
        Thread.Sleep(500);
    }

    static string GetOSVersion()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string version = RuntimeInformation.OSDescription;
            int buildNumber = GetWindowsBuildNumber();
            if (buildNumber >= 22000) return "Windows 11 (Build " + buildNumber + ")";
            return version;
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

    static double GetSystemUptime()
    {
        return Math.Round(Environment.TickCount64 / 60000.0, 2);
    }

    static List<string> GetStorageInfo()
    {
        List<string> drives = new List<string>();
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                drives.Add($"{drive.Name} - {Math.Round(drive.TotalSize / (1024.0 * 1024 * 1024), 2)} GB Total, " +
                           $"{Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024 * 1024), 2)} GB Free");
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
        try
        {
            using (var ping = new System.Net.NetworkInformation.Ping())
            {
                var reply = ping.Send("8.8.8.8", 1000);
                return (reply != null && reply.Status == System.Net.NetworkInformation.IPStatus.Success) ? "Connected" : "Disconnected";
            }
        }
        catch { return "Unknown"; }
    }

    static void PrintSystemInfo(dynamic info)
    {
        Console.WriteLine($"Machine Name: {info.MachineName}");
        Console.WriteLine($"OS Version: {info.OS}");
        Console.WriteLine($"Processor Count: {info.ProcessorCount}");
        Console.WriteLine($"User Domain Name: {info.UserDomainName}");
        Console.WriteLine($"User Name: {info.UserName}");
        Console.WriteLine($"System Uptime: {info.SystemUptime}");
        Console.WriteLine($"Network Status: {info.NetworkStatus}");

        Console.WriteLine("\n--- Storage Drives ---");
        foreach (var drive in info.Storage) Console.WriteLine($"- {drive}");

        Console.WriteLine("\n--- GPUs ---");
        foreach (var gpu in info.GPU) Console.WriteLine($"- {gpu}");
    }

    static void SaveLog(dynamic info)
    {
        string json = JsonConvert.SerializeObject(info, Formatting.Indented);
        File.WriteAllText("system_log.json", json);
        Console.WriteLine("\nSystem log saved!");
    }

    static void CompareLogs(dynamic previousLog, dynamic currentLog)
    {
        int totalFields = 0, unchangedFields = 0;

        Console.WriteLine("\n--- System Changes ---");

        var previousDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(previousLog));
        var currentDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(currentLog));

        foreach (var field in previousDict.Keys)
        {
            if (!currentDict.ContainsKey(field)) continue;

            totalFields++;
            string prevValue = JsonConvert.SerializeObject(previousDict[field]);
            string currValue = JsonConvert.SerializeObject(currentDict[field]);

            if (prevValue != currValue)
            {
                if (field == "Storage")
                {
                    List<string> prevDrives = JsonConvert.DeserializeObject<List<string>>(prevValue);
                    List<string> currDrives = JsonConvert.DeserializeObject<List<string>>(currValue);

                    Console.WriteLine("\n--- Storage Changes ---");
                    for (int i = 0; i < Math.Max(prevDrives.Count, currDrives.Count); i++)
                    {
                        string prevDrive = i < prevDrives.Count ? prevDrives[i] : "Not Present";
                        string currDrive = i < currDrives.Count ? currDrives[i] : "Not Present";
                        if (prevDrive != currDrive)
                        {
                            Console.WriteLine($"Drive {i + 1}: Current: {currDrive} | Previous: {prevDrive}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"- {field} | Current: {currValue} | Previous: {prevValue}");
                }
            }
            else
            {
                unchangedFields++;
            }
        }

        double similarity = (double)unchangedFields / totalFields * 100;
        Console.WriteLine($"\nSystem Similarity: {Math.Round(similarity, 2)}%");
    }
}
