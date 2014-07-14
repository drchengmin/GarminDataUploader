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


        [STAThread]
        static void Main(string[] args)
        {
            RunningAhead ra = new RunningAhead();
            //ra.GetAccessToken();
            ra.AccessToken = "JFPzuptoAYD0nVEn0pvGv4";
            var dt = ra.GetLastWorkoutTimeStamp();
            Console.WriteLine("Last workout {0}", dt);

            if (args.Length == 1)
            {
                // Uploads the specified file to RunningAhead
                string filename = args[0];
                if (!File.Exists(filename))
                {
                    Console.WriteLine("File not found: " + filename);
                    return;
                }
                else
                {
                    ra.UploadWorkout(filename);
                }
            }
        }
    }
}
