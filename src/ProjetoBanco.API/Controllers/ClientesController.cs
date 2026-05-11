using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoBanco.API.Data;
using ProjetoBanco.API.DTOs;
using ProjetoBanco.API.Models;
using ProjetoBanco.API.Services;

namespace ProjetoBanco.API.Controllers;

[ApiController]
[Route("api/clientes")]
[Produces("application/json")]
public class ClientesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IRegrasProdutoService _regras;
    private readonly ILogger<ClientesController> _logger;

    public ClientesController(AppDbContext db, IRegrasProdutoService regras, ILogger<ClientesController> logger)
    {
        _db     = db;
        _regras = regras;
        _logger = logger;
    }

    [HttpPost("pf")]
    [ProducesResponseType(typeof(ClienteResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CadastrarPF([FromBody] CriarPessoaFisicaDto dto)
    {
        var cpfExistente = await _db.PessoasFisicas
            .Where(p => p.CPF == dto.CPF)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (cpfExistente != 0)
            return Conflict(new { erro = $"CPF '{dto.CPF}' já cadastrado." });

        var agencia = await _db.Agencias.FindAsync(dto.AgenciaId);
        if (agencia == null)
            return NotFound(new { erro = $"Agência {dto.AgenciaId} não encontrada." });

        var pf = new PessoaFisica
        {
            Nome            = dto.Nome,
            Email           = dto.Email,
            Telefone        = dto.Telefone,
            CPF             = dto.CPF,
            DataNascimento  = dto.DataNascimento,
            AgenciaId       = dto.AgenciaId,
            ScoreCredito    = dto.ScoreCredito > 0 ? dto.ScoreCredito : _regras.CalcularScoreCredito(new PessoaFisica { DataNascimento = dto.DataNascimento })
        };

        _db.PessoasFisicas.Add(pf);
        await _db.SaveChangesAsync();

        _logger.LogInformation("PF cadastrada: Id={Id} CPF={CPF} Score={Score}", pf.Id, pf.CPF, pf.ScoreCredito);

        return CreatedAtAction(nameof(BuscarPorId), new { id = pf.Id }, ToDto(pf, agencia));
    }

    [HttpPost("pj")]
    [ProducesResponseType(typeof(ClienteResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CadastrarPJ([FromBody] CriarPessoaJuridicaDto dto)
    {
        var cnpjExistente = await _db.PessoasJuridicas
            .Where(p => p.CNPJ == dto.CNPJ)
            .Select(p => p.Id)
            .FirstOrDefaultAsync();

        if (cnpjExistente != 0)
            return Conflict(new { erro = $"CNPJ '{dto.CNPJ}' já cadastrado." });

        var agencia = await _db.Agencias.FindAsync(dto.AgenciaId);
        if (agencia == null)
            return NotFound(new { erro = $"Agência {dto.AgenciaId} não encontrada." });

        var pj = new PessoaJuridica
        {
            Nome          = dto.Nome,
            Email         = dto.Email,
            Telefone      = dto.Telefone,
            CNPJ          = dto.CNPJ,
            RazaoSocial   = dto.RazaoSocial,
            SetorAtuacao  = dto.SetorAtuacao,
            AgenciaId     = dto.AgenciaId
        };

        _db.PessoasJuridicas.Add(pj);
        await _db.SaveChangesAsync();

        _logger.LogInformation("PJ cadastrada: Id={Id} CNPJ={CNPJ}", pj.Id, pj.CNPJ);

        return CreatedAtAction(nameof(BuscarPorId), new { id = pj.Id }, ToDto(pj, agencia));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ClienteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BuscarPorId(int id)
    {
        var cliente = await _db.Clientes
            .Include(c => c.Agencia)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (cliente == null)
            return NotFound(new { erro = $"Cliente {id} não encontrado." });

        return Ok(ToDto(cliente, cliente.Agencia!));
    }

    private static ClienteResponseDto ToDto(Cliente c, Agencia a) => new(
        c.Id,
        c is PessoaFisica ? "PF" : "PJ",
        c.Nome, c.Email, c.Telefone, c.AgenciaId, a.Nome, c.DataCadastro,
        c is PessoaFisica pf2 ? pf2.CPF          : null,
        c is PessoaFisica pf3 ? pf3.DataNascimento : null,
        c is PessoaFisica pf4 ? pf4.ScoreCredito  : null,
        c is PessoaJuridica pj2 ? pj2.CNPJ        : null,
        c is PessoaJuridica pj3 ? pj3.RazaoSocial : null,
        c is PessoaJuridica pj4 ? pj4.SetorAtuacao : null
    );
}
