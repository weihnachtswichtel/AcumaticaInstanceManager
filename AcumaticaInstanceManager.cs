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

        internal void InstallAcumatica(string build)
        {
            string link = GetLinkToMSI(build);
            string downloadPath = Settings.DownloadPath;
            string downloadFilePath = Path.Combine(downloadPath, "AcumaticaERPInstall.msi");
            string acumaticaACPath = Path.Combine(Settings.ExtractPath, @"Acumatica ERP\Data\ac.exe");

            DirectoryInfo currentDownload = Directory.CreateDirectory(downloadPath);
            WebClient webClient = new WebClient();

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
            //To add removal procedure to clear up downloaded and extracted files.
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
