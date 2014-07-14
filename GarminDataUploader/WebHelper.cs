using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GarminDataUploader
{
    class WebHelper
    {
        public static string SendRequest(string url, string method, out HttpStatusCode statusCode, byte[] body = null)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = method;

            if (body != null)
            {
                request.ContentLength = body.Length;
                request.ContentType = "application/x-www-form-urlencoded";

                var stream = request.GetRequestStream();
                stream.Write(body, 0, body.Length);
            }

            HttpWebResponse response = null;

            try
            {
                response = (HttpWebResponse)request.GetResponse();                
            }
            catch (WebException ex)
            {
                response = (HttpWebResponse)ex.Response;
            }

            Debug.Assert(response != null);

            try
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    using (var streamReader = new StreamReader(responseStream))
                    {
                        statusCode = response.StatusCode;
                        return streamReader.ReadToEnd();
                    }
                }
            }
            finally
            {
                response.Dispose();
            }                     
        }
    }
}
