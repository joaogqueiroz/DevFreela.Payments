# DevFreela.Payments

The payment microservice for [DevFreela](https://github.com/joaogqueiroz/DevFreela). It was split out of the DevFreela monolith to explore service decomposition and asynchronous communication with RabbitMQ.

## How it works

1. When a client finishes a project, the DevFreela API publishes the payment details (project id, card data, amount) to the `Payments` queue.
2. `ProcessPaymentConsumer`, a hosted background service here, consumes the message and runs it through `PaymentService`. The payment gateway is simulated and always approves.
3. The service publishes a payment-approved integration event with the project id, which DevFreela consumes to mark the project as finished.

```
DevFreela.Api ──(Payments)──▶ DevFreela.Payments ──(payment approved)──▶ DevFreela.Api
```

It also exposes `POST /api/payments` to process a payment synchronously over HTTP.

## Tech stack

C# · .NET 8 · ASP.NET Core · RabbitMQ.Client · BackgroundService · Swagger

## Running locally

Requirements: .NET 8 SDK and RabbitMQ on `localhost:5672` (guest/guest). The `docker-compose.yml` in the DevFreela repo starts one.

```sh
dotnet run --project DevFreela.Payments.Api
```

It listens on `https://localhost:6001`, the address DevFreela expects in `Services:Payments`.
