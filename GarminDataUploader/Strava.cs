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
            string uri = "https://www.strava.com/api/v3/uploads";

            string ext = Path.GetExtension(filename);
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

            byte[] body = CreateMultiBody(boundary, props, new KeyValuePair<string,string>("file", filename));

            string response = PostMultipartWebRequest(uri, boundary, body);
            Console.WriteLine("Strava response: {0}", response);
        }

        void WriteStream(Stream stream, string s)
        {
            var data = Encoding.UTF8.GetBytes(s);
            stream.Write(data, 0, data.Length);
        }

        byte[] CreateMultiBody(string boundary, Dictionary<string, string> props, KeyValuePair<string, string> file)
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
                string ext = Path.GetExtension(file.Value);
                string contentType;
                if (ext == ".gpx" || ext == ".tcx")
                {
                    contentType = "application/xml";
                }
                else
                {
                    contentType = "application/octet-stream";
                }
                WriteStream(stream, string.Format("Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\n", file.Key, file.Value));
                WriteStream(stream, string.Format("Content-Type: {0}\r\n\r\n", contentType));

                var data = File.ReadAllBytes(file.Value);
                stream.Write(data, 0, data.Length);
                WriteStream(stream, "\r\n");

                WriteStream(stream, string.Format("--{0}--", boundary));

                return stream.ToArray();
            }
        }

        string PostMultipartWebRequest(string url, string boundary, byte[] body)
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
