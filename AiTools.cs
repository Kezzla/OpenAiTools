using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using System.IO;
using Azure.Storage.Blobs.Models;
using System.Text.RegularExpressions;

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
        void SetVoice(string voice);

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

        // Text to Speech
        Task<byte[]> TextToSpeech(string text);
        Task StreamAudioToFile(string text, string filePath, string voice);
        Task<string> StreamAudioToBlob(string text, string containerName, string fileName, string voice);
    }

    public class AiTools : IOpenAiTools
    {
        private string _apiKey = string.Empty;
        private string _chatModel = "gpt-4-2024-05-13";
        private string _imageModel = "dall-e-3";
        private string _imageEndpoint = "https://api.openai.com/v1/images/generations";
        private string _chatEndpoint = "https://api.openai.com/v1/chat/completions";
        private string _ttsEndpoint = "https://api.openai.com/v1/audio/speech";
        private readonly IHttpClientFactory _clientFactory;
        private readonly BlobServiceClient _blobServiceClient;
        private int _defaultMaxTokens = 300;
        private int _defaultImageWidth = 1024;
        private int _defaultImageHeight = 1024;
        private string _voice = "alloy";

        public AiTools(IHttpClientFactory clientFactory, BlobServiceClient blobServiceClient)
        {
            _clientFactory = clientFactory;
            _blobServiceClient = blobServiceClient;
        }

        public AiTools(IHttpClientFactory clientFactory, OpenAiSettings openAiSettings, BlobServiceClient blobServiceClient)
        {
            _clientFactory = clientFactory;
            _blobServiceClient = blobServiceClient;
            _apiKey = openAiSettings.ApiKey;
            _chatModel = openAiSettings.ChatModel;
            _imageModel = openAiSettings.ImageModel;
            _imageEndpoint = openAiSettings.ImageEndpoint;
            _chatEndpoint = openAiSettings.ChatEndpoint;
            _ttsEndpoint = openAiSettings.TtsEndpoint;
            _defaultMaxTokens = openAiSettings.DefaultMaxTokens;
            _defaultImageWidth = openAiSettings.DefaultImageWidth;
            _defaultImageHeight = openAiSettings.DefaultImageHeight;
            _voice = "alloy";
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
        public void SetVoice(string voice) => _voice = voice;

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
        public async Task<ChatResponse> GetChatResponse(string prompt, bool Json = false)
        {
            var client = SetClient();
            var request = GenerateChatRequestContent(prompt,JSON:Json);

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
            if (_defaultMaxTokens > maxTokens)
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

            var json = JsonSerializer.Serialize(requestContent, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        // Get Chat Response Object
        public async Task<T> GetChatResponseObject<T>(string prompt)
        {
            var exampleObject = Activator.CreateInstance<T>();
            var exampleJson = JsonSerializer.Serialize(exampleObject);
            var requestPrompt = $"{prompt} Return response in this Json format. This will not be read by a human.{exampleJson}";
            ChatResponse temp = await GetChatResponse(requestPrompt,true);

            if (temp.IsSuccess && temp.Choices.Count > 0)
            {
                string jsonContent = ExtractJsonFromResponse(temp.Choices[0].Message.Content);
                var responseObject = JsonSerializer.Deserialize<T>(jsonContent);
                return responseObject;
            }
            else
            {
                throw new Exception($"Error: {temp.ErrorMessage}");
            }
        }

        private string ExtractJsonFromResponse(string response)
        {
            // Use regex to extract JSON content from the response
            var jsonRegex = new Regex(@"{.*}");
            var match = jsonRegex.Match(response);
            if (match.Success)
            {
                return match.Value;
            }
            else
            {
                return response;
            }
        }


        public async Task<List<string>> GetMultipleChatResponses(string prompt, int count)
        {
            var responses = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var response = await GetChatResponse(prompt);
                if (response.IsSuccess)
                {
                    responses.Add(response.Choices[0].Message.Content);
                }
            }
            return responses;
        }

        public async Task<string> GetChatResponseString(string prompt, int choice = 0)
        {
            var response = await GetChatResponse(prompt);
            return response.IsSuccess ? response.Choices[choice].Message.Content : response.ErrorMessage;
        }

        public async Task<string> GetChatResponseString(string prompt, List<string> imageUrls)
        {
            var response = await GetChatResponse(prompt);
            return response.IsSuccess ? response.Choices[0].Message.Content : response.ErrorMessage;
        }

        public async Task<string> GetChatResponseString(string prompt)
        {
            var response = await GetChatResponse(prompt);
            return response.IsSuccess ? response.Choices[0].Message.Content : response.ErrorMessage;
        }

        public async Task<ChatResponse> GetChatResponseStructured(string prompt)
        {
            return await GetChatResponse(prompt);
        }

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

            var json = JsonSerializer.Serialize(requestContent, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        // Get Image Response
        public async Task<ImageResponse> GetImageResponse(string prompt, int count = 1, bool improvePrompt = true, int width = 1024, int height = 1024)
        {
            if(improvePrompt)prompt =await GetChatResponseString("Please generate an improved image prompt for Dall.e 3 from this information:"+prompt);
            var requestContent = GenerateImageRequestContent(prompt, count, width, height, improvePrompt);
            var client = SetClient();
            var response = await client.PostAsync(_imageEndpoint, requestContent);

            var responseData = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonSerializer.Deserialize<ImageResponse>(responseData);
                responseObject.IsSuccess = true;
                return responseObject;
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

        private StringContent GenerateImageRequestContent(string prompt, int count = 1, int width = 1024, int height = 1024, bool improvePrompt = true)
        {
            if (_defaultImageWidth > width)
            {
                width = _defaultImageWidth;
            }
            if (_defaultImageHeight > height)
            {
                height = _defaultImageHeight;
            }

            var requestContent = new Dictionary<string, object>
    {
        { "prompt", prompt },
        { "n", count },
        { "size", $"{width}x{height}" },
        { "model", _imageModel }
    };

            var json = JsonSerializer.Serialize(requestContent);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }


        public async Task<string> GetImageUrl(string prompt, int width, int height, bool improvePrompt = true)
        {
            var response = await GetImageResponse(prompt, 1, improvePrompt, width, height);
            return response.IsSuccess ? response.Data[0].Url : "Error";
        }

        public async Task<string> GetImageUrl(string prompt, int width, int height, List<string> urls, bool improvePrompt = true)
        {
            var response = await GetImageResponse(prompt, 1, improvePrompt, width, height);
            return response.IsSuccess ? response.Data[0].Url : "Error";
        }

        public async Task<byte[]> DownloadImage(string prompt, int width, int height, bool improvePrompt = true)
        {
            var url = await GetImageUrl(prompt, width, height, improvePrompt);
            if (url == "Error")
            {
                return new byte[0];
            }

            var client = _clientFactory.CreateClient();
            return await client.GetByteArrayAsync(url);
        }

        public async Task<byte[]> DownloadImage(string prompt, int width, int height, List<string> urls, bool improvePrompt = true)
        {
            var url = await GetImageUrl(prompt, width, height, urls, improvePrompt);
            if (url == "Error")
            {
                return new byte[0];
            }

            var client = _clientFactory.CreateClient();
            return await client.GetByteArrayAsync(url);
        }

        public async Task<List<string>> GetImageUrls(string prompt, int width, int height, int count, bool improvePrompt = true)
        {
            var response = await GetImageResponse(prompt, count, improvePrompt, width, height);
            return response.IsSuccess ? response.Data.Select(x => x.Url).ToList() : new List<string> { "Error" };
        }

        public async Task<List<string>> GetImageUrls(string prompt, int width, int height, List<string> urls, int count, bool improvePrompt = true)
        {
            var response = await GetImageResponse(prompt, count, improvePrompt, width, height);
            return response.IsSuccess ? response.Data.Select(x => x.Url).ToList() : new List<string> { "Error" };
        }

        public async Task<List<byte[]>> DownloadImages(string prompt, int width, int height, int count, bool improvePrompt = true)
        {
            var urls = await GetImageUrls(prompt, width, height, count, improvePrompt);
            var tasks = urls.Select(async url => await DownloadImageFromUrl(url)).ToList();
            return await Task.WhenAll(tasks).ContinueWith(t => t.Result.ToList());
        }

        public async Task<List<byte[]>> DownloadImages(string prompt, int width, int height, List<string> urls, int count, bool improvePrompt = true)
        {
            var urlList = await GetImageUrls(prompt, width, height, urls, count, improvePrompt);
            var tasks = urlList.Select(async url => await DownloadImageFromUrl(url)).ToList();
            return await Task.WhenAll(tasks).ContinueWith(t => t.Result.ToList());
        }

        private async Task<byte[]> DownloadImageFromUrl(string url)
        {
            if (url == "Error")
            {
                return new byte[0];
            }

            var client = _clientFactory.CreateClient();
            return await client.GetByteArrayAsync(url);
        }

        // Analyze Image
        public async Task<List<string>> AnalyzeImage(string url)
        {
            var client = SetClient();
            var response = await client.PostAsync($"{_imageEndpoint}/analyze", new StringContent(JsonSerializer.Serialize(new { image_url = url }), Encoding.UTF8, "application/json"));
            var responseData = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonSerializer.Deserialize<ImageAnalysisResponse>(responseData);
                return responseObject.Labels.Select(x => x.Name).ToList();
            }
            else
            {
                throw new Exception($"Error: {responseData}");
            }
        }

        public async Task<List<string>> AnalyzeImage(string url, string request)
        {
            var client = SetClient();
            var response = await client.PostAsync($"{_imageEndpoint}/analyze", new StringContent(request, Encoding.UTF8, "application/json"));
            var responseData = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonSerializer.Deserialize<ImageAnalysisResponse>(responseData);
                return responseObject.Labels.Select(x => x.Name).ToList();
            }
            else
            {
                throw new Exception($"Error: {responseData}");
            }
        }

        public async Task<List<string>> AnalyzeLocalImage(byte[] imageBytes)
        {
            var client = SetClient();
            var response = await client.PostAsync($"{_imageEndpoint}/analyze", new ByteArrayContent(imageBytes));
            var responseData = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var responseObject = JsonSerializer.Deserialize<ImageAnalysisResponse>(responseData);
                return responseObject.Labels.Select(x => x.Name).ToList();
            }
            else
            {
                throw new Exception($"Error: {responseData}");
            }
        }

        // Check API Health
        public async Task<string> CheckApiHealth()
        {
            var client = SetClient();
            var response = await client.GetAsync($"{_chatEndpoint}/health");
            return await response.Content.ReadAsStringAsync();
        }

        // Text to Speech
        public async Task<byte[]> TextToSpeech(string text)
        {
            var client = SetClient("Bearer", "application/json");

            // Create the request content
            var requestPayload = new
            {
                model = "tts-1",  // Ensure the model parameter is included
                input = text,
                voice = _voice
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            try
            {
                // Log the request content for debugging
                Console.WriteLine($"Request: {JsonSerializer.Serialize(requestPayload)}");

                // Send the POST request
                var response = await client.PostAsync(_ttsEndpoint, requestContent);

                // Log the response status
                Console.WriteLine($"Response Status: {response.StatusCode}");

                // Ensure we got a successful response
                response.EnsureSuccessStatusCode();

                // Read and return the response content as byte array
                var responseBytes = await response.Content.ReadAsByteArrayAsync();

                // Log the response length
                Console.WriteLine($"Response Length: {responseBytes.Length}");

                return responseBytes;
            }
            catch (HttpRequestException e)
            {
                // Log and rethrow the exception to handle it in the calling method
                Console.WriteLine($"Request error: {e.Message}");
                throw;
            }
            catch (Exception e)
            {
                // Log and rethrow any other exceptions
                Console.WriteLine($"Unexpected error: {e.Message}");
                throw;
            }
        }



        public async Task StreamAudioToFile(string text, string filePath, string voice)
        {
            var audioData = await TextToSpeech(text);
            await File.WriteAllBytesAsync(filePath, audioData);
        }
        public static string SanitizeBlobFileName(string fileName)
        {
            // Replace invalid characters with hyphens
            string sanitizedFileName = Regex.Replace(fileName, @"[^a-zA-Z0-9\-]", "-");

            // Ensure the file name is within the length limits
            if (sanitizedFileName.Length > 1024)
            {
                sanitizedFileName = sanitizedFileName.Substring(0, 1024);
            }

            return sanitizedFileName;
        }
        public async Task<string> StreamAudioToBlob(string text, string containerName, string fileName, string voice)
        {
            // Create the request content
            var requestPayload = new
            {
                model = "tts-1",
                input = text,
                voice = voice
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            // Set up the HTTP client
            var client = SetClient("Bearer", "application/json");

            // Send the POST request
            var response = await client.PostAsync(_ttsEndpoint, requestContent);

            // Ensure the response is successful
            response.EnsureSuccessStatusCode();

            // Get the response content as a stream
            var audioStream = await response.Content.ReadAsStreamAsync();

            // Sanitize the file name
            var sanitizedFileName = SanitizeBlobFileName(fileName);

            // Get a reference to the blob container
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            // Get a reference to the blob
            var blobClient = containerClient.GetBlobClient(sanitizedFileName);

            // Upload the audio stream to the blob
            await blobClient.UploadAsync(audioStream, new BlobHttpHeaders { ContentType = "audio/mpeg" });

            // Return the URL of the uploaded blob
            return blobClient.Uri.ToString();
        }

    }

    public class OpenAiSettings
    {
        public string ApiKey { get; set; }
        public string ChatModel { get; set; }
        public string ImageModel { get; set; }
        public string ChatEndpoint { get; set; }
        public string ImageEndpoint { get; set; }
        public string TtsEndpoint { get; set; }
        public int DefaultMaxTokens { get; set; }
        public int DefaultImageWidth { get; set; }
        public int DefaultImageHeight { get; set; }
    }

    public class ChatResponse
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

        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
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

    public class ImageResponse
    {
        [JsonPropertyName("data")]
        public List<ImageData> Data { get; set; }

        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class ImageData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("revised_prompt")]
        public string RevisedPrompt { get; set; }
    }

    public class ImageAnalysisResponse
    {
        [JsonPropertyName("labels")]
        public List<Label> Labels { get; set; }
    }

    public class Label
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

}
