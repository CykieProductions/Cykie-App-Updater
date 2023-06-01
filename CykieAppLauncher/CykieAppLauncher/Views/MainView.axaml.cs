using Avalonia.Controls;
using CykieAppLauncher.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace CykieAppLauncher.Views
{
    public partial class MainView : UserControl
    {
        public static MainView? Current { get; private set; }
        //SynchronizationContext? synchronizationContext;
        public TaskScheduler SyncedScheduler { get; private set; }

        public MainView()
        {
            InitializeComponent();
            Current = this;
            //synchronizationContext = SynchronizationContext.Current;
            SyncedScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            Current.MessageBox.IsVisible = false;
            Current.MessageBox.IsEnabled = false;
        }
    }
}