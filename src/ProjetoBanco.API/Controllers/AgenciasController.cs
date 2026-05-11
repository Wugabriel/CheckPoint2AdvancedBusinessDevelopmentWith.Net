using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoBanco.API.Data;
using ProjetoBanco.API.DTOs;
using ProjetoBanco.API.Models;

namespace ProjetoBanco.API.Controllers;

[ApiController]
[Route("api/agencias")]
[Produces("application/json")]
public class AgenciasController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<AgenciasController> _logger;

    public AgenciasController(AppDbContext db, ILogger<AgenciasController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(typeof(AgenciaResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cadastrar([FromBody] CriarAgenciaDto dto)
    {
        var codigoExistente = await _db.Agencias
            .Where(a => a.Codigo == dto.Codigo)
            .Select(a => a.Id)
            .FirstOrDefaultAsync();

        if (codigoExistente != 0)
            return Conflict(new { erro = $"Código de agência '{dto.Codigo}' já existe." });

        var agencia = new Agencia
        {
            Nome   = dto.Nome,
            Codigo = dto.Codigo,
            Cidade = dto.Cidade,
            Estado = dto.Estado
        };

        _db.Agencias.Add(agencia);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Agência cadastrada: Id={Id} Codigo={Codigo}", agencia.Id, agencia.Codigo);

        return CreatedAtAction(nameof(BuscarPorId), new { id = agencia.Id }, ToDto(agencia));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AgenciaResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BuscarPorId(int id)
    {
        var agencia = await _db.Agencias.FindAsync(id);

        if (agencia == null)
            return NotFound(new { erro = $"Agência {id} não encontrada." });

        return Ok(ToDto(agencia));
    }

    private static AgenciaResponseDto ToDto(Agencia a) =>
        new(a.Id, a.Nome, a.Codigo, a.Cidade, a.Estado);
}
