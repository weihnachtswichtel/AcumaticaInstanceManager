using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AcumaticaInstanceManager
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0) {
                int listBuildsCommandIndex = Array.IndexOf(args, "--lb");
                if (listBuildsCommandIndex >= 0){
                    ListBuilds(args.Length > listBuildsCommandIndex+1 ? CheckMajorVersionInput(args[listBuildsCommandIndex+1]) : null);
                }
            }
        }

        private static string CheckMajorVersionInput(string majorVersionRaw)
        {
            string majorVersion = majorVersionRaw.ToUpper().Replace(" ", string.Empty).Replace("R", ".");
            if (!majorVersion.Contains("--") && majorVersion.Length < 7){
                Regex MajorVersion20Plus = new Regex(@"2[0-9].[1-2]", RegexOptions.IgnoreCase);
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
