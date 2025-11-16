namespace Gwent.Core;

public class NetMessage
{
	public string Type { get; set; } = "";

	public string? PlayerId { get; set; }
	public string? Name { get; set; }
	public string? CardId { get; set; }
	public Row TargetRow { get; set; }
	public bool? IsHost { get; set; }
	public GameState? GameState { get; set; }
	public string? Error { get; set; }
}
