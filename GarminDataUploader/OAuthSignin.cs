using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GarminDataUploader
{
    class OAuthSignin
    {
        const string WebAuthHelperUrl = "https://psgarminuploader.azurewebsites.net/AuthRaStrava.cshtml";
        const string RunningAheadClientId = "354e8401381b40c1960c49e83a430a8d";

        public static string GetAccessToken(string url)
        {
            int width = 800;
            int height = 600;

            var form = new Form();
            form.Width = width;
            form.Height = height;

            var browser = new WebBrowser();
            browser.Width = width;
            browser.Height = height;
            browser.Url = new Uri(url);

            string authResponse = null;

            browser.DocumentCompleted += (object sender, WebBrowserDocumentCompletedEventArgs e) =>
            {
                if (browser.DocumentTitle.IndexOf("AuthorizationStatus") >= 0)
                {
                    authResponse = browser.Document.Body.OuterText;
                    form.Close();
                }
            };

            form.Controls.Add(browser);
            form.Shown += (object sender, EventArgs e) =>
                {
                    form.Activate();
                };

            form.ShowDialog();

            if (string.IsNullOrEmpty(authResponse))
            {
                throw new Exception("No authentication is performed");
            }

            Regex re = new Regex("\"access_token\":\"([^\"]+)\"");
            var matches = re.Match(authResponse);
            if (matches.Success)
            {
                return matches.Groups[1].Value;
            }
            else
            {
                throw new Exception("access_token not found in the response: " + authResponse);
            }
        }

        public static string GetRunningAheadAccessToken()
        {
            string url = string.Format(
                "https://www.runningahead.com/oauth2/authorize?response_type=code&client_id={0}&redirect_uri={1}&state=RaAuthorize",
                RunningAheadClientId,
                WebAuthHelperUrl);

            return GetAccessToken(url);
        }
    }
}
