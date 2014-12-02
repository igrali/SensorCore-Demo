using Lumia.Sense;
using Lumia.Sense.Testing;
using Microsoft.AspNet.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace SensorCoreDemo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private HubConnection connection;
        private string currentActivity = String.Empty;
        private CoreDispatcher dispatcher;
        private IHubProxy hubProxy;
        private uint initialWalkingSteps = 0;
        private uint initialRunningSteps = 0;
        private bool isRecordingSteps = false;
        private SenseRecorder recorder;
        private uint walkingSteps = 0;
        private uint runningSteps = 0;

        private ActivityMonitor activityMonitor;
        private PlaceMonitor placeMonitor;
        private StepCounter stepCounter;
        private TrackPointMonitor trackPointMonitor;

        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
            dispatcher = Windows.UI.Core.CoreWindow.GetForCurrentThread().Dispatcher;
            Window.Current.VisibilityChanged += CurrentWindow_VisibilityChanged;
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            //if ((await SenseHelper.GetSupportedCapabilitiesAsync()).StepCounterTrigger)
            //{
            //    await RegisterBackgroundTask(SenseTrigger.StepCounterUpdate, "stepCounterTrigger", "BackgroundTasks.StepCounterTrigger");
            //}
        }

        private async Task RegisterBackgroundTask(string triggerName, string taskName, string taskEntryPoint)
        {
            BackgroundAccessStatus result = await BackgroundExecutionManager.RequestAccessAsync();
            if (result != BackgroundAccessStatus.Denied)
            {
                // Remove previous registration
                foreach (var task in BackgroundTaskRegistration.AllTasks)
                {
                    if (task.Value.Name == taskName)
                    {
                        task.Value.Unregister(true);
                    }
                }

                // Register new trigger
                BackgroundTaskBuilder myTaskBuilder = new BackgroundTaskBuilder();
                var myTrigger = new DeviceManufacturerNotificationTrigger(triggerName, false);
                myTaskBuilder.SetTrigger(myTrigger);
                myTaskBuilder.TaskEntryPoint = taskEntryPoint;
                myTaskBuilder.Name = taskName;
                BackgroundTaskRegistration myTask = myTaskBuilder.Register();
            }
        }
        
        async void CurrentWindow_VisibilityChanged(object sender, Windows.UI.Core.VisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                if (activityMonitor != null)
                {
                    await CallSensorcoreApiAsync(async () => await activityMonitor.ActivateAsync());
                }
                if (placeMonitor != null)
                {
                    await CallSensorcoreApiAsync(async () => await placeMonitor.ActivateAsync());
                }
                if (stepCounter != null)
                {
                    await CallSensorcoreApiAsync(async () => await stepCounter.ActivateAsync());
                    StartStepCounterPolling();
                }
                if (trackPointMonitor != null)
                {
                    await CallSensorcoreApiAsync(async () => await trackPointMonitor.ActivateAsync());
                }
            }
            else
            {
                if (activityMonitor != null)
                {
                    await CallSensorcoreApiAsync(async () => await activityMonitor.DeactivateAsync());
                }
                if (placeMonitor != null)
                {
                    await CallSensorcoreApiAsync(async () => await placeMonitor.DeactivateAsync());
                }
                if (stepCounter != null)
                {
                    await CallSensorcoreApiAsync(async () => await stepCounter.DeactivateAsync());
                }
                if (trackPointMonitor != null)
                {
                    await CallSensorcoreApiAsync(async () => await trackPointMonitor.DeactivateAsync());
                }
            }
        }

        private async Task<bool> CallSensorcoreApiAsync(Func<Task> action)
        {
            Exception failure = null;

            try
            {
                await action();
            }
            catch (Exception e)
            {
                failure = e;
            }

            if (failure != null)
            {
                MessageDialog dialog;

                switch (SenseHelper.GetSenseError(failure.HResult))
                {
                    case SenseError.LocationDisabled:
                    {
                        dialog = new MessageDialog("Location has been disabled. Do you want to open Location settings now?", "Information");
                        dialog.Commands.Add(new UICommand("Yes", async cmd => await SenseHelper.LaunchLocationSettingsAsync()));
                        dialog.Commands.Add(new UICommand("No"));
                        await dialog.ShowAsync();
                        return false;
                    }
                    case SenseError.SenseDisabled:
                    {
                        dialog = new MessageDialog("Motion data has been disabled. Do you want to open Motion data settings now?", "Information");
                        dialog.Commands.Add(new UICommand("Yes", async cmd => await SenseHelper.LaunchSenseSettingsAsync()));
                        dialog.Commands.Add(new UICommand("No"));
                        await dialog.ShowAsync();
                        return false;
                    }
                    default:
                    {
                        dialog = new MessageDialog("Failure: " + SenseHelper.GetSenseError(failure.HResult), "");
                        await dialog.ShowAsync();
                        return false;
                    }
                }
            }

            return true;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (activityMonitor == null)
            {
                activityMonitor.ReadingChanged -= activityMonitor_ReadingChanged;
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            await CallSensorcoreApiAsync(async () =>
                {
                    if (activityMonitor == null)
                    {
                        activityMonitor = await ActivityMonitor.GetDefaultAsync();
                        var currentReading = await activityMonitor.GetCurrentReadingAsync();
                        currentActivity = currentReading.Mode.ToString();
                        ActivityText.Text = currentActivity;
                    }

                    activityMonitor.Enabled = true;
                    activityMonitor.ReadingChanged += activityMonitor_ReadingChanged;

                    if (placeMonitor == null)
                    {
                        placeMonitor = await PlaceMonitor.GetDefaultAsync();
                    }

                    if (stepCounter == null)
                    {
                        stepCounter = await StepCounter.GetDefaultAsync();
                        var currentReading = await stepCounter.GetCurrentReadingAsync();

                        initialWalkingSteps = currentReading.WalkingStepCount;
                        initialRunningSteps = currentReading.RunningStepCount;
                    }

                    if (trackPointMonitor == null)
                    {
                        trackPointMonitor = await TrackPointMonitor.GetDefaultAsync();
                    }
                }
            );

            //ActivityMonitorSimulator simulator = await ActivityMonitorSimulator.GetDefaultAsync();
            //var reading = await simulator.GetCurrentReadingAsync();
            //var readingLastTwoDays = await simulator.GetActivityHistoryAsync(DateTimeOffset.Now.AddDays(-2), TimeSpan.FromDays(2));
            //var reading5DaysAgo = await simulator.GetStepCountAtAsync(DateTimeOffset.Now.AddDays(-5));

            connection = new HubConnection(/*YOU SIGNALR ENDPOINT HERE TO BUILD*/);
            hubProxy = connection.CreateHubProxy("SensorCoreHub");

            await connection.Start();

            StartStepCounterPolling();
        }

        private void StartStepCounterPolling()
        {
            Task.Factory.StartNew(async () =>
                {
                    while (true)
                    {
                        await CallSensorcoreApiAsync(async () =>
                            {
                                var currentReading = await stepCounter.GetCurrentReadingAsync();

                                walkingSteps = currentReading.WalkingStepCount - initialWalkingSteps;
                                runningSteps = currentReading.RunningStepCount - initialRunningSteps;

                                try
                                {
                                    await hubProxy.Invoke("Send", walkingSteps.ToString(), runningSteps.ToString(), currentActivity);
                                }
                                catch (InvalidOperationException exception)
                                {
                                    // it happens.
                                }
                                catch (Exception exception)
                                {
                                    Debug.WriteLine(exception.Message);
                                }

                                await dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                    {
                                        WalkingStepCountText.Text = walkingSteps.ToString();
                                        RunningStepCountText.Text = runningSteps.ToString();
                                    }
                                );
                            }
                        );

                        await Task.Delay(500);
                    }
                }
            );
        }

        private async void activityMonitor_ReadingChanged(IActivityMonitor source, ActivityMonitorReading value)
        {
            currentActivity = value.Mode.ToString();
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ActivityText.Text = currentActivity;
                }
            );

            try
            { 
                await hubProxy.Invoke("Send", walkingSteps.ToString(), runningSteps.ToString(), currentActivity);
            }
            catch (InvalidOperationException exception)
            {
                // it happens.
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception.Message);
            }
        }

        private async void RecordSteps_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecordingSteps)
            {
                StepCounter stepCounter = await StepCounter.GetDefaultAsync();
                recorder = new SenseRecorder(stepCounter);
                await recorder.StartAsync();

                RecordButton.Content = "Stop recording steps";
                isRecordingSteps = true;
            }
            else
            {
                await recorder.StopAsync();
                await recorder.GetRecording().SaveAsync();

                RecordButton.Content = "Start recording steps";
                isRecordingSteps = false;

                var recording = await SenseRecording.LoadFromFileAsync("jsonData.txt");
                var simulator = await StepCounterSimulator.GetDefaultAsync(recording);
            }
        }
    }
}
