using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System.Configuration;

namespace SimulatedDevice
{
  class Program
  {
    static DeviceClient deviceClient;
    static string iotHubHostname = ConfigurationManager.AppSettings["iotHubHostname"];
    static string deviceId = ConfigurationManager.AppSettings["deviceId"];
    static string deviceSharedAccessKey = ConfigurationManager.AppSettings["deviceSharedAccessKey"];

    static void Main(string[] args)
    {
      deviceClient = DeviceClient.Create(iotHubHostname, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, deviceSharedAccessKey), TransportType.Mqtt);
      deviceClient.ProductInfo = "Asclepius Consortium Vitals Recorder";
      SendPatientTelemetryToHubAsync();
      Console.ReadLine();
    }

    private static async void SendPatientTelemetryToHubAsync()
    {
      // Minimum values for telemetry parameters
      double minBodyTemperature = 36.5; // degrees Celsius
      double minPulseRate = 60; // beats per minute
      double minRespirationRate = 12; // breaths per minute
      double minRoomTemperature = 18; // degrees Celsius
      double minRoomHumidity = 30; // percentage

      Random random = new Random();

      while (true)
      {
        // Pretentiously received from the medical device
        double currentBodyTemperature = minBodyTemperature + random.NextDouble() * 4; // 36.5-40.5 deg
        double currentPulseRate = minPulseRate + random.NextDouble() * 40; // 60-100 per min
        double currentRespirationRate = minRespirationRate + random.NextDouble() * 4; // 12-16 per min
        // Pretentiously received from on-board sensors
        double currentTemperature = minRoomTemperature + random.NextDouble() * 12; // 18-30 deg
        double currentHumidity = minRoomHumidity + random.NextDouble() * 30; // 30-60%

        // Combined telemetry data
        var deviceTelemetryData = new
        {
          messageId = Guid.NewGuid().ToString(),
          deviceId = deviceId,
          patientBodyTemperature = currentBodyTemperature,
          patientPulseRate = currentPulseRate,
          patientRespirationRate = currentRespirationRate,
          rooomTemperature = currentTemperature,
          roomHumidity = currentHumidity
        };

        // Serialize and send data to hub
        var messageString = JsonConvert.SerializeObject(deviceTelemetryData);
        var message = new Message(Encoding.ASCII.GetBytes(messageString));
        await deviceClient.SendEventAsync(message);

        // Output the sent message to console
        Console.WriteLine("Message at {0}: {1}", DateTime.Now, messageString);

        // Wait for 10 seconds before repeating
        await Task.Delay(10000);
      }
    }
  }
}
