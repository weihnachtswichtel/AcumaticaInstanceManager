using Microsoft.Web.Administration;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
//using System.ServiceModel.Channels;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;

namespace AcumaticaInstanceManager
{
    internal class AcumaticaInstanceManager
    {
        private static readonly object _lock = new object();
        private const string CompanyPrefixes = "ABC";
        private static int tenantsAmount;
        Settings Settings;
        public AcumaticaInstanceManager(Settings settings)
        {
            Settings = settings;
            tenantsAmount = 2;
        }

        internal static string GetLatestBuild()
        {
            IEnumerable<string> buildList = ListBuilds().OrderBy(b => b);
            return buildList.Count() > 0 ? buildList.Last() : null ;
        }

        internal static string GetLatestBuildForVersion(string majorVersion)
        {
            IEnumerable<string> buildList = ListBuilds(majorVersion).OrderBy(r => r);
            return buildList.Count() > 0 ? buildList.Last() : null;
        }

        internal void InstallAcumatica(string build, DBHelper dBHelper = null)
        {
            string link = GetLinkToMSI(build);
            string downloadPath = Settings.DownloadPath;
            string downloadFilePath = Path.Combine(downloadPath, "AcumaticaERPInstall.msi");
            string acumaticaACPath = Path.Combine(Settings.ExtractPath, @"Acumatica ERP\Data\ac.exe");

            DirectoryInfo currentDownload = Directory.CreateDirectory(downloadPath);
            WebClient webClient = new WebClient();
            
            Console.WriteLine($"Removing old Acumatica instance {Settings.InstanceName} if exists");
            if (!ClearInstance(Settings.SitesPath, Settings.InstanceName, dBHelper)){Console.WriteLine($"Could not remove old instance {Settings.InstanceName}"); return; }

            Console.WriteLine("Start to Download");
            lock (_lock)
            {
                webClient.DownloadFile(link, downloadFilePath);
            }
            Console.WriteLine("MSI file downloaded " + downloadFilePath);

            Directory.CreateDirectory(Settings.ExtractPath);
            string command = string.Format(@"/c msiexec /a {0} /qn TARGETDIR={1}", downloadFilePath, Settings.ExtractPath);
            Console.WriteLine("Extracting MSI package");
            ExecuteCommand(command);
            if (File.Exists(acumaticaACPath))
            {
                string companies = "";
                //Let's install multi tenant instance with different data template
                for (int i = 0; i <= tenantsAmount; i++)
                {
                    companies += string.Format(" -company:\"CompanyID={0};cn=Company{1};CompanyType={2};ParentID=1;Visible=Yes;\"", (i + 2).ToString(), CompanyPrefixes[i], i == 0 ? "F300" : "SalesDemo");
                }
                Console.WriteLine($"Prepaire for the instance {Settings.InstanceName} deployment");

                //Remove the site if was already deployed

                string createNewInstanceCommand = string.Format("-configmode:\"NewInstance\" -dbsrvname:\"{0}\" -dbname:\"{2}\" -dbnew:\"True\" -dbsrvwinauth:\"Yes\" {1} -iname:\"{2}\" -svirtdir:\"{2}\" -h:\"{3}\" -w:\"Default Web Site\" -po:\"{2}\" -a:\"AnonymousUser\" -op:\"Quiet\"", Settings.DBServerName, companies, Settings.InstanceName, Path.Combine(Settings.SitesPath, Settings.InstanceName));
                //When not WinAuth for SQL Server
                //string createNewInstanceCommand = string.Format("-configmode:\"NewInstance\" -dbsrvname:\"{0}\" -dbname:\"{2}\" -dbnew:\"True\" -dbwinauth:\"No\"  -dbnewuser:\"No\" -dbuser:\"{4}\" -dbpass:\"{5}\" {1} -iname:\"{2}\" -svirtdir:\"{2}\" -h:\"{3}\" -w:\"Default Web Site\" -po:\"{2}\" -a:\"AnonymousUser\" -op:\"Quiet\"", Settings.DBServerName, companies, Settings.InstanceName, Path.Combine(Settings.SitesPath, Settings.InstanceName), DBUser, DBPass);

                Console.WriteLine("Deploying Acumatica Instance...");
                ExecuteCommand(createNewInstanceCommand, acumaticaACPath);
                Console.WriteLine($"Instance {Settings.InstanceName} has been set");
            }
            //Removal procedure to clear up downloads. Extracted files are nested and will be remvoed as well.
            RemovePath(downloadPath, 5);
            if (dBHelper != null) {
                //Not to require passwoed change after installation for admin user. It will remain 'setup'. Otherwise it won't be possible to login via API
                dBHelper.setPasswordChangeRequired(false);
            }
        }

        internal void RemoveAcumatica()
        {
            if (ClearInstance(Settings.SitesPath, Settings.InstanceName, new DBHelper(Settings.DBServerName, Settings.InstanceName)))
            {
                Console.WriteLine($"{Settings.InstanceName} instance has been removed");
            }
            else {
                Console.WriteLine($"{Settings.InstanceName} instance could not be removed");
            }
        }

        internal static bool IsApplilcationExist(string instanceName)
        {
            using (ServerManager mgr = new ServerManager())
            {
                Application app = mgr.Sites["Default Web Site"].Applications["/"+instanceName];
                return app != null;
            }
        }

        internal static List<string> ListBuilds(string majorVersion = null)
        {
            Regex MajorVersion20Plus = new Regex(@"/2[0-9].[1-2]/", RegexOptions.IgnoreCase);
            List<string> listOfBuilds = new List<string>();
            using (var s3Client = new AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "http://s3.amazonaws.com/" }))
            {
                ListObjectsV2Request request = new ListObjectsV2Request
                {
                    BucketName = "acumatica-builds",
                    MaxKeys = 1000,
                    //if version specified only non-beta builds will be taken (as betas and previews are in build/preview/ bucket)
                    Prefix = string.IsNullOrEmpty(majorVersion) ? "" : "builds/" + majorVersion
                };
                ListObjectsV2Response response;
                do
                {
                    response = s3Client.ListObjectsV2(request);
                    listOfBuilds.AddRange(response.S3Objects.Where(S3o => 
                                                                    S3o.Key.Contains("AcumaticaERPInstall.msi") && 
                                                                    MajorVersion20Plus.IsMatch(S3o.Key) &&
                                                                    !S3o.Key.Contains("hotfix") &&
                                                                    !S3o.Key.Contains("hidden")
                                                                    ).ToList().Select(S3o => S3o.Key.Split('/')[!S3o.Key.Contains("preview") ? 1 : 2]+ '|' +S3o.Key.Split('/')[!S3o.Key.Contains("preview") ? 2 : 3]));
                    request.ContinuationToken = response.NextContinuationToken;

                } while (response.IsTruncated);
            }
            return listOfBuilds;
        }


        internal static string GetLinkToMSI(string build)
        {
            //It might be better to use S3 interfaces, as some late betas are still in preview folder but having GA build
            string link;
            string majorVersion = build.Substring(0, 4);
            if (build[4] == '9') {
                majorVersion = build.Substring(0, 4).Remove(3, 1).Insert(3, (Convert.ToInt16(char.GetNumericValue(build[3])) + 1).ToString());
                link = $"http://acumatica-builds.s3.amazonaws.com/builds/preview/{majorVersion}/{build}/AcumaticaERP/AcumaticaERPInstall.msi";
            }
            else {
                link = $"http://acumatica-builds.s3.amazonaws.com/builds/{majorVersion}/{build}/AcumaticaERP/AcumaticaERPInstall.msi";
            }
            //Check link
            HttpWebResponse response = null;
            var request = (HttpWebRequest)WebRequest.Create(link);
            request.Method = "HEAD";
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError &&
                     ex.Response != null)
                {
                    var resp = (HttpWebResponse)ex.Response;
                    if (resp.StatusCode == HttpStatusCode.NotFound)
                    {
                        link = null;
                    }
                }
            }
            finally
            {
                if (response != null)
                {
                    if (response.StatusCode != HttpStatusCode.OK) {
                        link = null;
                    }
                    response.Close();
                }
            }
            return link;
        }

        static bool ClearInstance(string sitePath, string instanceName, DBHelper dBHelper = null)
        {
            string path = Path.Combine(sitePath, instanceName);
            Console.WriteLine("Removing Registry Record...");
            DeleteRegistryRecord(Path.GetFileName(path));
            if (dBHelper != null)
            {
                Console.WriteLine("Removing Database if exists...");
                dBHelper.DropDB();
            }
            else { Console.WriteLine("DB handler not inialized. DB will be left"); }
            Console.WriteLine("Removing Virtual Directory...");
            DeleteApp(Path.GetFileName(path));
            Console.WriteLine("Removing App Pool...");
            DeleteAppPool(Path.GetFileName(path));
            Console.WriteLine("Removing Site if exists...");
            lock (_lock)
            {
                return RemovePath(path, 5) &&
                       RemovePath(Path.Combine(sitePath, "Customization", instanceName), 5) &&
                       RemovePath(Path.Combine(sitePath, "Snapshots", instanceName), 5) &&
                       RemovePath(Path.Combine(sitePath, "TemporaryAspFiles", instanceName), 5);
            }
        }

        static bool RemovePath(string path, int max, int current = 0)
        {
            if (current < max)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.EnumerateFiles(path).ToList().ForEach(file => System.IO.File.Delete(file));
                        Directory.EnumerateDirectories(path).ToList().ForEach(directory => Directory.Delete(directory, true));
                        Directory.Delete(path, true);
                    }
                    return true;
                }
                catch
                {
                    Console.WriteLine($"Could not remove the directory from {current + 1} time/s");
                    Thread.Sleep(2000);
                    return RemovePath(path, max, current + 1);
                }
            }
            else
            {
                return false;
            }
        }

        private static void DeleteApp(string virtualDirectory)
        {
            using (ServerManager mgr = new ServerManager())
            {
                List<string> appNames = GetVirtualDirectoriesForApplication(virtualDirectory);
                foreach (string appName in appNames)
                {
                    Application app = mgr.Sites["Default Web Site"].Applications[appName];
                    if (app == null)
                        continue;
                    mgr.Sites["Default Web Site"].Applications.Remove(app);
                    mgr.CommitChanges();
                }
            }
        }

        internal void RecycleAppPool(string poolName)
        {
            using (ServerManager mgr = new ServerManager())
            {
                ApplicationPool appPool = mgr.ApplicationPools[poolName];
                if (appPool == null)
                    return;
                appPool.Recycle();
            }
        }
        private static void DeleteAppPool(string poolName)
        {
            using (ServerManager mgr = new ServerManager())
            {
                ApplicationPool appPool = mgr.ApplicationPools[poolName];
                if (appPool == null)
                    return;

                ApplicationPoolCollection appColl = mgr.ApplicationPools;
                appColl.Remove(appPool);
                mgr.CommitChanges();
            }
        }

        private static List<string> GetVirtualDirectoriesForApplication(string appName)
        { 
            ServerManager manager = new ServerManager();
            Site defaultSite = manager.Sites["Default Web Site"];
            List<string> virtualDirectories = new List<string>();
            string appNameInPath;
            foreach (Application app in defaultSite.Applications)
            {
                foreach (VirtualDirectory vd in app.VirtualDirectories)
                {
                    appNameInPath = Path.GetFileName(Path.GetFullPath(vd.PhysicalPath).TrimEnd(Path.DirectorySeparatorChar)).ToUpper();
                    if (appNameInPath == appName.ToUpper())
                    {
                        virtualDirectories.Add(app.Path);
                    }
                }
            }
            return virtualDirectories;
        }

        private static void DeleteRegistryRecord(string instanceRecord)
        {
            string keyName = @"SOFTWARE\ACUMATICA ERP";
            using (RegistryKey key64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                                            RegistryView.Registry64))
            {
                using (RegistryKey subKey64 = key64.OpenSubKey(keyName, true))
                {
                    if (subKey64.OpenSubKey(instanceRecord) != null)
                    {
                        subKey64.DeleteSubKeyTree(instanceRecord);
                    }
                    else
                    {
                        Console.WriteLine("No Registry record found for " + instanceRecord);
                    }
                }
            }
        }

        private static void ExecuteCommand(string argument, string command = "cmd.exe")
        {
            System.Diagnostics.Process p = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = command;
            startInfo.Arguments = argument;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.CreateNoWindow = true;
            //startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo = startInfo;
            p.Start();
            p.WaitForExit();
            p.Close();
        }
    }
}
