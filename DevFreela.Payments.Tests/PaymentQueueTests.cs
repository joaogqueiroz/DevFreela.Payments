using System.Text.Json;

namespace DevFreela.Payments.Tests
{
    // The whole round trip through RabbitMQ: DevFreela publishes to "Payments" and waits for
    // "PaymentApproved". Tests in this class share one broker, so they run one after another.
    public class PaymentQueueTests : IClassFixture<PaymentsFactory>
    {
        private static readonly TimeSpan Wait = TimeSpan.FromSeconds(4);
        private readonly PaymentsFactory _factory;

        public PaymentQueueTests(PaymentsFactory factory)
        {
            _factory = factory;
        }

        private static string Payment(int idProject) =>
            $@"{{""IdProject"":{idProject},""CreditCardNumber"":""4111111111111111"",""Cvv"":""123"",""ExpiresAt"":""12/30"",""FullName"":""Client"",""Amount"":1500}}";

        [Fact]
        public async Task ValidPayment_IsApprovedForTheSameProject()
        {
            _factory.PublishPayment(Payment(idProject: 41));

            var approval = Assert.Single(await _factory.ApprovalsWithin(Wait));
            Assert.Equal(41, JsonDocument.Parse(approval).RootElement.GetProperty("IdProject").GetInt32());
        }

        [Fact]
        public async Task UnreadableMessage_IsDroppedAndTheConsumerKeepsWorking()
        {
            _factory.PublishPayment("this is not a payment");
            _factory.PublishPayment(Payment(idProject: 42));

            var approvals = await _factory.ApprovalsWithin(Wait);

            var approval = Assert.Single(approvals);
            Assert.Equal(42, JsonDocument.Parse(approval).RootElement.GetProperty("IdProject").GetInt32());
        }

        [Fact]
        public async Task ExpiredCard_IsNotApproved()
        {
            _factory.PublishPayment(
                @"{""IdProject"":44,""CreditCardNumber"":""4111111111111111"",""Cvv"":""123"",""ExpiresAt"":""01/20"",""FullName"":""Client"",""Amount"":1500}");

            Assert.Empty(await _factory.ApprovalsWithin(Wait));
        }
    }
}
