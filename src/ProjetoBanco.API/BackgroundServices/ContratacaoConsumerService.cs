using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjetoBanco.API.Data;
using ProjetoBanco.API.DTOs;
using ProjetoBanco.API.Models;
using ProjetoBanco.API.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ProjetoBanco.API.BackgroundServices;

public class ContratacaoConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ContratacaoConsumerService> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    private static readonly string[] Filas =
        { "contratacao.maquina", "contratacao.emprestimo", "contratacao.salario" };

    public ContratacaoConsumerService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ContratacaoConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Consumer] Iniciando ContratacaoConsumerService...");

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _config["RabbitMQ:Host"] ?? "localhost",
                Port     = int.Parse(_config["RabbitMQ:Port"] ?? "5672"),
                UserName = _config["RabbitMQ:User"] ?? "guest",
                Password = _config["RabbitMQ:Password"] ?? "guest",
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel    = _connection.CreateModel();
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            foreach (var fila in Filas)
            {
                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.Received += OnMensagemRecebida;

                _channel.BasicConsume(
                    queue:     fila,
                    autoAck:   false, 
                    consumer:  consumer
                );

                _logger.LogInformation("[Consumer] Consumindo fila: {Fila}", fila);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Consumer] Falha ao conectar no RabbitMQ. Consumer não iniciado.");
        }

        return Task.CompletedTask;
    }

    private async Task OnMensagemRecebida(object sender, BasicDeliverEventArgs ea)
    {
        var deliveryTag = ea.DeliveryTag;
        ContratacaoMensagemDto? msg = null;

        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            msg = JsonSerializer.Deserialize<ContratacaoMensagemDto>(json);

            if (msg == null)
            {
                _logger.LogWarning("[Consumer] Mensagem inválida recebida. DeliveryTag={Tag}", deliveryTag);
                _channel!.BasicNack(deliveryTag, multiple: false, requeue: false);
                return;
            }

            _logger.LogInformation(
                "[Consumer] Processando ContratacaoId={Id} Tipo={Tipo}",
                msg.ContratacaoId, msg.TipoProduto);

            await ProcessarContratacaoAsync(msg);

            _channel!.BasicAck(deliveryTag, multiple: false);
            _logger.LogInformation("[Consumer] ACK enviado para ContratacaoId={Id}", msg.ContratacaoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Consumer] ERRO ao processar ContratacaoId={Id}. Mensagem retorna para fila.",
                msg?.ContratacaoId);

            _channel!.BasicNack(deliveryTag, multiple: false, requeue: true);
        }
    }

    private async Task ProcessarContratacaoAsync(ContratacaoMensagemDto msg)
    {
        using var scope = _scopeFactory.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var regras  = scope.ServiceProvider.GetRequiredService<IRegrasProdutoService>();

        var contratacao = await db.Contratacoes
            .Include(c => c.Cliente)
            .Include(c => c.Produto)
            .FirstOrDefaultAsync(c => c.Id == msg.ContratacaoId);

        if (contratacao == null)
        {
            _logger.LogWarning("[Consumer] ContratacaoId={Id} não encontrada.", msg.ContratacaoId);
            return;
        }

        bool aprovado;
        string? motivo;
        decimal? taxaAplicada;

        switch (contratacao.Produto)
        {
            case MaquinaDeCartao maquina:
                (aprovado, motivo, taxaAplicada) = regras.AvaliarMaquinaDeCartao(contratacao.Cliente!, maquina);
                break;

            case Emprestimo emprestimo:
                (aprovado, motivo, taxaAplicada) = regras.AvaliarEmprestimo(
                    contratacao.Cliente!, emprestimo,
                    msg.ValorSolicitado ?? 0,
                    msg.PrazoMeses ?? 0);
                break;

            default:
                aprovado     = true;
                motivo       = null;
                taxaAplicada = null;
                break;
        }

        contratacao.Status            = aprovado ? StatusContratacao.APROVADA : StatusContratacao.RECUSADA;
        contratacao.MotivoRecusa      = motivo;
        contratacao.TaxaAplicada      = taxaAplicada;
        contratacao.DataProcessamento = DateTime.UtcNow;

        await db.SaveChangesAsync();

        _logger.LogInformation(
            "[Consumer] ContratacaoId={Id} finalizada com Status={Status}",
            contratacao.Id, contratacao.Status);
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
