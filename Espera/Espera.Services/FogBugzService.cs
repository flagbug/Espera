using Rareform.Validation;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace Espera.Services
{
    public static class FogBugzService
    {
        public static void SubmitCrashReport(string message, string stackTrace)
        {
            if (message == null)
                Throw.ArgumentNullException(() => message);

            if (stackTrace == null)
                Throw.ArgumentNullException(() => stackTrace);

            string url = "https://espera.fogbugz.com/scoutSubmit.asp";
            string userName = "Dennis Daume";
            string project = "Espera";
            string area = "CrashReports";
            string body = message + "\n\n" + stackTrace;

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/x-www-form-urlencoded";
            request.Method = "POST";

            string parameters = string.Empty;
            parameters += "Description=" + HttpUtility.UrlEncode(body);
            parameters += "&ScoutUserName=" + HttpUtility.UrlEncode(userName);
            parameters += "&ScoutProject=" + HttpUtility.UrlEncode(project);
            parameters += "&ScoutArea=" + HttpUtility.UrlEncode(area);
            parameters += "&ForceNewBug=" + "1";

            byte[] bytes = Encoding.ASCII.GetBytes(parameters);
            request.ContentLength = bytes.Length;

            using (Stream os = request.GetRequestStream())
            {
                os.Write(bytes, 0, bytes.Length);
            }

            using (request.GetResponse())
            { }
        }
    }
}