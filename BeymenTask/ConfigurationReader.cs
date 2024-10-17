using BeymenTask.Documents;
using BeymenTask.Model;
using Confluent.Kafka;
using MongoDB.Driver;
using Polly;
using Polly.CircuitBreaker;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MongoDB.Driver.WriteConcern;

namespace BeymenTask
{
    public class ConfigurationReader
    {
        private readonly string _applicationName;
        private readonly string _connectionString;
        private readonly int _refreshTimerIntervalInMs;
        private readonly IMongoCollection<ConfigurationRecord> _configCollection;
        private readonly IMongoCollection<OutboxRecord> _outboxCollection;
        private readonly IMongoCollection<LogRecord> _logCollection;
        private Dictionary<string, object> _configCache;
        private IConnection _rabbitMqConnection;
        private IModel _rabbitMqChannel;
        private readonly AsyncCircuitBreakerPolicy _circuitBreaker;
        private readonly bool _isRabbitMqEnabled;
        private Timer _refreshTimer;

        public ConfigurationReader(string applicationName, string connectionString, int refreshTimerIntervalInMs, string rabbitMqConnectionString = null)
        {
            _applicationName = applicationName;
            _connectionString = connectionString;
            _refreshTimerIntervalInMs = refreshTimerIntervalInMs;
            _configCache = new Dictionary<string, object>();

            var client = new MongoClient(_connectionString);
            var database = client.GetDatabase("ConfigurationDB");
            _configCollection = database.GetCollection<ConfigurationRecord>("ConfigurationRecord");
            _outboxCollection = database.GetCollection<OutboxRecord>("Outbox");
            _logCollection = database.GetCollection<LogRecord>("SystemLog");


            if (!string.IsNullOrEmpty(rabbitMqConnectionString))
            {
                var factory = new ConnectionFactory() { Uri = new Uri(rabbitMqConnectionString) };
                _rabbitMqConnection = factory.CreateConnection();
                _rabbitMqChannel = _rabbitMqConnection.CreateModel();
                _isRabbitMqEnabled = true;
                _rabbitMqChannel.QueueDeclare(queue: "config-updates", durable: false, exclusive: false, autoDelete: false, arguments: null);
            }
            else
            {
                _isRabbitMqEnabled = false;
            }

            _circuitBreaker = Policy
                    .Handle<Exception>()
                    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

            Task.Run(() => this.LoadConfigurationAsync(_applicationName)).ConfigureAwait(false).GetAwaiter().GetResult();

            _refreshTimer = new Timer(async _ => await RefreshConfigurationAsync(_applicationName), null, refreshTimerIntervalInMs, refreshTimerIntervalInMs);
        }

        private async Task LoadConfigurationAsync(string applicationName)
        {
            try
            {
                await _circuitBreaker.ExecuteAsync(async () =>
                {
                    var activeRecords = (await _configCollection.FindAsync(record => record.IsActive == true && record.ApplicationName == applicationName)).ToList();

                    lock (_configCache)  // For concurrency problem
                    {
                        _configCache = activeRecords.ToDictionary(
                            record => record.Name,
                            record => ConvertValue(record.Value, record.Type)
                        );
                    }

                    if (_isRabbitMqEnabled)
                    {
                        await ProcessOutboxAsync();
                    }
                });
            }
            catch (BrokenCircuitException ex)
            {
                await LogError("Circuit breaker is activated, skipping configuration load.", ex.ToString());
            }
            catch (Exception ex)
            {
                await LogError("Failed to load configuration.", ex.ToString());
            }
        }

        private async Task RefreshConfigurationAsync(string applicationName)
        {
            try
            {
                await LoadConfigurationAsync(applicationName);
            }
            catch (Exception ex)
            {
                await LogError("Failed to load configuration.", ex.ToString());
            }
        }

        public T GetValue<T>(string key)
        {
            if (_configCache.TryGetValue(key, out var value))
            {
                return (T)value;
            }

            throw new KeyNotFoundException($"Key '{key}' not found.");
        }

        private object ConvertValue(string value, string type)
        {
            return type switch
            {
                "int" => int.Parse(value),
                "double" => double.Parse(value),
                "bool" => bool.Parse(value),
                _ => value
            };
        }

        public async Task PublishConfigChange(string key, string newValue)
        {
            var outboxRecord = new OutboxRecord
            {
                Key = key,
                Value = newValue,
                Published = false,
                CreatedAt = DateTime.UtcNow
            };

            await _outboxCollection.InsertOneAsync(outboxRecord);

            await ProcessOutboxAsync();
        }

        private async Task ProcessOutboxAsync()
        {
            var unprocessedRecords = await _outboxCollection.Find(r => !r.Published).ToListAsync();

            foreach (var record in unprocessedRecords)
            {
                try
                {
                    var messageBody = $"{record.Key}:{record.Value}";
                    var body = System.Text.Encoding.UTF8.GetBytes(messageBody);

                    _rabbitMqChannel.BasicPublish(exchange: "", routingKey: "config-updates", basicProperties: null, body: body);

                    var filter = Builders<OutboxRecord>.Filter.Eq(r => r.Id, record.Id);
                    var update = Builders<OutboxRecord>.Update.Set(r => r.Published, true);
                    await _outboxCollection.UpdateOneAsync(filter, update);
                }
                catch (Exception ex)
                {
                    await LogError("Failed to publish rabbitmq.", ex.ToString());
                }
            }
        }

        private async Task LogError(string message, string ex)
        {
            var logRecord = new LogRecord
            {
                Message = message,
                ExceptionDetails = ex.ToString(),
                CreatedAt = DateTime.UtcNow
            };

            await _logCollection.InsertOneAsync(logRecord);
        }

        public async Task AddConfigRecord(ConfigurationRecordDto configurationRecordDto)
        {
            var configRecord = new ConfigurationRecord
            {
                ApplicationName = configurationRecordDto.ApplicationName,
                IsActive = configurationRecordDto.IsActive,
                Type = configurationRecordDto.Type,
                Name = configurationRecordDto.Name,
                Value = configurationRecordDto.Value
            };

            await _configCollection.InsertOneAsync(configRecord);
        }

        public async Task UpdateConfigRecordAsync(ConfigurationRecordDto configurationRecordDto)
        {
            if (configurationRecordDto == null)
            {
                throw new ArgumentNullException(nameof(configurationRecordDto));
            }

            FilterDefinitionBuilder<ConfigurationRecord> filterBuilder = Builders<ConfigurationRecord>.Filter;

            FilterDefinition<ConfigurationRecord> filter = filterBuilder.Eq(existingEntity => existingEntity.Id, configurationRecordDto.Id);

            var configRecord = new ConfigurationRecord
            {
                Id = configurationRecordDto.Id,
                ApplicationName = configurationRecordDto.ApplicationName,
                IsActive = configurationRecordDto.IsActive,
                Type = configurationRecordDto.Type,
                Name = configurationRecordDto.Name,
                UpdatedAt = DateTime.UtcNow,
                Value = configurationRecordDto.Value
            };

            await _configCollection.FindOneAndReplaceAsync(filter, configRecord);
        }

        public async Task<IReadOnlyCollection<ConfigurationRecord>> GetAllAsync(Expression<Func<ConfigurationRecord, bool>> filter)
        {
            return await _configCollection.Find(filter).ToListAsync();
        }

        public void Dispose()
        {
            _rabbitMqChannel?.Close();
            _rabbitMqConnection?.Close();
        }
    }
}
