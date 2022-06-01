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
        public AcumaticaInstanceManager()
        {
      
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

      
    }
}
