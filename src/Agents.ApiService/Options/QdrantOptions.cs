using System.ComponentModel.DataAnnotations;

public class QdrantOptions
{
    [Required]
    public string Endpoint { get; set; }
    [Required]
    public int VectorSize { get; set; }
}