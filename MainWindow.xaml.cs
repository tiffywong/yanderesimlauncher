using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace YandereSimLauncher {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private const string VERSION_URL = "http://yanderesimulator.com/version.txt";
        private const string ZIP_URL = "http://yanderesimulator.com/latest.zip";
        private const string ZIP_NAME = "content.zip";

        private WebClient webClient;
        private string gamePath;

        public MainWindow() {
            InitializeComponent();
            webClient = new WebClient();
            gamePath = AppDomain.CurrentDomain.BaseDirectory;
            /*AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                MessageBox.Show("Unexpected error happend. If you see this window second time, please email to 'gleb.noxcaos@gmail.com' and attach 'launcherCrashDump.txt that is next to this executable''", "Fatal error");
                using (var writer = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "launcherCrashDump.txt", true)) {
                    writer.WriteLine(DateTime.Today.ToShortDateString() + " at " + DateTime.Today.ToShortTimeString());
                    writer.WriteLine(e.ExceptionObject.ToString());
                    writer.WriteLine("---------------------------------------\n");
                }
                this.Close();
            };*/
        }

        private void WindowMouseDown(object sender, MouseButtonEventArgs e) {
            try {
                DragMove();
            } catch { }
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e) {
            this.Close();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        public static string GetData(string url) {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            WebRequest reqGET = WebRequest.Create(url);
            WebResponse resp = reqGET.GetResponse();
            Stream stream = resp.GetResponseStream();
            StreamReader sr = new StreamReader(stream);
            string html = sr.ReadToEnd();
            return html;
        }

        public static int BytesToMega(long bytes) {
            return (int)bytes / 1000000;
        }

        private bool IsGameUpToDate() {
            try {
                var executable = Directory.EnumerateFiles(gamePath, "YandereSim*.exe").ElementAt(0);
                var content = Directory.EnumerateDirectories(gamePath, "YandereSim*_Data").ElementAt(0);
                var serverVersion = GetData(VERSION_URL);
                var clientVersion = File.ReadAllText(content + "version");
                Debug.WriteLine(serverVersion);
                if (int.Parse(serverVersion) > int.Parse(clientVersion)) {
                    CleanUp();
                    return false;
                } else return true;
            } catch {
                CleanUp();
                return false;
            }
        }

        private void CleanUp() {
            var executable = Directory.EnumerateFiles(gamePath, "YandereSim*.exe");
            var content = Directory.EnumerateDirectories(gamePath, "YandereSim*_Data");
            if (executable.Any()) File.Delete(executable.ElementAt(0));
            if (content.Any()) Directory.Delete(content.ElementAt(0));
        }

        private void DownloadGameContents() {
            if (File.Exists(gamePath + ZIP_NAME)) File.Delete(gamePath + ZIP_NAME);
            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(UpdateProgressBar);
            webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadDataCompleted);
            webClient.DownloadFileAsync(new Uri(ZIP_URL), gamePath + ZIP_NAME);
        }

        private void DownloadDataCompleted(object sender, AsyncCompletedEventArgs e) {
            CleanUp();

            using (var arc = ZipFile.Read(gamePath + ZIP_NAME)) {
                arc.ExtractAll(gamePath, ExtractExistingFileAction.OverwriteSilently);
            }
            File.Delete(gamePath + ZIP_NAME);

            SetReadyStatus();
        }

        private void UpdateProgressBar(object sender, DownloadProgressChangedEventArgs e) {
            ProgressBar.Value = e.ProgressPercentage;
            ReportStatus(string.Format("Downloaded {0}mb of {1}mb", 
                BytesToMega(e.BytesReceived),
                BytesToMega(e.TotalBytesToReceive)
                ));
        }

        private void StartCheckThread() {
            ReportStatus("Checking game version...");
            if (!IsGameUpToDate()) {
                ReportStatus("Starting download");
                DownloadGameContents();
            } else {
                SetReadyStatus();
            }
        }

        private void ReportStatus(string status) {
            Dispatcher.Invoke(new Action(() => { DownloadStatus.Text = status; }));
        }

        private void SetReadyStatus() {
            Dispatcher.Invoke(new Action(() => {
                PlayButton.IsEnabled = true;
                DownloadStatus.Text = "Ready";
            }));
        }

        /// <summary>
        /// MAIN PROCEDURE
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowLoaded(object sender, RoutedEventArgs e) {
            Thread t = new Thread(StartCheckThread);
            t.Start();
        }
    }
}
