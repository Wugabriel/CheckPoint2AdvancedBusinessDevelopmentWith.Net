namespace ProjetoBanco.API.Models;

public abstract class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

    public int AgenciaId { get; set; }
    public Agencia? Agencia { get; set; }

    public ICollection<Contratacao> Contratacoes { get; set; } = new List<Contratacao>();
}

public class PessoaFisica : Cliente
{
    public string CPF { get; set; } = string.Empty;
    public DateTime DataNascimento { get; set; }
    public int ScoreCredito { get; set; }
}

public class PessoaJuridica : Cliente
{
    public string CNPJ { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string SetorAtuacao { get; set; } = string.Empty;
}
