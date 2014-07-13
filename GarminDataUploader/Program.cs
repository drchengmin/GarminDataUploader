using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GarminDataUploader
{
    class Program
    {
        static string SendRequest(string url, string method)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = method;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                // Console.WriteLine("Status = {0} {1}", (int)response.StatusCode, response.StatusDescription);
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (var streamReader = new StreamReader(responseStream))
                    {
                        return streamReader.ReadToEnd();                        
                    }
                }
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            //string accessToken = OAuthSignin.GetRunningAheadAccessToken();
            //Console.WriteLine("Access Token: {0}", accessToken);

            string accessToken = "JFPzuptoAYD0nVEn0pvGv4";

            // Get the last workout
            string url = string.Format(
                "https://api.runningahead.com/rest/logs/me/workouts?access_token={0}&limit=1&fields=12,13",
                accessToken);
            string response = SendRequest(url, "GET");
            Console.WriteLine("Response: {0}", response);

            Regex reDate = new Regex("\"date\":\"([^\"]+)\"");
            Regex reTime = new Regex("\"time\":\"([^\"]+)\"");
            var matchDate = reDate.Match(response);
            var matchTime = reTime.Match(response);
            if (matchDate.Success && matchTime.Success)
            {
                string datetime = matchDate.Groups[1].Value + " " + matchTime.Groups[1].Value;
                var dt = DateTime.Parse(datetime);
                Console.WriteLine("DateTime of the last workout: {0}", dt.ToString("O"));
            }
            else
            {
                throw new Exception("date or time not found in the response: " + response);
            }

            /*
            // Sample code to test web service API
            string responseText = SendRequest("http://api.openweathermap.org/data/2.5/weather?q=Redmond,WA",
                "GET");
            
            Console.WriteLine("BODY: {0}", responseText);

            Regex weatherRegex = new Regex("\"weather\":\\[([^]]+)\\]");
            var match = weatherRegex.Match(responseText);
            Console.WriteLine(match.Groups[1].Value);
             * */
        }
    }
}
