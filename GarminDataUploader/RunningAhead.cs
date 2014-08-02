using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace GarminDataUploader
{
    /// <summary>
    ///     RunningAHEAD web service to get the timestamp of the last workout and upload new workout
    /// </summary>
    class RunningAhead : WorkoutWebService
    {
        const string RunningAheadClientId = "354e8401381b40c1960c49e83a430a8d";
        
        public override void GetAccessToken()
        {
            string url = string.Format(
                "https://www.runningahead.com/oauth2/authorize?response_type=code&client_id={0}&redirect_uri={1}&state=RaAuthorize",
                RunningAheadClientId,
                OAuthSignin.WebAuthHelperUrl);

            m_accessToken = OAuthSignin.GetAccessToken(url);
        }

        public override DateTime GetLastWorkoutTimeStamp()
        {
            // Get the last workout
            string url = string.Format(
                "https://api.runningahead.com/rest/logs/me/workouts?access_token={0}&limit=1&fields=12,13",
                m_accessToken);
            HttpStatusCode statusCode;
            string response = WebHelper.SendRequest(url, "GET", out statusCode);
            Console.WriteLine("Response: {0}", response);

            Regex reDate = new Regex("\"date\":\"([^\"]+)\"");
            Regex reTime = new Regex("\"time\":\"([^\"]+)\"");
            var matchDate = reDate.Match(response);
            var matchTime = reTime.Match(response);
            if (matchDate.Success && matchTime.Success)
            {
                string datetime = matchDate.Groups[1].Value + " " + matchTime.Groups[1].Value;
                return DateTime.Parse(datetime);
            }
            else
            {
                return DateTime.MinValue;
            }
        }

        public override void UploadWorkout(string filename)
        {
            string extension = Path.GetExtension(filename).ToLowerInvariant();

            if (extension == ".tcx" || extension == ".fit" || extension == ".gpx")
            {
                extension = extension.Substring(1);
            }
            else
            {
                throw new Exception("File type " + extension + " is not supported");
            }

            string url = string.Format(
                "https://api.runningahead.com/rest/logs/me/workouts/{0}?access_token={1}",
                extension,
                m_accessToken);

            byte[] data = File.ReadAllBytes(filename);
            HttpStatusCode statusCode;
            string response = WebHelper.SendRequest(url, "POST", out statusCode, data);

            if (statusCode != HttpStatusCode.OK)
            {
                Console.WriteLine("Failed to import '{0}' to RunningAhead.", filename);
                Console.WriteLine("Response {0}: {1}", statusCode, response);
            }
        }
    }
}
