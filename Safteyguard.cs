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
using NINA.ViewModel.Sequencer;
using Accord.Statistics.Kernels;

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

        private string pluginName = "SafetyGuard";
        private static DateTime programStartTime = DateTime.Now;
        private static TimeSpan initialDelay = TimeSpan.FromSeconds(10);

        private bool startupFinished = false;
        private bool safteyMonitorConnected = false;
        private bool domeConnected = false;

        private int unsafeTriggerCounter = 0;

        private uint repeatErrorDelay = 60;

        private DomeInfo domeInfo = null;
        private SafetyMonitorInfo safetyMonitorInfo = null;

        private ShutterState domeStatus;
        private DateTime lastUpdateTime = DateTime.MinValue;

        [ImportingConstructor]
        public Safteyguard(IOptionsVM options,
                           ISafetyMonitorMediator safetyMonitorMediator,
                           IDomeMediator domeMediator,
                           ISequenceMediator sequenceMediator
                           ) {
            if (Settings.Default.UpdateSettings) {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }

            this.safetyMonitorMediator = safetyMonitorMediator;
            safetyMonitorMediator.RegisterConsumer(this);

            this.domeMediator = domeMediator;
            domeMediator.RegisterConsumer(this);

            this.sequenceMediator = sequenceMediator;

            Logger.Info("Starting plugin");
            //Notification.ShowInformation($"{pluginName} started");
        }

        public bool IsSafe {
            get {
                if (domeInfo?.Connected == false) {
                    return false;
                }

                if (safetyMonitorInfo?.Connected == false) {
                    return false;
                }

                if (safetyMonitorInfo?.IsSafe == false &&
                    domeInfo?.ShutterStatus != ShutterState.ShutterClosed) {
                    return false;
                }

                return true;
            }
        }

        public bool StartupFinished {
            get {
                if (DateTime.Now - programStartTime < initialDelay) {
                    lastUpdateTime = DateTime.UtcNow;
                    return false;
                }

                if (domeInfo?.Connected == true) {
                    domeConnected = true;
                }

                if (safetyMonitorInfo?.Connected == true) {
                    safteyMonitorConnected = true;
                }

                return (domeConnected && safteyMonitorConnected);
            }
        }

        public void CheckIfSafe() {
            if (!StartupFinished)
                return;

            if (IsSafe && unsafeTriggerCounter > 0) {
                unsafeTriggerCounter = 0;
                Safe();
            }

            if (!IsSafe) {
                if (unsafeTriggerCounter == 0 ||
                    DateTime.UtcNow - lastUpdateTime > TimeSpan.FromSeconds(repeatErrorDelay)) {
                    Unsafe();

                    lastUpdateTime = DateTime.UtcNow;
                    unsafeTriggerCounter++;
                }
            }
        }

        public void UpdateDeviceInfo(DomeInfo domeInfo) {
            this.domeInfo = domeInfo;
            CheckIfSafe();
        }

        public void UpdateDeviceInfo(SafetyMonitorInfo safteyInfo) {
            this.safetyMonitorInfo = safteyInfo;
            CheckIfSafe();
        }

        /// <summary>
        /// When it returns to safe condition, we can resume our sequence
        /// </summary>
        public void Safe() {
            Logger.Info("The conditions have reverted to a safe state.");
            Notification.ShowInformation($"{pluginName} - The conditions have reverted to a safe state.");
        }

        /// <summary>
        /// When it becomes unsafe, running sequence should be stopped
        /// * Park scope & dome
        /// * Execute predefined sequence
        /// </summary>
        public void Unsafe() {
            Logger.Warning("Conditions became unsafe");
            Notification.ShowWarning($"{pluginName} - Conditions became unsafe");

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