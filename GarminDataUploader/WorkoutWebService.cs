using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarminDataUploader
{
    /// <summary>
    ///     Web service for getting the timestamp of the last workout and upload new workout
    /// </summary>
    abstract class WorkoutWebService
    {
        /// <summary>
        ///     Access token used by OAuth web service
        /// </summary>
        protected string m_accessToken;

        public string AccessToken
        {
            get { return m_accessToken; }
            set { m_accessToken = value; }
        }

        /// <summary>
        ///     Gets the OAuth access token
        /// </summary>
        abstract public void GetAccessToken();

        /// <summary>
        ///     Gets the timestamp of the last uploaded workout
        /// </summary>
        abstract public DateTime GetLastWorkoutTimeStamp();

        /// <summary>
        ///     Uploads the workout specified by the full path
        /// </summary>
        abstract public void UploadWorkout(string fileName);
    }
}
