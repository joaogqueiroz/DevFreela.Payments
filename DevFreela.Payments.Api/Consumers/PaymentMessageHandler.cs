using System.Text.Json;
using System.Threading.Tasks;
using DevFreela.Payments.Api.Models;
using DevFreela.Payments.Api.Service;

namespace DevFreela.Payments.Api.Consumers
{
  // Decides what a payment request from the queue leads to, without any RabbitMQ code,
  // so it can be tested on its own.
  public class PaymentMessageHandler
  {
    private readonly IPaymentService _paymentService;

    public PaymentMessageHandler(IPaymentService paymentService)
    {
      _paymentService = paymentService;
    }

    // Returns the event to publish when the payment was approved, or null when the message
    // cannot be read or the payment was refused: DevFreela finishes the project only on approval.
    public async Task<PaymentApprovedIntegrationEvent?> HandleAsync(string message)
    {
      PaymentInfoInputModel? paymentInfo;
      try
      {
        paymentInfo = JsonSerializer.Deserialize<PaymentInfoInputModel>(message);
      }
      catch (JsonException)
      {
        return null;
      }

      if (paymentInfo == null)
      {
        return null;
      }

      var approved = await _paymentService.Process(paymentInfo);
      return approved ? new PaymentApprovedIntegrationEvent(paymentInfo.IdProject) : null;
    }
  }
}
