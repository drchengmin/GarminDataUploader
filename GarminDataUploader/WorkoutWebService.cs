using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarminDataUploader
{
    abstract class WorkoutWebService
    {
        protected string m_accessToken;

        public string AccessToken
        {
            get { return m_accessToken; }
            set { m_accessToken = value; }
        }

        abstract public void GetAccessToken();
        abstract public DateTime GetLastWorkoutTimeStamp();
        abstract public void UploadWorkout(string fileName);
    }
}
