using DevFreela.Payments.Api.Consumers;
using DevFreela.Payments.Api.Models;
using DevFreela.Payments.Api.Service;
using Moq;

namespace DevFreela.Payments.Tests
{
    public class PaymentMessageHandlerTests
    {
        // The message DevFreela publishes to the Payments queue (PaymentInfoDTO)
        private const string PaymentMessage =
            @"{""IdProject"":7,""CreditCardNumber"":""4111111111111111"",""Cvv"":""123"",""ExpiresAt"":""12/30"",""FullName"":""Client"",""Amount"":1500}";

        private static (PaymentMessageHandler, Mock<IPaymentService>) Handler(bool approves)
        {
            var paymentService = new Mock<IPaymentService>();
            paymentService.Setup(s => s.Process(It.IsAny<PaymentInfoInputModel>())).ReturnsAsync(approves);
            return (new PaymentMessageHandler(paymentService.Object), paymentService);
        }

        [Fact]
        public async Task ApprovedPayment_ReturnsTheEventForThatProject()
        {
            var (handler, paymentService) = Handler(approves: true);

            var approved = await handler.HandleAsync(PaymentMessage);

            Assert.NotNull(approved);
            Assert.Equal(7, approved!.IdProject);
            paymentService.Verify(s => s.Process(It.Is<PaymentInfoInputModel>(p =>
                p.IdProject == 7 && p.CreditCardNumber == "4111111111111111" && p.Amount == 1500)), Times.Once);
        }

        // The consumer used to publish PaymentApproved without looking at the result
        [Fact]
        public async Task RefusedPayment_ReturnsNoEvent()
        {
            var (handler, _) = Handler(approves: false);

            Assert.Null(await handler.HandleAsync(PaymentMessage));
        }

        [Theory]
        [InlineData("not json")]
        [InlineData(@"{ ""IdProject"": 7, ")]                               // cut off
        [InlineData(@"{ ""IdProject"": ""seven"", ""Amount"": 1500 }")]   // text in a number
        [InlineData(@"{ ""IdProject"": 7, ""Amount"": null }")]           // DevFreela sends a nullable amount
        [InlineData("null")]
        public async Task UnreadableMessage_ReturnsNoEventAndChargesNothing(string message)
        {
            var (handler, paymentService) = Handler(approves: true);

            Assert.Null(await handler.HandleAsync(message));
            paymentService.Verify(s => s.Process(It.IsAny<PaymentInfoInputModel>()), Times.Never);
        }
    }
}
