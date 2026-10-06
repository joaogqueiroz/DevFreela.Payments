using DevFreela.Payments.Api.Models;
using DevFreela.Payments.Api.Service;

namespace DevFreela.Payments.Tests
{
    public class PaymentServiceTests
    {
        private class FixedTime : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FixedTime(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static readonly DateTimeOffset Today = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

        private static Task<bool> Process(Action<PaymentInfoInputModel> change, DateTimeOffset? now = null)
        {
            // The data DevFreela sends in its own tests and seed
            var payment = new PaymentInfoInputModel
            {
                IdProject = 1,
                CreditCardNumber = "4111111111111111",
                Cvv = "123",
                ExpiresAt = "12/30",
                FullName = "Client",
                Amount = 1500
            };
            change(payment);
            return new PaymentService(new FixedTime(now ?? Today)).Process(payment);
        }

        [Fact]
        public async Task DevFreelaPayment_IsApproved()
        {
            Assert.True(await Process(_ => { }));
        }

        [Fact]
        public async Task NoPayment_IsRefused()
        {
            Assert.False(await new PaymentService(new FixedTime(Today)).Process(null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public async Task AmountNotAboveZero_IsRefused(decimal amount)
        {
            Assert.False(await Process(p => p.Amount = amount));
        }

        [Theory]
        [InlineData("4111111111111111")]   // Visa, 16 digits
        [InlineData("5555555555554444")]   // Mastercard
        [InlineData("378282246310005")]    // Amex, 15 digits
        [InlineData("4222222222222")]      // 13 digits
        public async Task ValidCardNumber_IsApproved(string number)
        {
            Assert.True(await Process(p => p.CreditCardNumber = number));
        }

        [Theory]
        [InlineData("4111111111111112")]       // fails the Luhn check
        [InlineData("4111 1111 1111 1111")]    // spaces
        [InlineData("4111-1111-1111-1111")]
        [InlineData("411111111111")]           // 12 digits
        [InlineData("41111111111111111111")]   // 20 digits
        [InlineData("411111111111111a")]
        [InlineData("")]
        [InlineData(null)]
        public async Task InvalidCardNumber_IsRefused(string? number)
        {
            Assert.False(await Process(p => p.CreditCardNumber = number!));
        }

        [Theory]
        [InlineData("123")]
        [InlineData("1234")]
        public async Task ThreeOrFourDigitCvv_IsApproved(string cvv)
        {
            Assert.True(await Process(p => p.Cvv = cvv));
        }

        [Theory]
        [InlineData("12")]
        [InlineData("12345")]
        [InlineData("12a")]
        [InlineData("")]
        [InlineData(null)]
        public async Task InvalidCvv_IsRefused(string? cvv)
        {
            Assert.False(await Process(p => p.Cvv = cvv!));
        }

        [Fact]
        public async Task CardExpiringThisMonth_IsApprovedUntilTheLastDay()
        {
            var lastMoment = new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero);

            Assert.True(await Process(p => p.ExpiresAt = "10/26", lastMoment));
        }

        [Fact]
        public async Task CardFromLastMonth_IsRefused()
        {
            Assert.False(await Process(p => p.ExpiresAt = "09/26"));
        }

        [Fact]
        public async Task CardExpiringThisMonth_IsRefusedFromTheFirstOfNextMonth()
        {
            var nextMonth = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

            Assert.False(await Process(p => p.ExpiresAt = "10/26", nextMonth));
        }

        [Theory]
        [InlineData("13/30")]
        [InlineData("1/30")]
        [InlineData("12/2030")]
        [InlineData("12-30")]
        [InlineData("")]
        [InlineData(null)]
        public async Task UnreadableExpiry_IsRefused(string? expiresAt)
        {
            Assert.False(await Process(p => p.ExpiresAt = expiresAt!));
        }
    }
}
