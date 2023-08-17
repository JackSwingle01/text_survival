using Newtonsoft.Json;
using System.Text;

namespace text_survival_rpg_web
{
    public static class Web
    {

        public static async void SendToWebApi(string content)
        {
            string apiUrl = Config.WebApiUrl;

            using var httpClient = new HttpClient();
            var payload = new
            {
                text = content
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync(apiUrl, httpContent);

                // You might want to check the response, for instance:
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Failed to send data to the API. Status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while sending data to API: {ex.Message}");
            }
        }
    }
}
