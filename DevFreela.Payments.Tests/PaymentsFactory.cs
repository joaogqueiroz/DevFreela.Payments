using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace DevFreela.Payments.Tests
{
    // Runs the real Payments API, with its queue consumer, against RabbitMQ in a container.
    public class PaymentsFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        // RabbitMQ refuses the guest user from outside localhost, which is how the mapped port looks
        private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-management")
            .WithUsername("payments")
            .WithPassword("payments")
            .Build();

        private IConnection? _connection;

        public async Task InitializeAsync()
        {
            await _rabbitMq.StartAsync();
            _ = Server;   // starts the host, so the consumer is listening before the tests publish
            _connection = new ConnectionFactory
            {
                HostName = _rabbitMq.Hostname,
                Port = _rabbitMq.GetMappedPublicPort(5672),
                UserName = "payments",
                Password = "payments"
            }.CreateConnection();
        }

        public new async Task DisposeAsync()
        {
            _connection?.Dispose();
            await base.DisposeAsync();
            await _rabbitMq.DisposeAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("RabbitMQ:HostName", _rabbitMq.Hostname);
            builder.UseSetting("RabbitMQ:Port", _rabbitMq.GetMappedPublicPort(5672).ToString());
            builder.UseSetting("RabbitMQ:UserName", "payments");
            builder.UseSetting("RabbitMQ:Password", "payments");
        }

        // Publishes to the queue DevFreela writes payment requests to
        public void PublishPayment(string json)
        {
            using var channel = _connection!.CreateModel();
            channel.QueueDeclare("Payments", false, false, false, null);
            channel.BasicPublish("", "Payments", null, Encoding.UTF8.GetBytes(json));
        }

        // Reads every PaymentApproved message that arrives within the wait time
        public async Task<List<string>> ApprovalsWithin(TimeSpan wait)
        {
            using var channel = _connection!.CreateModel();
            channel.QueueDeclare("PaymentApproved", false, false, false, null);
            var received = new List<string>();
            var until = DateTime.UtcNow + wait;
            while (DateTime.UtcNow < until)
            {
                var message = channel.BasicGet("PaymentApproved", autoAck: true);
                if (message != null)
                {
                    received.Add(Encoding.UTF8.GetString(message.Body.ToArray()));
                }
                else
                {
                    await Task.Delay(100);
                }
            }
            return received;
        }
    }
}
