OpenAiTools

OpenAiTools is a powerful and flexible C# library designed to interact with OpenAI's GPT and DALL-E models. This library allows developers to integrate advanced natural language processing and image generation capabilities into their applications with ease.
Features

    Chat Responses: Get detailed responses from OpenAI's GPT models.
    Image Generation: Generate images using OpenAI's DALL-E models.
    Customizable Settings: Easily configure API keys, endpoints, and models.
    Multiple Response Formats: Retrieve responses in various formats (string, byte arrays, objects).
    Image Analysis: Perform image analysis using the integrated methods.

Installation

To use OpenAiTools in your project, you need to install the necessary packages. You can do this via NuGet Package Manager or the .NET CLI.
Using NuGet Package Manager

bash

Install-Package OpenAiTools

Using .NET CLI

bash

dotnet add package OpenAiTools

Usage
Setting Up the Library

First, you need to set up the library with your OpenAI API key and configure the desired models and endpoints.

csharp

using System;
using System.Net.Http;
using OpenAiTools;

// Create an instance of IHttpClientFactory (in a real application, use dependency injection)
IHttpClientFactory clientFactory = new DefaultHttpClientFactory();

var openAiTools = new OpenAiTools(clientFactory);
openAiTools.SetApiKey("your-api-key-here");
openAiTools.SetChatModel("gpt-4-2024-05-13");
openAiTools.SetImageModel("dall-e-3");
openAiTools.SetChatEndpoint("https://api.openai.com/v1/chat/completions");
openAiTools.SetImageEndpoint("https://api.openai.com/v1/images/generations");

Getting Chat Responses

You can get chat responses in various formats using the provided methods.
Simple Chat Response

csharp

var response = await openAiTools.GetChatResponse("What is the weather like today?");
Console.WriteLine(response.Choices[0].Message.Content);

Chat Response as String

csharp

string chatResponse = await openAiTools.GetChatResponseString("Tell me a joke.");
Console.WriteLine(chatResponse);

Generating Images

You can generate images based on prompts and retrieve them in different formats.
Simple Image Generation

csharp

var imageResponse = await openAiTools.GetImageResponse("A futuristic city skyline.");
Console.WriteLine(imageResponse.Data[0].Url);

Download Image as Byte Array

csharp

byte[] imageBytes = await openAiTools.GetImage("A futuristic city skyline", 1024, 1024);
File.WriteAllBytes("futuristic_city.png", imageBytes);

Image Analysis

You can also perform image analysis using the integrated methods.

csharp

var analysisResults = await openAiTools.AnalyzeImage("https://example.com/image.jpg");
foreach (var result in analysisResults)
{
    Console.WriteLine(result);
}

Contributing

We welcome contributions from the community! If you'd like to contribute, please fork the repository and submit a pull request.
Reporting Issues

If you encounter any issues, please create a new issue in the Issues section of the repository.
License

This project is licensed under the MIT License. See the LICENSE file for details.
Acknowledgements

This library is built on top of OpenAI's powerful API. We thank OpenAI for providing such a fantastic service that enables developers to build innovative applications.
