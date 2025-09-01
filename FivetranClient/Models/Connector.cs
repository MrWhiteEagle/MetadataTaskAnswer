namespace FivetranClient.Models;

public class Connector
{
    public string Id { get; set; }
    public string? Service { get; set; }
    public string? Schema { get; set; }
    public bool? Paused { get; set; }
}