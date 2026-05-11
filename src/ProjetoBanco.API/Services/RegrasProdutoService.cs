using ProjetoBanco.API.Models;

namespace ProjetoBanco.API.Services;

public interface IRegrasProdutoService
{
    (bool aprovado, string? motivo, decimal? taxaAplicada) AvaliarMaquinaDeCartao(
        Cliente cliente, MaquinaDeCartao produto);

    (bool aprovado, string? motivo, decimal? taxaAplicada) AvaliarEmprestimo(
        Cliente cliente, Emprestimo produto, decimal valorSolicitado, int prazoMeses);

    int CalcularScoreCredito(PessoaFisica pf);
    decimal CalcularTaxaMDR(string setorAtuacao);
    decimal CalcularTaxaJuros(int scoreCredito);
}

public class RegrasProdutoService : IRegrasProdutoService
{
    private readonly ILogger<RegrasProdutoService> _logger;

    public RegrasProdutoService(ILogger<RegrasProdutoService> logger)
    {
        _logger = logger;
    }

    public (bool, string?, decimal?) AvaliarMaquinaDeCartao(Cliente cliente, MaquinaDeCartao produto)
    {
        if (cliente is PessoaFisica pf)
        {
            if (pf.ScoreCredito < 400)
                return (false, $"Score {pf.ScoreCredito} abaixo do mínimo (400) para Máquina de Cartão.", null);

            var taxa = CalcularTaxaMDR("VAREJO");
            _logger.LogInformation("PF aprovada para Maquina. Score={Score} Taxa={Taxa}%", pf.ScoreCredito, taxa);
            return (true, null, taxa);
        }

        if (cliente is PessoaJuridica pj)
        {
            var setoresProibidos = new[] { "CASSINO", "APOSTAS" };
            if (setoresProibidos.Contains(pj.SetorAtuacao.ToUpper()))
                return (false, $"Setor '{pj.SetorAtuacao}' não possui convênio para Máquina de Cartão.", null);

            var taxa = CalcularTaxaMDR(pj.SetorAtuacao);
            _logger.LogInformation("PJ aprovada para Maquina. Setor={Setor} Taxa={Taxa}%", pj.SetorAtuacao, taxa);
            return (true, null, taxa);
        }

        return (false, "Tipo de cliente inválido.", null);
    }

    public (bool, string?, decimal?) AvaliarEmprestimo(
        Cliente cliente, Emprestimo produto, decimal valorSolicitado, int prazoMeses)
    {
        if (valorSolicitado < produto.ValorMinimo)
            return (false, $"Valor mínimo para empréstimo é R${produto.ValorMinimo:N2}.", null);

        if (valorSolicitado > produto.ValorMaximo)
            return (false, $"Valor máximo para empréstimo é R${produto.ValorMaximo:N2}.", null);

        if (prazoMeses < 1 || prazoMeses > produto.PrazoMaximoMeses)
            return (false, $"Prazo deve ser entre 1 e {produto.PrazoMaximoMeses} meses.", null);

        if (cliente is PessoaFisica pf)
        {
            if (pf.ScoreCredito < 600)
                return (false, $"Score {pf.ScoreCredito} insuficiente. Mínimo: 600 para empréstimo.", null);

            var taxa = CalcularTaxaJuros(pf.ScoreCredito);
            _logger.LogInformation("Empréstimo PF aprovado. Score={Score} Taxa={Taxa}%/mês ValorSolicitado={Valor}", 
                pf.ScoreCredito, taxa, valorSolicitado);
            return (true, null, taxa);
        }

        if (cliente is PessoaJuridica pj)
        {
            var setoresRiscoAlto = new[] { "CONSTRUCAO", "AGRICULTURA" };
            if (setoresRiscoAlto.Contains(pj.SetorAtuacao.ToUpper()) && valorSolicitado > 50_000)
                return (false, $"Para setor '{pj.SetorAtuacao}', limite máximo é R$50.000.", null);

            var taxa = produto.TaxaJurosMensalBase + 0.5m;
            _logger.LogInformation("Empréstimo PJ aprovado. Setor={Setor} Taxa={Taxa}%/mês", pj.SetorAtuacao, taxa);
            return (true, null, taxa);
        }

        return (false, "Tipo de cliente inválido.", null);
    }

    public int CalcularScoreCredito(PessoaFisica pf)
    {
        var idade = DateTime.Today.Year - pf.DataNascimento.Year;
        return idade switch
        {
            < 18 => 0,
            < 25 => 400,
            < 35 => 600,
            < 50 => 750,
            _    => 850
        };
    }
    public decimal CalcularTaxaMDR(string setorAtuacao)
    {
        return setorAtuacao.ToUpper() switch
        {
            "VAREJO"    => 1.5m,
            "SERVICOS"  => 2.0m,
            "ALIMENTACAO" => 1.8m,
            "SAUDE"     => 1.6m,
            "TECNOLOGIA" => 2.2m,
            _           => 2.5m  
        };
    }

    public decimal CalcularTaxaJuros(int score)
    {
        return score switch
        {
            >= 900 => 0.99m,
            >= 800 => 1.29m,
            >= 700 => 1.59m,
            >= 600 => 1.99m,
            _      => 2.99m
        };
    }
}
