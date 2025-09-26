using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using MTPSimulator.App.Models;

namespace MTPSimulator.App.Core
{
    public sealed class MTPParser
    {
        public MTPNode ParseFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is ".mtp" or ".amlx")
            {
                return ParseAmlxArchive(filePath);
            }
            if (ext is ".aml" or ".xml")
            {
                var xml = File.ReadAllText(filePath);
                return Parse(xml);
            }

            throw new NotSupportedException($"Unsupported file type: {ext}");
        }

        public MTPNode Parse(string xmlContent)
        {
            var xdoc = XDocument.Parse(xmlContent);
            var root = new MTPNode { DisplayName = "MTP", BrowseName = "MTP", NodeClass = "Folder" };

            // This is a simplified placeholder. Real mapping should follow VDI/VDE/NAMUR 2658.
            foreach (var variable in xdoc.Descendants().Where(e => e.Name.LocalName == "Variable"))
            {
                var name = (string?)variable.Attribute("Name") ?? "Unknown";
                var type = (string?)variable.Attribute("DataType") ?? "Double";
                var ns = (string?)variable.Attribute("NamespaceIndex") ?? "2";
                var id = (string?)variable.Attribute("NodeId") ?? name;

                root.Children.Add(new MTPNode
                {
                    DisplayName = name,
                    BrowseName = name,
                    NodeClass = "Variable",
                    DataType = type,
                    NodeId = $"ns={ns};s={id}"
                });
            }

            // AutomationML Attribute fallback
            foreach (var attr in xdoc.Descendants().Where(e => e.Name.LocalName == "Attribute"))
            {
                var name = (string?)attr.Attribute("Name");
                if (string.IsNullOrWhiteSpace(name)) continue;

                var dt = (string?)attr.Attribute("AttributeDataType")
                         ?? (string?)attr.Attribute("DataType")
                         ?? "Double";
                var valueElement = attr.Elements().FirstOrDefault(e => e.Name.LocalName == "Value");
                var id = name!.Replace(' ', '_');

                root.Children.Add(new MTPNode
                {
                    DisplayName = name,
                    BrowseName = name,
                    NodeClass = "Variable",
                    DataType = dt,
                    NodeId = $"ns=2;s=AML/{id}"
                });
            }

            return root;
        }

        private static MTPNode ParseAmlxArchive(string filePath)
        {
            var root = new MTPNode { DisplayName = Path.GetFileName(filePath), BrowseName = "MTP", NodeClass = "Folder" };
            using var fs = File.OpenRead(filePath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

            foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".aml", true, CultureInfo.InvariantCulture) || e.FullName.EndsWith(".xml", true, CultureInfo.InvariantCulture)))
            {
                using var es = entry.Open();
                using var sr = new StreamReader(es);
                var content = sr.ReadToEnd();
                var subRoot = new MTPParser().Parse(content);
                foreach (var child in subRoot.Children)
                {
                    root.Children.Add(child);
                }
            }

            return root;
        }
    }
}

