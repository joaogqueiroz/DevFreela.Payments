using DevFreela.Payments.Api.Models;
using DevFreela.Payments.Api.Service;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DevFreela.Payments.Tests
{
    // Same API and broker, but the payment gateway refuses every payment
    public class RefusingPaymentsFactory : PaymentsFactory
    {
        private class RefusingPaymentService : IPaymentService
        {
            public Task<bool> Process(PaymentInfoInputModel paymentInfoInputModel) => Task.FromResult(false);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddScoped<IPaymentService, RefusingPaymentService>());
        }
    }

    public class RefusedPaymentQueueTests : IClassFixture<RefusingPaymentsFactory>
    {
        private readonly RefusingPaymentsFactory _factory;

        public RefusedPaymentQueueTests(RefusingPaymentsFactory factory)
        {
            _factory = factory;
        }

        // The consumer used to publish PaymentApproved without waiting for the gateway's answer,
        // so DevFreela finished projects whose payment had been refused
        [Fact]
        public async Task RefusedPayment_IsNotAnnounced()
        {
            _factory.PublishPayment(
                @"{""IdProject"":43,""CreditCardNumber"":""4111111111111111"",""Cvv"":""123"",""ExpiresAt"":""12/30"",""FullName"":""Client"",""Amount"":1500}");

            Assert.Empty(await _factory.ApprovalsWithin(TimeSpan.FromSeconds(4)));
        }
    }
}
