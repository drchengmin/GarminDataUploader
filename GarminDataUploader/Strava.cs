using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GarminDataUploader
{
    class Strava
    {
        const string StravaClientId = "2183";
        string m_accessToken;

        public string AccessToken
        {
            get { return m_accessToken; }
            set { m_accessToken = value; }
        }

        public void GetAccessToken()
        {
            string url = string.Format(
                "https://www.strava.com/oauth/authorize?" +
                "response_type=code&client_id={0}&redirect_uri={1}&state=StravaAuthorize&scope=write",
                StravaClientId,
                OAuthSignin.WebAuthHelperUrl);

            m_accessToken = OAuthSignin.GetAccessToken(url);
        }

        public DateTime GetLastWorkoutTimeStamp()
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
    }
}
