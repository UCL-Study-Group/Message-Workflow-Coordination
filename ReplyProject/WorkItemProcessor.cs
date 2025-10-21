using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace ReplyProject
{
    public class WorkItemProcessor
    {
        private readonly string _requestQueue = "work-requests";
        private readonly string _replyQueue = "work-replies";

        private async Task EnsureQueuesExistAsync(IChannel channel)
        {
            await channel.QueueDeclareAsync(
                queue: _requestQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            await channel.QueueDeclareAsync(
                queue: _replyQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        public async Task StartProcessingAsync(string rabbitMqHost)
        {
            var factory = new ConnectionFactory() { HostName = rabbitMqHost };
            var connection = await factory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            await EnsureQueuesExistAsync(channel);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                await channel.TxSelectAsync();

                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var correlationId = ea.BasicProperties.CorrelationId;
                    var replyTo = ea.BasicProperties.ReplyTo;

                    Console.WriteLine($"Received request: {message}");

                    // Actual work would be done here, but it's not needed for the task, so here is a Task.Delay instead
                    await Task.Delay(10000); // Simulate work

                    var result = "SUCCESS: Work completed!";

                    var replyProps = new BasicProperties
                    {
                        CorrelationId = correlationId
                    };

                    var replyBody = Encoding.UTF8.GetBytes(result);
                    await channel.BasicPublishAsync(
                        exchange: "",
                        routingKey: replyTo,
                        mandatory: false,
                        basicProperties: replyProps,
                        body: replyBody);

                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    await channel.TxCommitAsync();
                }
                catch (Exception ex)
                {
                    await channel.TxRollbackAsync();

                    Console.WriteLine($"Failed to process work: {ex.Message}");
                }
            };

            await channel.BasicConsumeAsync(
                queue: _requestQueue,
                autoAck: false,
                consumer: consumer);

            Console.WriteLine("Worker listening for requests...");
        }
    }
}
