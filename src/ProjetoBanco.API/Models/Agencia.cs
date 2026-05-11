namespace ProjetoBanco.API.Models;

public class Agencia
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;

    public ICollection<Cliente> Clientes { get; set; } = new List<Cliente>();
}
