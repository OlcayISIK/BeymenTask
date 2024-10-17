using BeymenTask;
using BeymenTask.Documents;
using BeymenTask.Model;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ConfigurationReaderTest
{
    public class ConfigurationReaderTests : IDisposable
    {
        private readonly Mock<IMongoCollection<ConfigurationRecord>> _mockConfigCollection;
        private readonly Mock<IMongoCollection<OutboxRecord>> _mockOutboxCollection;
        private readonly Mock<IMongoCollection<LogRecord>> _mockLogCollection;
        private readonly Mock<IConnection> _mockRabbitMqConnection;
        private readonly Mock<IModel> _mockRabbitMqChannel;
        private readonly ConfigurationReader _configurationReader;

        public ConfigurationReaderTests()
        {
            _mockConfigCollection = new Mock<IMongoCollection<ConfigurationRecord>>();
            _mockOutboxCollection = new Mock<IMongoCollection<OutboxRecord>>();
            _mockLogCollection = new Mock<IMongoCollection<LogRecord>>();
            _mockRabbitMqConnection = new Mock<IConnection>();
            _mockRabbitMqChannel = new Mock<IModel>();

            _mockRabbitMqConnection.Setup(c => c.CreateModel()).Returns(_mockRabbitMqChannel.Object);

            _configurationReader = new ConfigurationReader(
                "SERVICE-A",
                "mongodb://localhost:27017",
                60000
            );
        }

        public void Dispose()
        {
            _configurationReader.Dispose();
        }

        [Fact]
        public void Constructor_ShouldInitialize_WhenValidParametersArePassed()
        {
            var applicationName = "SERVICE-A";
            var connectionString = "mongodb://localhost:27017";
            var refreshInterval = 60000;
            var rabbitMqConnectionString = "amqp://localhost";

            // Act
            var configReader = new ConfigurationReader(applicationName, connectionString, refreshInterval, rabbitMqConnectionString);

            // Assert
            Assert.NotNull(configReader);
        }

        [Fact]
        public void GetValue_ShouldThrowKeyNotFoundException_WhenKeyDoesNotExist()
        {
            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => _configurationReader.GetValue<int>("NonExistentKey"));
        }

        [Fact]
        public async Task AddConfigRecord_ShouldInsertRecord()
        {
            // Arrange
            var newConfig = new ConfigurationRecordDto
            {
                ApplicationName = "TestApp",
                IsActive = true,
                Type = "string",
                Name = "TestKey",
                Value = "TestValue"
            };

            _mockConfigCollection.Setup(x => x.InsertOneAsync(It.IsAny<ConfigurationRecord>(), null, default))
                                 .Returns(Task.CompletedTask)
                                 .Verifiable();

            // Act
            await _configurationReader.AddConfigRecord(newConfig); 

            // Assert
            _mockConfigCollection.Verify(x => x.InsertOneAsync(It.IsAny<ConfigurationRecord>(), null, default), Times.Once);
        }


        [Fact]
        public async Task UpdateConfigRecordAsync_ShouldUpdateRecord()
        {
            var configId = ObjectId.GenerateNewId();

            // Arrange
            var updatedConfig = new ConfigurationRecordDto
            {
                Id = configId,
                ApplicationName = "TestApp",
                IsActive = true,
                Type = "string",
                Name = "TestKey",
                Value = "UpdatedValue"
            };

            _mockConfigCollection.Setup(x => x.FindOneAndReplaceAsync(
                    It.IsAny<FilterDefinition<ConfigurationRecord>>(),
                    It.IsAny<ConfigurationRecord>(),
                    null,
                    default))
                .ReturnsAsync((ConfigurationRecord)null)
                .Verifiable();

            // Act
            await _configurationReader.UpdateConfigRecordAsync(updatedConfig);

            // Assert
            _mockConfigCollection.Verify(x => x.FindOneAndReplaceAsync(
                It.IsAny<FilterDefinition<ConfigurationRecord>>(),
                It.IsAny<ConfigurationRecord>(),
                null,
                default), Times.Once);
        }

    }
}
