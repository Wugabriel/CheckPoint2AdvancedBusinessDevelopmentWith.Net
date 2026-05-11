namespace ProjetoBanco.API.Models;

public abstract class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
}

public class MaquinaDeCartao : Produto
{
    public decimal TaxaMDR { get; set; } = 1.99m;
    public string Modelo { get; set; } = string.Empty;
    public bool NecessitaAtivacaoFisica { get; set; } = true;
}

public class Emprestimo : Produto
{
    public decimal ValorMinimo { get; set; } = 1_000m;
    public decimal ValorMaximo { get; set; } = 100_000m;
    public int PrazoMaximoMeses { get; set; } = 60;
    public decimal TaxaJurosMensalBase { get; set; } = 1.5m;
}

public class ReceberSalario : Produto
{
    public string EmpresaConveniada { get; set; } = string.Empty;
}

public enum StatusContratacao
{
    PENDENTE,
    APROVADA,
    RECUSADA
}

public class Contratacao
{
    public int Id { get; set; }
    public DateTime DataSolicitacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataProcessamento { get; set; }
    public StatusContratacao Status { get; set; } = StatusContratacao.PENDENTE;
    public string? MotivoRecusa { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public int ProdutoId { get; set; }
    public Produto? Produto { get; set; }
    public decimal? ValorSolicitado { get; set; }
    public int? PrazoMeses { get; set; }
    public decimal? TaxaAplicada { get; set; }
}
