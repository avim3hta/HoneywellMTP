using System.Collections.Generic;

namespace MTPSimulator.App.Models
{
    public sealed class MTPNode
    {
        public string DisplayName { get; set; } = string.Empty;
        public string BrowseName { get; set; } = string.Empty;
        public string NodeClass { get; set; } = "Folder"; // Folder, Object, Variable
        public string? DataType { get; set; }
        public string? NodeId { get; set; }
        public List<MTPNode> Children { get; } = new();
    }
}

