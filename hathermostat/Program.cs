using HADotNet.Core;
using HADotNet.Core.Clients;

namespace HAThermostat;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Read API key from ~/.env file
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string envFilePath = Path.Combine(homeDir, ".env");
            
            if (!File.Exists(envFilePath))
            {
                Console.WriteLine("Error: ~/.env file not found. Please create it with your Home Assistant API key as API_KEY_HA=your_token");
                return;
            }
            
            string apiKey = "";
            foreach (string line in File.ReadAllLines(envFilePath))
            {
                if (line.StartsWith("API_KEY_HA="))
                {
                    apiKey = line.Substring("API_KEY_HA=".Length).Trim();
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Error: API_KEY_HA not found in ~/.env file");
                return;
            }

            // Initialize the Home Assistant client
            ClientFactory.Initialize("https://homeassistant.iot:8123", apiKey);
            
            // Get entity client
            var entityClient = ClientFactory.GetClient<EntityClient>();
            var statesClient = ClientFactory.GetClient<StatesClient>();
            
            // Get all climate entities
            var allEntities = await entityClient.GetEntities();
            var climateEntities = allEntities
                .Where(e => e.StartsWith("climate."))
                .ToList();
            
            if (!climateEntities.Any())
            {
                Console.WriteLine("No climate entities found in Home Assistant");
                return;
            }

            Console.WriteLine("Home Assistant Thermostat Status");
            Console.WriteLine("==============================");
            Console.WriteLine();

            foreach (var entityId in climateEntities)
            {
                var state = await statesClient.GetState(entityId);
                var attributes = state.Attributes as Dictionary<string, object>;
                
                if (attributes == null) continue;

                // Extract friendly name
                string friendlyName = attributes.TryGetValue("friendly_name", out var nameObj) 
                    ? nameObj?.ToString() ?? entityId
                    : entityId;

                // Extract key information
                attributes.TryGetValue("current_temperature", out var currentTemp);
                attributes.TryGetValue("temperature", out var targetTemp);
                attributes.TryGetValue("current_humidity", out var humidity);
                string mode = state.State;

                // Print information
                Console.WriteLine($"Thermostat: {friendlyName}");
                Console.WriteLine($"Current Temperature: {currentTemp}°F");
                Console.WriteLine($"Target Temperature: {targetTemp}°F");
                Console.WriteLine($"Current Humidity: {humidity}%");
                Console.WriteLine($"Mode: {char.ToUpper(mode[0]) + mode[1..]}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}