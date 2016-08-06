/// Yandere Simulator Launcher
/// By NoxCaos (http://noxcaos.github.io)
/// for 'Yandere Simulator' project
/// 
/// The launcher is available on GitHub 
/// by request and permission from YandereDeveloper.
/// 
/// This code is distributed under GPL license
/// 

using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

        private const string BASE_LINK =    "http://yanderesimulator.com/";
        private const string URLS_LINK =    "http://yanderesimulator.com/urls.txt";
        //private const string URLS_LINK =    "http://localhost:8080/urls.txt";
        private const string NEWS_URL =     "https://public-api.wordpress.com/rest/v1.1/sites/yanderedev.wordpress.com/posts/";
        private const string ZIP_NAME =     "content.zip";
        private const int VERSION =         2;

        private enum GameStatus { Updated, Outdated, NotDownloaded, ContentError }

        private enum LinkType {
            version, newlauncher, launcher,
            wordpress, youtube, twitter, twitch,
            volunteer, contact, about, donate
        }

        private Dictionary<LinkType, string> Links = new Dictionary<LinkType, string>() {
            { LinkType.version,     "http://yanderesimulator.com/version.txt" },
            { LinkType.launcher,    "http://yanderesimulator.com/launcherversion.txt" },
            { LinkType.newlauncher, "http://yanderesimulator.com/download/" },

            { LinkType.wordpress,   "http://yanderedev.wordpress.com/" },
            { LinkType.youtube,     "https://www.youtube.com/channel/UC1EBJfK7ltjYUFyzysKxr1g" },
            { LinkType.twitter,     "https://www.twitter.com/YandereDev" },
            { LinkType.twitch,      "https://www.twitch.tv/yanderedev" },

            { LinkType.volunteer,   "http://yanderesimulator.com/volunteer/" },
            { LinkType.contact,     "http://yanderesimulator.com/contact/" },
            { LinkType.about,       "http://yanderesimulator.com/about/" },
            { LinkType.donate,      "http://yanderesimulator.com/donate/" },
        };

        private List<string> contentLinks = new List<string>();

        private WebClient webClient;
        private string gamePath;
        private int newGameVersion;
        private int curLink;
        private Thread launcherThread;
        private bool isAppClosed;
        private bool isLinkUpdated;

        //private List<Post> posts;

        public MainWindow() {
            InitializeComponent();
            webClient = new WebClient();
            gamePath = AppDomain.CurrentDomain.BaseDirectory;

            //Uncomment this for release version
            AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                var box = MessageBox.Show("If you see this window second time, please report a bug on 'gleb.noxcaos@gmail.com' and attach 'launcherCrashDump.txt that is next to this executable''", "Fatal error");
                using (var writer = new StreamWriter(AppDomain.CurrentDomain.BaseDirectory + "launcherCrashDump.txt", true)) {
                    writer.WriteLine(DateTime.Today.ToShortDateString() + " at " + DateTime.Today.ToShortTimeString());
                    writer.WriteLine(e.ExceptionObject.ToString());
                    writer.WriteLine("---------------------------------------\n");
                }
                Process.Start(AppDomain.CurrentDomain.BaseDirectory);
                Process.Start("mailto:gleb.noxcaos@gmail.com");
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
            Process.Start(Links[GetLinkType(netw)]);
        }

        private void OnPlayButtonClick(object sender, RoutedEventArgs e) {
            try {
                Process.Start(gamePath + "YandereSimulator.exe");
                launcherThread.Abort();
                isAppClosed = true;
                Close();
            } catch (Win32Exception) { ReportStatus("Can't launch the game"); }
        }

        private void OnRedownloadClick(object sender, RoutedEventArgs e) {
            try {
                RedownloadButton.IsEnabled = false;
                PlayButton.IsEnabled = false;
                //CleanUp();
                var t = new Thread(() => StartCheckThread(true));
                t.Start();
            } catch { }
        }

        private void OnHeadTitleClick(object sender, MouseButtonEventArgs e) {
            try {
                var block = (TextBlock)sender;
                var parts = block.Name.Split('_');
                if (block.Name == "headText_Blog") {
                    Process.Start(Links[LinkType.wordpress]);
                } else {
                    Process.Start(Links[GetLinkType(parts[1])]);
                }
            } catch (ArgumentOutOfRangeException) {
                //Error in block name
            } catch(ArgumentException) {
                //The string is not convertable to enum
            }
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

        private static LinkType GetLinkType(string str) {
            return (LinkType) Enum.Parse(typeof(LinkType), str.Trim().ToLower());
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

        
        #endregion

        #region Logic
        private GameStatus GetGameStatus() {
            try {
                var serverVersion = GetData(Links[LinkType.version]);
                newGameVersion = int.Parse(serverVersion);
            } catch {
                ReportStatus("Server error. Can't check version");
            }

            try {
                var executable = Directory.EnumerateFiles(gamePath, "YandereSimulator.exe").ElementAt(0);
                var content = Directory.EnumerateDirectories(gamePath, "YandereSimulator_Data").ElementAt(0);
                var clientVersion = File.ReadAllText(content + "\\version");

                if (newGameVersion > int.Parse(clientVersion))
                    return GameStatus.Outdated;
                else return GameStatus.Updated;

            } catch { return GameStatus.NotDownloaded; }
        }

        private void CleanUp() {
            try {
                var executable = Directory.EnumerateFiles(gamePath, "YandereSimulator.exe");
                var content = Directory.EnumerateDirectories(gamePath, "YandereSimulator_Data");
                if (executable.Any()) File.Delete(executable.ElementAt(0));
                if (content.Any()) DeleteDirectory(content.ElementAt(0));
            } catch (IOException) {
                MessageBox.Show("Looks like you're running Yandere Simulator. Please close the game and try again.", "Ooops!");
                Dispatcher.Invoke(new Action(() => { Close(); }));
            }
        }

        private void DownloadGameContents() {
            if (contentLinks.Count > curLink) {
                ReportStatus("Starting download");

                if (File.Exists(gamePath + ZIP_NAME)) File.Delete(gamePath + ZIP_NAME);
                try {
                    webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(UpdateProgressBar);
                    webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadDataCompleted);
                    webClient.DownloadFileAsync(new Uri(contentLinks[curLink]), gamePath + ZIP_NAME);
                } catch (SocketException) {
                    curLink++;
                    if (launcherThread != null & launcherThread.IsAlive) launcherThread.Abort();
                    launcherThread = new Thread(StartCheckingInternetConnection);
                    launcherThread.Start();
                }
            } else {
                SetServerUnavailableStatus("Can't reach download source");
                MessageBox.Show("Can't reach download source. Maybe it is unavailable now or your antivirus blocks the connection", "Ooops!");
            }
        }

        private void DownloadDataCompleted(object sender, AsyncCompletedEventArgs e) {
            CleanUp();
            webClient.DownloadProgressChanged -= new DownloadProgressChangedEventHandler(UpdateProgressBar);
            webClient.DownloadFileCompleted -= new AsyncCompletedEventHandler(DownloadDataCompleted);

            try { GetData("http://simplehitcounter.com/hit.php?uid=2101053&f=16777215&b=0"); } catch { }
            ReportStatus("Extracting...");
            try {
                using (var arc = ZipFile.Read(gamePath + ZIP_NAME)) {
                    arc.ExtractAll(gamePath, ExtractExistingFileAction.OverwriteSilently);
                }
                File.Delete(gamePath + ZIP_NAME);
            } catch {
                ReportStatus("Error. Retrying...");
                curLink++;
                if (launcherThread != null & launcherThread.IsAlive) launcherThread.Abort();
                launcherThread = new Thread(StartCheckingInternetConnection);
                launcherThread.Start();
                return;
            }

            try {
                File.WriteAllText(gamePath + "YandereSimulator_Data\\" + "version", newGameVersion.ToString());
                SetReadyStatus();
            } catch {
                Dispatcher.Invoke(new Action(() => {
                    RedownloadButton.IsEnabled = true;
                    PlayButton.IsEnabled = false;
                }));
                MessageBox.Show("Seems like server has damaged files. We already working at it! Please, try downloading again in an hour", "Error");
            }
        }

        private void UpdateProgressBar(object sender, DownloadProgressChangedEventArgs e) {
            ReportProgress(e.ProgressPercentage);
            ReportStatus(string.Format("Downloaded {0}mb of {1}mb", 
                BytesToMega(e.BytesReceived),
                BytesToMega(e.TotalBytesToReceive)
                ));
        }

        private void StartCheckThread(bool force = false) {
            ReportStatus("Checking game version...");

            var status = GetGameStatus();
            if (force && status == GameStatus.Outdated) {
                DownloadGameContents();
                return;
            }

            switch (status) {
                case GameStatus.NotDownloaded:  DownloadGameContents();     break;
                case GameStatus.Outdated:       SetReadyToUpdateStatus();   break;
                case GameStatus.Updated:        SetReadyStatus();           break;
                default:                        DownloadGameContents();     break;
            }
        }

        private void UpdateLinks() {
            try {
                var links = GetData(URLS_LINK).Split('\n');

                foreach (var l in links) {
                    try {
                        var lnk = l.Split('"');

                        if (lnk[0].Trim().StartsWith("contents")) {
                            contentLinks.Add(GetLink(lnk[1]));

                        } else Links[GetLinkType(lnk[0])] = GetLink(lnk[1]);

                    } catch (Exception) { continue; }
                }
                isLinkUpdated = true;

            } catch (WebException) { }
        }

        private string GetLink(string inp) {
            var newLink = inp.Trim();
            if (!newLink.StartsWith("http")) {
                newLink = "http://" + newLink;
            }
            return newLink;
        }

        private bool IsLauncherUpdated() {
            int ver = 0;
            try {
                var launcherVersion = GetData(Links[LinkType.launcher]);
                ver = int.Parse(launcherVersion);
            } catch (FormatException) { return true; }

            return ver == VERSION;
        }

        private void StartCheckingInternetConnection() {
            ReportStatus("Checking server connection...");
            try {
                GetData(BASE_LINK);
                if (!isLinkUpdated) UpdateLinks();
                if (!IsLauncherUpdated()) {
                    MessageBox.Show("This launcher is now obsolete. A new launcher is now available. Please download the new launcher from the official website!", "Outdated version");
                    Process.Start(Links[LinkType.newlauncher]);
                    Dispatcher.Invoke(new Action(() => {
                        this.Close();
                    }));
                    return;
                }

                var t = new Thread(() => StartCheckThread());
                t.Start();
            } catch (WebException) {
                SetServerUnavailableStatus("Can't connect to update server");
                Thread.Sleep(5000);
                if (!isAppClosed) {
                    launcherThread = new Thread(StartCheckingInternetConnection);
                    launcherThread.Start();
                }
            } catch (SocketException) {
                SetServerUnavailableStatus("Can't connect to update server");
                MessageBox.Show("Can't connect. Looks like your antivirus blocks the connection. Please add the app to exclusions or turn the antivirus off", "Ooops!");
            } finally {
                SetServerUnavailableStatus("Can't connect to update server");
                MessageBox.Show("The connection is blocked. Try moving launcher to C:\\ProgramFiles folder and try again", "Ooops!");
            }
        }

        private void SetServerUnavailableStatus(string serverStatus) {
            ReportStatus(serverStatus);
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

        private void SetReadyToUpdateStatus() {
            Dispatcher.Invoke(new Action(() => {
                PlayButton.IsEnabled = true;
                RedownloadButton.IsEnabled = true;
                RedownloadButton.Content = "Update now";
                RedownloadButton.ToolTip = "Update to new version";
                DownloadStatus.Text = "New version is available!";
                ProgressBar.Value = 0;
            }));
        }

        private void SetReadyStatus() {
            Dispatcher.Invoke(new Action(() => {
                PlayButton.IsEnabled = true;
                RedownloadButton.IsEnabled = true;
                RedownloadButton.Content = "Force re-download";
                RedownloadButton.ToolTip = "Re-download the game in case it doesn't work";
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
