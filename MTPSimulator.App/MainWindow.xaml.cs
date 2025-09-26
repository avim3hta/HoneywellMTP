using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using MTPSimulator.App.Core;
using MTPSimulator.App.Models;
using System.Collections.Generic;

namespace MTPSimulator.App
{
	public partial class MainWindow : Window
	{
		private readonly OPCUAServer _server;
		private readonly MTPParser _parser;
		private readonly ObservableCollection<LiveValueRow> _liveValues = new();
		private WebHost? _webHost;

		public MainWindow()
		{
			InitializeComponent();
			_server = new OPCUAServer();
			_parser = new MTPParser();
			GridValues.ItemsSource = _liveValues;

			_server.ValueChanged += OnServerValueChanged;
		}

		private void BtnLoadMtp_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog
			{
				Filter = "MTP/AML/AMLX (*.mtp;*.amlx;*.aml;*.xml)|*.mtp;*.amlx;*.aml;*.xml|All files (*.*)|*.*",
				Title = "Open MTP file"
			};
			if (dlg.ShowDialog() == true)
			{
				try
				{
					var root = _parser.ParseFile(dlg.FileName);
					TreeNodes.ItemsSource = new[] { root };
					_server.LoadNodes(root);
					TxtStatus.Text = $"Loaded MTP: {Path.GetFileName(dlg.FileName)}";

					// Initialize grid rows from parsed nodes and publish snapshot for web
					_liveValues.Clear();
					var snapshot = new List<VariableInfo>();
					foreach (var n in Enumerate(root))
					{
						if (n.NodeClass == "Variable")
						{
							_liveValues.Add(new LiveValueRow
							{
								NodeId = n.NodeId ?? n.DisplayName,
								DisplayName = n.DisplayName,
								DataType = n.DataType ?? string.Empty,
								Value = null,
								Server = _server
							});
							snapshot.Add(new VariableInfo
							{
								NodeId = n.NodeId ?? n.DisplayName,
								DisplayName = n.DisplayName,
								DataType = n.DataType ?? string.Empty,
								Value = null
							});
						}
					}
					VariableSnapshotProvider.Update(snapshot);
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
				_webHost ??= new WebHost(_server);
				await _webHost.StartAsync();
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
				if (_webHost != null) _ = _webHost.StopAsync();
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, ex.Message, "Server stop failed", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void OnServerValueChanged(string nodeId, object value)
		{
			Dispatcher.Invoke(() =>
			{
				var row = FindRow(nodeId);
				if (row != null)
				{
					row.Value = value?.ToString();
				}
			});
		}

		private LiveValueRow? FindRow(string nodeId)
		{
			foreach (var r in _liveValues)
			{
				if (string.Equals(r.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
					return r;
			}
			return null;
		}

		private static IEnumerable<MTPNode> Enumerate(MTPNode node)
		{
			yield return node;
			foreach (var c in node.Children)
			{
				foreach (var d in Enumerate(c))
					yield return d;
			}
		}

		private sealed class LiveValueRow : System.ComponentModel.INotifyPropertyChanged
		{
			public OPCUAServer? Server { get; set; }
			public string NodeId { get; set; } = string.Empty;
			public string DisplayName { get; set; } = string.Empty;
			public string DataType { get; set; } = string.Empty;

			private string? _value;
			public string? Value
			{
				get => _value;
				set
				{
					if (_value == value) return;
					_value = value;
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
					// Push the write to server if possible and parse basic numeric types
					if (Server != null && !string.IsNullOrWhiteSpace(NodeId))
					{
						object writeVal = value ?? string.Empty;
						if (double.TryParse(value, out var d)) writeVal = d;
						Server.TryWriteValue(NodeId, writeVal);
					}
				}
			}

			public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
		}
	}
}

