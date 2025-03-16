using HADotNet.Core;
using HADotNet.Core.Clients;
using System;
using System.IO;
using System.Threading.Tasks;

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
            Console.WriteLine("Connecting to Home Assistant...");
            ClientFactory.Initialize("https://homeassistant.iot:8123", apiKey);
            
            // Get entity client and states client
            var entityClient = ClientFactory.GetClient<EntityClient>();
            var statesClient = ClientFactory.GetClient<StatesClient>();
            
            // Find climate/thermostat entities
            Console.WriteLine("Searching for thermostat entities...");
            var allEntities = await entityClient.GetEntities();
            var thermostatEntities = allEntities
                .Where(e => e.StartsWith("climate."))
                .ToList();
            
            if (!thermostatEntities.Any())
            {
                Console.WriteLine("No thermostat entities found in Home Assistant");
                return;
            }
            
            Console.WriteLine($"Found {thermostatEntities.Count} thermostat(s)");
            Console.WriteLine();
            
            // Get and display the state of each thermostat
            foreach (var entityId in thermostatEntities)
            {
                var state = await statesClient.GetState(entityId);
                
                // Try to extract friendly name from attributes
                string friendlyName = entityId;
                try
                {
                    if (state.Attributes is Dictionary<string, object> attributesDict)
                    {
                        if (attributesDict.TryGetValue("friendly_name", out var friendlyNameObj))
                        {
                            friendlyName = friendlyNameObj?.ToString() ?? entityId;
                        }
                    }
                }
                catch
                {
                    // If we can't extract the friendly name, just use the entity ID
                    friendlyName = entityId.Replace("climate.", "").Replace("_", " ");
                    friendlyName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(friendlyName);
                }
                
                Console.WriteLine($"Thermostat: {friendlyName}");
                Console.WriteLine("==========================");
                
                // Extract thermostat data
                string currentMode = "Unknown";
                double? currentTemp = null;
                double? targetTemp = null;
                double? humidity = null;
                
                try
                {
                    // Get current mode (heat, cool, etc.)
                    currentMode = state.State?.ToString() ?? "Unknown";
                    
                    if (state.Attributes is Dictionary<string, object> attrs)
                    {
                        // Get current temperature
                        if (attrs.TryGetValue("current_temperature", out var currentTempObj) && currentTempObj != null)
                        {
                            if (double.TryParse(currentTempObj.ToString(), out double temp))
                            {
                                currentTemp = temp;
                            }
                        }
                        
                        // Get target temperature
                        if (attrs.TryGetValue("temperature", out var targetTempObj) && targetTempObj != null)
                        {
                            if (double.TryParse(targetTempObj.ToString(), out double temp))
                            {
                                targetTemp = temp;
                            }
                        }
                        
                        // Get current humidity if available
                        if (attrs.TryGetValue("current_humidity", out var humidityObj) && humidityObj != null)
                        {
                            if (double.TryParse(humidityObj.ToString(), out double hum))
                            {
                                humidity = hum;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing thermostat data: {ex.Message}");
                }
                
                // Display the information in a human-friendly format
                Console.WriteLine($"Status: {FormatMode(currentMode)}");
                
                if (currentTemp.HasValue)
                {
                    Console.WriteLine($"Current Temperature: {currentTemp:F1}°F / {FahrenheitToCelsius(currentTemp.Value):F1}°C");
                }
                else
                {
                    Console.WriteLine("Current Temperature: Not available");
                }
                
                if (targetTemp.HasValue)
                {
                    Console.WriteLine($"Target Temperature: {targetTemp:F1}°F / {FahrenheitToCelsius(targetTemp.Value):F1}°C");
                }
                else
                {
                    Console.WriteLine("Target Temperature: Not available");
                }
                
                if (humidity.HasValue)
                {
                    Console.WriteLine($"Current Humidity: {humidity:F1}%");
                }
                else
                {
                    Console.WriteLine("Current Humidity: Not available");
                }
                
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    
    // Helper method to convert Celsius to Fahrenheit
    private static double CelsiusToFahrenheit(double celsius)
    {
        return (celsius * 9/5) + 32;
    }
    
    // Helper method to convert Fahrenheit to Celsius
    private static double FahrenheitToCelsius(double fahrenheit)
    {
        return (fahrenheit - 32) * 5/9;
    }
    
    // Helper method to format the mode in a more human-readable way
    private static string FormatMode(string mode)
    {
        if (string.IsNullOrEmpty(mode) || mode.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return "Unknown";
            
        return mode.ToLower() switch
        {
            "heat" => "Heating",
            "cool" => "Cooling (AC)",
            "heat_cool" => "Auto (Heat/Cool)",
            "auto" => "Auto",
            "dry" => "Dry",
            "fan_only" => "Fan Only",
            "off" => "Off",
            _ => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(mode)
        };
    }
}
