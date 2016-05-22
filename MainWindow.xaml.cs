using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace YandereSimLauncher {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        private const string BLOG_LINK = "http://yanderesimulator.com/";
        private const string VERSION_URL = "http://yanderesimulator.com/version.txt";
        private const string ZIP_URL = "http://yanderesimulator.com/latest.zip";
        private const string NEWS_URL = "https://public-api.wordpress.com/rest/v1.1/sites/yanderedev.wordpress.com/posts/";
        private const string ZIP_NAME = "content.zip";

        private static Dictionary<string, string> SocialLinks = new Dictionary<string, string>() {
            { "wordpress",  "http://yanderedev.wordpress.com/" },
            { "youtube",    "https://www.youtube.com/channel/UC1EBJfK7ltjYUFyzysKxr1g" },
            { "twitter",    "https://twitter.com/YandereDev" },
            { "patreon",    "https://www.patreon.com/YandereDev?ty=h" },
            { "tumblr",     "http://yanderedev.tumblr.com/" }
        };

        private WebClient webClient;
        private string gamePath;
        private int newGameVersion;
        private Thread launcherThread;
        private bool isAppClosed;

        //private List<Post> posts;

        public MainWindow() {
            InitializeComponent();
            webClient = new WebClient();
            gamePath = AppDomain.CurrentDomain.BaseDirectory;

            //Uncomment this for release version
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                var box = MessageBox.Show("If you see this window second time, please report a bug on 'http://yanderesimulator.com/contact.html' and attach 'launcherCrashDump.txt that is next to this executable''", "Fatal error");
                using (var writer = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "launcherCrashDump.txt", true)) {
                    writer.WriteLine(DateTime.Today.ToShortDateString() + " at " + DateTime.Today.ToShortTimeString());
                    writer.WriteLine(e.ExceptionObject.ToString());
                    writer.WriteLine("---------------------------------------\n");
                }
                Process.Start(AppDomain.CurrentDomain.BaseDirectory);
                Process.Start("http://yanderesimulator.com/contact.html");
                this.Close();
            };
        }

        #region Events
        private void WindowMouseDown(object sender, MouseButtonEventArgs e) {
            try {
                DragMove();
            } catch { }
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e) {
            launcherThread.Abort();
            isAppClosed = true;
            this.Close();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e) {
            WindowState = WindowState.Minimized;
        }

        private void OnSocialButtonClick(object sender, MouseButtonEventArgs e) {
            var netw = ((Image)sender).Name.Split('_')[1];
            Process.Start(SocialLinks[netw]);
        }

        private void OnPlayButtonClick(object sender, RoutedEventArgs e) {
            Process.Start(gamePath + "YandereSimulator.exe");
            launcherThread.Abort();
            isAppClosed = true;
            this.Close();
        }

        private void OnRedownloadClick(object sender, RoutedEventArgs e) {
            try {
                RedownloadButton.IsEnabled = false;
                PlayButton.IsEnabled = false;
                CleanUp();
                Thread t = new Thread(StartCheckingInternetConnection);
                t.Start();
            } catch { }
        }
        #endregion

        #region Static
        public static string GetData(string url) {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            WebRequest reqGET = WebRequest.Create(url);
            WebResponse resp = reqGET.GetResponse();
            Stream stream = resp.GetResponseStream();
            StreamReader sr = new StreamReader(stream);
            string html = sr.ReadToEnd();
            return html;
        }

        public static void DeleteDirectory(string target_dir) {
            string[] files = Directory.GetFiles(target_dir);
            string[] dirs = Directory.GetDirectories(target_dir);

            foreach (string file in files) {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs) {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        public static string BytesToMega(long bytes) {
            return (bytes / 1000000.0).ToString("0.00");
        }
        #endregion

        #region Animations
        private void OnButtonHover(object sender, MouseEventArgs e) {
            ScaleTransform rt = new ScaleTransform();
            Image obj = (Image)sender;
            obj.RenderTransform = rt;
            obj.RenderTransformOrigin = new Point(0.5, 0.5);
            DoubleAnimation da = new DoubleAnimation(1, 1.25, new Duration(TimeSpan.FromSeconds(0.2)));
            da.FillBehavior = FillBehavior.HoldEnd;

            rt.BeginAnimation(ScaleTransform.ScaleXProperty, da);
            rt.BeginAnimation(ScaleTransform.ScaleYProperty, da);
        }

        private void OnButtonLeave(object sender, MouseEventArgs e) {
            ScaleTransform rt = new ScaleTransform();
            Image obj = (Image)sender;
            obj.RenderTransform = rt;
            obj.RenderTransformOrigin = new Point(0.5, 0.5);
            DoubleAnimation da = new DoubleAnimation(1.25, 1, new Duration(TimeSpan.FromSeconds(0.2)));
            da.FillBehavior = FillBehavior.Stop;

            rt.BeginAnimation(ScaleTransform.ScaleXProperty, da);
            rt.BeginAnimation(ScaleTransform.ScaleYProperty, da);
        }

        private void OnTextLeave(object sender, MouseEventArgs e) {
            var block = (TextBlock)sender;
            block.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
        }

        private void OnTextEnter(object sender, MouseEventArgs e) {
            var text = (TextBlock)sender;
            text.Foreground = new SolidColorBrush(Color.FromArgb(255, 127, 13, 100));
        }

        private void OnPostTitleClick(object sender, MouseButtonEventArgs e) {
            var block = (TextBlock)sender;
            block.Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 125, 205));
        }

        private void OnHeadTitleClick(object sender, MouseButtonEventArgs e) {
            try {
                var block = (TextBlock)sender;
                var parts = block.Name.Split('_');
                if(block.Name == "headText_Blog") {
                    Process.Start(SocialLinks["wordpress"]);
                } else if (parts.Length == 2) {
                    Process.Start(BLOG_LINK + parts[1]);
                } else Process.Start(BLOG_LINK + parts[1] + "-" + parts[2]);
            } catch (ArgumentOutOfRangeException) { }
        }
        #endregion

        #region Logic
        private bool IsGameUpToDate() {
            try {
                var serverVersion = GetData(VERSION_URL);
                newGameVersion = int.Parse(serverVersion);
            } catch {
                
            }

            try {
                var executable = Directory.EnumerateFiles(gamePath, "YandereSimulator.exe").ElementAt(0);
                var content = Directory.EnumerateDirectories(gamePath, "YandereSimulator_Data").ElementAt(0);
                var clientVersion = File.ReadAllText(content + "\\version");

                if (newGameVersion > int.Parse(clientVersion)) {
                    CleanUp();
                    return false;
                } else return true;
            } catch {
                CleanUp();
                return false;
            }
        }

        private void CleanUp() {
            var executable = Directory.EnumerateFiles(gamePath, "YandereSimulator.exe");
            var content = Directory.EnumerateDirectories(gamePath, "YandereSimulator_Data");
            if (executable.Any()) File.Delete(executable.ElementAt(0));
            if (content.Any()) DeleteDirectory(content.ElementAt(0));
        }

        private void DownloadGameContents() {
            if (File.Exists(gamePath + ZIP_NAME)) File.Delete(gamePath + ZIP_NAME);
            webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(UpdateProgressBar);
            webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadDataCompleted);
            webClient.DownloadFileAsync(new Uri(ZIP_URL), gamePath + ZIP_NAME);
        }

        private void DownloadDataCompleted(object sender, AsyncCompletedEventArgs e) {
            CleanUp();
            webClient.DownloadProgressChanged -= new DownloadProgressChangedEventHandler(UpdateProgressBar);
            webClient.DownloadFileCompleted -= new AsyncCompletedEventHandler(DownloadDataCompleted);

            GetData("http://simplehitcounter.com/hit.php?uid=2101053&f=16777215&b=0");
            ReportStatus("Extracting...");
            try {
                using (var arc = ZipFile.Read(gamePath + ZIP_NAME)) {
                    arc.ExtractAll(gamePath, ExtractExistingFileAction.OverwriteSilently);
                }
                File.Delete(gamePath + ZIP_NAME);
            } catch {
                ReportStatus("Can't finish extracting");
                Dispatcher.Invoke(new Action(() => {
                    RedownloadButton.IsEnabled = true;
                    PlayButton.IsEnabled = false;
                }));
            }
            try {
                File.WriteAllText(gamePath + "YandereSimulator_Data\\" + "version", newGameVersion.ToString());
            } catch {
                Dispatcher.Invoke(new Action(() => {
                    RedownloadButton.IsEnabled = true;
                    PlayButton.IsEnabled = false;
                }));
                MessageBox.Show("Validation error happend while installing. However, the game was successfully downloaded and extracted. You can try starting it manually", "Version validator");
                Process.Start(gamePath);
            }
            SetReadyStatus();
        }

        private void UpdateProgressBar(object sender, DownloadProgressChangedEventArgs e) {
            ReportProgress(e.ProgressPercentage);
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

        private void StartCheckingInternetConnection() {
            ReportStatus("Checking server connection...");
            try {
                GetData(BLOG_LINK);
                Thread t = new Thread(StartCheckThread);
                t.Start();
            } catch (WebException) {
                ReportStatus("Can't connect to update server");
                try {
                    var executable = Directory.EnumerateFiles(gamePath, "YandereSimulator.exe").ElementAt(0);
                    var content = Directory.EnumerateDirectories(gamePath, "YandereSimulator_Data").ElementAt(0);
                    Dispatcher.Invoke(new Action(() => {
                        PlayButton.IsEnabled = true;
                    }));
                } catch {
                    Dispatcher.Invoke(new Action(() => {
                        RedownloadButton.IsEnabled = false;
                        PlayButton.IsEnabled = false;
                    }));
                }
                Thread.Sleep(5000);
                if (!isAppClosed) {
                    launcherThread = new Thread(StartCheckingInternetConnection);
                    launcherThread.Start();
                }
            }
        }

        private void ReportProgress(int progress) {
            Dispatcher.Invoke(new Action(() => {
                ProgressBar.Value = progress;
            }));
        }

        private void ReportStatus(string status) {
            Dispatcher.Invoke(new Action(() => {
                DownloadStatus.Text = status;
            }));
        }

        private void SetReadyStatus() {
            Dispatcher.Invoke(new Action(() => {
                PlayButton.IsEnabled = true;
                RedownloadButton.IsEnabled = true;
                DownloadStatus.Text = "Game is up-to-date";
                ProgressBar.Value = 100;
            }));
        }
        #endregion

        /// <summary>
        /// MAIN PROCEDURE
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowLoaded(object sender, RoutedEventArgs e) {
            RedownloadButton.IsEnabled = false;
            PlayButton.IsEnabled = false;
            launcherThread = new Thread(StartCheckingInternetConnection);
            launcherThread.Start();

            //Test
            //throw new OutOfMemoryException();
        }
    }
}
