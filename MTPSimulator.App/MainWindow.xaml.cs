using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using MTPSimulator.App.Core;
using MTPSimulator.App.Models;

namespace MTPSimulator.App
{
    public partial class MainWindow : Window
    {
        private readonly OPCUAServer _server;
        private readonly MTPParser _parser;
        private readonly ObservableCollection<LiveValueRow> _liveValues = new();

        public MainWindow()
        {
            InitializeComponent();
            _server = new OPCUAServer();
            _parser = new MTPParser();
            GridValues.ItemsSource = _liveValues;
        }

        private void BtnLoadMtp_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "MTP XML (*.xml)|*.xml|All files (*.*)|*.*",
                Title = "Open MTP file"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var xml = File.ReadAllText(dlg.FileName);
                    var root = _parser.Parse(xml);
                    TreeNodes.ItemsSource = new[] { root };
                    _server.LoadNodes(root);
                    TxtStatus.Text = $"Loaded MTP: {Path.GetFileName(dlg.FileName)}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Parse error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _server.StartAsync(TxtEndpoint.Text);
                TxtStatus.Text = "Server running";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Server start failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _server.Stop();
                TxtStatus.Text = "Server stopped";
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Server stop failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private sealed class LiveValueRow
        {
            public string NodeId { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string DataType { get; set; } = string.Empty;
            public string? Value { get; set; }
        }
    }
}

