using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace KWire
{
    static class Logfile
    {
        //static int LinesWritten; Deprecated when running as service. 

        public static void Init()
        {
            

            MainMenu();
            Write("Logfile initiated");
            DeleteOld();
        }


        public static void Write (string logmessage)
        {
            var ProgramStart = DateTime.Now;
            string Date = ProgramStart.ToString("d");
            string LogFileName = "KWire_" + Date + ".log";
            string ProgramPath = AppDomain.CurrentDomain.BaseDirectory;
            string logfile = ProgramPath + @LogFileName;

            //string cleanedLogmessage = logmessage.Replace("\n", "").Replace("\r", "");

            //write to logfile. 
            var CurrentTime = DateTime.Now;
            string Time = CurrentTime.ToString("HH:mm:ss:fff");
            string WriteToLog = Time + " :: " + logmessage;

            string toLog = WriteToLog.Trim();

            try
            {
                File.AppendAllText(logfile, toLog + Environment.NewLine);

                
            }
            catch (Exception error)
            {
                Console.WriteLine("Could not write to logfile ATM!");
                Console.WriteLine(error.Message);
            }

            Console.WriteLine(WriteToLog);

        }
        public static void DeleteOld()
        {
            string ProgramPath = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo dir = new DirectoryInfo(ProgramPath);
            DateTime testDate = DateTime.Now.AddDays(-15);
            foreach (FileInfo f in dir.GetFiles())
            {
                DateTime fileAge = f.LastWriteTime;
                var FileExt = f.Extension;
                if ((fileAge < testDate) && (FileExt == ".log"))
                {
                    Write("File " + f.Name + " is older than 15 days, deleted...");
                    File.Delete(f.FullName);
                }

            }
        }
        public static void MainMenu()
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            //Console.WriteLine(version); // -> "1.1.2.10"
            //Version v = Assembly.GetExecutingAssembly().GetName().Version;
            //string About = string.Format(CultureInfo.InvariantCulture, @"Mandozzi Communication Server Service {0}.{1}.{2} (r{3})", v.Major, v.Minor, v.Build, v.Revision);
            Write("#####################################################################");
            Write("KWire AES67 to AutoCam .NET CORE v " + version + " by Kristoffer L-S");
            Write("#####################################################################");
            Write("");
            Write("Please report errors to kristoffer@nrk.no");
            Write("");
        }
    }
}
