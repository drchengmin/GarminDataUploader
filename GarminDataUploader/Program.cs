using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GarminDataUploader
{
    class Program
    {
        static void Main(string[] args)
        {
            // Sample code to test web service API
            string url = "http://api.openweathermap.org/data/2.5/weather?q=Redmond,WA";
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "GET";
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                Console.WriteLine("Status = {0} {1}", (int)response.StatusCode, response.StatusDescription);

                using (Stream responseStream = response.GetResponseStream())
                {
                    using (var streamReader = new StreamReader(responseStream))
                    {
                        string responseText = streamReader.ReadToEnd();
                        Console.WriteLine("BODY: {0}", responseText);
                    }
                }
            }
        }
    }
}
