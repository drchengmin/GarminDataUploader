using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

namespace GarminDataUploader
{
    abstract class WebHelper
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

        static void WriteStream(Stream stream, string s)
        {
            var data = Encoding.UTF8.GetBytes(s);
            stream.Write(data, 0, data.Length);
        }

        public static byte[] CreateMultiBody(string boundary, Dictionary<string, string> props, KeyValuePair<string, string> file)
        {
            using (var stream = new MemoryStream())
            {
                foreach (string key in props.Keys)
                {
                    WriteStream(stream, string.Format("--{0}\r\n", boundary));
                    WriteStream(stream, string.Format("Content-Disposition: form-data; name=\"{0}\"\r\n\r\n", key));
                    WriteStream(stream, props[key]);
                    WriteStream(stream, "\r\n");
                }

                WriteStream(stream, string.Format("--{0}\r\n", boundary));
                string name = Path.GetFileName(file.Value);
                string ext = Path.GetExtension(file.Value).ToLowerInvariant();
                string contentType;
                if (ext == ".gpx" || ext == ".tcx")
                {
                    contentType = "application/xml";
                }
                else
                {
                    contentType = "application/octet-stream";
                }
                WriteStream(stream, string.Format("Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\n", file.Key, name));
                WriteStream(stream, string.Format("Content-Type: {0}\r\n\r\n", contentType));

                var data = File.ReadAllBytes(file.Value);
                stream.Write(data, 0, data.Length);
                WriteStream(stream, "\r\n");

                WriteStream(stream, string.Format("--{0}--", boundary));

                return stream.ToArray();
            }
        }

        public static string PostMultipartWebRequest(string url, string boundary, byte[] body)
        {
            var request = WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.ContentLength = body.Length;

            var dataStream = request.GetRequestStream();
            dataStream.Write(body, 0, body.Length);
            dataStream.Close();

            using (var response = request.GetResponse())
            {
                using (dataStream = response.GetResponseStream())
                {
                    using (var reader = new StreamReader(dataStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }
    }
}
