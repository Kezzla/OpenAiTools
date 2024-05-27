using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace OpenAiTools
{
    public interface IOpenAiTools
    {
        // Settings
        void SetChatEndpoint(string endpoint);
        void SetImageEndpoint(string endpoint);
        void SetChatModel(string model);
        void SetImageModel(string model);
        void SetApiKey(string apiKey);
        void SetDefaultMaxTokens(int maxTokens);
        void SetDefaultImageDimensions(int width, int height);

        // Get Chat Responses in various formats
        Task<ChatResponse> GetChatResponse(string prompt);
        Task<string> GetChatResponseString(string prompt);
        Task<string> GetChatResponseString(string prompt, int choice = 0);
        Task<string> GetChatResponseString(string prompt, List<string> imageUrls);
        Task<T> GetChatResponseObject<T>(string prompt);
        Task<ChatResponse> GetChatResponseStructured(string prompt);
        Task<ChatResponse> ContinueChatConversation(List<Dictionary<string, string>> messages);
        Task<List<string>> GetMultipleChatResponses(string prompt, int count);

        // Get Image Responses in various formats
        Task<ImageResponse> GetImageResponse(string prompt, int count = 1, bool improvePrompt = true, int width = 1024, int height = 1024);
        Task<string> GetImageUrl(string prompt, int width, int height, bool improvePrompt = true);
        Task<string> GetImageUrl(string prompt, int width, int height, List<string> urls, bool improvePrompt = true);
        Task<byte[]> DownloadImage(string prompt, int width, int height, bool improvePrompt = true);
        Task<byte[]> DownloadImage(string prompt, int width, int height, List<string> urls, bool improvePrompt = true);
        Task<List<string>> GetImageUrls(string prompt, int width, int height, int count, bool improvePrompt = true);
        Task<List<string>> GetImageUrls(string prompt, int width, int height, List<string> urls, int count, bool improvePrompt = true);
        Task<List<byte[]>> DownloadImages(string prompt, int width, int height, int count, bool improvePrompt = true);
        Task<List<byte[]>> DownloadImages(string prompt, int width, int height, List<string> urls, int count, bool improvePrompt = true);

        // Perform Image Analysis
        Task<List<string>> AnalyzeImage(string url);
        Task<List<string>> AnalyzeImage(string url, string request);
        Task<List<string>> AnalyzeLocalImage(byte[] imageBytes);

        // Check API Health
        Task<string> CheckApiHealth();
    }

    public class AiTools : IOpenAiTools
    {
        private string _apiKey = string.Empty;
        private string _chatModel = "gpt-4-2024-05-13";
        private string _imageModel = "dall-e-3";
        private string _imageEndpoint = "https://api.openai.com/v1/images/generations";
        private string _chatEndpoint = "https://api.openai.com/v1/chat/completions";
        private readonly IHttpClientFactory _clientFactory;
        private int _defaultMaxTokens = 300;
        private int _defaultImageWidth = 1024;
        private int _defaultImageHeight = 1024;

        public AiTools(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }
        public AiTools(IHttpClientFactory clientFactory, OpenAiSettings openAiSettings)
        {
            _clientFactory = clientFactory;
            _apiKey = openAiSettings.ApiKey;
            _chatModel = openAiSettings.ChatModel;
            _imageModel = openAiSettings.ImageModel;
            _imageEndpoint = openAiSettings.ImageEndpoint;
            _chatEndpoint = openAiSettings.ChatEndpoint;
            _defaultMaxTokens = openAiSettings.DefaultMaxTokens;
            _defaultImageWidth = openAiSettings.DefaultImageWidth;
            _defaultImageHeight = openAiSettings.DefaultImageHeight;
        }

        // Setting methods
        public void SetApiKey(string apiKey) => _apiKey = apiKey;
        public void SetChatEndpoint(string endpoint) => _chatEndpoint = endpoint;
        public void SetImageEndpoint(string endpoint) => _imageEndpoint = endpoint;
        public void SetChatModel(string model) => _chatModel = model;
        public void SetImageModel(string model) => _imageModel = model;
        public void SetDefaultMaxTokens(int maxTokens) => _defaultMaxTokens = maxTokens;
        public void SetDefaultImageDimensions(int width, int height)
        {
            _defaultImageWidth = width;
            _defaultImageHeight = height;
        }

        private HttpClient SetClient(string bearer = "Bearer", string mediaTypeWithQualityHeaderValue = "application/json")
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(bearer, _apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaTypeWithQualityHeaderValue));
            return client;
        }

        // Get Chat Response
        public async Task<ChatResponse> GetChatResponse(string prompt)
        {
            var client = SetClient();
            var request = GenerateChatRequestContent(prompt);

            try
            {
                var response = await client.PostAsync(_chatEndpoint, request);
                return await ExportToChatResponse(response);
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Unhandled Error: {ex.Message}"
                };
            }
        }

        private async Task<ChatResponse> ExportToChatResponse(HttpResponseMessage response)
        {
            var responseData = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                ChatResponse responseObject = JsonSerializer.Deserialize<ChatResponse>(responseData);
                if (responseObject != null)
                {
                    responseObject.IsSuccess = true;
                    return responseObject;
                }
                else
                {
                    return new ChatResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to deserialize response."
                    };
                }
            }
            else
            {
                return new ChatResponse
                {
                    IsSuccess = false,
                    ErrorMessage = responseData
                };
            }
        }

        private StringContent GenerateChatRequestContent(string prompt, List<string> urls = null, string role = "You are a helpful AI", bool JSON = false, int maxTokens = 300)
        {
            if(_defaultMaxTokens>maxTokens)
            {
                maxTokens = _defaultMaxTokens;
            }
            var messages = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "role", "system" },
                    { "content", role }
                },
                new Dictionary<string, object>
                {
                    { "role", "user" },
                    { "content", prompt }
                }
            };

            if (urls != null)
            {
                foreach (var url in urls.Where(x => x != "Error"))
                {
                    var imageUrl = new Dictionary<string, object>
                    {
                        { "type", "image_url" },
                        { "image_url", url }
                    };
                    ((List<Dictionary<string, object>>)messages[1]["content"]).Add(imageUrl);
                }
            }

            var requestContent = new Dictionary<string, object>
            {
                { "model", _chatModel },
                { "messages", messages },
                { "max_tokens", maxTokens }
            };

            if (JSON)
            {
                requestContent["response_format"] = new { type = "json_object" };
            }

            var json = JsonSerializer.Serialize(requestContent, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        // Get Chat Response Object
        public async Task<T> GetChatResponseObject<T>(string prompt)
        {
            var requestContent = GenerateChatRequestContent(prompt, JSON: true);

            var client = SetClient();
            var response = await client.PostAsync(_chatEndpoint, requestContent);

            var responseData = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonSerializer.Deserialize<T>(responseData);
                return responseObject;
            }
            else
            {
                throw new Exception($"Error: {responseData}");
            }
        }

        // Get Chat Response String with choice
        public async Task<string> GetChatResponseString(string prompt, int choice = 0)
        {
            var chatResponse = await GetChatResponse(prompt);

            if (chatResponse.IsSuccess)
            {
                if (chatResponse.Choices != null && chatResponse.Choices.Count > 0)
                {
                    return chatResponse.Choices[choice].Message.Content;
                }
                else
                {
                    return "No valid response from the API.";
                }
            }
            else
            {
                throw new Exception($"Error: {chatResponse.ErrorMessage}");
            }
        }

        // Get Chat Response String without choice
        public async Task<string> GetChatResponseString(string prompt)
        {
            var chatResponse = await GetChatResponse(prompt);

            if (chatResponse.IsSuccess)
            {
                if (chatResponse.Choices != null && chatResponse.Choices.Count > 0)
                {
                    return chatResponse.Choices[0].Message.Content;
                }
                else
                {
                    return "No valid response from the API.";
                }
            }
            else
            {
                throw new Exception($"Error: {chatResponse.ErrorMessage}");
            }
        }

        // Get Chat Response String with image URLs
        public async Task<string> GetChatResponseString(string prompt, List<string> imageUrls)
        {
            var requestContent = GenerateChatRequestContent(prompt, urls: imageUrls);

            var client = SetClient();
            var response = await client.PostAsync(_chatEndpoint, requestContent);

            var responseData = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonSerializer.Deserialize<ChatResponse>(responseData);
                if (responseObject != null && responseObject.Choices != null && responseObject.Choices.Count > 0)
                {
                    return responseObject.Choices[0].Message.Content;
                }
                else
                {
                    return "No valid response from the API.";
                }
            }
            else
            {
                throw new Exception($"Error: {responseData}");
            }
        }

        // Get Chat Response Structured
        public async Task<ChatResponse> GetChatResponseStructured(string prompt)
        {
            return await GetChatResponse(prompt);
        }

        // Continue Chat Conversation
        public async Task<ChatResponse> ContinueChatConversation(List<Dictionary<string, string>> messages)
        {
            var client = SetClient();
            var requestContent = GenerateChatRequestContent(messages);

            try
            {
                var response = await client.PostAsync(_chatEndpoint, requestContent);
                return await ExportToChatResponse(response);
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Unhandled Error: {ex.Message}"
                };
            }
        }

        private StringContent GenerateChatRequestContent(List<Dictionary<string, string>> messages)
        {
            var requestContent = new Dictionary<string, object>
            {
                { "model", _chatModel },
                { "messages", messages },
                { "max_tokens", _defaultMaxTokens }
            };

            var json = JsonSerializer.Serialize(requestContent, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        // Get Multiple Chat Responses
        public async Task<List<string>> GetMultipleChatResponses(string prompt, int count)
        {
            var responses = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var response = await GetChatResponseString(prompt);
                responses.Add(response);
            }
            return responses;
        }

        // Improve Image Prompt
        private async Task<string> ImproveImagePrompt(string prompt)
        {
            var improvedPrompt = await GetChatResponseString($"Improve this image generation prompt: {prompt}. This is for Dall.e 3. this should be less than 4000 Characters. Please use as close to 4000 characters as possible. This will not be read by a human.");
            return improvedPrompt;
        }

        // Get Image Response
        public async Task<ImageResponse> GetImageResponse(string prompt, int count = 1, bool improvePrompt = true, int width = 1024, int height = 1024)
        {
            if (improvePrompt)
            {
                prompt = await ImproveImagePrompt(prompt);
            }

            var client = SetClient();
            var request = GenerateImageRequestContent(prompt, count: count, width: width, height: height);

            try
            {
                var response = await client.PostAsync(_imageEndpoint, request);
                return await ExportToImageResponse(response);
            }
            catch (Exception ex)
            {
                return new ImageResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Unhandled Error: {ex.Message}"
                };
            }
        }

        private async Task<ImageResponse> ExportToImageResponse(HttpResponseMessage response)
        {
            var responseData = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonSerializer.Deserialize<ImageResponse>(responseData);
                if (responseObject != null)
                {
                    responseObject.IsSuccess = true;
                    return responseObject;
                }
                else
                {
                    return new ImageResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to deserialize response."
                    };
                }
            }
            else
            {
                return new ImageResponse
                {
                    IsSuccess = false,
                    ErrorMessage = responseData
                };
            }
        }

        private StringContent GenerateImageRequestContent(string prompt, List<string> urls = null, int width = 1024, int height = 1024, int count = 1)
        {
            var requestContent = new Dictionary<string, object>
            {
                { "model", _imageModel },
                { "prompt", prompt },
                { "size", width + "x" + height },
                { "n", count }
            };

            if (urls != null)
            {
                requestContent["image_urls"] = urls;
            }

            var json = JsonSerializer.Serialize(requestContent, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        // Get Image URL
        public async Task<string> GetImageUrl(string prompt, int width, int height, bool improvePrompt = true)
        {
            var response = await GetImageResponse(prompt, 1, improvePrompt, width, height);
            return response.IsSuccess && response.Data.Count > 0 ? response.Data[0].Url : null;
        }

        public async Task<string> GetImageUrl(string prompt, int width, int height, List<string> urls, bool improvePrompt = true)
        {
            if (improvePrompt)
            {
                prompt = await ImproveImagePrompt(prompt);
            }

            var requestContent = GenerateImageRequestContent(prompt, urls, width, height);

            var client = SetClient();
            var response = await client.PostAsync(_imageEndpoint, requestContent);

            var imageResponse = await ExportToImageResponse(response);

            if (imageResponse.Data != null && imageResponse.Data.Count > 0)
            {
                return imageResponse.Data[0].Url;
            }
            else
            {
                throw new Exception("No valid response from the API.");
            }
        }

        // Download Image
        public async Task<byte[]> DownloadImage(string prompt, int width, int height, bool improvePrompt = true)
        {
            var imageUrl = await GetImageUrl(prompt, width, height, improvePrompt);
            return await DownloadImageFromUrl(imageUrl);
        }

        public async Task<byte[]> DownloadImage(string prompt, int width, int height, List<string> urls, bool improvePrompt = true)
        {
            var imageUrl = await GetImageUrl(prompt, width, height, urls, improvePrompt);
            return await DownloadImageFromUrl(imageUrl);
        }

        // Multiple Image URL methods
        public async Task<List<string>> GetImageUrls(string prompt, int width, int height, int count, bool improvePrompt = true)
        {
            var response = await GetImageResponse(prompt, count, improvePrompt, width, height);
            return response.IsSuccess ? response.Data.ConvertAll(data => data.Url) : new List<string>();
        }

        public async Task<List<string>> GetImageUrls(string prompt, int width, int height, List<string> urls, int count, bool improvePrompt = true)
        {
            if (improvePrompt)
            {
                prompt = await ImproveImagePrompt(prompt);
            }

            var requestContent = GenerateImageRequestContent(prompt, urls, width, height, count);

            var client = SetClient();
            var response = await client.PostAsync(_imageEndpoint, requestContent);

            var imageResponse = await ExportToImageResponse(response);

            if (imageResponse.Data != null && imageResponse.Data.Count > 0)
            {
                var urlsList = new List<string>();

                foreach (var data in imageResponse.Data)
                {
                    urlsList.Add(data.Url);
                }

                return urlsList;
            }
            else
            {
                throw new Exception("No valid response from the API.");
            }
        }

        // Download Images
        public async Task<List<byte[]>> DownloadImages(string prompt, int width, int height, int count, bool improvePrompt = true)
        {
            var imageUrls = await GetImageUrls(prompt, width, height, count, improvePrompt);
            var images = new List<byte[]>();
            foreach (var url in imageUrls)
            {
                images.Add(await DownloadImageFromUrl(url));
            }
            return images;
        }

        public async Task<List<byte[]>> DownloadImages(string prompt, int width, int height, List<string> urls, int count, bool improvePrompt = true)
        {
            var imageUrlsList = await GetImageUrls(prompt, width, height, urls, count, improvePrompt);
            var images = new List<byte[]>();
            foreach (var url in imageUrlsList)
            {
                images.Add(await DownloadImageFromUrl(url));
            }
            return images;
        }

        // Download Image Helper
        private async Task<byte[]> DownloadImageFromUrl(string url)
        {
            var client = _clientFactory.CreateClient();
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        // Perform Image Analysis
        public async Task<List<string>> AnalyzeImage(string url)
        {
            var client = SetClient();
            var requestContent = GenerateImageAnalysisRequestContent(url);

            try
            {
                var response = await client.PostAsync(_imageEndpoint, requestContent);
                return await ExportToImageAnalysisResponse(response);
            }
            catch (Exception ex)
            {
                return new List<string> { $"Unhandled Error: {ex.Message}" };
            }
        }

        public async Task<List<string>> AnalyzeImage(string url, string request)
        {
            var client = SetClient();
            var requestContent = GenerateImageAnalysisRequestContent(url, request);

            try
            {
                var response = await client.PostAsync(_imageEndpoint, requestContent);
                return await ExportToImageAnalysisResponse(response);
            }
            catch (Exception ex)
            {
                return new List<string> { $"Unhandled Error: {ex.Message}" };
            }
        }

        private async Task<List<string>> ExportToImageAnalysisResponse(HttpResponseMessage response)
        {
            var responseData = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonSerializer.Deserialize<List<string>>(responseData);
                return responseObject ?? new List<string> { "Failed to deserialize response." };
            }
            else
            {
                return new List<string> { responseData };
            }
        }

        private StringContent GenerateImageAnalysisRequestContent(string url, string request = null)
        {
            var requestContent = new Dictionary<string, object>
            {
                { "model", _imageModel },
                { "url", url }
            };

            if (request != null)
            {
                requestContent["request"] = request;
            }

            var json = JsonSerializer.Serialize(requestContent, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        // Analyze Local Image
        public async Task<List<string>> AnalyzeLocalImage(byte[] imageBytes)
        {
            var client = SetClient();
            var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            content.Add(imageContent, "file", "image.jpg");

            try
            {
                var response = await client.PostAsync(_imageEndpoint, content);
                return await ExportToImageAnalysisResponse(response);
            }
            catch (Exception ex)
            {
                return new List<string> { $"Unhandled Error: {ex.Message}" };
            }
        }

        // Check API Health
        public async Task<string> CheckApiHealth()
        {
            var client = SetClient();
            try
            {
                var response = await client.GetAsync(_chatEndpoint + "/health");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return $"Unhandled Error: {ex.Message}";
            }
        }
    }

    public class Response
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ChatResponse : Response
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("choices")]
        public List<Choice> Choices { get; set; }

        [JsonPropertyName("usage")]
        public Usage Usage { get; set; }

        [JsonPropertyName("system_fingerprint")]
        public string SystemFingerprint { get; set; }
    }

    public class Choice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public Message Message { get; set; }

        [JsonPropertyName("logprobs")]
        public object Logprobs { get; set; }

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    public class Usage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    public class ImageResponse : Response
    {
        [JsonPropertyName("data")]
        public List<ImageData> Data { get; set; }
    }

    public class ImageData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("revised_prompt")]
        public string RevisedPrompt { get; set; }
    }

    public class AnalyzeImageResponse
    {
        [JsonPropertyName("analyze_results")]
        public List<string> AnalyzeResults { get; set; }
    }

    public class OpenAiSettings
    {
        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; }

        [JsonPropertyName("chat_model")]
        public string ChatModel { get; set; }

        [JsonPropertyName("image_model")]
        public string ImageModel { get; set; }

        [JsonPropertyName("chat_endpoint")]
        public string ChatEndpoint { get; set; }

        [JsonPropertyName("image_endpoint")]
        public string ImageEndpoint { get; set; }

        [JsonPropertyName("default_max_tokens")]
        public int DefaultMaxTokens { get; set; }

        [JsonPropertyName("default_image_width")]
        public int DefaultImageWidth { get; set; }

        [JsonPropertyName("default_image_height")]
        public int DefaultImageHeight { get; set; }
    }
}
