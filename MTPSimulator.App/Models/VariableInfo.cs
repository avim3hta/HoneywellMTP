namespace MTPSimulator.App.Models
{
	public sealed class VariableInfo
	{
		public string NodeId { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public string DataType { get; set; } = string.Empty;
		public object? Value { get; set; }
		public byte? Access { get; set; }
		public string? Description { get; set; }
	}
}
