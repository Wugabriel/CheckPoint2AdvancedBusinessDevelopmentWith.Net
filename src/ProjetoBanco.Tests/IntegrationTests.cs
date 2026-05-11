using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ProjetoBanco.API.Data;
using ProjetoBanco.API.DTOs;
using ProjetoBanco.API.Models;
using Xunit;

namespace ProjetoBanco.Tests;

/// <summary>
/// Testes de integração cobrindo todos os fluxos críticos (seção 3.4 do enunciado).
/// Usa WebApplicationFactory com InMemory e Mock do RabbitMQ.
/// </summary>
public class IntegrationTests : IClassFixture<BancoWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly BancoWebApplicationFactory _factory;

    public IntegrationTests(BancoWebApplicationFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // ── Helper: cria agência via API ──────────────────────────────────────────
    private async Task<int> CriarAgenciaAsync(string codigo = "0001")
    {
        var dto  = new CriarAgenciaDto("Agência Central", codigo, "São Paulo", "SP");
        var resp = await _client.PostAsJsonAsync("/api/agencias", dto);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<AgenciaResponseDto>();
        return body!.Id;
    }

    // ── Helper: cria produto direto no banco ──────────────────────────────────
    private async Task<int> CriarProdutoAsync<T>(T produto) where T : Produto
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return produto.Id;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Cadastro de cliente PF
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CadastrarPF_Valido_Retorna201()
    {
        var agId = await CriarAgenciaAsync("0010");
        var dto  = new CriarPessoaFisicaDto("João Silva", "joao@email.com", "11999990000",
            "123.456.789-09", new DateTime(1990, 1, 1), agId);

        var resp = await _client.PostAsJsonAsync("/api/clientes/pf", dto);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ClienteResponseDto>();
        Assert.Equal("PF", body!.Tipo);
        Assert.Equal("123.456.789-09", body.CPF);
    }

    [Fact]
    public async Task CadastrarPF_CPFDuplicado_Retorna409()
    {
        var agId = await CriarAgenciaAsync("0011");
        var dto  = new CriarPessoaFisicaDto("Ana Lima", "ana@email.com", "11888880000",
            "111.111.111-11", new DateTime(1985, 5, 10), agId);

        await _client.PostAsJsonAsync("/api/clientes/pf", dto);
        var resp2 = await _client.PostAsJsonAsync("/api/clientes/pf", dto);

        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Cadastro de cliente PJ
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CadastrarPJ_Valido_Retorna201()
    {
        var agId = await CriarAgenciaAsync("0020");
        var dto  = new CriarPessoaJuridicaDto("Empresa ABC", "contato@abc.com", "1133330000",
            "12.345.678/0001-90", "ABC Ltda", "VAREJO", agId);

        var resp = await _client.PostAsJsonAsync("/api/clientes/pj", dto);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ClienteResponseDto>();
        Assert.Equal("PJ", body!.Tipo);
        Assert.Equal("12.345.678/0001-90", body.CNPJ);
    }

    [Fact]
    public async Task CadastrarPJ_CNPJDuplicado_Retorna409()
    {
        var agId = await CriarAgenciaAsync("0021");
        var dto  = new CriarPessoaJuridicaDto("Empresa XYZ", "xyz@email.com", "1144440000",
            "99.999.999/0001-99", "XYZ Ltda", "SERVICOS", agId);

        await _client.PostAsJsonAsync("/api/clientes/pj", dto);
        var resp2 = await _client.PostAsJsonAsync("/api/clientes/pj", dto);

        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Vincular cliente a agência inexistente
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CadastrarPF_AgenciaInexistente_Retorna404()
    {
        var dto = new CriarPessoaFisicaDto("Pedro Costa", "pedro@email.com", "11777770000",
            "222.333.444-55", new DateTime(1992, 3, 20), agenciaId: 99999);

        var resp = await _client.PostAsJsonAsync("/api/clientes/pf", dto);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Solicitação de contratação válida (publica na fila → 202)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SolicitarContratacao_Valida_Retorna202()
    {
        var agId = await CriarAgenciaAsync("0030");
        var pfDto = new CriarPessoaFisicaDto("Maria Souza", "maria@email.com", "11666660000",
            "333.444.555-66", new DateTime(1988, 7, 15), agId, ScoreCredito: 700);
        var pfResp = await _client.PostAsJsonAsync("/api/clientes/pf", pfDto);
        var pf = await pfResp.Content.ReadFromJsonAsync<ClienteResponseDto>();

        var prodId = await CriarProdutoAsync(new MaquinaDeCartao
        {
            Nome  = "Maquininha Pro",
            Descricao = "Aceita todas as bandeiras",
            TaxaMDR = 1.5m,
            Modelo = "POS-500",
            Ativo = true
        });

        var contDto = new SolicitarContratacaoDto(pf!.Id, prodId, null, null);
        var resp    = await _client.PostAsJsonAsync("/api/contratacoes", contDto);

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ContratacaoResponseDto>();
        Assert.Equal("PENDENTE", body!.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Contratação para cliente inexistente → 404
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SolicitarContratacao_ClienteInexistente_Retorna404()
    {
        var prodId = await CriarProdutoAsync(new MaquinaDeCartao
        {
            Nome = "Maquina Test", Ativo = true
        });

        var dto  = new SolicitarContratacaoDto(99999, prodId, null, null);
        var resp = await _client.PostAsJsonAsync("/api/contratacoes", dto);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. Consulta de status após processamento (simulado via banco direto)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ConsultarContratacao_Aprovada_RetornaStatusCorreto()
    {
        // Cria contratação diretamente no banco (simula Consumer)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agencia = new Agencia { Nome = "AG Teste", Codigo = "0040", Cidade = "SP", Estado = "SP" };
        db.Agencias.Add(agencia);
        await db.SaveChangesAsync();

        var pf = new PessoaFisica
        {
            Nome = "Test User", Email = "t@t.com", CPF = "444.555.666-77",
            DataNascimento = new DateTime(1985, 1, 1), ScoreCredito = 800, AgenciaId = agencia.Id
        };
        db.PessoasFisicas.Add(pf);

        var produto = new MaquinaDeCartao { Nome = "Maquina Status", Ativo = true };
        db.MaquinasDeCartao.Add(produto);
        await db.SaveChangesAsync();

        var contratacao = new Contratacao
        {
            ClienteId  = pf.Id, ProdutoId  = produto.Id,
            Status     = StatusContratacao.APROVADA,
            TaxaAplicada = 1.5m,
            DataProcessamento = DateTime.UtcNow
        };
        db.Contratacoes.Add(contratacao);
        await db.SaveChangesAsync();

        var resp = await _client.GetAsync($"/api/contratacoes/{contratacao.Id}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ContratacaoResponseDto>();
        Assert.Equal("APROVADA", body!.Status);
        Assert.Equal(1.5m, body.TaxaAplicada);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. Health check retorna Healthy
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task HealthCheck_Retorna200()
    {
        var resp = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 8. Empréstimo sem campos obrigatórios → 400
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task SolicitarContratacao_EmprestimoSemCampos_Retorna400()
    {
        var agId = await CriarAgenciaAsync("0050");
        var pfDto = new CriarPessoaFisicaDto("Carlos Souza", "carlos@email.com", "11555550000",
            "555.666.777-88", new DateTime(1980, 2, 10), agId, ScoreCredito: 750);
        var pfResp = await _client.PostAsJsonAsync("/api/clientes/pf", pfDto);
        var pf = await pfResp.Content.ReadFromJsonAsync<ClienteResponseDto>();

        var prodId = await CriarProdutoAsync(new Emprestimo
        {
            Nome = "Empréstimo Pessoal", ValorMinimo = 1000, ValorMaximo = 100_000,
            PrazoMaximoMeses = 60, TaxaJurosMensalBase = 1.5m, Ativo = true
        });

        // Não passa ValorSolicitado nem PrazoMeses
        var dto  = new SolicitarContratacaoDto(pf!.Id, prodId, null, null);
        var resp = await _client.PostAsJsonAsync("/api/contratacoes", dto);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
