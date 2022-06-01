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
    }
}
