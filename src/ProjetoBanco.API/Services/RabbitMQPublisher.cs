using System.Text;
using System.Text.Json;
using ProjetoBanco.API.DTOs;
using RabbitMQ.Client;

namespace ProjetoBanco.API.Services;

public interface IRabbitMQPublisher
{
    void PublicarContratacao(ContratacaoMensagemDto mensagem);
}

public class RabbitMQPublisher : IRabbitMQPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQPublisher> _logger;

    private const string ExchangeName = "banco.contratacoes";

    public RabbitMQPublisher(IConfiguration config, ILogger<RabbitMQPublisher> logger)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ:Host"] ?? "localhost",
            Port     = int.Parse(config["RabbitMQ:Port"] ?? "5672"),
            UserName = config["RabbitMQ:User"] ?? "guest",
            Password = config["RabbitMQ:Password"] ?? "guest",
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel    = _connection.CreateModel();
        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);

        foreach (var tipo in new[] { "MAQUINA", "EMPRESTIMO", "SALARIO" })
        {
            var fila = $"contratacao.{tipo.ToLower()}";
            _channel.QueueDeclare(fila, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(fila, ExchangeName, routingKey: tipo);
        }
    }

    public void PublicarContratacao(ContratacaoMensagemDto mensagem)
    {
        var json  = JsonSerializer.Serialize(mensagem);
        var bytes = Encoding.UTF8.GetBytes(json);

        var props = _channel.CreateBasicProperties();
        props.Persistent  = true;
        props.ContentType = "application/json";

        _channel.BasicPublish(
            exchange:   ExchangeName,
            routingKey: mensagem.TipoProduto,
            basicProperties: props,
            body: bytes
        );

        _logger.LogInformation(
            "[RabbitMQ] Mensagem publicada | ContratacaoId={Id} | RoutingKey={Tipo}",
            mensagem.ContratacaoId, mensagem.TipoProduto);
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
