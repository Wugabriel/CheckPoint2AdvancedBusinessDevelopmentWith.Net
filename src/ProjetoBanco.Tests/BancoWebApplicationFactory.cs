using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ProjetoBanco.API.Data;
using ProjetoBanco.API.Services;

namespace ProjetoBanco.Tests;

public class BancoWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Troca Oracle por InMemory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(opt =>
                opt.UseInMemoryDatabase("TestDb"));

            // Mock do RabbitMQ — não queremos conexão real nos testes
            var publisherDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRabbitMQPublisher));
            if (publisherDescriptor != null) services.Remove(publisherDescriptor);

            var mockPublisher = new Mock<IRabbitMQPublisher>();
            mockPublisher.Setup(p => p.PublicarContratacao(It.IsAny<ProjetoBanco.API.DTOs.ContratacaoMensagemDto>()));
            services.AddSingleton(mockPublisher.Object);

            // Remove o BackgroundService consumer (não tem RabbitMQ em teste)
            var consumerDescriptor = services.SingleOrDefault(
                d => d.ImplementationType == typeof(ProjetoBanco.API.BackgroundServices.ContratacaoConsumerService));
            if (consumerDescriptor != null) services.Remove(consumerDescriptor);
        });
    }
}
