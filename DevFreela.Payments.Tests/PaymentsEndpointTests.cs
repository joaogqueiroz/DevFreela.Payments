using System.Net;
using System.Text;

namespace DevFreela.Payments.Tests
{
    // POST /api/payments: 204 when the gateway approves, 400 otherwise
    public class PaymentsEndpointTests : IClassFixture<PaymentsFactory>
    {
        private readonly HttpClient _client;

        public PaymentsEndpointTests(PaymentsFactory factory)
        {
            _client = factory.CreateClient();
        }

        private Task<HttpResponseMessage> Post(string json) =>
            _client.PostAsync("/api/payments", new StringContent(json, Encoding.UTF8, "application/json"));

        [Fact]
        public async Task ValidPayment_ReturnsNoContent()
        {
            var response = await Post(
                @"{""idProject"":1,""creditCardNumber"":""4111111111111111"",""cvv"":""123"",""expiresAt"":""12/30"",""fullName"":""Client"",""amount"":1500}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Theory]
        [InlineData(@"{""idProject"":1,""creditCardNumber"":""4111111111111112"",""cvv"":""123"",""expiresAt"":""12/30"",""fullName"":""Client"",""amount"":1500}")]
        [InlineData(@"{""idProject"":1,""creditCardNumber"":""4111111111111111"",""cvv"":""1"",""expiresAt"":""12/30"",""fullName"":""Client"",""amount"":1500}")]
        [InlineData(@"{""idProject"":1,""creditCardNumber"":""4111111111111111"",""cvv"":""123"",""expiresAt"":""01/20"",""fullName"":""Client"",""amount"":1500}")]
        [InlineData(@"{""idProject"":1,""creditCardNumber"":""4111111111111111"",""cvv"":""123"",""expiresAt"":""12/30"",""fullName"":""Client"",""amount"":0}")]
        [InlineData(@"{""idProject"":1,""amount"":""a lot""}")]
        [InlineData("not json")]
        public async Task InvalidPayment_ReturnsBadRequest(string json)
        {
            var response = await Post(json);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
