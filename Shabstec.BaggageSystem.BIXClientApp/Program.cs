using System;
using System.Text;
using System.Threading;
using BIXClientApp_RabbitMQ;
using Microsoft.Azure.ServiceBus.Core;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BIXBrokerApp_RabbitMQ
{
    class Program
    {

        static void Main(string[] args)
        {
            RabbitMQManager rabbitMQManager = new RabbitMQManager();
            // Initialize RabbitMQ and Service Bus connections
            rabbitMQManager.ProcessMessage();
      
        }



    }
}
