using Photon.NINA.SafteyGuard.Properties;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Image.ImageData;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Settings = Photon.NINA.SafteyGuard.Properties.Settings;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Equipment.MySafetyMonitor;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyDome;
using NINA.Equipment.Interfaces;
using NINA.Sequencer.Interfaces.Mediator;

namespace Photon.NINA.SafteyGuard {

    /// <summary>
    /// This class exports the IPluginManifest interface and will be used for the general plugin information and options
    /// The base class "PluginBase" will populate all the necessary Manifest Meta Data out of the AssemblyInfo attributes. Please fill these accoringly
    ///
    /// An instance of this class will be created and set as datacontext on the plugin options tab in N.I.N.A. to be able to configure global plugin settings
    /// The user interface for the settings will be defined by a DataTemplate with the key having the naming convention "Safteyguard_Options" where Safteyguard corresponds to the AssemblyTitle - In this template example it is found in the Options.xaml
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class Safteyguard : PluginBase, ISafetyMonitorConsumer, IDomeConsumer {
        private readonly IPluginOptionsAccessor pluginSettings;
        private readonly IProfileService profileService;
        private readonly IDomeMediator domeMediator;
        private readonly ISafetyMonitorMediator safetyMonitorMediator;
        private readonly ISequenceMediator sequenceMediator;
        private readonly IApplicationMediator applicationMediator;

        private string pluginName = "SafetyGuard";
        private static DateTime programStartTime = DateTime.Now;
        private static TimeSpan initialDelay = TimeSpan.FromSeconds(10);

        private uint repeatErrorDelay = 60;

        private ShutterState domeStatus;
        private DateTime lastUpdateTime = DateTime.MinValue;

        [ImportingConstructor]
        public Safteyguard(IOptionsVM options,
                           ISafetyMonitorMediator safetyMonitorMediator,
                           IDomeMediator domeMediator,
                           ISequenceMediator sequenceMediator,
                           IApplicationMediator applicationMediator) {
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            this.applicationMediator = applicationMediator;

            this.safetyMonitorMediator = safetyMonitorMediator;
            safetyMonitorMediator.RegisterConsumer(this);

            this.domeMediator = domeMediator;
            domeMediator.RegisterConsumer(this);

            this.sequenceMediator = sequenceMediator;

            Logger.Info("Starting plugin");
            Notification.ShowInformation($"{pluginName} started");
        }

        public void CheckIfSafe() {
            if (DateTime.Now - programStartTime < initialDelay) {
                lastUpdateTime = DateTime.UtcNow;
                return;
            }

            bool isSafe = false;

            DomeInfo domeInfo = null;
            SafetyMonitorInfo safetyMonitorInfo = null;
            string response = String.Empty;

            try {
                domeInfo = domeMediator.GetInfo();
                safetyMonitorInfo = safetyMonitorMediator.GetInfo();
            } catch (Exception) {
                Logger.Info("Not yet loaded");
                return;
            }

            Logger.Info("Checking Dome");
            if (domeInfo?.Connected == false) {
                response += "Dome not connected!";
                isSafe = false;
            }

            Logger.Info("Checking SafteyMonitor");
            if (safetyMonitorInfo?.Connected == false) {
                response += "Safety Monitor not connected!";
                isSafe = false;
            }

            Logger.Info("Checking isSafe");
            if (safetyMonitorInfo?.IsSafe == false &&
                domeInfo?.ShutterStatus != ShutterState.ShutterClosed) {
                response = "Status became unsafe";
                isSafe = false;
            }

            if (!isSafe && DateTime.UtcNow - lastUpdateTime < TimeSpan.FromSeconds(repeatErrorDelay)) {
                Unsafe(response);

                lastUpdateTime = DateTime.UtcNow;
            }
        }

        public void UpdateDeviceInfo(DomeInfo domeInfo) {
            CheckIfSafe();
        }

        public void UpdateDeviceInfo(SafetyMonitorInfo safteyInfo) {
            CheckIfSafe();
        }

        public void Unsafe(string response) {
            Notification.ShowWarning($"{pluginName} - {response}");

            //sequenceMediator.RegisterSequenceNavigation;
            // Pause sequence

            // park scope
            // close dome
        }

        public void Dispose() {
            safetyMonitorMediator.RemoveConsumer(this);
            domeMediator.RemoveConsumer(this);
        }
    }
}