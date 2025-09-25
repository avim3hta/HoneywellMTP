using System;
using System.Collections.Generic;
using System.Xml.Linq;
using MTPSimulator.App.Models;

namespace MTPSimulator.App.Core
{
    public sealed class MTPParser
    {
        public MTPNode Parse(string xmlContent)
        {
            var xdoc = XDocument.Parse(xmlContent);
            var root = new MTPNode { DisplayName = "MTP", BrowseName = "MTP", NodeClass = "Folder" };

            // This is a simplified placeholder. Real mapping should follow VDI/VDE/NAMUR 2658.
            foreach (var variable in xdoc.Descendants("Variable"))
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

            return root;
        }
    }
}

