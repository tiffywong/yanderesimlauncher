using System;
using System.Diagnostics;
using System.Windows;

namespace YandereSimLauncher {

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application {

        /// <summary>
        /// Forces app to run single-instance
        /// </summary>
        /// <param name="e"></param>
        protected override void OnStartup(StartupEventArgs e) {

            // Get Reference to the current Process
            var thisProc = Process.GetCurrentProcess();

            // Check how many total processes have the same name as the current one
            if (Process.GetProcessesByName(thisProc.ProcessName).Length > 1) {

                // If ther is more than one, than it is already running.
                MessageBox.Show("Application is already running.");
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);
        }
    }
}
