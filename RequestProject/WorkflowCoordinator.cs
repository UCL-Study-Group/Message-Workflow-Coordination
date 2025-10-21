using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace RequestProject
{
    public class WorkflowCoordinator
    {
        private readonly string _connectionString;
        private readonly ConnectionFactory _connectionFactory;
        private IConnection _rabbitConnection;
        private readonly string _requestQueue = "work-requests";
        private readonly string _replyQueue = "work-replies";

        public WorkflowCoordinator(string dbConnectionString, string rabbitMqHost)
        {
            _connectionString = dbConnectionString;
            _connectionFactory = new ConnectionFactory
            {
                HostName = rabbitMqHost
            };
        }

        private async Task InitializeRabbitAsync()
        {
            if (_rabbitConnection == null || !_rabbitConnection.IsOpen)
            {
                _rabbitConnection = await _connectionFactory.CreateConnectionAsync();
            }
        }

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

        public async Task StartWorkAsync(int workItemID)
        {
            await InitializeRabbitAsync();

            // Has to have TransactionScopeAsyncFlowOption enabled to run async/on different threats
            using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                var channel = await _rabbitConnection.CreateChannelAsync();

                await EnsureQueuesExistAsync(channel);
                await channel.TxSelectAsync();

                try
                {
                    // Gets item from db
                    await using (var connection = new NpgsqlConnection(_connectionString))
                    {
                        await connection.OpenAsync();
                        await using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = @"
                                    UPDATE work_items
                                    SET status = 'InProgress',
                                        started_at = NOW()
                                    WHERE id = @id AND status = 'Pending'";
                            cmd.Parameters.AddWithValue("id", workItemID);

                            int rowsAffected = await cmd.ExecuteNonQueryAsync();
                            if (rowsAffected == 0)
                            {
                                throw new InvalidOperationException("Work item not available");
                            }
                        }
                    }

                    // Sends request to rabbitMQ
                    var message = Encoding.UTF8.GetBytes($"Process work item {workItemID}");
                    var properties = new BasicProperties
                    {
                        Persistent = true,
                        CorrelationId = workItemID.ToString(),
                        ReplyTo = _replyQueue
                    };

                    await channel.BasicPublishAsync(
                        exchange: "",
                        routingKey: _requestQueue,
                        mandatory: false,
                        basicProperties: properties,
                        body: message);

                    await channel.TxCommitAsync();
                    scope.Complete();

                    Console.WriteLine($"Work item {workItemID} started and request sent");
                }
                catch (Exception ex)
                {
                    await channel.TxRollbackAsync();

                    Console.WriteLine($"Failed to start work: {ex.Message}");
                }
            }
        }

        public async Task CompleteWorkAsync()
        {
            await InitializeRabbitAsync();

            var channel = await _rabbitConnection.CreateChannelAsync();
            await EnsureQueuesExistAsync(channel);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                // Has to have TransactionScopeAsyncFlowOption enabled to run async/on different threats
                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    await channel.TxSelectAsync();

                    try
                    {
                        var replyBody = Encoding.UTF8.GetString(ea.Body.ToArray());
                        var workItemId = int.Parse(ea.BasicProperties.CorrelationId);
                        var deliveryTag = ea.DeliveryTag;

                        Console.WriteLine($"Received reply for work item {workItemId}: {replyBody}");

                        // Update item in database
                        await using (var connection = new NpgsqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();
                            await using (var cmd = connection.CreateCommand())
                            {
                                // Determine if work succeeded or failed based on reply
                                bool success = replyBody.Contains("SUCCESS");

                                cmd.CommandText = @"
                                    UPDATE work_items
                                    SET status = @Status,
                                        completed_at = NOW(),
                                        result = @Result
                                    WHERE id = @Id";
                                cmd.Parameters.AddWithValue("Id", workItemId);
                                cmd.Parameters.AddWithValue("Status", success ? "Completed" : "Failed");
                                cmd.Parameters.AddWithValue("Result", replyBody);

                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        await channel.BasicAckAsync(deliveryTag, multiple: false);
                        await channel.TxCommitAsync();

                        scope.Complete();

                        Console.WriteLine($"Work item {workItemId} completed and reply acknowledged");
                    }
                    catch (Exception ex)
                    {
                        await channel.TxRollbackAsync();
                        Console.WriteLine($"Failed to complete work: {ex.Message}");
                    }
                }
            };

            await channel.BasicConsumeAsync(
                queue: _replyQueue,
                autoAck: false,
                consumer: consumer);

            Console.WriteLine("Completer listening for requests...");
        }
    }
}