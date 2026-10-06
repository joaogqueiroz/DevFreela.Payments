using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DevFreela.Payments.Api.Models;
namespace DevFreela.Payments.Api.Service
{
  // Simulated gateway: there is no real charge, but it refuses the data a real one would refuse
  public class PaymentService : IPaymentService
  {
    private readonly TimeProvider _timeProvider;

    public PaymentService(TimeProvider timeProvider)
    {
      _timeProvider = timeProvider;
    }

    public Task<bool> Process(PaymentInfoInputModel paymentInfoInputModel)
    {
      var payment = paymentInfoInputModel;
      var approved = payment != null
        && payment.Amount > 0
        && IsValidCardNumber(payment.CreditCardNumber)
        && IsDigits(payment.Cvv, 3, 4)
        && !IsExpired(payment.ExpiresAt);

      return Task.FromResult(approved);
    }

    private static bool IsDigits(string? value, int minLength, int maxLength) =>
      value != null && value.Length >= minLength && value.Length <= maxLength && value.All(char.IsAsciiDigit);

    // Luhn check: catches mistyped card numbers
    private static bool IsValidCardNumber(string? number)
    {
      if (!IsDigits(number, 13, 19))
      {
        return false;
      }

      var sum = 0;
      for (var i = 0; i < number!.Length; i++)
      {
        var digit = number[number.Length - 1 - i] - '0';
        if (i % 2 == 1)
        {
          digit *= 2;
          if (digit > 9)
          {
            digit -= 9;
          }
        }
        sum += digit;
      }
      return sum % 10 == 0;
    }

    // ExpiresAt is MM/yy; the card is valid through the last day of that month
    private bool IsExpired(string? expiresAt)
    {
      if (!DateTime.TryParseExact(expiresAt, "MM/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiry))
      {
        return true;
      }

      var today = _timeProvider.GetUtcNow().UtcDateTime.Date;
      return today >= expiry.AddMonths(1);
    }
  }
}