namespace FivetranClient.Models;

public class PaginatedRoot<T>
{
    public Data<T>? Data { get; set; }
}