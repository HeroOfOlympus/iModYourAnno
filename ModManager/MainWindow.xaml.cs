﻿using System;
using System.Windows;
using Imya.UI.Popup;
using Imya.UI.Properties;
using Imya.UI.Utils;
using Imya.UI.Views;

namespace Imya.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Properties.Settings Settings { get; } = Properties.Settings.Default;

        public MainWindow()
        {
            InitializeComponent();
            SetUpEmbeddedConsole();

            Console.WriteLine("Modders Are gonna take over the World!!!");
            DataContext = this;
            MinHeight = Settings.Default.MIN_WINDOW_HEIGHT;
            MinWidth = Settings.Default.MIN_WINDOW_WIDTH;

            Title = "iMYA";
        }

        public void SetUpEmbeddedConsole()
        {
            Console.SetOut(new EmbeddedConsole(ConsoleLogTextBox.ConsoleOut, this));
        }
    }
}
