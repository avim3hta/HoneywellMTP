using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MTPSimulator.App.Models;

namespace MTPSimulator.App.Core
{
	public sealed class WebHost
	{
		private readonly OPCUAServer _server;
		private IHost? _host;

		public WebHost(OPCUAServer server)
		{
			_server = server;
		}

		public async Task StartAsync(int port = 5288, CancellationToken ct = default)
		{
			if (_host != null) return;
			_host = Host.CreateDefaultBuilder()
				.ConfigureWebHostDefaults(webBuilder =>
				{
					webBuilder.UseKestrel().UseUrls($"http://localhost:{port}");
					webBuilder.ConfigureServices(services =>
					{
						services.AddCors(options =>
						{
							options.AddDefaultPolicy(policy =>
								policy
									.AllowAnyHeader()
									.AllowAnyMethod()
									.SetIsOriginAllowed(_ => true)
									.AllowCredentials());
						});
						services.AddSignalR();
					});
					webBuilder.Configure(app =>
					{
						var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();
						if (env.IsDevelopment())
						{
							app.UseDeveloperExceptionPage();
						}
						app.UseRouting();
						app.UseCors();
                        app.UseEndpoints(endpoints =>
						{
							endpoints.MapHub<ValuesHub>("/hub/values");
							// Minimal REST endpoints
                            endpoints.MapGet("/api/variables", (Func<IEnumerable<VariableInfo>>)(() => VariableSnapshotProvider.Snapshot));
							endpoints.MapPost("/api/write", async (WriteRequest body, IHubContext<ValuesHub> hub) =>
							{
								var ok = _server.TryWriteValue(body.NodeId, body.Value ?? string.Empty);
								if (ok)
								{
									await hub.Clients.All.SendAsync("value", body.NodeId, body.Value);
								}
								return Microsoft.AspNetCore.Http.Results.Ok(new { success = ok });
							});
							endpoints.MapPost("/api/mtp/upload", async (HttpRequest request, IHubContext<ValuesHub> hub) =>
							{
								if (!request.HasFormContentType)
									return Microsoft.AspNetCore.Http.Results.BadRequest("multipart/form-data expected");
								var form = await request.ReadFormAsync();
								var file = form.Files.Count > 0 ? form.Files[0] : null;
								if (file == null || file.Length == 0)
									return Microsoft.AspNetCore.Http.Results.BadRequest("file missing");
								string temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName));
								await using (var fs = File.Create(temp))
								{
									await file.CopyToAsync(fs);
								}
                                try
								{
									var parser = new MTPParser();
									var root = parser.ParseFile(temp);
									_server.LoadNodes(root);
									// refresh snapshot
                                    var snapshot = BuildSnapshot(root);
									VariableSnapshotProvider.Update(snapshot);
									await hub.Clients.All.SendAsync("variables", snapshot);
									return Microsoft.AspNetCore.Http.Results.Ok(new { success = true, count = snapshot.Count });
								}
								finally
								{
									try { File.Delete(temp); } catch { }
								}
							});
						});
					});
				})
				.Build();

            _server.ValueChanged += async (nodeId, value) =>
			{
				if (_host == null) return;
				try
				{
					var hub = _host.Services.GetRequiredService<IHubContext<ValuesHub>>();
                    await hub.Clients.All.SendAsync("value", nodeId, value);
				}
				catch { }
			};

            _server.ExternalValueWritten += async (nodeId, value) =>
            {
                if (_host == null) return;
                try
                {
                    var hub = _host.Services.GetRequiredService<IHubContext<ValuesHub>>();
                    // dedicated event so the UI can only update details on external writes
                    await hub.Clients.All.SendAsync("externalWrite", nodeId, value);
                }
                catch { }
            };

			await _host.StartAsync(ct).ConfigureAwait(false);
		}

        private static List<VariableInfo> BuildSnapshot(MTPNode root)
		{
			var list = new List<VariableInfo>();
			void Walk(MTPNode n)
			{
				if (n.NodeClass == "Variable")
				{
					list.Add(new VariableInfo
					{
						NodeId = n.NodeId ?? n.DisplayName,
						DisplayName = n.DisplayName,
                        DataType = n.DataType ?? string.Empty,
                        Value = null,
                        Access = n.Access,
                        Description = n.Description
					});
				}
				foreach (var c in n.Children)
					Walk(c);
			}
			Walk(root);
			return list;
		}

		public async Task StopAsync(CancellationToken ct = default)
		{
			if (_host != null)
			{
				try { await _host.StopAsync(ct).ConfigureAwait(false); } catch { }
				try { _host.Dispose(); } catch { }
				_host = null;
			}
		}
	}

	public sealed class ValuesHub : Hub { }

	public static class VariableSnapshotProvider
	{
		public static IReadOnlyList<VariableInfo> Snapshot { get; private set; } = Array.Empty<VariableInfo>();
		public static void Update(IReadOnlyList<VariableInfo> items) => Snapshot = items;
	}

	public sealed class WriteRequest
	{
		public string NodeId { get; set; } = string.Empty;
		public object? Value { get; set; }
	}
}

