using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace GarminDataUploader
{
    class Strava : WorkoutWebService
    {
        const string StravaClientId = "2183";
        
        public override void GetAccessToken()
        {
            string url = string.Format(
                "https://www.strava.com/oauth/authorize?" +
                "response_type=code&client_id={0}&redirect_uri={1}&state=StravaAuthorize&scope=write",
                StravaClientId,
                OAuthSignin.WebAuthHelperUrl);

            m_accessToken = OAuthSignin.GetAccessToken(url);
        }

        public override DateTime GetLastWorkoutTimeStamp()
        {
            string url = string.Format(
                "https://www.strava.com/api/v3/athlete/activities?per_page=1&access_token={0}",
                m_accessToken);

            HttpStatusCode statusCode;

            string response = WebHelper.SendRequest(url, "GET", out statusCode);
            Console.WriteLine("Response: {0}", response);
            Regex regex = new Regex("\"start_date\":\"([^\\s\"]+)\"");
            var matchDate = regex.Match(response);
            if (matchDate.Success)
            {
                return DateTime.Parse(matchDate.Groups[1].Value);
            }
            else
            {
                return DateTime.MinValue;
            }
        }

        public override void UploadWorkout(string filename)
        {
            var name = Path.GetFileName(filename);
            string uri = "http://www.strava.com/api/v3/uploads";

            string ext = Path.GetExtension(filename).ToLowerInvariant();
            if (ext == ".tcx")
            {
                ext = "tcx";
            }
            else if (ext == ".gpx")
            {
                ext = "gpx";
            }
            else if (ext == ".fit")
            {
                ext = "fit";
            }
            else
            {
                throw new Exception("Format " + ext + " is not supported");
            }

            string boundary = "----------" + Guid.NewGuid().ToString();
            var props = new Dictionary<string, string>();
            props["access_token"] = m_accessToken;
            props["external_id"] = name;
            props["data_type"] = ext;

            byte[] body = WebHelper.CreateMultiBody(boundary, props, new KeyValuePair<string,string>("file", filename));

            string response = WebHelper.PostMultipartWebRequest(uri, boundary, body);
            Console.WriteLine("Strava response: {0}", response);
        }
    }
}
