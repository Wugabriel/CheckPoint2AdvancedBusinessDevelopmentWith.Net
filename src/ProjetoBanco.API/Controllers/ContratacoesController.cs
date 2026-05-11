using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoBanco.API.Data;
using ProjetoBanco.API.DTOs;
using ProjetoBanco.API.Models;
using ProjetoBanco.API.Services;

namespace ProjetoBanco.API.Controllers;

[ApiController]
[Route("api/contratacoes")]
[Produces("application/json")]
public class ContratacoesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRabbitMQPublisher _publisher;
    private readonly ILogger<ContratacoesController> _logger;

    public ContratacoesController(
        AppDbContext db,
        IRabbitMQPublisher publisher,
        ILogger<ContratacoesController> logger)
    {
        _db        = db;
        _publisher = publisher;
        _logger    = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ContratacaoResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Solicitar([FromBody] SolicitarContratacaoDto dto)
    {
        var cliente = await _db.Clientes
            .Include(c => c.Agencia)
            .FirstOrDefaultAsync(c => c.Id == dto.ClienteId);

        if (cliente == null)
            return NotFound(new { erro = $"Cliente {dto.ClienteId} não encontrado." });

        var produto = await _db.Produtos.FindAsync(dto.ProdutoId);
        if (produto == null)
            return NotFound(new { erro = $"Produto {dto.ProdutoId} não encontrado." });

        if (!produto.Ativo)
            return BadRequest(new { erro = "Produto inativo." });

        if (produto is Emprestimo && (dto.ValorSolicitado == null || dto.PrazoMeses == null))
            return BadRequest(new { erro = "ValorSolicitado e PrazoMeses são obrigatórios para Empréstimo." });

        var contratacao = new Contratacao
        {
            ClienteId       = dto.ClienteId,
            ProdutoId       = dto.ProdutoId,
            Status          = StatusContratacao.PENDENTE,
            ValorSolicitado = dto.ValorSolicitado,
            PrazoMeses      = dto.PrazoMeses
        };

        _db.Contratacoes.Add(contratacao);
        await _db.SaveChangesAsync();

        var tipoProduto = produto switch
        {
            MaquinaDeCartao => "MAQUINA",
            Emprestimo      => "EMPRESTIMO",
            ReceberSalario  => "SALARIO",
            _               => "DESCONHECIDO"
        };

        var mensagem = new ContratacaoMensagemDto(
            contratacao.Id,
            dto.ClienteId,
            dto.ProdutoId,
            tipoProduto,
            dto.ValorSolicitado,
            dto.PrazoMeses,
            DateTime.UtcNow
        );

        _publisher.PublicarContratacao(mensagem);

        _logger.LogInformation(
            "Contratação solicitada: Id={Id} Cliente={ClienteId} Produto={ProdutoId} Tipo={Tipo}",
            contratacao.Id, dto.ClienteId, dto.ProdutoId, tipoProduto);

        return AcceptedAtAction(nameof(BuscarPorId),
            new { id = contratacao.Id },
            await ToDto(contratacao, cliente, produto));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ContratacaoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BuscarPorId(int id)
    {
        var contratacao = await _db.Contratacoes
            .Include(c => c.Cliente)
            .Include(c => c.Produto)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contratacao == null)
            return NotFound(new { erro = $"Contratação {id} não encontrada." });

        return Ok(await ToDto(contratacao, contratacao.Cliente!, contratacao.Produto!));
    }

    private static Task<ContratacaoResponseDto> ToDto(Contratacao c, Cliente cliente, Produto produto)
    {
        var tipo = produto switch
        {
            MaquinaDeCartao => "MaquinaDeCartao",
            Emprestimo      => "Emprestimo",
            ReceberSalario  => "ReceberSalario",
            _               => produto.GetType().Name
        };

        return Task.FromResult(new ContratacaoResponseDto(
            c.Id,
            cliente.Id, cliente.Nome,
            produto.Id, produto.Nome, tipo,
            c.Status.ToString(),
            c.DataSolicitacao,
            c.DataProcessamento,
            c.MotivoRecusa,
            c.ValorSolicitado,
            c.PrazoMeses,
            c.TaxaAplicada
        ));
    }
}
