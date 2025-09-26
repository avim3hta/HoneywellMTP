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
        public byte? Access { get; set; } // 0=NoAccess,1=Read,2=Write,3=Read/Write
        public string? Description { get; set; }
        public List<MTPNode> Children { get; } = new();
    }
}

