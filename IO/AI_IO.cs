using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace text_survival.IO
{
    public class AI_IO
    {
        private OpenAI_API.Chat.Conversation chat;
        public AI_IO()
        {
            OpenAIAPI api = new OpenAIAPI(Config.APIKey);
            chat = api.Chat.CreateConversation();
            chat.Model = Model.GPT4_Turbo;
            chat.RequestParameters.Temperature = .1;

            chat.AppendSystemMessage("You are the dungeon master for a DnD video game. Emphasise and expand on the story, only relay stats or options if the player would notice (for exampmle if they aren't hungry, don't mention it). " +
                "STEP 1: Read the program's output. " +
                " STEP 2: Convey the important info to the user in your own words and ask the user what they want to do. " +
                " STEP 3: If what they say is close enough to one of the numbered actions listed by the program " +
                "then select it using the EXACT format: \"SELECT 1\" (Without quotes and replacing the 1 with their selection) for the program to parse. " +
                " If your output is anything other than that format, then then the program will assume you are talking to the player. " + 
                " If the player's choice is not close enough to one of the options, then give them a hint of what actions they could do and they can try again. " +
                " STEP 4: The program will provide a new description of the game state and options and the process will repeat.");

        }
        public async Task<string> ExecuteAsync()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var message in Output.OutputQueue)
            {
                sb.Append(message);
            }
            Output.OutputQueue.Clear();
            chat.AppendUserInput($"[START PROGRAM OUTPUT] {sb} [END PROGRAM OUTPUT. Emphasise and expand on the story, only relay stats or options if the player would notice. However don't be too verbose. ]");


            await foreach (var res in chat.StreamResponseEnumerableFromChatbotAsync())
            {
                Console.Write(res);
            }

            Console.WriteLine("");
            string input = Console.ReadLine();

            chat.AppendUserInput($"[PLAYER INPUT] {input} [END PLAYER INPUT. RESPOND WITH ONLY \"SELECT <number>\"]");

            string response = await chat.GetResponseFromChatbotAsync();

            while (true)
            {
                //Console.WriteLine($"AI output: {response}");
               
                string pattern = @"\bSELECT\s+(\d+)\b";

                // Match the pattern in the response string
                Match match = Regex.Match(response.Trim(), pattern);

                // Extract the selection string if there's a match
                if (match.Success)
                {
                    string selection = match.Groups[1].Value.Trim();
                    // Use the extracted selection
                    //Console.WriteLine($"selection: {selection}");
                    if (int.TryParse(selection, out int result))
                    {
                        return result.ToString();
                    }
                    else
                    {
                        throw new Exception($"Invalid selection from AI: {selection}, {result}");
                    }
                }
                else
                {
                    foreach (char c in response.ToCharArray())
                    {
                        Console.Write(c);
                        Thread.Sleep(1);
                    }
                    chat.AppendUserInput(Console.ReadLine());
                    response = await chat.GetResponseFromChatbotAsync();
                }

            }


        }
    }
}
