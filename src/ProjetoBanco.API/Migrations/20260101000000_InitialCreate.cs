
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjetoBanco.API.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AGENCIAS",
                columns: table => new
                {
                    Id     = table.Column<int>(nullable: false).Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    Nome   = table.Column<string>(maxLength: 200, nullable: false),
                    Codigo = table.Column<string>(maxLength: 10, nullable: false),
                    Cidade = table.Column<string>(maxLength: 100, nullable: false),
                    Estado = table.Column<string>(maxLength: 2, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_AGENCIAS", x => x.Id));

            migrationBuilder.CreateTable(
                name: "PRODUTOS",
                columns: table => new
                {
                    Id           = table.Column<int>(nullable: false).Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TIPO_PRODUTO = table.Column<string>(maxLength: 21, nullable: false),
                    Nome         = table.Column<string>(maxLength: 200, nullable: false),
                    Descricao    = table.Column<string>(maxLength: 500, nullable: true),
                    Ativo        = table.Column<bool>(nullable: false, defaultValue: true),
                    TaxaMDR             = table.Column<decimal>(type: "NUMBER(5,2)", nullable: true),
                    Modelo              = table.Column<string>(maxLength: 100, nullable: true),
                    NecessitaAtivacaoFisica = table.Column<bool>(nullable: true),
                    ValorMinimo         = table.Column<decimal>(type: "NUMBER(15,2)", nullable: true),
                    ValorMaximo         = table.Column<decimal>(type: "NUMBER(15,2)", nullable: true),
                    PrazoMaximoMeses    = table.Column<int>(nullable: true),
                    TaxaJurosMensalBase = table.Column<decimal>(type: "NUMBER(5,2)", nullable: true),
                    EmpresaConveniada   = table.Column<string>(maxLength: 200, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_PRODUTOS", x => x.Id));

            migrationBuilder.CreateTable(
                name: "CLIENTES",
                columns: table => new
                {
                    Id            = table.Column<int>(nullable: false).Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    TIPO_CLIENTE  = table.Column<string>(maxLength: 2, nullable: false),
                    Nome          = table.Column<string>(maxLength: 200, nullable: false),
                    Email         = table.Column<string>(maxLength: 200, nullable: false),
                    Telefone      = table.Column<string>(maxLength: 20, nullable: true),
                    DataCadastro  = table.Column<DateTime>(nullable: false),
                    AgenciaId     = table.Column<int>(nullable: false),
                    // PF
                    CPF             = table.Column<string>(maxLength: 14, nullable: true),
                    DataNascimento  = table.Column<DateTime>(nullable: true),
                    ScoreCredito    = table.Column<int>(nullable: true, defaultValue: 0),
                    // PJ
                    CNPJ         = table.Column<string>(maxLength: 18, nullable: true),
                    RazaoSocial  = table.Column<string>(maxLength: 300, nullable: true),
                    SetorAtuacao = table.Column<string>(maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLIENTES", x => x.Id);
                    table.ForeignKey("FK_CLIENTES_AGENCIAS", x => x.AgenciaId, "AGENCIAS", "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CONTRATACOES",
                columns: table => new
                {
                    Id                = table.Column<int>(nullable: false).Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    DataSolicitacao   = table.Column<DateTime>(nullable: false),
                    DataProcessamento = table.Column<DateTime>(nullable: true),
                    Status            = table.Column<string>(maxLength: 20, nullable: false),
                    MotivoRecusa      = table.Column<string>(maxLength: 500, nullable: true),
                    ClienteId         = table.Column<int>(nullable: false),
                    ProdutoId         = table.Column<int>(nullable: false),
                    ValorSolicitado   = table.Column<decimal>(type: "NUMBER(15,2)", nullable: true),
                    PrazoMeses        = table.Column<int>(nullable: true),
                    TaxaAplicada      = table.Column<decimal>(type: "NUMBER(5,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CONTRATACOES", x => x.Id);
                    table.ForeignKey("FK_CONTRATACOES_CLIENTES", x => x.ClienteId, "CLIENTES", "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_CONTRATACOES_PRODUTOS", x => x.ProdutoId, "PRODUTOS", "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_AGENCIAS_Codigo",  "AGENCIAS",  "Codigo",  unique: true);
            migrationBuilder.CreateIndex("IX_CLIENTES_CPF",     "CLIENTES",  "CPF",     unique: true);
            migrationBuilder.CreateIndex("IX_CLIENTES_CNPJ",    "CLIENTES",  "CNPJ",    unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("CONTRATACOES");
            migrationBuilder.DropTable("CLIENTES");
            migrationBuilder.DropTable("PRODUTOS");
            migrationBuilder.DropTable("AGENCIAS");
        }
    }
}
