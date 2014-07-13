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

        static void Main(string[] args)
        {
            // Sample code to test web service API
            string responseText = SendRequest("http://api.openweathermap.org/data/2.5/weather?q=Redmond,WA",
                "GET");
            
            Console.WriteLine("BODY: {0}", responseText);

            Regex weatherRegex = new Regex("\"weather\":\\[([^]]+)\\]");
            var match = weatherRegex.Match(responseText);
            Console.WriteLine(match.Groups[1].Value);
        }
    }
}
