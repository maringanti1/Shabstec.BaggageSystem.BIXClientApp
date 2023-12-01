using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Microsoft.Azure.ServiceBus;
using RabbitMQ.Client.Events;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace BIXClientApp_RabbitMQ
{

    public class RabbitMQManager
    {
        private ConnectionFactory factory;
        private IConnection connection;
        private IModel channel;
        private static Publisher publisher;
        private static IQueueClient serviceBusQueueClient; 
        public RabbitMQManager()
        { 
        }

        public void InitializeRabbitMQ(Publisher publisher)
        {
            Console.WriteLine($"InitializeRabbitMQ initialization is started!");
            try
            {
                Console.WriteLine($"Wait for 1 min on initial setup - started!");
                // Sleep for 2 minutes (2 * 60 * 1000 milliseconds)
                Thread.Sleep(1 * 60 * 1000);
                Console.WriteLine($"Wait for 1 min on initial setup - completed!");
                string rabbitMQHost = publisher.ConfigData.RabbitMQHostSecretName;
                int rabbitMQPort = int.Parse(publisher.ConfigData.RabbitMQPortSecretName);
                string rabbitMQUsername = publisher.ConfigData.RabbitMQUsernameSecretName;
                string rabbitMQPassword = publisher.ConfigData.RabbitMQPasswordSecretName;

                Console.WriteLine(rabbitMQHost);
                Console.WriteLine(rabbitMQPort);
                Console.WriteLine(rabbitMQUsername);
                Console.WriteLine(rabbitMQPassword);

                factory = new ConnectionFactory
                {
                    HostName = rabbitMQHost,
                    Port = rabbitMQPort,
                    UserName = rabbitMQUsername,
                    Password = rabbitMQPassword,
                    VirtualHost= "test"
                };

                connection = factory.CreateConnection();
                Console.WriteLine($"connection - completed!");
                channel = connection.CreateModel();
                Console.WriteLine($"InitializeRabbitMQ channel established successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while initializing RabbitMQ: {ex.Message}");
            }
        }
        public async Task GetMessageFromRabbitMQ(Publisher publisher)
        {
            foreach (var entry in publisher.SubscriberTopics.SubTopics)
            {
                Console.WriteLine("Subscriber mapped :" + entry.TopicName);
                string topic = entry.TopicName;
                string brokerUrl = entry.TopicName; 
                string exchangeName = brokerUrl;
                string queueName = topic;
                string routingKey = topic; 

                channel.QueueBind(queueName, exchangeName, routingKey);

                var consumer = new EventingBasicConsumer(channel);
                Console.WriteLine("Comsumer setup: " + queueName);
                consumer.Received += async (sender, e) =>
                {
                    try
                    {
                        Console.WriteLine("Received message from RabbitMQ");
                        byte[] messageBytes = e.Body.ToArray();
                        string message = Encoding.UTF8.GetString(messageBytes);
                        Console.WriteLine("Received message from RabbitMQ: " + message);

                        // Send the message to the Service Bus queue
                        var serviceBusMessage = new Message(Encoding.UTF8.GetBytes(message));
                        await serviceBusQueueClient.SendAsync(serviceBusMessage);

                        Console.WriteLine("Received message from RabbitMQ and sent it to Service Bus.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                };

                channel.BasicConsume(queueName, true, consumer);
            }
            Console.WriteLine("Waiting for new messages in Rabbitmq - started");
            while (true)
            {
                // Your message processing logic here

                // Sleep for a while to avoid busy-waiting
                Thread.Sleep(millisecondsTimeout: 10000); // Sleep for 1 second (adjust as needed)
            }
            Console.WriteLine("Waiting for new messages in Rabbitmq - ended"); 

            
        }

        //public async void GetMessageFromRabbitMQ(Publisher publisher)
        //{
        //    foreach (var entry in publisher.SubscriberTopics.SubTopics)
        //    {
        //        string topic = entry.TopicName;
        //        string brokerUrl = entry.TopicName;
        //        // Configure RabbitMQ exchange and queue binding
        //        string exchangeName = brokerUrl; // Replace with your exchange name
        //        string queueName = topic; // Replace with your queue name
        //        string routingKey = topic; // Replace with your routing key 
        //                                   // Bind the queue to the exchange with the routing key
        //                                   // Declare the exchange
        //        channel.ExchangeDeclare(exchangeName, ExchangeType.Direct);

        //        // Declare the queue (it will be created if it doesn't exist)
        //        channel.QueueDeclare(queueName, durable: false, exclusive: false, 
        //            autoDelete: false);


        //        channel.QueueBind(queueName, exchangeName, routingKey); 
        //        // Set up a consumer to receive messages from RabbitMQ
        //        var consumer = new EventingBasicConsumer(channel);
        //        consumer.Received += HandleRabbitMQMessageReceived;
        //        channel.BasicConsume(queueName, true, consumer);
        //    }
        //    Console.WriteLine("Waiting for messages from RabbitMQ. Press any key to exit.");
        //    Console.WriteLine("While loop started...");            
        //    channel.Close();
        //    while (true)
        //    {
        //        // Your message processing logic here

        //        // Sleep for a while to avoid busy-waiting
        //        Thread.Sleep(millisecondsTimeout: 10000); // Sleep for 1 second (adjust as needed)
        //    }
        //    await serviceBusQueueClient.CloseAsync();
        //}

        private static async void HandleRabbitMQMessageReceived(object sender, BasicDeliverEventArgs e)
        {
            try
            {
                Console.WriteLine("Received message from RabbitMQ");
                // Process the received RabbitMQ message
                byte[] messageBytes = e.Body.ToArray();
                string message = Encoding.UTF8.GetString(messageBytes);
                Console.WriteLine("Received message from RabbitMQ " + message);
                // Send the message to the Service Bus queue
                var serviceBusMessage = new Message(Encoding.UTF8.GetBytes(message));

                await serviceBusQueueClient.SendAsync(serviceBusMessage);

                Console.WriteLine("Received message from RabbitMQ and sent it to Service Bus.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        public void Close()
        {
            // Close the channel and connection
            channel.Close();
            connection.Close();
        }

        public void ProcessMessage()
        {
            string xmlData = ""; 
            // Get the XML data from the environment variable
            string xmlDataFromEnvironment = Environment.GetEnvironmentVariable("XMLFILE");
            Console.WriteLine("XMLFILE environment variable is " + xmlDataFromEnvironment) ;
            if (string.IsNullOrEmpty(xmlDataFromEnvironment))
            {
                Console.WriteLine("XMLFILE environment variable not found");
                Console.WriteLine("ProcessMessage is started");
                Console.WriteLine("Checking for PublisherConfiguration xml file in local folder");
                // Check if the file exists in the directory of the executable
                string executableDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string filePathInExecutableDir = Path.Combine(executableDirectory, "PublisherConfiguration.xml");

                if (!File.Exists(filePathInExecutableDir))
                {
                    Console.WriteLine("PublisherConfiguration.xml file is not found in " + filePathInExecutableDir);
                    return;
                }
                else
                {
                    Console.WriteLine("PublisherConfiguration.xml file is found in " + filePathInExecutableDir);
                }
                // Load XML from a file or another source
                xmlData = File.ReadAllText("PublisherConfiguration.xml");
            }
            else
            {
                Console.WriteLine("Taking from environment variable"); 
                // Convert the Base64 string to bytes
               byte[] base64Bytes = Convert.FromBase64String(xmlDataFromEnvironment);

                // Convert the bytes to an XML string
                xmlData = Encoding.UTF8.GetString(base64Bytes);
            }
            try
            {
                Console.WriteLine(xmlData);
                // Create an XmlSerializer for ConfigData
                XmlSerializer serializer = new XmlSerializer(typeof(Publisher));

                using (StringReader reader = new StringReader(xmlData))
                {
                    publisher = (Publisher)serializer.Deserialize(reader);
                    // Now, you can access the configuration data using publisher object.
                }
            } catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                return;
            }
            
            InitializeRabbitMQ(publisher);
            InitializeServiceBus(publisher);
            GetMessageFromRabbitMQ(publisher);

        }
        public void InitializeServiceBus(Publisher publisher)
        { 
            string servicebus = publisher.ConfigData.SubscribedQueueEndPoint;
            string queuename = publisher.ConfigData.SubscribedQueueName;  
            // Replace "ConnectionString" with your actual Service Bus connection string
            serviceBusQueueClient = new QueueClient(servicebus, queuename); 
        } 
    }
}