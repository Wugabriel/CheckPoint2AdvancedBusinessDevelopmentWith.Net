using Microsoft.Extensions.Logging.Abstractions;
using ProjetoBanco.API.Models;
using ProjetoBanco.API.Services;
using Xunit;

namespace ProjetoBanco.Tests;

/// <summary>Testes unitários das regras de negócio dos produtos.</summary>
public class RegrasProdutoServiceTests
{
    private readonly RegrasProdutoService _svc =
        new(NullLogger<RegrasProdutoService>.Instance);

    // ── Score ─────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(17, 0)]
    [InlineData(22, 400)]
    [InlineData(30, 600)]
    [InlineData(40, 750)]
    [InlineData(55, 850)]
    public void CalcularScoreCredito_RetornaEsperado(int idade, int scoreEsperado)
    {
        var pf = new PessoaFisica { DataNascimento = DateTime.Today.AddYears(-idade) };
        Assert.Equal(scoreEsperado, _svc.CalcularScoreCredito(pf));
    }

    // ── Taxa MDR ──────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("VAREJO", 1.5)]
    [InlineData("SERVICOS", 2.0)]
    [InlineData("TECNOLOGIA", 2.2)]
    [InlineData("OUTRO_QUALQUER", 2.5)]
    public void CalcularTaxaMDR_RetornaCorreta(string setor, double taxaEsperada)
    {
        var taxa = _svc.CalcularTaxaMDR(setor);
        Assert.Equal((decimal)taxaEsperada, taxa);
    }

    // ── Taxa Juros ────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(950, 0.99)]
    [InlineData(800, 1.29)]
    [InlineData(700, 1.59)]
    [InlineData(600, 1.99)]
    [InlineData(300, 2.99)]
    public void CalcularTaxaJuros_RetornaCorreta(int score, double taxaEsperada)
    {
        Assert.Equal((decimal)taxaEsperada, _svc.CalcularTaxaJuros(score));
    }

    // ── Máquina de Cartão ─────────────────────────────────────────────────────
    [Fact]
    public void AvaliarMaquinaDeCartao_PF_ScoreBaixo_Recusa()
    {
        var pf      = new PessoaFisica { ScoreCredito = 300, Nome = "Teste" };
        var produto = new MaquinaDeCartao();
        var (aprovado, motivo, _) = _svc.AvaliarMaquinaDeCartao(pf, produto);
        Assert.False(aprovado);
        Assert.Contains("Score", motivo);
    }

    [Fact]
    public void AvaliarMaquinaDeCartao_PF_ScoreOk_Aprova()
    {
        var pf      = new PessoaFisica { ScoreCredito = 500, Nome = "Teste" };
        var produto = new MaquinaDeCartao();
        var (aprovado, _, taxa) = _svc.AvaliarMaquinaDeCartao(pf, produto);
        Assert.True(aprovado);
        Assert.NotNull(taxa);
    }

    [Fact]
    public void AvaliarMaquinaDeCartao_PJ_SetorProibido_Recusa()
    {
        var pj      = new PessoaJuridica { SetorAtuacao = "CASSINO", Nome = "Empresa X" };
        var produto = new MaquinaDeCartao();
        var (aprovado, motivo, _) = _svc.AvaliarMaquinaDeCartao(pj, produto);
        Assert.False(aprovado);
        Assert.Contains("convênio", motivo);
    }

    [Fact]
    public void AvaliarMaquinaDeCartao_PJ_SetorOk_Aprova()
    {
        var pj      = new PessoaJuridica { SetorAtuacao = "VAREJO", Nome = "Empresa Y" };
        var produto = new MaquinaDeCartao();
        var (aprovado, _, taxa) = _svc.AvaliarMaquinaDeCartao(pj, produto);
        Assert.True(aprovado);
        Assert.Equal(1.5m, taxa);
    }

    // ── Empréstimo ────────────────────────────────────────────────────────────
    [Fact]
    public void AvaliarEmprestimo_PF_ScoreInsuficiente_Recusa()
    {
        var pf      = new PessoaFisica { ScoreCredito = 550, Nome = "Teste" };
        var produto = new Emprestimo { ValorMinimo = 1000, ValorMaximo = 100_000, PrazoMaximoMeses = 60, TaxaJurosMensalBase = 1.5m };
        var (aprovado, motivo, _) = _svc.AvaliarEmprestimo(pf, produto, 5000, 12);
        Assert.False(aprovado);
        Assert.Contains("600", motivo);
    }

    [Fact]
    public void AvaliarEmprestimo_PF_ScoreOk_Aprova()
    {
        var pf      = new PessoaFisica { ScoreCredito = 750, Nome = "Teste" };
        var produto = new Emprestimo { ValorMinimo = 1000, ValorMaximo = 100_000, PrazoMaximoMeses = 60, TaxaJurosMensalBase = 1.5m };
        var (aprovado, _, taxa) = _svc.AvaliarEmprestimo(pf, produto, 10_000, 24);
        Assert.True(aprovado);
        Assert.Equal(1.59m, taxa); // score 700-800
    }

    [Fact]
    public void AvaliarEmprestimo_ValorAbaixoMinimo_Recusa()
    {
        var pf      = new PessoaFisica { ScoreCredito = 700, Nome = "Teste" };
        var produto = new Emprestimo { ValorMinimo = 1000, ValorMaximo = 100_000, PrazoMaximoMeses = 60, TaxaJurosMensalBase = 1.5m };
        var (aprovado, motivo, _) = _svc.AvaliarEmprestimo(pf, produto, 500, 12);
        Assert.False(aprovado);
        Assert.Contains("mínimo", motivo);
    }

    [Fact]
    public void AvaliarEmprestimo_PJ_SetorRiscoAlto_LimiteExcedido_Recusa()
    {
        var pj      = new PessoaJuridica { SetorAtuacao = "CONSTRUCAO", Nome = "Construtora" };
        var produto = new Emprestimo { ValorMinimo = 1000, ValorMaximo = 100_000, PrazoMaximoMeses = 60, TaxaJurosMensalBase = 1.5m };
        var (aprovado, motivo, _) = _svc.AvaliarEmprestimo(pj, produto, 80_000, 24);
        Assert.False(aprovado);
        Assert.Contains("50.000", motivo);
    }
}
