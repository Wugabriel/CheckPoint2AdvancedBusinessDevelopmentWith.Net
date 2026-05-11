namespace ProjetoBanco.API.DTOs;

public record CriarAgenciaDto(
    string Nome,
    string Codigo,
    string Cidade,
    string Estado
);

public record AgenciaResponseDto(
    int Id,
    string Nome,
    string Codigo,
    string Cidade,
    string Estado
);

public record CriarPessoaFisicaDto(
    string Nome,
    string Email,
    string Telefone,
    string CPF,
    DateTime DataNascimento,
    int AgenciaId,
    int ScoreCredito = 500
);

public record CriarPessoaJuridicaDto(
    string Nome,
    string Email,
    string Telefone,
    string CNPJ,
    string RazaoSocial,
    string SetorAtuacao,
    int AgenciaId
);

public record ClienteResponseDto(
    int Id,
    string Tipo,
    string Nome,
    string Email,
    string Telefone,
    int AgenciaId,
    string AgenciaNome,
    DateTime DataCadastro,
    // PF extras
    string? CPF,
    DateTime? DataNascimento,
    int? ScoreCredito,
    // PJ extras
    string? CNPJ,
    string? RazaoSocial,
    string? SetorAtuacao
);

public record SolicitarContratacaoDto(
    int ClienteId,
    int ProdutoId,
    decimal? ValorSolicitado,
    int? PrazoMeses
);

public record ContratacaoResponseDto(
    int Id,
    int ClienteId,
    string ClienteNome,
    int ProdutoId,
    string ProdutoNome,
    string TipoProduto,
    string Status,
    DateTime DataSolicitacao,
    DateTime? DataProcessamento,
    string? MotivoRecusa,
    decimal? ValorSolicitado,
    int? PrazoMeses,
    decimal? TaxaAplicada
);

public record ContratacaoMensagemDto(
    int ContratacaoId,
    int ClienteId,
    int ProdutoId,
    string TipoProduto,
    decimal? ValorSolicitado,
    int? PrazoMeses,
    DateTime EnviadoEm
);
