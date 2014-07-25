﻿using Microsoft.Win32;
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
        const string RunningAheadRegKey = "RunningAhead Access Token";
        const string AppRegistryKeyName = "GarminUploader";

        static System.DateTime GetFitTimestamp(string fileName)
        {
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

        static string LoadAccessToken()
        {
            using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey("Software"))
            {
                using (RegistryKey appKey = rootKey.OpenSubKey(AppRegistryKeyName))
                {
                    if (appKey == null)
                    {
                        return string.Empty;
                    }
                    else
                    {
                        return (string)appKey.GetValue(RunningAheadRegKey);
                    }
                }
            }
        }

        static void SaveAccessToken(string token)
        {
            using (RegistryKey rootKey = Registry.CurrentUser.OpenSubKey("Software", true))
            {
                using (RegistryKey appKey = rootKey.CreateSubKey(AppRegistryKeyName))
                {
                    appKey.SetValue(RunningAheadRegKey, token);
                }
            }
        }

        [STAThread]
        static void Main(string[] args)
        {
            RunningAhead ra = new RunningAhead();
            
            ra.AccessToken = LoadAccessToken();
            if (string.IsNullOrEmpty(ra.AccessToken))
            {
                ra.GetAccessToken();
            }

            var lastUploadedWorkoutTime = ra.GetLastWorkoutTimeStamp();
            Console.WriteLine("Last workout {0}", lastUploadedWorkoutTime);

            SaveAccessToken(ra.AccessToken);

            string[] directories = GetGarminDataDirectories();

            if (args.Length == 1)
            {
                // Uploads the specified file to RunningAhead
                string filename = args[0];
                if (!System.IO.File.Exists(filename))
                {
                    Console.WriteLine("File not found: " + filename);
                    return;
                }
                else
                {
                    ra.UploadWorkout(filename);
                }
            }
            else if (args.Length == 0)
            {
                foreach (string directory in directories)
                {
                    string[] files = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
                    foreach (string file in files)
                    {
                        // Skips the non-supported workout file
                        string extension = Path.GetExtension(file);
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
                                ra.UploadWorkout(file);
                            }
                        }
                        else
                        {
                            if (GetFitTimestamp(file) > lastUploadedWorkoutTime)
                            {
                                ra.UploadWorkout(file);
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
                        ra.UploadWorkout(fileName);
                        Console.WriteLine("File {0} has been uploaded", fileName);
                    }
                }
            }
        }
    }
}
