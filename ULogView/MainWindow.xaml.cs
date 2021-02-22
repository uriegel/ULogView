﻿using System.Threading.Tasks;
using System.Windows;

namespace ULogView
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        void Window_Loaded(object sender, RoutedEventArgs e) => LogServer.start();

        void OnDropFile(string file) => Task.Run(() => LogServer.indexFile(file));

        void webView_NavigationCompleted(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
            => DropFile.Initialize(this, OnDropFile);

        // TODO Array.Parallel.map
    }
}
