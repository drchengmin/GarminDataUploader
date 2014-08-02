using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Xml;
using Dynastream.Fit;

namespace GarminDataUploader
{
    class Program
    {
        /// <summary>
        ///     Registry key for saving the RunningAHEAD and Strava access tokens.
        /// </summary>
        const string AppRegistryKeyName = "GarminUploader";

        /// <summary>
        ///     Gets the timestamp of the FIT file, which is the start time of the first lap
        /// </summary>
        /// <param name="fileName">Full path of the FIT file</param>
        static System.DateTime GetFitTimestamp(string fileName)
        {
            // Saves the timestamp of every lap read from the file
            var fitLapTimestamps = new List<System.DateTime>();
            
            var mesgBroadcaster = new MesgBroadcaster();
            var decoder = new Decode();

            decoder.MesgEvent += (object sender, MesgEventArgs e) =>
            {
                mesgBroadcaster.OnMesg(sender, e);
            };

            mesgBroadcaster.LapMesgEvent += (object sender, MesgEventArgs e) =>
            {
                LapMesg mesg = (LapMesg)e.mesg;
                fitLapTimestamps.Add(mesg.GetStartTime().GetDateTime());
            };

            using (var fitSource = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                if (decoder.IsFIT(fitSource))
                {
                    if (decoder.CheckIntegrity(fitSource) && decoder.Read(fitSource))
                    {
                        var startTime = fitLapTimestamps[0];
                        // Converts the UTC time in FIT file to local time
                        return TimeZoneInfo.ConvertTimeFromUtc(startTime, TimeZoneInfo.Local);
                    }
                }
            }

            return System.DateTime.MinValue;
        }

        /// <summary>
        ///     Gets the timestamp of the TCX file, which is the start time of the first lap
        /// </summary>
        /// <param name="fileName">Full path of the TCX file</param>
        static System.DateTime GetTcxTimestamp(string fileName)
        {
            var doc = new XmlDocument();
            doc.Load(fileName);

            var namespaceManager = new XmlNamespaceManager(doc.NameTable);
            namespaceManager.AddNamespace("gm", "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2");

            var node = doc.SelectSingleNode("//gm:Lap", namespaceManager);
            if (node != null)
            {
                var dt = node.Attributes["StartTime"];
                return System.DateTime.Parse(dt.Value);
            }
            else
            {
                Console.WriteLine("Failed to find the start time of the first lap in file {0}", fileName);
                return System.DateTime.MinValue;
            }
        }

        /// <summary>
        ///     Gets the collection of Garmin data file store locations, including both old Garmin Communicator and
        ///     new Garmin Express software.
        /// </summary>
        static string[] GetGarminDataDirectories()
        {
            List<string> dirs = new List<string>();

            string appData = Environment.GetEnvironmentVariable("APPDATA");
            string garminDir = Path.Combine(appData, "Garmin\\Devices");
            try
            {
                string[] garminSubDirs = Directory.GetDirectories(garminDir, "*", SearchOption.TopDirectoryOnly);
                foreach (var subdir in garminSubDirs)
                {
                    dirs.Add(Path.Combine(subdir, "history"));
                    dirs.Add(Path.Combine(subdir, "activities"));
                }
            }
            catch (DirectoryNotFoundException)
            {
            }

            string programData = Environment.GetEnvironmentVariable("ProgramData");
            string garminDirNew = Path.Combine(programData, "Garmin\\GarminConnect");
            try
            {
                string[] garminSubDirsNew = Directory.GetDirectories(garminDirNew, "*", SearchOption.TopDirectoryOnly);
                foreach (var subdir in garminSubDirsNew)
                {
                    dirs.Add(Path.Combine(subdir, "FIT_TYPE_4"));
                }
            }
            catch (DirectoryNotFoundException)
            {
            }

            return dirs.ToArray();
        }

        /// <summary>
        ///     Reads the access token from the registry
        /// </summary>
        /// <param name="serverType">Either RunningAhead of Strava</param>
        /// <param name="accessToken">Access token being read</param>
        static void LoadAccessToken(string serverType, out string accessToken)
        {
            accessToken = null;

            using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey("Software"))
            {
                using (RegistryKey appKey = rootKey.OpenSubKey(AppRegistryKeyName))
                {
                    if (appKey != null)
                    {
                        accessToken = (string)appKey.GetValue(serverType);
                    }
                }
            }
        }

        /// <summary>
        ///     Saves the access token to the registry
        /// </summary>
        /// <param name="serverType">Either RunningAhead of Strava</param>
        /// <param name="accessToken">Access token to be saved</param>
        static void SaveAccessToken(string serverType, string accessToken)
        {
            using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey("Software", true))
            {
                using (RegistryKey appKey = rootKey.CreateSubKey(AppRegistryKeyName))
                {
                    appKey.SetValue(serverType, accessToken);
                }
            }
        }

        /// <summary>
        ///     Prints the program usage, command line arguments
        /// </summary>
        static void PrintUsage()
        {
            Console.WriteLine("USAGE: GarminDataUploader [RA | Strava] <File name>");
            Console.WriteLine("\r\nIf file name is specified, the program will upload the file directly.  Otherwise " +
                "Garmin data locations will be searched, and the data files that are newer than the last uploaded one " +
                "will be uploaded.  Then the program will monitor the directory and upload all new data files being " +
                "detected.");
        }

        /// <summary>
        ///     Main entry
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            string fileToUpload;

            if (args.Length == 0 || args.Length > 2)
            {
                PrintUsage();
                return;
            }
            else if (args.Length == 1)
            {
                fileToUpload = null;
            }
            else
            {
                fileToUpload = args[1];
            }

            WorkoutWebService workoutServer;
            string serverType;

            if (string.Compare(args[0], "ra", true) == 0)
            {
                workoutServer = new RunningAhead();
                serverType = "RunningAhead";
            }
            else if (string.Compare(args[0], "strava", true) == 0)
            {
                workoutServer = new Strava();
                serverType = "Strava";
            }
            else
            {
                PrintUsage();
                return;
            }

            string accessToken;
            LoadAccessToken(serverType, out accessToken);
            workoutServer.AccessToken = accessToken;
            
            if (string.IsNullOrEmpty(workoutServer.AccessToken))
            {
                workoutServer.GetAccessToken();
            }
            
            // Gets the timestamp of the last uploaded workout, so we can compare with the timestamp of the data files
            // on the disk to know which ones should be skipped.
            var lastUploadedWorkoutTime = workoutServer.GetLastWorkoutTimeStamp();
            Console.WriteLine("Last workout {0}", lastUploadedWorkoutTime);

            SaveAccessToken(serverType, workoutServer.AccessToken);

            string[] directories = GetGarminDataDirectories();

            if (fileToUpload != null)
            {
                // Uploads the single workout file and exits.

                if (!System.IO.File.Exists(fileToUpload))
                {
                    Console.WriteLine("File not found: " + fileToUpload);
                    return;
                }
                else
                {
                    workoutServer.UploadWorkout(fileToUpload);
                }
            }
            else
            {
                // Searches all the directories and finds out which one has the data files

                foreach (string directory in directories)
                {
                    string[] files = null;
                    try
                    {
                        files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        continue;
                    }

                    foreach (string file in files)
                    {
                        // Skips the non-supported workout file
                        string extension = Path.GetExtension(file).ToLowerInvariant();
                        if (extension != ".tcx" && extension != ".fit")
                        {
                            continue;
                        }

                        // Skips the workout that are older than the last uploaded one
                        if (System.IO.File.GetLastWriteTime(file) < lastUploadedWorkoutTime)
                        {
                            continue;
                        }

                        if (extension == ".tcx")
                        {
                            if (GetTcxTimestamp(file) > lastUploadedWorkoutTime)
                            {
                                workoutServer.UploadWorkout(file);
                            }
                        }
                        else
                        {
                            if (GetFitTimestamp(file) > lastUploadedWorkoutTime)
                            {
                                workoutServer.UploadWorkout(file);
                            }
                        }
                    }

                    string fileType;
                    if (directory.EndsWith("history"))
                    {
                        fileType = "*.tcx";
                    }
                    else
                    {
                        fileType = "*.fit";
                    }

                    // Now starts to monitor the directory for new files and upload them
                    var watcher = new FileSystemWatcher(directory, fileType);
                    watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

                    while (true)
                    {
                        var changedResult = watcher.WaitForChanged(WatcherChangeTypes.Created);
                        Console.WriteLine("Find a new file {0}", changedResult.Name);

                        // When the file just shows up, it maybe in use by the Garmin device software (e.g.
                        // uploading to Garmin Connect website).  So we wait for 60 seconds and try to upload.
                        Thread.Sleep(TimeSpan.FromSeconds(60));

                        // Combines the directory name with the file name to get the full path
                        var fileName = Path.Combine(directory, changedResult.Name);

                        // Uploads the workout file
                        workoutServer.UploadWorkout(fileName);
                        Console.WriteLine("File {0} has been uploaded", fileName);
                    }
                }
            }
        }
    }
}
