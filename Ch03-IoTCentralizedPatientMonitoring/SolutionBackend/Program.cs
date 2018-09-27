using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceBus.Messaging;
using Microsoft.Azure.Devices;
using Microsoft.Azure.NotificationHubs;
using Newtonsoft.Json;
using System.Configuration;

namespace SolutionBackend
{
  class Program
  {
    static EventHubClient eventHubClient;
    static ServiceClient serviceClient;
    static NotificationHubClient notificationHubClient;
    static string connectionString = ConfigurationManager.AppSettings["connectionString"];
    static string deviceToCloudEndPoint = ConfigurationManager.AppSettings["deviceToCloudEndPoint"];
    static string receiverDeviceId = ConfigurationManager.AppSettings["receiverDeviceId"];
    static string notificationHubName = ConfigurationManager.AppSettings["notificationHubName"];
    static string notificationHubConnectionString = ConfigurationManager.AppSettings["notificationHubConnectionString"];

    static void Main(string[] args)
    {
      // Initialize event hub client to receive device to cloud messages
      eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, deviceToCloudEndPoint);
      // Initialize service client to send cloud to device messages
      serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
      // Initialize notification hub client to send cloud to mobile push notifications
      notificationHubClient = NotificationHubClient.CreateClientFromConnectionString(notificationHubConnectionString, notificationHubName);

      // Get the first partition
      var firstPartition = eventHubClient.GetRuntimeInformation().PartitionIds[0];
      // Start receiving messages from medical device
      ReceiveMessagesAsync(firstPartition).Wait();
    }

    private static async Task ReceiveMessagesAsync(string partition)
    {
      // Create a receiver to read messages written to the given partition starting at given date-time
      var eventHubReceiver = eventHubClient.GetDefaultConsumerGroup().CreateReceiver(partition, DateTime.UtcNow);

      // Start receiving messages
      while (true)
      {
        EventData eventData = await eventHubReceiver.ReceiveAsync();
        if (eventData == null) continue;
        // Extract the message
        string serializedMessage = Encoding.UTF8.GetString(eventData.GetBytes()); // serialized JSON string
        // Display the message on console
        ShowMessageOnConsole(serializedMessage);
        // Analyze the message
        AnalyzeMessage(serializedMessage);
      }
    }

    private static void AnalyzeMessage(string messageJSONString)
    {
      // Deserialize the message
      var messageType = new
      {
        messageId = "",
        deviceId = "",
        patientBodyTemperature = 0.0d,
        patientPulseRate = 0.0d,
        patientRespirationRate = 0.0d,
        rooomTemperature = 0.0d,
        roomHumidity = 0.0d
      };
      var message = JsonConvert.DeserializeAnonymousType(messageJSONString, messageType);

      //  If patient's body temperature is more than 102 deg Fahrenheit:
      //  1. send an alert to the alarm device to inform hospital staff
      //  2. send a push notification to registered phones to inform doctors
      if (message.patientBodyTemperature > 38.89)
      {
        Console.WriteLine("SENDING HIGH BODY TEMPERATURE ALERT TO PATIENT'S ALARM DEVICE.");
        SendMessageToDeviceAsync(receiverDeviceId, "high-body-temp-alert").Wait();
        Console.WriteLine("SENDING HIGH BODY TEMPERATURE ALERT TO NOTIFICATION HUB.");
        SendPushNotificationAsync("ALERT: Patient in Room 1 has high fever.").Wait();
      }
    }

    private static void ShowMessageOnConsole(string message)
    {
      Console.WriteLine();
      Console.WriteLine("Message received: " + message);
    }

    private async static Task SendMessageToDeviceAsync(string deviceId, string message)
    {
      var commandMessage = new Message(Encoding.ASCII.GetBytes(message));
      await serviceClient.SendAsync(deviceId, commandMessage);
    }

    private async static Task SendPushNotificationAsync(string message)
    {
      await notificationHubClient.SendGcmNativeNotificationAsync("{ \"data\" : {\"message\":\"" + message + "\"}}");
    }
  }
}
