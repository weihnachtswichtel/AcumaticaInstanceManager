using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AcumaticaInstanceManager
{
    class Program
    {
        static Regex MajorVersion20Plus = new Regex(@"2[0-9][.][1-2]", RegexOptions.IgnoreCase);
        static Regex Build20Plus        = new Regex(@"2[0-9].[0-2][0-9]{2}.[0-9]{4}", RegexOptions.IgnoreCase);
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                //List builds
                int listBuildsCommandIndex = Array.IndexOf(args, "--listbuilds");
                if (listBuildsCommandIndex >= 0)
                {
                    ListBuilds(args.Length > listBuildsCommandIndex + 1 ? CheckMajorVersionInput(args[listBuildsCommandIndex + 1]) : null);
                    return;
                }
                //Install Acumatica
                int installAcumaticaCommandIndex = Array.IndexOf(args, "--install");
                if (installAcumaticaCommandIndex >= 0)
                {
                    string build = string.Empty;
                    //get last build of latest version, including beta and preview if no parameter specified
                    if (args.Length == installAcumaticaCommandIndex + 1)
                    {
                        build = AcumaticaInstanceManager.GetLatestBuild();
                    }
                    else
                    {
                        string paramBuildOrVersion = args[installAcumaticaCommandIndex + 1].Replace(" ", string.Empty);
                        //check if build specified
                        if (Build20Plus.Match(paramBuildOrVersion).Success)
                        {
                            build = Build20Plus.Match(paramBuildOrVersion).Value;
                        }
                        //check if major version specified and get latest build for this verison excludign beta
                        else if (MajorVersion20Plus.Match(paramBuildOrVersion.ToUpper().Replace("R", ".")).Success)
                        {
                            build = AcumaticaInstanceManager.GetLatestBuildForVersion(MajorVersion20Plus.Match(paramBuildOrVersion.ToUpper().Replace("R", ".")).Value);
                        }
                        else
                        {
                            Console.WriteLine($"Specified value {args[installAcumaticaCommandIndex + 1]} neither Acumatica build, nor Version");
                        }
                    }
                    if (string.IsNullOrEmpty(build))
                    {
                        Console.WriteLine("No build could be found");
                    }
                    else
                    {
                        Console.WriteLine(String.Format("Build {0} of Acumatica ERP 20{1} is taken for further installation",
                                                            build.Contains('|') ? build.Split('|')[1] : build,
                                                            build.Contains('|') ? build.Split('|')[0].Replace(".", " R") : build.Substring(0, 4).Replace(".", " R")));
                        InstallAcumaticaERP(build.Contains('|') ? build.Split('|')[1] : build);
                    }
                    return;
                }
                int removeAcumaticaCommandIndex = Array.IndexOf(args, "--remove");
                if (removeAcumaticaCommandIndex >= 0)
                {
                    if (args.Length > removeAcumaticaCommandIndex + 1)
                    {
                        string instanceName = args[removeAcumaticaCommandIndex + 1];
                        if (AcumaticaInstanceManager.IsApplilcationExist(instanceName))
                        {
                            Console.WriteLine($"{instanceName} found, Deleting");
                            Settings settings = new Settings
                            {
                                InstanceName = instanceName,
                                SitesPath = @"C:\Acumatica\",
                                DBServerName = "MSK-LT-75"
                            };
                            AcumaticaInstanceManager instanceManager = new AcumaticaInstanceManager(settings);
                            instanceManager.RemoveAcumatica();
                        }
                        else
                        {
                            Console.WriteLine($"No {instanceName} application found");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Instance name should be specified");
                    }
                }
            }
            else{ Console.WriteLine("Instance name should be specified"); }
            Console.ReadLine();
        }

        private static void InstallAcumaticaERP(string build)
        {
            string DefaultDownlodPath = GetDefaultDownloadFolderPath();
            string MSIDowbloadFolder  = "AcumaticaMSI";
            string ExtactMSIFolder    = "Extract";

            Settings settings = new Settings
            {
                DownloadPath = Path.Combine(DefaultDownlodPath, MSIDowbloadFolder),
                ExtractPath  = Path.Combine(DefaultDownlodPath, MSIDowbloadFolder, ExtactMSIFolder),
                InstanceName = build.Substring(0, 6).Replace(".", "R"),     //Will be something like 21R203
                SitesPath    = @"C:\Acumatica\",
                DBServerName = "MSK-LT-75"
            };
            AcumaticaInstanceManager instanceManager = new AcumaticaInstanceManager(settings);
            instanceManager.InstallAcumatica(build);
        }

        //Getting the default Download derectory
        static string GetDefaultDownloadFolderPath() {
            return SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0);
        }

        [DllImport("shell32",
        CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern string SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags,
        int hToken = 0);

        private static string CheckMajorVersionInput(string majorVersionRaw)
        {
            string majorVersion = majorVersionRaw.ToUpper().Replace(" ", string.Empty).Replace("R", ".");
            if (!majorVersion.Contains("--") && majorVersion.Length < 7)
            {
                string checkedVersion = MajorVersion20Plus.Match(majorVersion).Value;
                if (!string.IsNullOrEmpty(checkedVersion)) { return checkedVersion; } else { Console.WriteLine("Acumatica Version Version could not be parsed in " + majorVersionRaw); return null;}
            } else { return null;}
        }

        static void ListBuilds(string majorVersion = null)
        {
            List<string> builds = AcumaticaInstanceManager.ListBuilds(majorVersion);
            if (builds.Count == 0 && !string.IsNullOrEmpty(majorVersion)) { Console.WriteLine($"No Acumatica ERP builds found for {majorVersion} Version");}
            foreach (string build in builds.OrderBy(r=>r))
            {
                if (string.IsNullOrEmpty(majorVersion) || majorVersion != build.Split('|')[0])
                {
                    majorVersion = build.Split('|')[0];
                    Console.WriteLine();
                    Console.WriteLine("- Acumatica ERP 20" + majorVersion.Replace('.', 'R'));
                }
                Console.WriteLine("|--" + build.Split('|')[1]);
            }
            Console.ReadLine();
        }
    }
}
