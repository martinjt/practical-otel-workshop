using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

const string QUEUE_NAME = "traced_queue";
const int MESSAGES_TO_SEND = 5;
var source = new ActivitySource("traced_queue");
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resource => resource.AddService("dotnet-queue"))
    .AddSource(source.Name)
    .AddOtlpExporter()
    .Build();

Console.WriteLine("Press [enter] to start sending messages.");
Console.ReadLine();

var factory = new ConnectionFactory { HostName = "localhost" };
using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(queue: QUEUE_NAME,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

var sendActivity = source.StartActivity("Send Messages");
for (int i = 1; i < MESSAGES_TO_SEND + 1; i++)
{
    var properties = channel.CreateBasicProperties();
    properties.Headers = new Dictionary<string, object>();
    Propagators.DefaultTextMapPropagator.Inject(
        new PropagationContext(Activity.Current!.Context, Baggage.Current), 
        properties.Headers, 
        (headers, key, value) => headers.Add(new(key, value)));

    var message = $"Message {i}";
    var body = Encoding.UTF8.GetBytes(message);
    channel.BasicPublish(exchange: string.Empty,
                         routingKey: QUEUE_NAME,
                         basicProperties: properties,
                         body: body);
}
sendActivity?.Stop();


var consumer = new EventingBasicConsumer(channel);
var received = 0;
consumer.Received += (model, ea) =>
{
    var propagationContext = Propagators.DefaultTextMapPropagator.Extract(
        new PropagationContext(new ActivityContext(), Baggage.Current),
        ea.BasicProperties.Headers,
        (headers, keyToFind) =>
        {
            if (headers.TryGetValue(keyToFind, out var value) && value is byte[] str)
                return [Encoding.UTF8.GetString(str)];

            return [];
        });

    using var receiveActivity = source.StartActivity("Receive Messages", ActivityKind.Consumer, propagationContext.ActivityContext);
    // using var receiveLinkedActivity = source.StartActivity("Receive Message", ActivityKind.Consumer, null, links:
    //     [new ActivityLink(propagationContext.ActivityContext)]);
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine($"Received {message}");
        received++;
    }
};
channel.BasicConsume(queue: QUEUE_NAME,
                     autoAck: true,
                     consumer: consumer);

var iterations = 0;
while (received < MESSAGES_TO_SEND || iterations < 10)
{ await Task.Delay(10); iterations++; }
Console.WriteLine("Press [enter] to exit.");
Console.ReadLine();
tracerProvider.ForceFlush();

