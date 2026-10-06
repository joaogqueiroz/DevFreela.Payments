using DevFreela.Payments.Api.Models;
using DevFreela.Payments.Api.Service;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DevFreela.Payments.Api.Consumers
{
    public class ProcessPaymentConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProcessPaymentConsumer> _logger;
        private const string QUEUE_NAME = "Payments";
        private const string PAYMENT_APPROVED_QUEUE = "PaymentApproved";

        public ProcessPaymentConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<ProcessPaymentConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var rabbitMq = configuration.GetSection("RabbitMQ");
            var _factory = new ConnectionFactory
            {
                HostName = rabbitMq["HostName"],
                Port = rabbitMq.GetValue<int>("Port"),
                UserName = rabbitMq["UserName"],
                Password = rabbitMq["Password"]
            };
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(
              queue: QUEUE_NAME,
              durable: false,
              exclusive: false,
              autoDelete: false,
              arguments: null);

            _channel.QueueDeclare(
                queue: PAYMENT_APPROVED_QUEUE,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (sender, eventArgs) =>
            {
                var paymentInfoJson = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var handler = new PaymentMessageHandler(scope.ServiceProvider.GetRequiredService<IPaymentService>());

                    // Only an approved payment is announced; a refused or unreadable one is not
                    var paymentApproved = await handler.HandleAsync(paymentInfoJson);
                    if (paymentApproved != null)
                    {
                        var paymentApprovedBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(paymentApproved));
                        _channel.BasicPublish(
                            exchange: "",
                            routingKey: PAYMENT_APPROVED_QUEUE,
                            basicProperties: null,
                            body: paymentApprovedBytes);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Payment message could not be processed");
                }

                // Retrying would not change the outcome, so the message is always removed
                _channel.BasicAck(eventArgs.DeliveryTag, false);
            };
            _channel.BasicConsume(QUEUE_NAME, false, consumer);
            return Task.CompletedTask;
        }
    }
}