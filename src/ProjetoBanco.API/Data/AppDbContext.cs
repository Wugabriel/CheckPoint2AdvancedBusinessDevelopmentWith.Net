using Microsoft.EntityFrameworkCore;
using ProjetoBanco.API.Models;

namespace ProjetoBanco.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<PessoaFisica> PessoasFisicas => Set<PessoaFisica>();
    public DbSet<PessoaJuridica> PessoasJuridicas => Set<PessoaJuridica>();
    public DbSet<Agencia> Agencias => Set<Agencia>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<MaquinaDeCartao> MaquinasDeCartao => Set<MaquinaDeCartao>();
    public DbSet<Emprestimo> Emprestimos => Set<Emprestimo>();
    public DbSet<ReceberSalario> ReceberSalarios => Set<ReceberSalario>();
    public DbSet<Contratacao> Contratacoes => Set<Contratacao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Cliente>(e =>
        {
            e.ToTable("CLIENTES");
            e.HasDiscriminator<string>("TIPO_CLIENTE")
             .HasValue<PessoaFisica>("PF")
             .HasValue<PessoaJuridica>("PJ");

            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Telefone).HasMaxLength(20);

            e.HasOne(c => c.Agencia)
             .WithMany(a => a.Clientes)
             .HasForeignKey(c => c.AgenciaId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PessoaFisica>(e =>
        {
            e.Property(x => x.CPF).HasMaxLength(14).IsRequired();
            e.HasIndex(x => x.CPF).IsUnique();
            e.Property(x => x.ScoreCredito).HasDefaultValue(0);
        });

        modelBuilder.Entity<PessoaJuridica>(e =>
        {
            e.Property(x => x.CNPJ).HasMaxLength(18).IsRequired();
            e.HasIndex(x => x.CNPJ).IsUnique();
            e.Property(x => x.RazaoSocial).HasMaxLength(300).IsRequired();
            e.Property(x => x.SetorAtuacao).HasMaxLength(100);
        });

        modelBuilder.Entity<Produto>(e =>
        {
            e.ToTable("PRODUTOS");
            e.HasDiscriminator<string>("TIPO_PRODUTO")
             .HasValue<MaquinaDeCartao>("MAQUINA")
             .HasValue<Emprestimo>("EMPRESTIMO")
             .HasValue<ReceberSalario>("SALARIO");

            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.Property(x => x.Descricao).HasMaxLength(500);
        });

        modelBuilder.Entity<MaquinaDeCartao>(e =>
        {
            e.Property(x => x.TaxaMDR).HasColumnType("NUMBER(5,2)");
            e.Property(x => x.Modelo).HasMaxLength(100);
        });

        modelBuilder.Entity<Emprestimo>(e =>
        {
            e.Property(x => x.ValorMinimo).HasColumnType("NUMBER(15,2)");
            e.Property(x => x.ValorMaximo).HasColumnType("NUMBER(15,2)");
            e.Property(x => x.TaxaJurosMensalBase).HasColumnType("NUMBER(5,2)");
        });

        modelBuilder.Entity<Agencia>(e =>
        {
            e.ToTable("AGENCIAS");
            e.Property(x => x.Nome).HasMaxLength(200).IsRequired();
            e.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.Property(x => x.Cidade).HasMaxLength(100);
            e.Property(x => x.Estado).HasMaxLength(2);
        });

        modelBuilder.Entity<Contratacao>(e =>
        {
            e.ToTable("CONTRATACOES");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.MotivoRecusa).HasMaxLength(500);
            e.Property(x => x.ValorSolicitado).HasColumnType("NUMBER(15,2)");
            e.Property(x => x.TaxaAplicada).HasColumnType("NUMBER(5,2)");

            e.HasOne(c => c.Cliente)
             .WithMany(cl => cl.Contratacoes)
             .HasForeignKey(c => c.ClienteId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(c => c.Produto)
             .WithMany()
             .HasForeignKey(c => c.ProdutoId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
