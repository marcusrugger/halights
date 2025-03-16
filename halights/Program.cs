﻿using HADotNet.Core;
using HADotNet.Core.Clients;

namespace HALights;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Parse command line argument for state filter
            string? stateFilter = args.Length > 0 ? args[0].ToLower() : null;
            if (stateFilter != null && stateFilter != "on" && stateFilter != "off")
            {
                Console.WriteLine("Error: State filter must be either 'on' or 'off'");
                return;
            }

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
            
            bool continueRunning = true;
            while (continueRunning)
            {
                // Get all entities
                var entityClient = ClientFactory.GetClient<EntityClient>();
                var statesClient = ClientFactory.GetClient<StatesClient>();
                
                // Filter for light entities
                var allEntities = await entityClient.GetEntities();
                var lightEntities = allEntities
                    .Where(e => e.StartsWith("light."))
                    .ToList();
                
                if (!lightEntities.Any())
                {
                    Console.WriteLine("No light entities found in Home Assistant");
                    return;
                }
                
                // Get states for all lights
                var lightStates = new List<(string EntityId, dynamic State, string? FriendlyName)>();
                foreach (var entityId in lightEntities)
                {
                    var state = await statesClient.GetState(entityId);
                    
                    // Skip if state doesn't match filter
                    string currentState = state.State.ToString().ToLower();
                    if (stateFilter != null && currentState != stateFilter)
                    {
                        continue;
                    }

                    // Try to extract friendly name from attributes
                    string? friendlyName = null;
                    try
                    {
                        // Now we know attributes is a Dictionary<string, object>
                        if (state.Attributes is Dictionary<string, object> attributesDict)
                        {
                            if (attributesDict.TryGetValue("friendly_name", out var friendlyNameObj))
                            {
                                friendlyName = friendlyNameObj?.ToString();
                            }
                        }
                    }
                    catch
                    {
                        // If we can't extract the friendly name, just use the entity ID
                    }
                    
                    // If no friendly name found, use a cleaned up version of the entity ID
                    if (string.IsNullOrEmpty(friendlyName))
                    {
                        // Remove the "light." prefix and replace underscores with spaces
                        friendlyName = entityId.Replace("light.", "").Replace("_", " ");
                        // Capitalize first letter of each word
                        friendlyName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(friendlyName);
                    }
                    
                    lightStates.Add((entityId, state, friendlyName));
                }

                if (!lightStates.Any())
                {
                    Console.WriteLine($"No lights found in '{stateFilter}' state");
                    return;
                }
                
                // Display all lights with current states
                Console.Clear();
                Console.WriteLine("Home Assistant Lights Control");
                Console.WriteLine("============================");
                if (stateFilter != null)
                {
                    Console.WriteLine($"Showing lights that are {stateFilter.ToUpper()}");
                }
                Console.WriteLine();
                
                // Find the maximum length of the friendly names for proper alignment
                int maxNameLength = lightStates.Max(x => x.FriendlyName?.Length ?? 0);
                // Calculate the width needed for the number column based on the number of lights
                int numberWidth = lightStates.Count.ToString().Length;
                
                // Create format string parts for consistent display
                string numberFormat = $"{{0,{numberWidth}}}.";
                string nameFormat = $"{{1,-{maxNameLength}}}";
                string statusFormat = "{2}";
                
                // Combine the parts to create the complete format string
                string listFormat = $"{numberFormat} {nameFormat}  {statusFormat}";
                
                for (int i = 0; i < lightStates.Count; i++)
                {
                    var (entityId, state, friendlyName) = lightStates[i];
                    
                    string status = state.State.ToString().Equals("on", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF";
                    
                    // If the light is ON, print with colors
                    if (status == "ON")
                    {
                        // Number in red
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write(string.Format(numberFormat, (i + 1)));
                        
                        // Name in blue
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write(string.Format($" {{0,-{maxNameLength}}}  ", friendlyName));
                        
                        // Status in green
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(status);
                        
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine(string.Format(listFormat, (i + 1), friendlyName, status));
                    }
                }
                
                Console.WriteLine();
                Console.WriteLine("Enter the number of the light to toggle or press Enter to exit:");
                string? input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                {
                    continueRunning = false;
                    continue;
                }
                
                if (int.TryParse(input, out int selection) && selection > 0 && selection <= lightStates.Count)
                {
                    var (entityId, _, friendlyName) = lightStates[selection - 1];
                    
                    // Toggle the selected light
                    var service = ClientFactory.GetClient<ServiceClient>();
                    var data = new Dictionary<string, object> { { "entity_id", entityId } };
                    await service.CallService("light", "toggle", data);
                    
                    Console.WriteLine($"Toggling {friendlyName}...");
                    // Short delay to allow the state change to propagate
                    await Task.Delay(1000);
                }
                else
                {
                    Console.WriteLine("Invalid selection. Press any key to continue...");
                    Console.ReadKey();
                }
            }
            
            Console.WriteLine("Goodbye!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
