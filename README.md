Dynamic Configuration Library
This project aims to provide a dynamic configuration structure for applications. It allows access to configuration keys stored in files like web.config and app.config through a centralized and dynamic system, enabling updates without requiring deployment, restart, or recycle.

Overview
The configuration records are stored in a MongoDB database. The library (DLL) is designed to be accessible for various project types including Web, WCF, and Web API. When integrated into a project, such as “SERVICE-A”, the library retrieves all relevant records associated with that application.

Key Features
Dynamic Configuration: Access configuration records dynamically from a MongoDB storage.
Typed Configuration Values: Supports multiple data types including integer, string, double, and boolean.
Active Records Filtering: Only retrieves records with IsActive=1.
Polling Mechanism: Regularly checks for new records and updates existing values.
Application Isolation: Each service can only access its own configuration records using a Message Broker.
Robustness: Continues functioning with the last successful configuration in case of storage access issues.
Concurrency Handling: Implements TPL and async/await to manage concurrency issues effectively.
Design Patterns: Follows established design and architectural patterns.
Test-Driven Development (TDD): The library is developed with a focus on TDD, including unit tests.
Docker Compose Support: The entire ecosystem can be run using Docker Compose.

Getting Started
Installation
To install the library, clone the repository and add the DLL to your project:

git clone https://github.com/OlcayISIK/BeymenTask.git
Initialization
The library should be initialized with three parameters:

var configurationReader = new ConfigurationReader(applicationName, connectionString, refreshTimerIntervalInMs);
applicationName: The name of the application the library will operate within.
connectionString: Connection details for the MongoDB storage.
refreshTimerIntervalInMs: The interval for checking the storage for updates (in milliseconds).
Usage
To retrieve configuration values, use the GetValue<T> method:
string siteName = configurationReader.GetValue<string>("SiteName");
This example would return the value associated with the key "SiteName", such as "boyner.com.tr".

Web Interface
A web interface allows for listing, updating, and adding new configuration records. Users can filter records by name on the client side.

Contributing
Contributions are welcome! Please fork the repository and submit a pull request.

Testing
Unit tests are included to ensure the reliability of the library. You can run the tests using the following command:
dotnet test

Running with Docker Compose
The entire ecosystem can be run using Docker Compose. Ensure you have Docker and Docker Compose installed, then execute:

docker-compose up
This will start all necessary services defined in the docker-compose.yml file.
