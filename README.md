MTP OPC UA Simulator
====================

A Windows WPF (.NET 8) OPC UA server simulator that reads an MTP file and exposes corresponding OPC UA nodes, enabling DCS/POL teams to validate integrations without physical equipment.

Features
--------
- Load an MTP XML and generate an address space
- Start an OPC UA server with a configurable endpoint
- Read/Write variables via any OPC UA test client (e.g., UaExpert)
- Simple value simulation (sine + noise) for numeric variables

Requirements
------------
- Windows 10+
- .NET 8 SDK (install from Microsoft or via winget)
- NuGet package: `OPCFoundation.NetStandard.Opc.Ua`

Project Layout
--------------
```
MTPSimulator.App/
  Core/
    MTPParser.cs           # Parse MTP XML into MTPNode tree (simplified)
    OPCUAServer.cs         # OPC UA server + NodeManager
    SimulationEngine.cs    # Data simulation
  Models/
    MTPNode.cs             # MTP node representation
    SimulationConfig.cs    # Simulation settings
  UI/
    MainWindow.xaml        # App shell (start/stop, endpoint, load MTP)
    NodeBrowser.xaml       # (reserved) tree/grid visualization
  Utils/
    DataGenerators.cs      # Sine/ramp/noise helpers
    ConfigManager.cs       # Load/save SimulationConfig
```

Build
-----
1) Ensure .NET 8 SDK is installed.
   - winget: `winget install --id Microsoft.DotNet.SDK.8 --silent --accept-source-agreements --accept-package-agreements`
   - Or download from Microsoft (`https://dotnet.microsoft.com/download`)

2) Restore and build:
```bash
cd <repo-root>
dotnet restore MTPSimulator.sln
dotnet build MTPSimulator.sln -c Release
```

Run
---
- Launch the WPF app (from Visual Studio or `dotnet run` in `MTPSimulator.App`).
- Endpoint accepts ip:port or full opc.tcp form; examples:
  - `opc.tcp://127.0.0.1:4840`
  - `127.0.0.1:4840` (auto-normalized to `opc.tcp://127.0.0.1:4840`)
- Click "Load MTP..." to select your MTP XML.
- Click "Start Server" to host the address space.

MTP Inputs (MTP-MAHE folder)
----------------------------
- You can place your vendor MTP in `MTP-MAHE` or anywhere on disk.
- Supported formats:
  - `.mtp` / `.amlx` (AutomationML container ZIP)
  - `.aml` / `.xml`
- The parser scans the AMLX archive for `.aml`/`.xml` and extracts `<Variable .../>` and AML `<Attribute ...><Value>...</Value></Attribute>` as scalar variables. Extend as needed for full mapping.

Test with UaExpert
------------------
1) Open UaExpert and add a new server connection.
2) Enter the simulator endpoint, e.g. `opc.tcp://<your-ip>:4840`.
3) Browse to `Objects -> MTP` and expand variables.
4) Read/Write values. Numeric variables auto-update via simulation.

MTP Parsing Notes
-----------------
- `Core/MTPParser.cs` currently looks for `<Variable Name="..." DataType="..." NamespaceIndex="..." NodeId="..."/>` elements and creates scalar variables. This is a minimal placeholder pending full VDI/VDE/NAMUR 2658 mapping.

OPC UA Server internals
-----------------------
- Uses `StandardServer` with a custom `SimulatorNodeManager` to translate `MTPNode` into `FolderState`/`BaseDataVariableState` under `Objects/MTP`.
- Security: self-signed app certificate auto-created; untrusted certificates auto-accepted for lab/demo use.
- Endpoint normalization: `ip:port` is converted to `opc.tcp://ip:port`.

Read/Write Behavior
-------------------
- Variables are `CurrentReadOrWrite`.
- `SimulationEngine` updates numeric values; NodeManager pushes timestamps and change masks.
- To customize write handling, extend NodeManager to intercept writes.

Known Limitations / Next Steps
------------------------------
- Minimal MTP mapping. Implement full PEA mapping as needed.
- Add alarms/conditions if required by test plan.
- Persist last-used MTP and endpoint.
- Enhance UI with tree/live grid and write controls.

FAQ
---
- Do I have to include `opc.tcp://`?  No. Enter `ip:port`; it will normalize.
- Admin rights required?  No. Ensure your chosen port is allowed through the firewall.

License
-------
For hackathon/demo purposes.

