#r "Newtonsoft.Json"
using System;
using System.Net;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;

using Microsoft.Bot.Connector;

using Newtonsoft.Json;

// For more information about this template visit http://aka.ms/azurebots-csharp-luis
[Serializable]
public class BasicLuisDialog : LuisDialog<object>
{
    public BasicLuisDialog() : base(new LuisService(new LuisModelAttribute("391b906b-13e3-4aff-a806-fa7de78950b2", "5e86b5619df548889f6c79bbcf4e53b5")))
    {
    }

    [LuisIntent("None")]
    public async Task NoneIntent(IDialogContext context, LuisResult result)
    {
        await context.PostAsync($"You have reached the none intent. You said: {result.Query}"); //
        context.Wait(MessageReceived);
    }

    // Go to https://luis.ai and create a new intent, then train/publish your luis app.
    // Finally replace "MyIntent" with the name of your newly created intent in the following handler
    [LuisIntent("askQuestion")]
    public async Task askQuestion(IDialogContext context, LuisResult result)
    {
        EntityRecommendation word;
        if (result.TryFindEntity("word", out word))
        {

            //word.Entity를 QnAmaker로 보내는 루틴
            var responseString = String.Empty;

            // Send question to API QnA bot
            var knowledgebaseId = "c9f021df-ccdd-4d11-9e17-9ea9bf48f68e"; // Use knowledge base id created.
            var qnamakerSubscriptionKey = "b69365ae37d04bac848fe95dff809d31"; //Use subscription key assigned to you.

            //Build the URI
            Uri qnamakerUriBase = new Uri("https://westus.api.cognitive.microsoft.com/qnamaker/v1.0");
            var builder = new UriBuilder($"{qnamakerUriBase}/knowledgebases/{knowledgebaseId}/generateAnswer");

            //Add the question as part of the body
            //var postBody = $"{{\"question\": \"{activity.Text}\"}}";
            var qnaword = Regex.Replace(word.Entity, @"\s+", "");
            var postBody = $"{{\"question\": \"{qnaword}\"}}";


            //Send the POST request
            using (WebClient client = new WebClient())
            {
                //Set the encoding to UTF8
                client.Encoding = System.Text.Encoding.UTF8;

                //Add the subscription key header
                client.Headers.Add("Ocp-Apim-Subscription-Key", qnamakerSubscriptionKey);
                client.Headers.Add("Content-Type", "application/json");
                responseString = client.UploadString(builder.Uri, postBody);
            }

            //Json parsing
            dynamic stuff = JsonConvert.DeserializeObject(responseString);
            //send msg
            if (stuff.score == "0")
            {
                await context.PostAsync($"{qnaword}는 제가 잘 모르는 단어라서 Bing Search를 사용해봤어요!");

                const string bingAPIkey = "f35f52e4b54b455bb881fd2b6b2d7a75";
              
                string queryUri = "https://api.cognitive.microsoft.com/bing/v5.0/search?q="+ qnaword+ "&count=2&offset=0&mkt=ko-kr&safesearch=Strict";

                //Helper objects to call the News Search API and store the response
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", bingAPIkey); //authentication header to pass the API key
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                string bingRawResponse; //raw response from REST endpoint
                BingWebResults bingJsonResponse = null; //Deserialized response 

                try
                {
                    bingRawResponse = await httpClient.GetStringAsync(queryUri);
                    await context.PostAsync($"{bingRawResponse}");
                    //have to configure this section
                    //bingJsonResponse = JsonConvert.DeserializeObject<BingWebResults>(bingRawResponse);
                }
                catch (Exception e)
                {
                    //add code to handle exceptions while calling the REST endpoint and/or deserializing the object
                }

                //NewsResult[] newsResult = bingJsonResponse.value;

                //if (newsResult == null || newsResult.Length == 0)
                //{
                //    //add code to handle the case where results are null are zero
                //}
            }
            else
            {
                await context.PostAsync($"{word.Entity} : {stuff.answer}");
            }
        }
        else
        {
            await context.PostAsync($"미안해요 다시한번 말해주세요!"); //
        }
        context.Wait(MessageReceived);
    }
    [LuisIntent("greeting")]
    public async Task greeting(IDialogContext context, LuisResult result)
    {
        await context.PostAsync($"안녕하세요?"); //
        context.Wait(MessageReceived);
    }
}
public class BingWebResults
{
    public string _type { get; set; }
    public int totalEstimatedMatches { get; set; }
    public string readLink { get; set; }
    public string webSearchUrl { get; set; }
    public ImageResult[] value { get; set; }
}
public class ImageResult
{
    public string name { get; set; }
    public string webSearchUrl { get; set; }
    public string thumbnailUrl { get; set; }
    public object datePublished { get; set; }
    public string contentUrl { get; set; }
    public string hostPageUrl { get; set; }
    public string contentSize { get; set; }
    public string encodingFormat { get; set; }
    public string hostPageDisplayUrl { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    public string accentColor { get; set; }
}