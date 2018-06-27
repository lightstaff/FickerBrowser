// <copyright file="MainWindow.xaml.cs" company="Lightstaff">
// Copyright (c) Lightstaff. All rights reserved.
// </copyright>

namespace FickerBrowser
{
    using System.Windows;

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.ViewModel = new AppViewModel();
            this.InitializeComponent();
            this.DataContext = this.ViewModel;
        }

        public AppViewModel ViewModel { get; private set; }
    }
}
